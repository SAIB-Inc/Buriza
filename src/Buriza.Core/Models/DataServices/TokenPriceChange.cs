using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.DataServices;

public class TokenPriceChange
{
    public Dictionary<PriceTimeframe, decimal> Changes { get; set; } = [];
}
