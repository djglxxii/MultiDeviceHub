using Mdh.Core.Vendors;

namespace Mdh.Vendor.Demo;

public sealed class DemoAckRuleRegistry : IAckRuleRegistry
{
    public MessageAckRule GetRule(string messageType)
    {
        return messageType switch
        {
            "HEL.R01" => new MessageAckRule { UsesAckHandshake = true, DefaultTypeCd = "AA" },
            "DST.R01" => new MessageAckRule { UsesAckHandshake = true, DefaultTypeCd = "AA" },
            "REQ.R01" => new MessageAckRule { UsesAckHandshake = true, DefaultTypeCd = "AA" },
            "OBS.R01" => new MessageAckRule { UsesAckHandshake = true, DefaultTypeCd = "AA" },
            "OPL.R01" => new MessageAckRule { UsesAckHandshake = true, DefaultTypeCd = "AA" },
            "PTL.R01" => new MessageAckRule { UsesAckHandshake = true, DefaultTypeCd = "AA" },
            "EOT.R01" => new MessageAckRule { UsesAckHandshake = false, DefaultTypeCd = "AA" },
            "END.R01" => new MessageAckRule { UsesAckHandshake = false, DefaultTypeCd = "AA" },
            "ACK.R01" => new MessageAckRule { UsesAckHandshake = false, DefaultTypeCd = "AA" },
            _ => new MessageAckRule { UsesAckHandshake = false, DefaultTypeCd = "AA" }
        };
    }
}
