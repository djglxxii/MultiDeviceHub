namespace Mdh.Core.Sessions;

public sealed class TerminationState
{
    public bool IsTerminationRequested { get; private set; }
    public string? Reason { get; private set; }

    public void Request(string reason)
    {
        if (IsTerminationRequested)
        {
            return;
        }

        IsTerminationRequested = true;
        Reason = reason;
    }
}
