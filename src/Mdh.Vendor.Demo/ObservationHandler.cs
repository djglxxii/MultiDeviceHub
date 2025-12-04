using System.Text;
using System.Xml.Linq;
using Mdh.Core.Engine;
using Mdh.Core.Sessions;
using Microsoft.Extensions.Logging;

namespace Mdh.Vendor.Demo;

public sealed class ObservationHandler : IHandler
{
    private readonly ILogger<ObservationHandler> _logger;

    public ObservationHandler(ILogger<ObservationHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken)
    {
        if (context.CurrentMessageType == "OBS.R01")
        {
            _logger.LogInformation("Session {SessionId} received OBS.R01", context.SessionId);

            try
            {
                var xml = Encoding.UTF8.GetString(context.CurrentRawMessage.Span);
                var doc = XDocument.Parse(xml);
                var obs = doc.Root?.Element("OBS");

                if (obs != null)
                {
                    var observationId = obs.Element("OBS.observation_id")?.Attribute("V")?.Value;
                    var testId = obs.Element("OBS.test_id")?.Attribute("V")?.Value;
                    var result = obs.Element("OBS.result")?.Attribute("V")?.Value;

                    _logger.LogInformation(
                        "Session {SessionId} observation: ID={ObservationId}, Test={TestId}, Result={Result}",
                        context.SessionId, observationId, testId, result);

                    // In a real implementation, we would store this observation
                    // For this PoC, we just log it
                }
                else
                {
                    _logger.LogWarning("Session {SessionId} OBS.R01 missing OBS element", context.SessionId);
                    context.AckError.ReportError("INVALID", "Missing OBS element");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Session {SessionId} failed to parse OBS.R01", context.SessionId);
                context.AckError.ReportError("PARSE_ERROR", ex.Message);
            }
        }
        else if (context.CurrentMessageType == "EOT.R01")
        {
            try
            {
                var xml = Encoding.UTF8.GetString(context.CurrentRawMessage.Span);
                var doc = XDocument.Parse(xml);
                var eot = doc.Root?.Element("EOT");
                var topic = eot?.Element("EOT.topic")?.Attribute("V")?.Value;

                if (topic == "OBS")
                {
                    _logger.LogInformation(
                        "Session {SessionId} received EOT.R01 for observations topic",
                        context.SessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Session {SessionId} failed to parse EOT.R01", context.SessionId);
            }
        }

        await next();
    }
}
