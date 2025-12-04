using System.Net.Sockets;
using Mdh.Core.Engine;

namespace Mdh.Host.Tcp;

public sealed class NetworkStreamAbstraction : INetworkStreamAbstraction
{
    private readonly NetworkStream _stream;

    public NetworkStreamAbstraction(NetworkStream stream)
    {
        _stream = stream;
    }

    public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _stream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _stream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public void Close()
    {
        _stream.Close();
    }
}
