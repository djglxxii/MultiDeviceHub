using Mdh.Core.Outbound;
using Mdh.Core.Sessions;

namespace Mdh.Core.Inbound;

public interface IHandler
{
    Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken);
}

public interface IOutboundHandler : IHandler
{
    Task OnOutboundCompletedAsync(
        SessionContext context,
        OutboundMessage message,
        OutboundOutcome outcome,
        CancellationToken cancellationToken);
}
