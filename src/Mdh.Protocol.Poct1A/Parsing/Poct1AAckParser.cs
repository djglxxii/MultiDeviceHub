using System.Text;
using System.Xml.Linq;
using Mdh.Core.Outbound;
using Mdh.Core.Protocols;

namespace Mdh.Protocol.Poct1A.Parsing;

public sealed class Poct1AAckParser : IPoct1AAckParser
{
    public AckResult ParseAck(ReadOnlyMemory<byte> rawXml)
    {
        var xml = Encoding.UTF8.GetString(rawXml.Span);
        var doc = XDocument.Parse(xml, LoadOptions.None);

        var root = doc.Root ?? throw new InvalidOperationException("Missing root element");
        if (!string.Equals(root.Name.LocalName, "ACK.R01", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Not an ACK.R01 (Root={root.Name.LocalName})");
        }

        var ack = root.Element("ACK") ?? throw new InvalidOperationException("Missing ACK element");

        var typeCd = ack.Element("ACK.type_cd")?.Attribute("V")?.Value ?? string.Empty;
        var ackControlIdRaw = ack.Element("ACK.ack_control_id")?.Attribute("V")?.Value;

        if (!int.TryParse(ackControlIdRaw, out var ackControlId))
        {
            throw new InvalidOperationException("ACK.ack_control_id is missing or invalid");
        }

        var errorCode = ack.Element("ACK.error_cd")?.Attribute("V")?.Value;
        var errorMessage = ack.Element("ACK.error_txt")?.Attribute("V")?.Value;

        return new AckResult
        {
            AckControlId = ackControlId,
            TypeCd = typeCd,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
