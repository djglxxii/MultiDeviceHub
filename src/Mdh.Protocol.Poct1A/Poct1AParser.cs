using System.Text;
using System.Xml.Linq;
using Mdh.Core.Engine;

namespace Mdh.Protocol.Poct1A;

public sealed class Poct1AParser : IPoct1AParser
{
    public (string MessageType, int? ControlId) ParseMetadata(ReadOnlyMemory<byte> rawXml)
    {
        try
        {
            var xml = Encoding.UTF8.GetString(rawXml.Span);
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            
            if (root == null)
            {
                return ("UNKNOWN", null);
            }

            var messageType = root.Name.LocalName;
            
            // Try to find control_id in HDR
            int? controlId = null;
            var hdr = root.Element("HDR");
            if (hdr != null)
            {
                var controlIdElement = hdr.Element("HDR.control_id");
                if (controlIdElement != null)
                {
                    var controlIdAttr = controlIdElement.Attribute("V");
                    if (controlIdAttr != null && int.TryParse(controlIdAttr.Value, out var cid))
                    {
                        controlId = cid;
                    }
                }
            }

            return (messageType, controlId);
        }
        catch
        {
            return ("UNKNOWN", null);
        }
    }
}
