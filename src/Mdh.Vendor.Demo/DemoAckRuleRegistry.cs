using Mdh.Core.Vendors;

namespace Mdh.Vendor.Demo;

public sealed class DemoAckRuleRegistry : IAckRuleRegistry
{
    public MessageAckRule GetRule(string messageType)
    {
        var mt = messageType.Trim();

        return mt switch
        {
            "HEL.R01" or "DST.R01" or "REQ.R01" or "OBS.R01" or "OPL.R01" or "PTL.R01" => new MessageAckRule
            {
                UsesAckHandshake = true,
                DefaultTypeCd = "AA"
            },

            "EOT.R01" or "END.R01" or "ACK.R01" => new MessageAckRule
            {
                UsesAckHandshake = false,
                DefaultTypeCd = "AA"
            },

            _ => new MessageAckRule
            {
                UsesAckHandshake = true,
                DefaultTypeCd = "AA"
            }
        };
    }
}
