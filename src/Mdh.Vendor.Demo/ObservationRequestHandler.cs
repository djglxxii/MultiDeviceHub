using Mdh.Core.Engine;
using Mdh.Core.Outbound;
using Mdh.Core.Sessions;
using Mdh.Protocol.Poct1A;
using Microsoft.Extensions.Logging;

namespace Mdh.Vendor.Demo;

public sealed class ObservationRequestHandler : IHandler, IOutboundHandler
{
    private readonly ILogger<ObservationRequestHandler> _logger;
    private static int _controlIdCounter = 2000;
    private static int GenerateControlId() => Interlocked.Increment(ref _controlIdCounter);

    public ObservationRequestHandler(ILogger<ObservationRequestHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken)
    {
        if (context.CurrentMessageType == "DST.R01")
        {
            var hasNewObservations = context.Items.TryGetValue("HasNewObservations", out var value) &&
                                     value is bool hasNew && hasNew;

            if (hasNewObservations)
            {
                _logger.LogInformation(
                    "Session {SessionId} device has new observations, requesting observations",
                    context.SessionId);

                var controlId = GenerateControlId();
                var payload = MessageBuilders.BuildReqR01(controlId);

                var message = new OutboundMessage
                {
                    MessageType = "REQ.R01",
                    Payload = payload,
                    ControlId = controlId,
                    OriginatingHandler = this,
                    IsSystemMessage = false
                };

                var frame = new OutboundFrame { Label = "Request Observations" };
                frame.Messages.Enqueue(message);
                context.OutboundStack.Push(frame);

                _logger.LogInformation(
                    "Session {SessionId} queued REQ.R01 (ControlId: {ControlId})",
                    context.SessionId, controlId);
            }
        }

        await next();
    }

    public Task OnOutboundCompletedAsync(
        SessionContext context,
        OutboundMessage message,
        OutboundOutcome outcome,
        CancellationToken cancellationToken)
    {
        if (message.MessageType == "REQ.R01")
        {
            _logger.LogInformation(
                "Session {SessionId} REQ.R01 (ControlId: {ControlId}) completed with status {Status}",
                context.SessionId, message.ControlId, outcome.Status);

            if (outcome.Status == OutboundStatus.AckError && outcome.AckResult != null)
            {
                _logger.LogWarning(
                    "Session {SessionId} REQ.R01 received error ACK: {ErrorCode} - {ErrorMessage}",
                    context.SessionId, outcome.AckResult.ErrorCode, outcome.AckResult.ErrorMessage);
            }
        }

        return Task.CompletedTask;
    }
}
