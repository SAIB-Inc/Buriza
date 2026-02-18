using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Chain;

public record ChainInfo
{
    public required ChainType Chain { get; init; }
    public required string Network { get; init; }
    public required string Symbol { get; init; }
    public required string Name { get; init; }
    public required int Decimals { get; init; }
}
