using Mdh.Core.Inbound;
using Mdh.Core.Outbound;
using Mdh.Core.Sessions;
using Mdh.Protocol.Poct1A.Builders;
using Microsoft.Extensions.Logging;

namespace Mdh.Vendor.Demo.Handlers;

public sealed class ObservationRequestHandler : IOutboundHandler
{
    public Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken)
    {
        if (!string.Equals(context.CurrentMessageType, "DST.R01", StringComparison.OrdinalIgnoreCase))
        {
            return next();
        }

        var hasNew = context.Items.TryGetValue("HasNewObservations", out var value)
            && value is bool b
            && b;

        if (!hasNew)
        {
            return next();
        }

        // Avoid repeated REQ spam in this PoC.
        if (context.Items.ContainsKey("ReqSent"))
        {
            return next();
        }

        context.Items["ReqSent"] = true;

        var controlId = context.NextOutboundControlId++;
        var payload = SimpleMessageBuilders.BuildReqR01(controlId);

        var frame = new OutboundFrame { Label = "REQ (Request Observations)" };
        frame.Messages.Enqueue(new OutboundMessage
        {
            MessageType = "REQ.R01",
            ControlId = controlId,
            Payload = payload,
            OriginatingHandler = this,
            IsSystemMessage = false
        });

        lock (context.OutboundLock)
        {
            context.OutboundStack.Push(frame);
        }

        context.Logger.LogInformation("Session {SessionId}: Demo ObservationRequestHandler queued REQ.R01 (ControlId={ControlId})",
            context.SessionId,
            controlId);

        return next();
    }

    public Task OnOutboundCompletedAsync(SessionContext context, OutboundMessage message, OutboundOutcome outcome, CancellationToken cancellationToken)
    {
        if (!string.Equals(message.MessageType, "REQ.R01", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        context.Logger.LogInformation(
            "Session {SessionId}: REQ.R01 outbound completed Status={Status} AckType={AckType} AckControlId={AckControlId}",
            context.SessionId,
            outcome.Status,
            outcome.AckResult?.TypeCd,
            outcome.AckResult?.AckControlId);

        return Task.CompletedTask;
    }
}
