using System.Text;
using System.Xml.Linq;
using Mdh.Core.Engine;
using Mdh.Core.Outbound;
using Mdh.Core.Protocols;
using Mdh.Core.Sessions;
using Mdh.Core.Vendors;
using Microsoft.Extensions.Logging;

namespace Mdh.Core.Engine;

public sealed class SessionEngine
{
    private readonly SessionContext _context;
    private readonly IPoct1AParser _parser;
    private readonly INetworkStreamAbstraction _stream;
    private readonly ILogger<SessionEngine> _logger;
    private readonly IAckBuilder _ackBuilder;
    private int _nextControlId = 2; // Start at 2 since first ACK typically uses 1
    private OutboundMessage? _pendingAckMessage;

    public SessionEngine(
        SessionContext context,
        IPoct1AParser parser,
        INetworkStreamAbstraction stream,
        IAckBuilder ackBuilder,
        ILogger<SessionEngine> logger)
    {
        _context = context;
        _parser = parser;
        _stream = stream;
        _ackBuilder = ackBuilder;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Session {SessionId} engine started", _context.SessionId);

        var inboundTask = RunInboundLoopAsync(cancellationToken);
        var outboundTask = RunOutboundLoopAsync(cancellationToken);

        await Task.WhenAny(inboundTask, outboundTask);

        _logger.LogInformation("Session {SessionId} engine stopped", _context.SessionId);
    }

    private async Task RunInboundLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!_context.Termination.IsTerminationRequested && !cancellationToken.IsCancellationRequested)
            {
                var message = await ReadXmlMessageAsync(cancellationToken);
                if (message.IsEmpty)
                {
                    _logger.LogInformation("Session {SessionId} received empty message, closing", _context.SessionId);
                    break;
                }

                var (messageType, controlId) = _parser.ParseMetadata(message);
                _context.CurrentMessageType = messageType;
                _context.CurrentControlId = controlId;
                _context.CurrentRawMessage = message;
                _context.AckError.Reset();

                _logger.LogInformation(
                    "Session {SessionId} received inbound {MessageType} (ControlId: {ControlId})",
                    _context.SessionId, messageType, controlId);

                _context.History.RecordInbound(messageType, controlId);

                // Check if this is an ACK for an outbound message
                if (messageType == "ACK.R01" && _pendingAckMessage != null)
                {
                    await HandleInboundAckAsync(message, cancellationToken);
                }
                else
                {
                    // Run handler pipeline
                    await RunPipelineAsync(cancellationToken);

                    // Determine if we need to send an ACK
                    if (_context.VendorPack is IPoct1AVendorPack poct1APack)
                    {
                        var rule = poct1APack.AckRules.GetRule(messageType);
                        if (rule.UsesAckHandshake)
                        {
                            var ackTypeCd = _context.AckError.HasError ? "AE" : rule.DefaultTypeCd;
                            var ackPayload = _ackBuilder.BuildAck(
                                messageType,
                                controlId ?? 0,
                                ackTypeCd,
                                _context.AckError.ErrorCode,
                                _context.AckError.ErrorMessage);

                            var ackControlId = _nextControlId++;
                            var ackMessage = new OutboundMessage
                            {
                                MessageType = "ACK.R01",
                                Payload = ackPayload,
                                ControlId = ackControlId,
                                OriginatingHandler = new SystemHandler(),
                                IsSystemMessage = true,
                                AckRequired = false
                            };

                            var ackFrame = new OutboundFrame { Label = "System ACK" };
                            ackFrame.Messages.Enqueue(ackMessage);
                            _context.OutboundStack.Push(ackFrame);

                            _logger.LogInformation(
                                "Session {SessionId} queued ACK {AckTypeCd} for {MessageType} (ControlId: {ControlId})",
                                _context.SessionId, ackTypeCd, messageType, controlId);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} inbound loop error", _context.SessionId);
        }
        finally
        {
            _context.Termination.Request("Inbound loop ended");
        }
    }

    private async Task HandleInboundAckAsync(ReadOnlyMemory<byte> ackMessage, CancellationToken cancellationToken)
    {
        try
        {
            var xml = Encoding.UTF8.GetString(ackMessage.Span);
            var doc = XDocument.Parse(xml);
            var ackElement = doc.Root?.Element("ACK");
            var ackControlIdStr = ackElement?.Element("ACK.ack_control_id")?.Attribute("V")?.Value;
            var typeCd = ackElement?.Element("ACK.type_cd")?.Attribute("V")?.Value ?? "AA";
            var errorCode = ackElement?.Element("ACK.error_code")?.Attribute("V")?.Value;
            var errorMessage = ackElement?.Element("ACK.error_message")?.Attribute("V")?.Value;

            if (int.TryParse(ackControlIdStr, out var ackControlId) && _pendingAckMessage != null)
            {
                if (ackControlId == _pendingAckMessage.ControlId)
                {
                    var outcome = new OutboundOutcome
                    {
                        Status = typeCd == "AA" ? OutboundStatus.Acked : OutboundStatus.AckError,
                        AckResult = new AckResult
                        {
                            AckControlId = ackControlId,
                            TypeCd = typeCd,
                            ErrorCode = errorCode,
                            ErrorMessage = errorMessage
                        }
                    };

                    if (_pendingAckMessage.OriginatingHandler is IOutboundHandler outboundHandler)
                    {
                        await outboundHandler.OnOutboundCompletedAsync(_context, _pendingAckMessage, outcome, cancellationToken);
                    }

                    _logger.LogInformation(
                        "Session {SessionId} received ACK {TypeCd} for outbound {MessageType} (ControlId: {ControlId})",
                        _context.SessionId, typeCd, _pendingAckMessage.MessageType, ackControlId);

                    _pendingAckMessage = null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Session {SessionId} failed to parse inbound ACK", _context.SessionId);
        }
    }

    private async Task RunPipelineAsync(CancellationToken cancellationToken)
    {
        if (_context.Pipeline.Count == 0)
        {
            return;
        }

        async Task Next(int index)
        {
            if (index < _context.Pipeline.Count)
            {
                await _context.Pipeline[index].HandleAsync(_context, () => Next(index + 1), cancellationToken);
            }
        }

        await Next(0);
    }

    private async Task RunOutboundLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!_context.Termination.IsTerminationRequested && !cancellationToken.IsCancellationRequested)
            {
                if (_context.OutboundStack.Count == 0)
                {
                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                var frame = _context.OutboundStack.Peek();
                if (frame.Messages.Count == 0)
                {
                    _context.OutboundStack.Pop();
                    continue;
                }

                var message = frame.Messages.Dequeue();

                // Determine if ACK is required
                if (!message.IsSystemMessage && _context.VendorPack is IPoct1AVendorPack poct1APack)
                {
                    var rule = poct1APack.AckRules.GetRule(message.MessageType);
                    message.AckRequired = rule.UsesAckHandshake;
                }
                else
                {
                    message.AckRequired = false;
                }

                // Send message
                await _stream.WriteAsync(message.Payload.ToArray(), 0, message.Payload.Length, cancellationToken);

                _logger.LogInformation(
                    "Session {SessionId} sent outbound {MessageType} (ControlId: {ControlId}, AckRequired: {AckRequired})",
                    _context.SessionId, message.MessageType, message.ControlId, message.AckRequired);

                _context.History.RecordOutbound(message.MessageType, message.ControlId);

                if (!message.AckRequired)
                {
                    var outcome = new OutboundOutcome { Status = OutboundStatus.SentNoAckExpected };
                    if (message.OriginatingHandler is IOutboundHandler outboundHandler)
                    {
                        await outboundHandler.OnOutboundCompletedAsync(_context, message, outcome, cancellationToken);
                    }
                }
                else
                {
                    // Wait for ACK
                    _pendingAckMessage = message;
                    var ackReceived = await WaitForAckAsync(message, cancellationToken);

                    if (!ackReceived)
                    {
                        var timeoutOutcome = new OutboundOutcome { Status = OutboundStatus.AckTimeout };
                        if (message.OriginatingHandler is IOutboundHandler outboundHandler)
                        {
                            await outboundHandler.OnOutboundCompletedAsync(_context, message, timeoutOutcome, cancellationToken);
                        }
                        _pendingAckMessage = null;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} outbound loop error", _context.SessionId);
        }
        finally
        {
            _context.Termination.Request("Outbound loop ended");
        }
    }

    private async Task<bool> WaitForAckAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout && !cancellationToken.IsCancellationRequested)
        {
            if (_pendingAckMessage == null)
            {
                // ACK was received and processed
                return true;
            }
            await Task.Delay(100, cancellationToken);
        }

        return false;
    }

    private async Task<ReadOnlyMemory<byte>> ReadXmlMessageAsync(CancellationToken cancellationToken)
    {
        var buffer = new List<byte>();
        var tagStack = new Stack<string>();
        var inTag = false;
        var currentTag = new StringBuilder();
        var xmlBuffer = new byte[4096];
        var foundRoot = false;
        string? rootTag = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await _stream.ReadAsync(xmlBuffer, 0, xmlBuffer.Length, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            for (int i = 0; i < bytesRead; i++)
            {
                var b = xmlBuffer[i];
                buffer.Add(b);

                if (b == '<')
                {
                    inTag = true;
                    currentTag.Clear();
                }
                else if (b == '>')
                {
                    inTag = false;
                    var tag = currentTag.ToString();
                    var tagName = tag.Split(new[] { ' ', '/', '?' }, StringSplitOptions.RemoveEmptyEntries)[0];

                    if (!foundRoot && !tag.StartsWith("?") && !tag.StartsWith("!"))
                    {
                        rootTag = tagName;
                        foundRoot = true;
                    }

                    if (tag.StartsWith("/"))
                    {
                        // Closing tag
                        if (tagStack.Count > 0 && tagStack.Peek() == tagName)
                        {
                            tagStack.Pop();
                            if (tagStack.Count == 0 && foundRoot && tagName == rootTag)
                            {
                                // Complete message
                                return buffer.ToArray();
                            }
                        }
                    }
                    else if (!tag.EndsWith("/") && !tag.StartsWith("?") && !tag.StartsWith("!"))
                    {
                        // Opening tag
                        tagStack.Push(tagName);
                    }
                }
                else if (inTag)
                {
                    currentTag.Append((char)b);
                }
            }
        }

        return buffer.Count > 0 ? buffer.ToArray() : ReadOnlyMemory<byte>.Empty;
    }

    private sealed class SystemHandler : IHandler
    {
        public Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
