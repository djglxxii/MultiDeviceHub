using System.Text;
using System.Xml.Linq;
using Mdh.Core.Protocols;

namespace Mdh.Protocol.Poct1A.Builders;

public sealed class AckBuilder : IPoct1AAckBuilder
{
    public ReadOnlyMemory<byte> BuildAck(
        string inboundMessageType,
        int? inboundControlId,
        string typeCd,
        string? errorCode,
        string? errorMessage)
    {
        var controlId = inboundControlId ?? 0;

        var ack = new XElement("ACK.R01",
            new XElement("HDR",
                new XElement("HDR.message_type", new XAttribute("V", "ACK.R01")),
                new XElement("HDR.control_id", new XAttribute("V", controlId.ToString())),
                new XElement("HDR.version_id", new XAttribute("V", "POCT1")),
                new XElement("HDR.creation_dttm", new XAttribute("V", DateTimeOffset.UtcNow.ToString("O")))
            ),
            new XElement("ACK",
                new XElement("ACK.type_cd", new XAttribute("V", typeCd)),
                new XElement("ACK.ack_control_id", new XAttribute("V", controlId.ToString()))
            )
        );

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            ack.Element("ACK")?.Add(new XElement("ACK.error_cd", new XAttribute("V", errorCode)));
        }

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            ack.Element("ACK")?.Add(new XElement("ACK.error_txt", new XAttribute("V", errorMessage)));
        }

        var doc = new XDocument(ack);
        var xml = doc.ToString(SaveOptions.DisableFormatting);
        return Encoding.UTF8.GetBytes(xml);
    }
}
