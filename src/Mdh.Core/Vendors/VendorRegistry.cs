namespace Mdh.Core.Vendors;

public sealed class VendorRegistry
{
    private readonly IReadOnlyList<IVendorDevicePack> _packs;

    public VendorRegistry(IEnumerable<IVendorDevicePack> packs)
    {
        _packs = packs.ToArray();
    }

    public IEnumerable<IVendorDevicePack> GetByProtocol(string protocolName)
    {
        // PoC: only POCT1A is supported, but keep shape for future.
        if (!string.Equals(protocolName, "POCT1A", StringComparison.OrdinalIgnoreCase))
        {
            return Enumerable.Empty<IVendorDevicePack>();
        }

        return _packs;
    }
}
