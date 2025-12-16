using Mdh.Core.Inbound;

namespace Mdh.Core.Vendors;

public interface IVendorDevicePack
{
    string Name { get; }
    bool IsMatch(ReadOnlyMemory<byte> initialMessage);
    IReadOnlyList<IHandler> BuildPipeline(IServiceProvider services);
}

public sealed class MessageAckRule
{
    public bool UsesAckHandshake { get; init; }
    public string DefaultTypeCd { get; init; } = "AA";
}

public interface IAckRuleRegistry
{
    MessageAckRule GetRule(string messageType);
}

public interface IPoct1AVendorPack : IVendorDevicePack
{
    IAckRuleRegistry AckRules { get; }
}
