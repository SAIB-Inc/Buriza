using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Chain;

public record ChainInfo(
    ChainType Chain,
    string Network,
    string Symbol,
    string Name,
    int Decimals);
