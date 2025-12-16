using System.Text;
using System.Xml.Linq;
using Mdh.Core.Inbound;
using Mdh.Core.Sessions;
using Microsoft.Extensions.Logging;

namespace Mdh.Vendor.Demo.Handlers;

public sealed class DeviceStatusHandler : IHandler
{
    public Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken)
    {
        if (!string.Equals(context.CurrentMessageType, "DST.R01", StringComparison.OrdinalIgnoreCase))
        {
            return next();
        }

        var hasNewObservations = false;

        try
        {
            var xml = Encoding.UTF8.GetString(context.CurrentRawMessage.Span);
            var doc = XDocument.Parse(xml);

            var qtyRaw = doc.Root?
                .Element("DST")?
                .Element("DST.new_observations_qty")?
                .Attribute("V")?
                .Value;

            if (int.TryParse(qtyRaw, out var qty))
            {
                hasNewObservations = qty > 0;
            }
        }
        catch
        {
            // ignore
        }

        context.Items["HasNewObservations"] = hasNewObservations;

        context.Logger.LogInformation(
            "Session {SessionId}: Demo DeviceStatusHandler DST.R01 HasNewObservations={HasNewObservations}",
            context.SessionId,
            hasNewObservations);

        return next();
    }
}
