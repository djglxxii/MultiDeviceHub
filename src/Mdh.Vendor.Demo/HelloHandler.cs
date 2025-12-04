using System.Text;
using System.Xml.Linq;
using Mdh.Core.Engine;
using Mdh.Core.Sessions;
using Microsoft.Extensions.Logging;

namespace Mdh.Vendor.Demo;

public sealed class HelloHandler : IHandler
{
    private readonly ILogger<HelloHandler> _logger;

    public HelloHandler(ILogger<HelloHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken)
    {
        if (context.CurrentMessageType == "HEL.R01")
        {
            _logger.LogInformation("Session {SessionId} received HEL.R01", context.SessionId);

            try
            {
                var xml = Encoding.UTF8.GetString(context.CurrentRawMessage.Span);
                var doc = XDocument.Parse(xml);
                var hel = doc.Root?.Element("HEL");

                if (hel != null)
                {
                    var deviceId = hel.Element("HEL.device_id")?.Attribute("V")?.Value;
                    var version = hel.Element("HEL.version")?.Attribute("V")?.Value;

                    if (!string.IsNullOrEmpty(deviceId))
                    {
                        context.Items["DeviceId"] = deviceId;
                        _logger.LogInformation("Session {SessionId} device ID: {DeviceId}, version: {Version}",
                            context.SessionId, deviceId, version ?? "unknown");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Session {SessionId} failed to parse HEL.R01", context.SessionId);
            }
        }

        await next();
    }
}
