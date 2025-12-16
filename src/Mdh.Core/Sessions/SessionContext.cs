using System.Collections.Concurrent;
using Mdh.Core.History;
using Mdh.Core.Inbound;
using Mdh.Core.Outbound;
using Mdh.Core.Vendors;
using Microsoft.Extensions.Logging;

namespace Mdh.Core.Sessions;

public sealed class SessionContext
{
    public SessionContext(Guid sessionId, ILogger logger)
    {
        SessionId = sessionId;
        Logger = logger;
        History = new ListMessageHistory();
        AckError = new AckErrorState();
        Termination = new TerminationState();
    }

    public Guid SessionId { get; }

    public IVendorDevicePack? VendorPack { get; set; }

    public IReadOnlyList<IHandler> Pipeline { get; set; } = Array.Empty<IHandler>();

    public Stack<OutboundFrame> OutboundStack { get; } = new();

    // Synchronizes access between inbound/outbound loops.
    public object OutboundLock { get; } = new();

    public string? CurrentMessageType { get; set; }
    public int? CurrentControlId { get; set; }
    public ReadOnlyMemory<byte> CurrentRawMessage { get; set; }

    public IMessageHistory History { get; }
    public AckErrorState AckError { get; }
    public TerminationState Termination { get; }

    public ILogger Logger { get; }

    // Convenience bag for PoC handler state.
    public ConcurrentDictionary<string, object> Items { get; } = new();

    // Convenience counter for outbound control IDs.
    public int NextOutboundControlId { get; set; } = 1;
}
