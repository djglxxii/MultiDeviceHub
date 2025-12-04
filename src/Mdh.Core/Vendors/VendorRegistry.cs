namespace Mdh.Core.Vendors;

public sealed class VendorRegistry
{
    private readonly List<IVendorDevicePack> _packs = new();

    public void Register(IVendorDevicePack pack)
    {
        _packs.Add(pack);
    }

    public IEnumerable<IVendorDevicePack> GetByProtocol(string protocolName)
    {
        // For this PoC, we only support POCT1A
        if (protocolName == "POCT1A")
        {
            return _packs;
        }
        return Enumerable.Empty<IVendorDevicePack>();
    }

    public IEnumerable<IVendorDevicePack> GetAll() => _packs;
}
