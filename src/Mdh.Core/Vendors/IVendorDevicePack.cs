using Mdh.Core.Engine;

namespace Mdh.Core.Vendors;

public interface IVendorDevicePack
{
    string Name { get; }
    bool IsMatch(ReadOnlyMemory<byte> initialMessage);
    IReadOnlyList<IHandler> BuildPipeline(IServiceProvider services);
}
