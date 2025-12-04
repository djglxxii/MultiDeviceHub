using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace Mdh.Protocol.Poct1A;

public static class MessageBuilders
{
    private static int _controlIdCounter = 1000;
    private static int GenerateControlId() => Interlocked.Increment(ref _controlIdCounter);

    public static ReadOnlyMemory<byte> BuildReqR01(int controlId)
    {
        var creationDttm = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var doc = new XDocument(
            new XElement("REQ.R01",
                new XElement("HDR",
                    new XElement("HDR.message_type", new XAttribute("V", "REQ.R01")),
                    new XElement("HDR.control_id", new XAttribute("V", controlId.ToString())),
                    new XElement("HDR.version_id", new XAttribute("V", "POCT1")),
                    new XElement("HDR.creation_dttm", new XAttribute("V", creationDttm))
                ),
                new XElement("REQ",
                    new XElement("REQ.request_type", new XAttribute("V", "OBS"))
                )
            )
        );

        var xml = doc.ToString(SaveOptions.DisableFormatting);
        return Encoding.UTF8.GetBytes(xml);
    }

    public static ReadOnlyMemory<byte> BuildOplR01(int controlId, int sequenceNumber, int totalCount)
    {
        var creationDttm = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var doc = new XDocument(
            new XElement("OPL.R01",
                new XElement("HDR",
                    new XElement("HDR.message_type", new XAttribute("V", "OPL.R01")),
                    new XElement("HDR.control_id", new XAttribute("V", controlId.ToString())),
                    new XElement("HDR.version_id", new XAttribute("V", "POCT1")),
                    new XElement("HDR.creation_dttm", new XAttribute("V", creationDttm))
                ),
                new XElement("OPL",
                    new XElement("OPL.sequence_number", new XAttribute("V", sequenceNumber.ToString())),
                    new XElement("OPL.total_count", new XAttribute("V", totalCount.ToString())),
                    new XElement("OPL.operator",
                        new XElement("OPL.operator.id", new XAttribute("V", $"OP{sequenceNumber}")),
                        new XElement("OPL.operator.name", new XAttribute("V", $"Operator {sequenceNumber}"))
                    )
                )
            )
        );

        var xml = doc.ToString(SaveOptions.DisableFormatting);
        return Encoding.UTF8.GetBytes(xml);
    }

    public static ReadOnlyMemory<byte> BuildPtlR01(int controlId, int sequenceNumber, int totalCount)
    {
        var creationDttm = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var doc = new XDocument(
            new XElement("PTL.R01",
                new XElement("HDR",
                    new XElement("HDR.message_type", new XAttribute("V", "PTL.R01")),
                    new XElement("HDR.control_id", new XAttribute("V", controlId.ToString())),
                    new XElement("HDR.version_id", new XAttribute("V", "POCT1")),
                    new XElement("HDR.creation_dttm", new XAttribute("V", creationDttm))
                ),
                new XElement("PTL",
                    new XElement("PTL.sequence_number", new XAttribute("V", sequenceNumber.ToString())),
                    new XElement("PTL.total_count", new XAttribute("V", totalCount.ToString())),
                    new XElement("PTL.patient",
                        new XElement("PTL.patient.id", new XAttribute("V", $"PT{sequenceNumber}")),
                        new XElement("PTL.patient.name", new XAttribute("V", $"Patient {sequenceNumber}"))
                    )
                )
            )
        );

        var xml = doc.ToString(SaveOptions.DisableFormatting);
        return Encoding.UTF8.GetBytes(xml);
    }

    public static ReadOnlyMemory<byte> BuildEotR01(int controlId, string topic = "OBS")
    {
        var creationDttm = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var doc = new XDocument(
            new XElement("EOT.R01",
                new XElement("HDR",
                    new XElement("HDR.message_type", new XAttribute("V", "EOT.R01")),
                    new XElement("HDR.control_id", new XAttribute("V", controlId.ToString())),
                    new XElement("HDR.version_id", new XAttribute("V", "POCT1")),
                    new XElement("HDR.creation_dttm", new XAttribute("V", creationDttm))
                ),
                new XElement("EOT",
                    new XElement("EOT.topic", new XAttribute("V", topic))
                )
            )
        );

        var xml = doc.ToString(SaveOptions.DisableFormatting);
        return Encoding.UTF8.GetBytes(xml);
    }

    public static ReadOnlyMemory<byte> BuildEndR01(int controlId)
    {
        var creationDttm = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var doc = new XDocument(
            new XElement("END.R01",
                new XElement("HDR",
                    new XElement("HDR.message_type", new XAttribute("V", "END.R01")),
                    new XElement("HDR.control_id", new XAttribute("V", controlId.ToString())),
                    new XElement("HDR.version_id", new XAttribute("V", "POCT1")),
                    new XElement("HDR.creation_dttm", new XAttribute("V", creationDttm))
                )
            )
        );

        var xml = doc.ToString(SaveOptions.DisableFormatting);
        return Encoding.UTF8.GetBytes(xml);
    }
}
