using Buriza.Data.Models.Enums;

namespace Buriza.Core.Models;

/// <summary>
/// Configuration for a chain provider (e.g., UTxO RPC endpoint).
/// Stored per-wallet to allow different wallets to use different providers.
/// </summary>
public record ProviderConfig
{
    /// <summary>The chain this configuration applies to.</summary>
    public required ChainType Chain { get; init; }

    /// <summary>RPC endpoint URL (e.g., "https://cardano-mainnet.utxorpc-m1.demeter.run").</summary>
    public required string Endpoint { get; init; }

    /// <summary>Network type (Mainnet, Preprod, Preview).</summary>
    public required NetworkType Network { get; init; }

    /// <summary>Optional API key for authenticated endpoints.</summary>
    public string? ApiKey { get; init; }

    /// <summary>User-friendly name for this configuration (e.g., "Demeter Mainnet", "My Node").</summary>
    public string? Name { get; init; }
}

/// <summary>
/// Well-known provider presets for easy configuration.
/// </summary>
public static class ProviderPresets
{
    public static class Cardano
    {
        public static ProviderConfig DemeterMainnet(string apiKey) => new()
        {
            Chain = ChainType.Cardano,
            Endpoint = "https://cardano-mainnet.utxorpc-m1.demeter.run",
            Network = NetworkType.Mainnet,
            ApiKey = apiKey,
            Name = "Demeter Mainnet"
        };

        public static ProviderConfig DemeterPreprod(string apiKey) => new()
        {
            Chain = ChainType.Cardano,
            Endpoint = "https://cardano-preprod.utxorpc-m1.demeter.run",
            Network = NetworkType.Preprod,
            ApiKey = apiKey,
            Name = "Demeter Preprod"
        };

        public static ProviderConfig DemeterPreview(string apiKey) => new()
        {
            Chain = ChainType.Cardano,
            Endpoint = "https://cardano-preview.utxorpc-m1.demeter.run",
            Network = NetworkType.Preview,
            ApiKey = apiKey,
            Name = "Demeter Preview"
        };

        public static ProviderConfig Custom(string endpoint, NetworkType network, string? apiKey = null, string? name = null) => new()
        {
            Chain = ChainType.Cardano,
            Endpoint = endpoint,
            Network = network,
            ApiKey = apiKey,
            Name = name ?? "Custom"
        };
    }
}
