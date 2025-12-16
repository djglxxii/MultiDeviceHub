using Mdh.Core.Inbound;

namespace Mdh.Core.Outbound;

public sealed class OutboundMessage
{
    public string MessageType { get; init; } = string.Empty;
    public ReadOnlyMemory<byte> Payload { get; init; }
    public int ControlId { get; init; }
    public IHandler OriginatingHandler { get; init; } = default!;
    public bool IsSystemMessage { get; init; }
    public bool AckRequired { get; set; }
}

public sealed class OutboundFrame
{
    public Queue<OutboundMessage> Messages { get; } = new();
    public string? Label { get; init; }
}

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
    public string TypeCd { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class OutboundOutcome
{
    public OutboundStatus Status { get; init; }
    public AckResult? AckResult { get; init; }
}
