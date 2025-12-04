namespace Mdh.Core.Outbound;

public sealed class OutboundFrame
{
    public Queue<OutboundMessage> Messages { get; } = new();
    public string? Label { get; init; }
}
