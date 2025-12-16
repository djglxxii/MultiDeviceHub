using System.Diagnostics;
using Mdh.Core.History;
using Mdh.Core.Inbound;
using Mdh.Core.Outbound;
using Mdh.Core.Protocols;
using Mdh.Core.Sessions;
using Mdh.Core.Vendors;
using Microsoft.Extensions.Logging;

namespace Mdh.Core.Engine;

public sealed class SessionEngine
{
    private static readonly TimeSpan OutboundIdleDelay = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan DefaultAckTimeout = TimeSpan.FromSeconds(5);

    private readonly SessionContext _context;
    private readonly VendorRegistry _vendorRegistry;
    private readonly IServiceProvider _services;
    private readonly IPoct1AParser _parser;
    private readonly IPoct1AAckParser _ackParser;
    private readonly IPoct1AAckBuilder _ackBuilder;
    private readonly INetworkStreamAbstraction _stream;
    private readonly ILogger<SessionEngine> _logger;
    private readonly SimpleXmlMessageFramer _framer = new();

    private readonly object _ackLock = new();
    private TaskCompletionSource<AckResult>? _pendingAck;
    private int? _expectedAckControlId;

    public SessionEngine(
        SessionContext context,
        VendorRegistry vendorRegistry,
        IServiceProvider services,
        IPoct1AParser parser,
        IPoct1AAckParser ackParser,
        IPoct1AAckBuilder ackBuilder,
        INetworkStreamAbstraction stream,
        ILogger<SessionEngine> logger)
    {
        _context = context;
        _vendorRegistry = vendorRegistry;
        _services = services;
        _parser = parser;
        _ackParser = ackParser;
        _ackBuilder = ackBuilder;
        _stream = stream;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken token)
    {
        _logger.LogInformation("Session {SessionId} starting (Remote={Remote})",
            _context.SessionId,
            _stream.RemoteEndPoint?.Value ?? "unknown");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);

        var inbound = Task.Run(() => InboundLoopAsync(linkedCts.Token), linkedCts.Token);
        var outbound = Task.Run(() => OutboundLoopAsync(linkedCts.Token), linkedCts.Token);

        var completed = await Task.WhenAny(inbound, outbound);
        _context.Termination.Request("Loop completed");
        linkedCts.Cancel();

        try
        {
            await Task.WhenAll(inbound, outbound);
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        _logger.LogInformation("Session {SessionId} ending (Reason={Reason})",
            _context.SessionId,
            _context.Termination.Reason ?? "unknown");
    }

    private async Task InboundLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !_context.Termination.IsTerminationRequested)
        {
            ReadOnlyMemory<byte>? raw;
            try
            {
                raw = await _framer.ReadNextMessageAsync(_stream, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session {SessionId}: inbound read failed", _context.SessionId);
                _context.Termination.Request("Inbound read failed");
                return;
            }

            if (raw is null)
            {
                _context.Termination.Request("Connection closed");
                return;
            }

            _context.CurrentRawMessage = raw.Value;

            string messageType;
            int? controlId;
            try
            {
                (messageType, controlId) = _parser.ParseMetadata(raw.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session {SessionId}: failed to parse inbound metadata", _context.SessionId);
                _context.Termination.Request("Inbound parse error");
                return;
            }

            _context.CurrentMessageType = messageType;
            _context.CurrentControlId = controlId;
            _context.AckError.Reset();

            _context.History.Add(new MessageHistoryEntry(DateTimeOffset.UtcNow, MessageDirection.Inbound, messageType, controlId));

            _logger.LogInformation("Session {SessionId}: IN {MessageType} (ControlId={ControlId})",
                _context.SessionId, messageType, controlId);

            if (_context.VendorPack is null)
            {
                if (!TryBindVendorPack(raw.Value, out var vendorPack))
                {
                    _context.Termination.Request("No matching vendor pack");
                    return;
                }

                _context.VendorPack = vendorPack;
                _context.Pipeline = vendorPack.BuildPipeline(_services);

                _logger.LogInformation("Session {SessionId}: bound vendor pack '{Vendor}'",
                    _context.SessionId, vendorPack.Name);
            }

            if (string.Equals(messageType, "ACK.R01", StringComparison.OrdinalIgnoreCase))
            {
                TryResolvePendingAck(raw.Value);
                // We never ACK an ACK.
                continue;
            }

            try
            {
                await InvokePipelineAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session {SessionId}: pipeline error", _context.SessionId);
                _context.Termination.Request("Pipeline error");
                return;
            }

            TryAutoAckInbound(messageType, controlId);
        }
    }

    private bool TryBindVendorPack(ReadOnlyMemory<byte> initialMessage, out IPoct1AVendorPack vendorPack)
    {
        foreach (var pack in _vendorRegistry.GetByProtocol("POCT1A"))
        {
            if (pack is IPoct1AVendorPack poct && poct.IsMatch(initialMessage))
            {
                vendorPack = poct;
                return true;
            }
        }

        vendorPack = null!;
        return false;
    }

    private async Task InvokePipelineAsync(CancellationToken token)
    {
        var pipeline = _context.Pipeline;
        if (pipeline.Count == 0)
        {
            return;
        }

        var index = -1;

        Task Next()
        {
            index++;
            if (index >= pipeline.Count)
            {
                return Task.CompletedTask;
            }

            var handler = pipeline[index];
            return handler.HandleAsync(_context, Next, token);
        }

        await Next();
    }

    private void TryAutoAckInbound(string inboundMessageType, int? inboundControlId)
    {
        if (_context.VendorPack is not IPoct1AVendorPack poct)
        {
            return;
        }

        var rule = poct.AckRules.GetRule(inboundMessageType);
        if (!rule.UsesAckHandshake)
        {
            return;
        }

        if (string.Equals(inboundMessageType, "ACK.R01", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var typeCd = _context.AckError.HasError ? "AE" : rule.DefaultTypeCd;

        var payload = _ackBuilder.BuildAck(
            inboundMessageType,
            inboundControlId,
            typeCd,
            _context.AckError.ErrorCode,
            _context.AckError.ErrorMessage);

        var ackControlId = inboundControlId ?? 0;

        var frame = new OutboundFrame
        {
            Label = $"ACK for {inboundMessageType}"
        };

        frame.Messages.Enqueue(new OutboundMessage
        {
            MessageType = "ACK.R01",
            ControlId = ackControlId,
            Payload = payload,
            OriginatingHandler = SystemHandler.Instance,
            IsSystemMessage = true,
            AckRequired = false
        });

        lock (_context.OutboundLock)
        {
            _context.OutboundStack.Push(frame);
        }

        _logger.LogInformation("Session {SessionId}: queued OUT ACK.R01 (For={InboundType}, AckControlId={AckControlId}, TypeCd={TypeCd})",
            _context.SessionId,
            inboundMessageType,
            ackControlId,
            typeCd);
    }

    private async Task OutboundLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !_context.Termination.IsTerminationRequested)
        {
            OutboundFrame? frame = null;
            OutboundMessage? message = null;

            lock (_context.OutboundLock)
            {
                if (_context.OutboundStack.Count > 0)
                {
                    frame = _context.OutboundStack.Peek();
                    if (frame.Messages.Count == 0)
                    {
                        _context.OutboundStack.Pop();
                        frame = null;
                    }
                    else
                    {
                        message = frame.Messages.Dequeue();
                    }
                }
            }

            if (message is null)
            {
                await Task.Delay(OutboundIdleDelay, token);
                continue;
            }

            if (!message.IsSystemMessage && _context.VendorPack is IPoct1AVendorPack poct)
            {
                var rule = poct.AckRules.GetRule(message.MessageType);
                message.AckRequired = rule.UsesAckHandshake;
            }
            else
            {
                message.AckRequired = false;
            }

            _logger.LogInformation("Session {SessionId}: OUT {MessageType} (ControlId={ControlId}, AckRequired={AckRequired}, Frame={FrameLabel})",
                _context.SessionId,
                message.MessageType,
                message.ControlId,
                message.AckRequired,
                frame?.Label ?? "(none)");

            _context.History.Add(new MessageHistoryEntry(DateTimeOffset.UtcNow, MessageDirection.Outbound, message.MessageType, message.ControlId));

            try
            {
                await _stream.WriteAsync(message.Payload, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session {SessionId}: outbound write failed", _context.SessionId);
                _context.Termination.Request("Outbound write failed");
                return;
            }

            OutboundOutcome outcome;

            if (!message.AckRequired)
            {
                outcome = new OutboundOutcome { Status = OutboundStatus.SentNoAckExpected };
            }
            else
            {
                AckResult? ackResult = null;
                var status = await WaitForAckAsync(message.ControlId, DefaultAckTimeout, token);

                if (status is AckWaitOutcome.Acked ok)
                {
                    ackResult = ok.Ack;
                    status = new AckWaitOutcome.Acked(ok.Ack);
                }

                switch (status)
                {
                    case AckWaitOutcome.Acked acked:
                        ackResult = acked.Ack;
                        outcome = new OutboundOutcome
                        {
                            Status = string.Equals(ackResult.TypeCd, "AA", StringComparison.OrdinalIgnoreCase)
                                ? OutboundStatus.Acked
                                : OutboundStatus.AckError,
                            AckResult = ackResult
                        };
                        break;
                    case AckWaitOutcome.Timeout:
                        outcome = new OutboundOutcome { Status = OutboundStatus.AckTimeout };
                        break;
                    case AckWaitOutcome.Error err:
                        outcome = new OutboundOutcome
                        {
                            Status = OutboundStatus.AckError,
                            AckResult = err.Ack
                        };
                        break;
                    default:
                        outcome = new OutboundOutcome { Status = OutboundStatus.AckTimeout };
                        break;
                }
            }

            if (message.OriginatingHandler is IOutboundHandler outboundHandler)
            {
                try
                {
                    await outboundHandler.OnOutboundCompletedAsync(_context, message, outcome, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Session {SessionId}: outbound completion callback failed", _context.SessionId);
                }
            }
        }
    }

    private void TryResolvePendingAck(ReadOnlyMemory<byte> rawAck)
    {
        AckResult ack;
        try
        {
            ack = _ackParser.ParseAck(rawAck);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Session {SessionId}: failed to parse ACK.R01", _context.SessionId);
            return;
        }

        _logger.LogInformation("Session {SessionId}: IN ACK.R01 (AckControlId={AckControlId}, TypeCd={TypeCd})",
            _context.SessionId,
            ack.AckControlId,
            ack.TypeCd);

        lock (_ackLock)
        {
            if (_pendingAck is null || _expectedAckControlId is null)
            {
                return;
            }

            if (_expectedAckControlId.Value != ack.AckControlId)
            {
                return;
            }

            _pendingAck.TrySetResult(ack);
        }
    }

    private async Task<AckWaitOutcome> WaitForAckAsync(int expectedAckControlId, TimeSpan timeout, CancellationToken token)
    {
        Task<AckResult> waitTask;

        lock (_ackLock)
        {
            if (_pendingAck is not null)
            {
                // PoC: only one outstanding ACK-required outbound message at a time.
                _pendingAck.TrySetCanceled(token);
            }

            _expectedAckControlId = expectedAckControlId;
            _pendingAck = new TaskCompletionSource<AckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            waitTask = _pendingAck.Task;
        }

        try
        {
            var completed = await Task.WhenAny(waitTask, Task.Delay(timeout, token));
            if (completed != waitTask)
            {
                return new AckWaitOutcome.Timeout();
            }

            var ack = await waitTask;

            if (string.Equals(ack.TypeCd, "AA", StringComparison.OrdinalIgnoreCase))
            {
                return new AckWaitOutcome.Acked(ack);
            }

            return new AckWaitOutcome.Error(ack);
        }
        catch (OperationCanceledException)
        {
            return new AckWaitOutcome.Timeout();
        }
        finally
        {
            lock (_ackLock)
            {
                _pendingAck = null;
                _expectedAckControlId = null;
            }
        }
    }

    private abstract record AckWaitOutcome
    {
        public sealed record Acked(AckResult Ack) : AckWaitOutcome;
        public sealed record Error(AckResult Ack) : AckWaitOutcome;
        public sealed record Timeout : AckWaitOutcome;
    }

    private sealed class SystemHandler : IHandler
    {
        public static readonly SystemHandler Instance = new();

        private SystemHandler() { }

        public Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken) => next();
    }
}
