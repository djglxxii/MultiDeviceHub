using Mdh.Core.Outbound;

namespace Mdh.Core.Protocols;

public interface IPoct1AParser
{
    (string MessageType, int? ControlId) ParseMetadata(ReadOnlyMemory<byte> rawXml);
}

public interface IPoct1AAckParser
{
    AckResult ParseAck(ReadOnlyMemory<byte> rawXml);
}

public interface IPoct1AAckBuilder
{
    ReadOnlyMemory<byte> BuildAck(
        string inboundMessageType,
        int? inboundControlId,
        string typeCd,
        string? errorCode,
        string? errorMessage);
}
