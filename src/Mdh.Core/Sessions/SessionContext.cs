using Mdh.Core.Engine;
using Mdh.Core.Inbound;
using Mdh.Core.Outbound;
using Mdh.Core.Vendors;
using Microsoft.Extensions.Logging;

namespace Mdh.Core.Sessions;

public sealed class SessionContext
{
    public Guid SessionId { get; init; }
    public IVendorDevicePack? VendorPack { get; set; }
    public IReadOnlyList<IHandler> Pipeline { get; set; } = Array.Empty<IHandler>();
    public Stack<OutboundFrame> OutboundStack { get; } = new();
    public string? CurrentMessageType { get; set; }
    public int? CurrentControlId { get; set; }
    public ReadOnlyMemory<byte> CurrentRawMessage { get; set; }
    public IMessageHistory History { get; } = new MessageHistory();
    public AckErrorState AckError { get; } = new();
    public TerminationState Termination { get; } = new();
    public ILogger Logger { get; init; } = null!;
    public Dictionary<string, object?> Items { get; } = new();
}
