namespace Mdh.Core.History;

public enum MessageDirection
{
    Inbound,
    Outbound
}

public sealed record MessageHistoryEntry(
    DateTimeOffset Timestamp,
    MessageDirection Direction,
    string MessageType,
    int? ControlId);

public interface IMessageHistory
{
    IReadOnlyList<MessageHistoryEntry> Entries { get; }
    void Add(MessageHistoryEntry entry);
}

public sealed class ListMessageHistory : IMessageHistory
{
    private readonly List<MessageHistoryEntry> _entries = new();

    public IReadOnlyList<MessageHistoryEntry> Entries => _entries;

    public void Add(MessageHistoryEntry entry) => _entries.Add(entry);
}
