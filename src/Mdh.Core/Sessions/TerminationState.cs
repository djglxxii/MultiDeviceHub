namespace Mdh.Core.Sessions;

public sealed class TerminationState
{
    public bool IsTerminationRequested => Reason != null;
    public string? Reason { get; private set; }

    public void Request(string reason)
    {
        Reason = reason;
    }
}
