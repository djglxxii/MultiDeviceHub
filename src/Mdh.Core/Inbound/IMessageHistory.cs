namespace Mdh.Core.Inbound;

public interface IMessageHistory
{
    void RecordInbound(string messageType, int? controlId);
    void RecordOutbound(string messageType, int controlId);
    IReadOnlyList<MessageRecord> GetHistory();
}

public sealed class MessageRecord
{
    public DateTime Timestamp { get; init; }
    public string MessageType { get; init; } = string.Empty;
    public int? ControlId { get; init; }
    public bool IsInbound { get; init; }
}

public sealed class MessageHistory : IMessageHistory
{
    private readonly List<MessageRecord> _records = new();

    public void RecordInbound(string messageType, int? controlId)
    {
        _records.Add(new MessageRecord
        {
            Timestamp = DateTime.UtcNow,
            MessageType = messageType,
            ControlId = controlId,
            IsInbound = true
        });
    }

    public void RecordOutbound(string messageType, int controlId)
    {
        _records.Add(new MessageRecord
        {
            Timestamp = DateTime.UtcNow,
            MessageType = messageType,
            ControlId = controlId,
            IsInbound = false
        });
    }

    public IReadOnlyList<MessageRecord> GetHistory() => _records.AsReadOnly();
}
