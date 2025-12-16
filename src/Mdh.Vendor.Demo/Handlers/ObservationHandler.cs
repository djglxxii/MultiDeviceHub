using System.Text;
using System.Xml.Linq;
using Mdh.Core.Inbound;
using Mdh.Core.Sessions;
using Microsoft.Extensions.Logging;

namespace Mdh.Vendor.Demo.Handlers;

public sealed class ObservationHandler : IHandler
{
    public Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken)
    {
        if (string.Equals(context.CurrentMessageType, "OBS.R01", StringComparison.OrdinalIgnoreCase))
        {
            return HandleObsAsync(context);
        }

        if (string.Equals(context.CurrentMessageType, "EOT.R01", StringComparison.OrdinalIgnoreCase))
        {
            return HandleEotAsync(context, next);
        }

        return next();
    }

    private Task HandleObsAsync(SessionContext context)
    {
        try
        {
            var xml = Encoding.UTF8.GetString(context.CurrentRawMessage.Span);
            var doc = XDocument.Parse(xml);

            if (doc.Root?.Element("OBS") is null)
            {
                context.AckError.ReportError("BAD_OBS", "OBS element missing");
                context.Logger.LogWarning("Session {SessionId}: OBS.R01 malformed (missing OBS element)", context.SessionId);
                return Task.CompletedTask;
            }

            context.Logger.LogInformation("Session {SessionId}: Demo ObservationHandler processed OBS.R01", context.SessionId);
        }
        catch (Exception ex)
        {
            context.AckError.ReportError("BAD_XML", "Malformed XML");
            context.Logger.LogWarning(ex, "Session {SessionId}: OBS.R01 parse failed", context.SessionId);
        }

        return Task.CompletedTask;
    }

    private Task HandleEotAsync(SessionContext context, Func<Task> next)
    {
        try
        {
            var xml = Encoding.UTF8.GetString(context.CurrentRawMessage.Span);
            var doc = XDocument.Parse(xml);
            var topic = doc.Root?.Element("EOT")?.Element("EOT.topic")?.Attribute("V")?.Value;

            if (string.Equals(topic, "OBS", StringComparison.OrdinalIgnoreCase))
            {
                context.Logger.LogInformation("Session {SessionId}: Demo ObservationHandler saw EOT for observations", context.SessionId);
            }
        }
        catch
        {
            // ignore
        }

        return next();
    }
}
