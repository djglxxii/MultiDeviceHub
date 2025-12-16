using System;

namespace Mdh.Core.Sessions;

public sealed class AckErrorState
{
    public bool HasError { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void Reset()
    {
        HasError = false;
        ErrorCode = null;
        ErrorMessage = null;
    }

    public void ReportError(string errorCode, string errorMessage)
    {
        HasError = true;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}
