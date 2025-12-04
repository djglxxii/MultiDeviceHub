using Mdh.Core.Sessions;

namespace Mdh.Core.Engine;

public interface IHandler
{
    Task HandleAsync(SessionContext context, Func<Task> next, CancellationToken cancellationToken);
}
