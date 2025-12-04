using Mdh.Core.Engine;
using Mdh.Core.Outbound;
using Mdh.Core.Sessions;
using Mdh.Protocol.Poct1A;
using Microsoft.Extensions.Logging;

namespace Mdh.Vendor.Demo;

public sealed class ListUploadHandler : IHandler, IOutboundHandler
{
    private readonly ILogger<ListUploadHandler> _logger;
    private static int _controlIdCounter = 3000;
    private static int GenerateControlId() => Interlocked.Increment(ref _controlIdCounter);
    private bool _oplSent = false;
    private bool _ptlSent = false;

    public ListUploadHandler(ILogger<ListUploadHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken)
    {
        // Send OPL and PTL after DST has been processed
        if (context.CurrentMessageType == "DST.R01" && !_oplSent)
        {
            _logger.LogInformation("Session {SessionId} initiating operator list upload", context.SessionId);

            // Create OPL frame with 3 OPL messages + EOT
            var oplFrame = new OutboundFrame { Label = "Operator List" };
            for (int i = 1; i <= 3; i++)
            {
                var controlId = GenerateControlId();
                var payload = MessageBuilders.BuildOplR01(controlId, i, 3);
                var message = new OutboundMessage
                {
                    MessageType = "OPL.R01",
                    Payload = payload,
                    ControlId = controlId,
                    OriginatingHandler = this,
                    IsSystemMessage = false
                };
                oplFrame.Messages.Enqueue(message);
            }

            // Add EOT for OPL
            var eotControlId = GenerateControlId();
            var eotPayload = MessageBuilders.BuildEotR01(eotControlId, "OPL");
            var eotMessage = new OutboundMessage
            {
                MessageType = "EOT.R01",
                Payload = eotPayload,
                ControlId = eotControlId,
                OriginatingHandler = this,
                IsSystemMessage = false
            };
            oplFrame.Messages.Enqueue(eotMessage);

            context.OutboundStack.Push(oplFrame);
            _oplSent = true;
        }

        if (context.CurrentMessageType == "DST.R01" && !_ptlSent)
        {
            _logger.LogInformation("Session {SessionId} initiating patient list upload", context.SessionId);

            // Create PTL frame with 3 PTL messages + EOT
            var ptlFrame = new OutboundFrame { Label = "Patient List" };
            for (int i = 1; i <= 3; i++)
            {
                var controlId = GenerateControlId();
                var payload = MessageBuilders.BuildPtlR01(controlId, i, 3);
                var message = new OutboundMessage
                {
                    MessageType = "PTL.R01",
                    Payload = payload,
                    ControlId = controlId,
                    OriginatingHandler = this,
                    IsSystemMessage = false
                };
                ptlFrame.Messages.Enqueue(message);
            }

            // Add EOT for PTL
            var eotControlId = GenerateControlId();
            var eotPayload = MessageBuilders.BuildEotR01(eotControlId, "PTL");
            var eotMessage = new OutboundMessage
            {
                MessageType = "EOT.R01",
                Payload = eotPayload,
                ControlId = eotControlId,
                OriginatingHandler = this,
                IsSystemMessage = false
            };
            ptlFrame.Messages.Enqueue(eotMessage);

            context.OutboundStack.Push(ptlFrame);
            _ptlSent = true;
        }

        await next();
    }

    public Task OnOutboundCompletedAsync(
        SessionContext context,
        OutboundMessage message,
        OutboundOutcome outcome,
        CancellationToken cancellationToken)
    {
        if (message.MessageType == "OPL.R01" || message.MessageType == "PTL.R01")
        {
            _logger.LogInformation(
                "Session {SessionId} {MessageType} (ControlId: {ControlId}) completed with status {Status}",
                context.SessionId, message.MessageType, message.ControlId, outcome.Status);

            if (outcome.Status == OutboundStatus.AckError && outcome.AckResult != null)
            {
                _logger.LogWarning(
                    "Session {SessionId} {MessageType} received error ACK: {ErrorCode} - {ErrorMessage}",
                    context.SessionId, message.MessageType, outcome.AckResult.ErrorCode, outcome.AckResult.ErrorMessage);
            }
        }
        else if (message.MessageType == "EOT.R01")
        {
            _logger.LogInformation(
                "Session {SessionId} EOT.R01 (ControlId: {ControlId}) sent (no ACK expected)",
                context.SessionId, message.ControlId);
        }

        return Task.CompletedTask;
    }
}
