using Buriza.Core.Models;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Interfaces.Chain;

/// <summary>
/// Registry for chain providers. Creates and caches providers using app settings/session configuration.
/// </summary>
public interface IChainRegistry : IDisposable
{
    /// <summary>Gets or creates a provider for the specified chain. Configuration is resolved from app settings/session. Access config via provider.Config.</summary>
    IChainProvider GetProvider(ChainInfo chainInfo);

    /// <summary>Gets all supported chain types.</summary>
    IReadOnlyList<ChainType> GetSupportedChains();

    /// <summary>Validates that a provider configuration is valid and can connect.</summary>
    Task<bool> ValidateConfigAsync(ProviderConfig config, CancellationToken ct = default);

    /// <summary>Invalidates all cached providers. Call when config changes require reconnection.</summary>
    void InvalidateCache();

    /// <summary>Invalidates cached provider for a specific chain. Call when that chain's config changes.</summary>
    void InvalidateCache(ChainInfo chainInfo);
}
