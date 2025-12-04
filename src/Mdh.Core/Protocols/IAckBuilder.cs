namespace Mdh.Core.Protocols;

public interface IAckBuilder
{
    ReadOnlyMemory<byte> BuildAck(
        string inboundMessageType,
        int inboundControlId,
        string ackTypeCd,
        string? errorCode,
        string? errorMessage);
}
