using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;

namespace Buriza.Core.Interfaces;

/// <summary>
/// Factory for creating chain providers and chain wallets.
/// Provides unified access to chain configuration.
/// </summary>
public interface IBurizaChainProviderFactory : IDisposable
{
    /// <summary>Creates a provider for the specified chain using current settings (for network operations).</summary>
    IBurizaChainProvider CreateProvider(ChainInfo chainInfo);

    /// <summary>Creates a provider with explicit endpoint/apiKey (for validation).</summary>
    IBurizaChainProvider CreateProvider(string endpoint, string? apiKey = null);

    /// <summary>Creates a chain wallet for the specified chain (key derivation, tx building, signing).</summary>
    IChainWallet CreateChainWallet(ChainInfo chainInfo);

    /// <summary>Gets the list of supported chain types.</summary>
    IReadOnlyList<ChainType> GetSupportedChains();

    /// <summary>Validates connection to the specified endpoint.</summary>
    Task<bool> ValidateConnectionAsync(string endpoint, string? apiKey = null, CancellationToken ct = default);

    /// <summary>Sets a session-scoped chain configuration (custom endpoint/apiKey).</summary>
    void SetChainConfig(ChainInfo chainInfo, ServiceConfig config);

    /// <summary>Clears a session-scoped chain configuration.</summary>
    void ClearChainConfig(ChainInfo chainInfo);
}
