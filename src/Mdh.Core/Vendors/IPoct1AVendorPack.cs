namespace Mdh.Core.Vendors;

public interface IPoct1AVendorPack : IVendorDevicePack
{
    IAckRuleRegistry AckRules { get; }
}
