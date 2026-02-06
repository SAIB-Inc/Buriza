using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Chain;

public record ChainInfo
{
    public required ChainType Chain { get; init; }
    public required NetworkType Network { get; init; }
    public required string Symbol { get; init; }
    public required string Name { get; init; }
    public required int Decimals { get; init; }

    public string DisplayName => $"{Name} {Network}";
}

public static class ChainRegistry
{
    public static readonly ChainInfo CardanoMainnet = new()
    {
        Chain = ChainType.Cardano,
        Network = NetworkType.Mainnet,
        Symbol = "ADA",
        Name = "Cardano",
        Decimals = 6
    };

    public static readonly ChainInfo CardanoPreview = new()
    {
        Chain = ChainType.Cardano,
        Network = NetworkType.Preview,
        Symbol = "tADA",
        Name = "Cardano",
        Decimals = 6
    };

    public static readonly ChainInfo CardanoPreprod = new()
    {
        Chain = ChainType.Cardano,
        Network = NetworkType.Preprod,
        Symbol = "tADA",
        Name = "Cardano",
        Decimals = 6
    };

    public static ChainInfo Get(ChainType chain, NetworkType network) =>
        (chain, network) switch
        {
            (ChainType.Cardano, NetworkType.Mainnet) => CardanoMainnet,
            (ChainType.Cardano, NetworkType.Preview) => CardanoPreview,
            (ChainType.Cardano, NetworkType.Preprod) => CardanoPreprod,
            _ => throw new NotSupportedException($"Chain {chain} {network} not supported")
        };
}
