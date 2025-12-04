using Mdh.Core.Engine;

namespace Mdh.Core.Outbound;

public sealed class OutboundMessage
{
    public string MessageType { get; init; } = string.Empty;
    public ReadOnlyMemory<byte> Payload { get; init; }
    public int ControlId { get; init; }
    public IHandler OriginatingHandler { get; init; } = null!;
    public bool IsSystemMessage { get; init; }
    public bool AckRequired { get; set; }
}
