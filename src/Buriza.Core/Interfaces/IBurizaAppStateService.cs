using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;

namespace Buriza.Core.Interfaces;

/// <summary>
/// Application state service interface for wallet operations.
/// Provides caching for addresses and custom chain configurations.
/// </summary>
public interface IBurizaAppStateService
{
    #region Address Cache

    /// <summary>Gets a cached address if available.</summary>
    string? GetCachedAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange);
    /// <summary>Caches a derived address.</summary>
    void CacheAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange, string address);
    /// <summary>Returns true if any addresses are cached for the account.</summary>
    bool HasCachedAddresses(Guid walletId, ChainInfo chainInfo, int accountIndex);
    /// <summary>Clears cached addresses for a wallet.</summary>
    void ClearWalletCache(Guid walletId);

    #endregion

    #region Custom Chain Config (session-only, decrypted)

    /// <summary>Gets session-scoped chain config (endpoint/apiKey).</summary>
    ServiceConfig? GetChainConfig(ChainInfo chainInfo);
    /// <summary>Sets session-scoped chain config.</summary>
    void SetChainConfig(ChainInfo chainInfo, ServiceConfig config);
    /// <summary>Clears session-scoped chain config.</summary>
    void ClearChainConfig(ChainInfo chainInfo);

    #endregion

    /// <summary>Clears all cached state.</summary>
    void ClearCache();
}
