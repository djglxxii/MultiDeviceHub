namespace Mdh.Core.Engine;

public interface INetworkStreamAbstraction
{
    Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
    Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
    void Close();
}
