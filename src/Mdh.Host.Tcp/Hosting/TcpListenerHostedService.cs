using System.Net;
using System.Net.Sockets;
using Mdh.Core.Engine;
using Mdh.Core.Protocols;
using Mdh.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mdh.Host.Tcp.Hosting;

public sealed class TcpListenerHostedService : BackgroundService
{
    private readonly IOptions<MdhOptions> _options;
    private readonly IServiceProvider _services;
    private readonly ILogger<TcpListenerHostedService> _logger;

    public TcpListenerHostedService(IOptions<MdhOptions> options, IServiceProvider services, ILogger<TcpListenerHostedService> logger)
    {
        _options = options;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = _options.Value.ListenPort;
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        _logger.LogInformation("MDH TCP host listening on port {Port}", port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = Task.Run(() => HandleClientAsync(client, stoppingToken), stoppingToken);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken stoppingToken)
    {
        var remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        var sessionId = Guid.NewGuid();

        using var scope = _services.CreateScope();

        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var sessionLogger = loggerFactory.CreateLogger($"Session-{sessionId:N}");

        var context = new SessionContext(sessionId, sessionLogger);

        _logger.LogInformation("Session {SessionId} accepted from {Remote}", sessionId, remote);

        await using var networkStream = client.GetStream();
        var stream = new StreamNetworkStreamAbstraction(networkStream, new EndPointInfo(remote));

        try
        {
            var engine = ActivatorUtilities.CreateInstance<SessionEngine>(scope.ServiceProvider, context, stream);
            await engine.RunAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} crashed", sessionId);
        }
        finally
        {
            try { client.Close(); } catch { /* ignore */ }
            _logger.LogInformation("Session {SessionId} closed", sessionId);
        }
    }
}
