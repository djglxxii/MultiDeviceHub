using System.Text;
using System.Xml.Linq;
using Mdh.Core.Engine;
using Mdh.Core.Sessions;
using Microsoft.Extensions.Logging;

namespace Mdh.Vendor.Demo;

public sealed class DeviceStatusHandler : IHandler
{
    private readonly ILogger<DeviceStatusHandler> _logger;

    public DeviceStatusHandler(ILogger<DeviceStatusHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken)
    {
        if (context.CurrentMessageType == "DST.R01")
        {
            _logger.LogInformation("Session {SessionId} received DST.R01", context.SessionId);

            try
            {
                var xml = Encoding.UTF8.GetString(context.CurrentRawMessage.Span);
                var doc = XDocument.Parse(xml);
                var dst = doc.Root?.Element("DST");

                if (dst != null)
                {
                    var newObservationsQty = dst.Element("DST.new_observations_qty")?.Attribute("V")?.Value;
                    if (int.TryParse(newObservationsQty, out var qty))
                    {
                        context.Items["NewObservationsQty"] = qty;
                        context.Items["HasNewObservations"] = qty > 0;

                        _logger.LogInformation(
                            "Session {SessionId} device reports {Qty} new observations",
                            context.SessionId, qty);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Session {SessionId} failed to parse DST.R01", context.SessionId);
            }
        }

        await next();
    }
}
