namespace Mdh.Core.Protocols;

public interface INetworkStreamAbstraction
{
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
    EndPointInfo? RemoteEndPoint { get; }
}

public sealed record EndPointInfo(string Value);

public sealed class StreamNetworkStreamAbstraction : INetworkStreamAbstraction
{
    private readonly Stream _stream;

    public StreamNetworkStreamAbstraction(Stream stream, EndPointInfo? remoteEndPoint)
    {
        _stream = stream;
        RemoteEndPoint = remoteEndPoint;
    }

    public EndPointInfo? RemoteEndPoint { get; }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        _stream.ReadAsync(buffer, cancellationToken);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        _stream.WriteAsync(buffer, cancellationToken);
}
