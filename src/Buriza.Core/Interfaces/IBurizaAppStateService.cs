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

    string? GetCachedAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange);
    void CacheAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange, string address);
    bool HasCachedAddresses(Guid walletId, ChainInfo chainInfo, int accountIndex);
    void ClearWalletCache(Guid walletId);

    #endregion

    #region Custom Chain Config (session-only, decrypted)

    ServiceConfig? GetChainConfig(ChainInfo chainInfo);
    void SetChainConfig(ChainInfo chainInfo, ServiceConfig config);
    void ClearChainConfig(ChainInfo chainInfo);

    #endregion

    void ClearCache();
}
