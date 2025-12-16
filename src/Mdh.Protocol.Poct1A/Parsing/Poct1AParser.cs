using System.Text;
using System.Xml.Linq;
using Mdh.Core.Protocols;

namespace Mdh.Protocol.Poct1A.Parsing;

public sealed class Poct1AParser : IPoct1AParser
{
    public (string MessageType, int? ControlId) ParseMetadata(ReadOnlyMemory<byte> rawXml)
    {
        var xml = Encoding.UTF8.GetString(rawXml.Span);
        var doc = XDocument.Parse(xml, LoadOptions.None);

        var root = doc.Root ?? throw new InvalidOperationException("Missing root element");
        var messageType = root.Name.LocalName;

        int? controlId = null;

        var hdr = root.Element("HDR");
        if (hdr is not null)
        {
            var control = hdr.Element("HDR.control_id");
            var v = control?.Attribute("V")?.Value;
            if (int.TryParse(v, out var parsed))
            {
                controlId = parsed;
            }
        }

        return (messageType, controlId);
    }
}
