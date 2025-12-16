using System.Text;
using System.Xml.Linq;
using Mdh.Core.Inbound;
using Mdh.Core.Sessions;
using Microsoft.Extensions.Logging;

namespace Mdh.Vendor.Demo.Handlers;

public sealed class HelloHandler : IHandler
{
    public Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken)
    {
        if (!string.Equals(context.CurrentMessageType, "HEL.R01", StringComparison.OrdinalIgnoreCase))
        {
            return next();
        }

        context.Logger.LogInformation("Session {SessionId}: Demo HelloHandler received HEL.R01", context.SessionId);

        try
        {
            var xml = Encoding.UTF8.GetString(context.CurrentRawMessage.Span);
            var doc = XDocument.Parse(xml);
            var version = doc.Root?.Element("HDR")?.Element("HDR.version_id")?.Attribute("V")?.Value;

            if (!string.IsNullOrWhiteSpace(version))
            {
                context.Items["DeviceVersionId"] = version;
            }
        }
        catch
        {
            // Keep PoC lenient.
        }

        return next();
    }
}
