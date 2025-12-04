using System.Text;
using System.Xml.Linq;
using Mdh.Core.Protocols;

namespace Mdh.Protocol.Poct1A;

public sealed class AckBuilder : IAckBuilder
{
    public ReadOnlyMemory<byte> BuildAck(
        string inboundMessageType,
        int inboundControlId,
        string ackTypeCd,
        string? errorCode,
        string? errorMessage)
    {
        var ackControlId = GenerateControlId();
        var creationDttm = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var doc = new XDocument(
            new XElement("ACK.R01",
                new XElement("HDR",
                    new XElement("HDR.message_type", new XAttribute("V", "ACK.R01")),
                    new XElement("HDR.control_id", new XAttribute("V", ackControlId.ToString())),
                    new XElement("HDR.version_id", new XAttribute("V", "POCT1")),
                    new XElement("HDR.creation_dttm", new XAttribute("V", creationDttm))
                ),
                new XElement("ACK",
                    new XElement("ACK.type_cd", new XAttribute("V", ackTypeCd)),
                    new XElement("ACK.ack_control_id", new XAttribute("V", inboundControlId.ToString())),
                    errorCode != null ? new XElement("ACK.error_code", new XAttribute("V", errorCode)) : null,
                    errorMessage != null ? new XElement("ACK.error_message", new XAttribute("V", errorMessage)) : null
                )
            )
        );

        var xml = doc.ToString(SaveOptions.DisableFormatting);
        return Encoding.UTF8.GetBytes(xml);
    }

    private static int _controlIdCounter = 1;
    private static int GenerateControlId() => Interlocked.Increment(ref _controlIdCounter);
}
