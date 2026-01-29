using Buriza.Core.Models;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Interfaces.Chain;

/// <summary>
/// Factory for creating chain providers from configuration.
/// Allows wallets to have custom provider settings (e.g., self-hosted UTxO RPC).
/// </summary>
public interface IChainProviderFactory : IDisposable
{
    /// <summary>Creates a provider for the specified chain using the given configuration. Providers are cached by config.</summary>
    IChainProvider Create(ProviderConfig config);

    /// <summary>Gets the default configuration for a chain and network (with API key from app settings).</summary>
    ProviderConfig GetDefaultConfig(ChainType chain, NetworkType network = NetworkType.Mainnet);

    /// <summary>Gets all supported chain types.</summary>
    IReadOnlyList<ChainType> GetSupportedChains();

    /// <summary>Validates that a provider configuration is valid and can connect.</summary>
    Task<bool> ValidateConfigAsync(ProviderConfig config, CancellationToken ct = default);
}
