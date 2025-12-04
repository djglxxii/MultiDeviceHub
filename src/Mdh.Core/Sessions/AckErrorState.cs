namespace Mdh.Core.Sessions;

public sealed class AckErrorState
{
    public bool HasError => ErrorCode != null;
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void Reset()
    {
        ErrorCode = null;
        ErrorMessage = null;
    }

    public void ReportError(string errorCode, string errorMessage)
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}
