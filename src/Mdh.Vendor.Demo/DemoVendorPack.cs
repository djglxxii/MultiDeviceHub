using System.Text;
using System.Xml.Linq;
using Mdh.Core.Engine;
using Mdh.Core.Vendors;
using Microsoft.Extensions.DependencyInjection;

namespace Mdh.Vendor.Demo;

public sealed class DemoVendorPack : IPoct1AVendorPack
{
    public string Name => "Demo POCT1A Device";
    public IAckRuleRegistry AckRules { get; } = new DemoAckRuleRegistry();

    public bool IsMatch(ReadOnlyMemory<byte> initialMessage)
    {
        try
        {
            var xml = Encoding.UTF8.GetString(initialMessage.Span);
            var doc = XDocument.Parse(xml);
            var root = doc.Root;

            if (root == null)
            {
                return false;
            }

            // Check if root element ends with .R01
            if (!root.Name.LocalName.EndsWith(".R01"))
            {
                return false;
            }

            // Check for POCT1 version
            var hdr = root.Element("HDR");
            if (hdr != null)
            {
                var versionId = hdr.Element("HDR.version_id");
                if (versionId != null)
                {
                    var versionAttr = versionId.Attribute("V");
                    if (versionAttr != null && versionAttr.Value == "POCT1")
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<IHandler> BuildPipeline(IServiceProvider services)
    {
        return new IHandler[]
        {
            services.GetRequiredService<HelloHandler>(),
            services.GetRequiredService<DeviceStatusHandler>(),
            services.GetRequiredService<ObservationRequestHandler>(),
            services.GetRequiredService<ObservationHandler>(),
            services.GetRequiredService<ListUploadHandler>()
        };
    }
}
