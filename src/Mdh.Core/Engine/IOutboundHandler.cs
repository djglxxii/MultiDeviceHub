using Mdh.Core.Outbound;
using Mdh.Core.Sessions;

namespace Mdh.Core.Engine;

public interface IOutboundHandler : IHandler
{
    Task OnOutboundCompletedAsync(
        SessionContext context,
        OutboundMessage message,
        OutboundOutcome outcome,
        CancellationToken cancellationToken);
}
