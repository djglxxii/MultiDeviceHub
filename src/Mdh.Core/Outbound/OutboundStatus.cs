namespace Mdh.Core.Outbound;

public enum OutboundStatus
{
    SentNoAckExpected,
    Acked,
    AckTimeout,
    AckError
}

public sealed class AckResult
{
    public int AckControlId { get; init; }
    public string TypeCd { get; init; } = string.Empty; // AA, AE, etc.
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class OutboundOutcome
{
    public OutboundStatus Status { get; init; }
    public AckResult? AckResult { get; init; }
}
