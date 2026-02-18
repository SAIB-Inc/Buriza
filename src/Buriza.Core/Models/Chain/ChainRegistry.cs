using Buriza.Core.Models.Enums;

namespace Buriza.Core.Models.Chain;

public static class ChainRegistry
{
    public static readonly ChainInfo CardanoMainnet = new()
    {
        Chain = ChainType.Cardano,
        Network = "mainnet",
        Symbol = "ADA",
        Name = "Cardano",
        Decimals = 6
    };

    public static readonly ChainInfo CardanoPreview = new()
    {
        Chain = ChainType.Cardano,
        Network = "preview",
        Symbol = "tADA",
        Name = "Cardano",
        Decimals = 6
    };

    public static readonly ChainInfo CardanoPreprod = new()
    {
        Chain = ChainType.Cardano,
        Network = "preprod",
        Symbol = "tADA",
        Name = "Cardano",
        Decimals = 6
    };

    public static ChainInfo Get(ChainType chain, string network) =>
        (chain, network) switch
        {
            (ChainType.Cardano, "mainnet") => CardanoMainnet,
            (ChainType.Cardano, "preview") => CardanoPreview,
            (ChainType.Cardano, "preprod") => CardanoPreprod,
            _ => throw new NotSupportedException($"Chain {chain} {network} not supported")
        };
}
