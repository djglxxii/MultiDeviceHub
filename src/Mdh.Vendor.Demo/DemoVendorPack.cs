using System.Text;
using System.Xml.Linq;
using Mdh.Core.Inbound;
using Mdh.Core.Vendors;

namespace Mdh.Vendor.Demo;

public sealed class DemoVendorPack : IPoct1AVendorPack
{
    public DemoVendorPack(DemoAckRuleRegistry ackRules)
    {
        AckRules = ackRules;
    }

    public string Name => "Demo POCT1A Device";

    public IAckRuleRegistry AckRules { get; }

    public bool IsMatch(ReadOnlyMemory<byte> initialMessage)
    {
        try
        {
            var xml = Encoding.UTF8.GetString(initialMessage.Span);
            var doc = XDocument.Parse(xml);

            var root = doc.Root;
            if (root is null)
            {
                return false;
            }

            if (!root.Name.LocalName.EndsWith(".R01", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var version = root.Element("HDR")?.Element("HDR.version_id")?.Attribute("V")?.Value;
            return string.Equals(version, "POCT1", StringComparison.OrdinalIgnoreCase);
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
            Get<Handlers.HelloHandler>(services),
            Get<Handlers.DeviceStatusHandler>(services),
            Get<Handlers.ObservationRequestHandler>(services),
            Get<Handlers.ObservationHandler>(services),
            Get<Handlers.ListUploadHandler>(services)
        };
    }

    private static T Get<T>(IServiceProvider services) where T : notnull
    {
        return (T)(services.GetService(typeof(T))
            ?? throw new InvalidOperationException($"Handler not registered: {typeof(T).FullName}"));
    }
}
