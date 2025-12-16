using System.Text;
using System.Xml.Linq;

namespace Mdh.Protocol.Poct1A.Builders;

public static class SimpleMessageBuilders
{
    public static ReadOnlyMemory<byte> BuildReqR01(int controlId)
    {
        var root = new XElement("REQ.R01",
            Hdr(controlId, "REQ.R01"),
            new XElement("REQ",
                new XElement("REQ.request_cd", new XAttribute("V", "OBS"))
            ));

        return ToUtf8(root);
    }

    public static ReadOnlyMemory<byte> BuildOplR01(int controlId, int chunkIndex)
    {
        var root = new XElement("OPL.R01",
            Hdr(controlId, "OPL.R01"),
            new XElement("OPL",
                new XElement("OPL.chunk_idx", new XAttribute("V", chunkIndex.ToString())),
                new XElement("OPL.operator_id", new XAttribute("V", $"demo-op-{chunkIndex}"))
            ));

        return ToUtf8(root);
    }

    public static ReadOnlyMemory<byte> BuildPtlR01(int controlId, int chunkIndex)
    {
        var root = new XElement("PTL.R01",
            Hdr(controlId, "PTL.R01"),
            new XElement("PTL",
                new XElement("PTL.chunk_idx", new XAttribute("V", chunkIndex.ToString())),
                new XElement("PTL.patient_id", new XAttribute("V", $"demo-pt-{chunkIndex}"))
            ));

        return ToUtf8(root);
    }

    public static ReadOnlyMemory<byte> BuildEotR01(int controlId, string topic)
    {
        var root = new XElement("EOT.R01",
            Hdr(controlId, "EOT.R01"),
            new XElement("EOT",
                new XElement("EOT.topic", new XAttribute("V", topic))
            ));

        return ToUtf8(root);
    }

    public static ReadOnlyMemory<byte> BuildEndR01(int controlId, string reason)
    {
        var root = new XElement("END.R01",
            Hdr(controlId, "END.R01"),
            new XElement("END",
                new XElement("END.reason", new XAttribute("V", reason))
            ));

        return ToUtf8(root);
    }

    private static XElement Hdr(int controlId, string messageType) =>
        new("HDR",
            new XElement("HDR.message_type", new XAttribute("V", messageType)),
            new XElement("HDR.control_id", new XAttribute("V", controlId.ToString())),
            new XElement("HDR.version_id", new XAttribute("V", "POCT1")),
            new XElement("HDR.creation_dttm", new XAttribute("V", DateTimeOffset.UtcNow.ToString("O")))
        );

    private static ReadOnlyMemory<byte> ToUtf8(XElement root)
    {
        var doc = new XDocument(root);
        var xml = doc.ToString(SaveOptions.DisableFormatting);
        return Encoding.UTF8.GetBytes(xml);
    }
}
