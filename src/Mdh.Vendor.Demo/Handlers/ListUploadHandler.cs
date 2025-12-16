using Mdh.Core.Inbound;
using Mdh.Core.Outbound;
using Mdh.Core.Sessions;
using Mdh.Protocol.Poct1A.Builders;
using Microsoft.Extensions.Logging;

namespace Mdh.Vendor.Demo.Handlers;

public sealed class ListUploadHandler : IOutboundHandler
{
    public Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken)
    {
        if (!string.Equals(context.CurrentMessageType, "DST.R01", StringComparison.OrdinalIgnoreCase))
        {
            return next();
        }

        // Only do this once per session in the PoC.
        if (context.Items.ContainsKey("ListUploadsPushed"))
        {
            return next();
        }

        context.Items["ListUploadsPushed"] = true;

        // Push OPL then PTL so stack ordering is exercised (PTL will run first).
        PushOplFrame(context);
        PushPtlFrame(context);

        return next();
    }

    private void PushOplFrame(SessionContext context)
    {
        var frame = new OutboundFrame { Label = "OPL upload" };

        for (var i = 1; i <= 3; i++)
        {
            var controlId = context.NextOutboundControlId++;
            frame.Messages.Enqueue(new OutboundMessage
            {
                MessageType = "OPL.R01",
                ControlId = controlId,
                Payload = SimpleMessageBuilders.BuildOplR01(controlId, i),
                OriginatingHandler = this,
                IsSystemMessage = false
            });
        }

        var eotControl = context.NextOutboundControlId++;
        frame.Messages.Enqueue(new OutboundMessage
        {
            MessageType = "EOT.R01",
            ControlId = eotControl,
            Payload = SimpleMessageBuilders.BuildEotR01(eotControl, "OPL"),
            OriginatingHandler = this,
            IsSystemMessage = false
        });

        lock (context.OutboundLock)
        {
            context.OutboundStack.Push(frame);
        }

        context.Logger.LogInformation("Session {SessionId}: queued OPL upload frame (3 chunks + EOT)", context.SessionId);
    }

    private void PushPtlFrame(SessionContext context)
    {
        var frame = new OutboundFrame { Label = "PTL upload" };

        for (var i = 1; i <= 3; i++)
        {
            var controlId = context.NextOutboundControlId++;
            frame.Messages.Enqueue(new OutboundMessage
            {
                MessageType = "PTL.R01",
                ControlId = controlId,
                Payload = SimpleMessageBuilders.BuildPtlR01(controlId, i),
                OriginatingHandler = this,
                IsSystemMessage = false
            });
        }

        var eotControl = context.NextOutboundControlId++;
        frame.Messages.Enqueue(new OutboundMessage
        {
            MessageType = "EOT.R01",
            ControlId = eotControl,
            Payload = SimpleMessageBuilders.BuildEotR01(eotControl, "PTL"),
            OriginatingHandler = this,
            IsSystemMessage = false
        });

        lock (context.OutboundLock)
        {
            context.OutboundStack.Push(frame);
        }

        context.Logger.LogInformation("Session {SessionId}: queued PTL upload frame (3 chunks + EOT)", context.SessionId);
    }

    public Task OnOutboundCompletedAsync(SessionContext context, OutboundMessage message, OutboundOutcome outcome, CancellationToken cancellationToken)
    {
        if (message.MessageType is "OPL.R01" or "PTL.R01")
        {
            context.Logger.LogInformation(
                "Session {SessionId}: {MessageType} (ControlId={ControlId}) outcome={Status} AckType={AckType}",
                context.SessionId,
                message.MessageType,
                message.ControlId,
                outcome.Status,
                outcome.AckResult?.TypeCd);
        }
        else if (message.MessageType is "EOT.R01")
        {
            if (outcome.Status == OutboundStatus.SentNoAckExpected)
            {
                context.Logger.LogInformation(
                    "Session {SessionId}: {FrameLabel} completed (EOT sent)",
                    context.SessionId,
                    InferLabel(context, message));
            }
        }

        return Task.CompletedTask;
    }

    private static string InferLabel(SessionContext context, OutboundMessage message)
    {
        // Minimal helper for logging.
        return message.Payload.Length > 0 ? "List upload" : "List upload";
    }
}
