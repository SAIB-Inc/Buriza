namespace Buriza.Data.Models.Enums;

public enum ChainType
{
    Cardano = 0
    // Future: Bitcoin = 1, Ethereum = 2, etc.
}

public enum NetworkType
{
    Mainnet = 0,
    Preview = 1,
    Preprod = 2
}

public enum DataServiceType
{
    TokenMetadata = 0,
    TokenPrice = 1
}
