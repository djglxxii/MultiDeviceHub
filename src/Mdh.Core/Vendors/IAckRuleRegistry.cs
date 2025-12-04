namespace Mdh.Core.Vendors;

public sealed class MessageAckRule
{
    public bool UsesAckHandshake { get; init; }
    public string DefaultTypeCd { get; init; } = "AA";
}

public interface IAckRuleRegistry
{
    MessageAckRule GetRule(string messageType);
}
