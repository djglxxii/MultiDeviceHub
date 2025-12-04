namespace Mdh.Core.Engine;

public interface IPoct1AParser
{
    (string MessageType, int? ControlId) ParseMetadata(ReadOnlyMemory<byte> rawXml);
}
