using System.Net;
using System.Net.Sockets;
using Mdh.Core.Engine;
using Mdh.Core.Outbound;
using Mdh.Core.Protocols;
using Mdh.Core.Sessions;
using Mdh.Core.Vendors;
using Mdh.Protocol.Poct1A;
using Mdh.Vendor.Demo;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mdh.Host.Tcp;

public sealed class TcpListenerHostedService : BackgroundService
{
    private readonly ILogger<TcpListenerHostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _listenPort;
    private TcpListener? _listener;

    public TcpListenerHostedService(
        ILogger<TcpListenerHostedService> logger,
        IServiceProvider serviceProvider,
        IOptions<MdhOptions> options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _listenPort = options.Value.ListenPort;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener = new TcpListener(IPAddress.Any, _listenPort);
        _listener.Start();

        _logger.LogInformation("MDH TCP listener started on port {Port}", _listenPort);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var tcpClient = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(tcpClient, stoppingToken), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCP listener error");
        }
        finally
        {
            _listener?.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        var remoteEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";

        _logger.LogInformation(
            "Session {SessionId} connected from {RemoteEndpoint}",
            sessionId, remoteEndpoint);

        try
        {
            using (tcpClient)
            using (var stream = tcpClient.GetStream())
            {
                var streamAbstraction = new NetworkStreamAbstraction(stream);

                // Create session context
                var context = new SessionContext
                {
                    SessionId = sessionId,
                    Logger = _serviceProvider.GetRequiredService<ILogger<SessionContext>>()
                };

                // Wait for first message to identify protocol and vendor pack
                var firstMessage = await ReadFirstMessageAsync(streamAbstraction, cancellationToken);
                if (firstMessage.IsEmpty)
                {
                    _logger.LogWarning("Session {SessionId} closed before first message", sessionId);
                    return;
                }

                // For this PoC, assume POCT1A and use DemoVendorPack
                var vendorRegistry = _serviceProvider.GetRequiredService<VendorRegistry>();
                var vendorPack = vendorRegistry.GetByProtocol("POCT1A")
                    .FirstOrDefault(p => p.IsMatch(firstMessage));

                if (vendorPack == null)
                {
                    _logger.LogWarning("Session {SessionId} no matching vendor pack found", sessionId);
                    return;
                }

                context.VendorPack = vendorPack;
                context.Pipeline = vendorPack.BuildPipeline(_serviceProvider);

                _logger.LogInformation(
                    "Session {SessionId} matched vendor pack: {VendorPackName}",
                    sessionId, vendorPack.Name);

                // Create parser and ack builder
                var parser = _serviceProvider.GetRequiredService<IPoct1AParser>();
                var ackBuilder = _serviceProvider.GetRequiredService<IAckBuilder>();
                var engineLogger = _serviceProvider.GetRequiredService<ILogger<SessionEngine>>();

                // Create and run session engine
                var engine = new SessionEngine(context, parser, streamAbstraction, ackBuilder, engineLogger);

                // Push first message back for processing
                context.CurrentRawMessage = firstMessage;
                var (messageType, controlId) = parser.ParseMetadata(firstMessage);
                context.CurrentMessageType = messageType;
                context.CurrentControlId = controlId;
                context.History.RecordInbound(messageType, controlId);

                // Process first message
                context.AckError.Reset();
                await RunPipelineAsync(context, cancellationToken);

                // Determine if we need to send an ACK for first message
                if (vendorPack is IPoct1AVendorPack poct1APack)
                {
                    var rule = poct1APack.AckRules.GetRule(messageType);
                    if (rule.UsesAckHandshake)
                    {
                        var ackTypeCd = context.AckError.HasError ? "AE" : rule.DefaultTypeCd;
                        var ackPayload = ackBuilder.BuildAck(
                            messageType,
                            controlId ?? 0,
                            ackTypeCd,
                            context.AckError.ErrorCode,
                            context.AckError.ErrorMessage);

                        var ackControlId = 1;
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
                        context.OutboundStack.Push(ackFrame);
                    }
                }

                // Run engine
                await engine.RunAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} error", sessionId);
        }
        finally
        {
            _logger.LogInformation("Session {SessionId} disconnected", sessionId);
        }
    }

    private async Task RunPipelineAsync(SessionContext context, CancellationToken cancellationToken)
    {
        if (context.Pipeline.Count == 0)
        {
            return;
        }

        async Task Next(int index)
        {
            if (index < context.Pipeline.Count)
            {
                await context.Pipeline[index].HandleAsync(context, () => Next(index + 1), cancellationToken);
            }
        }

        await Next(0);
    }

    private async Task<ReadOnlyMemory<byte>> ReadFirstMessageAsync(
        INetworkStreamAbstraction stream,
        CancellationToken cancellationToken)
    {
        var buffer = new List<byte>();
        var tagStack = new Stack<string>();
        var inTag = false;
        var currentTag = new System.Text.StringBuilder();
        var xmlBuffer = new byte[4096];
        var foundRoot = false;
        string? rootTag = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await stream.ReadAsync(xmlBuffer, 0, xmlBuffer.Length, cancellationToken);
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

    public override void Dispose()
    {
        _listener?.Stop();
        base.Dispose();
    }
}

public sealed class MdhOptions
{
    public int ListenPort { get; set; } = 5000;
}
