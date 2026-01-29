using Buriza.Data.Models.Enums;

namespace Buriza.Core.Interfaces;

public interface ISessionService : IDisposable
{
    #region Address Cache

    string? GetCachedAddress(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange);
    void CacheAddress(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange, string address);
    bool HasCachedAddresses(int walletId, ChainType chain, int accountIndex);

    #endregion

    #region Custom Provider Config Cache (session-only)

    /// <summary>Gets a cached custom API key for a chain/network. Returns null if not set.</summary>
    string? GetCustomApiKey(ChainType chain, NetworkType network);

    /// <summary>Caches a decrypted custom API key for the session.</summary>
    void SetCustomApiKey(ChainType chain, NetworkType network, string apiKey);

    /// <summary>Checks if a custom API key is cached for a chain/network.</summary>
    bool HasCustomApiKey(ChainType chain, NetworkType network);

    /// <summary>Removes a custom API key from the session cache.</summary>
    void ClearCustomApiKey(ChainType chain, NetworkType network);

    /// <summary>Gets a cached custom endpoint for a chain/network. Returns null if not set.</summary>
    string? GetCustomEndpoint(ChainType chain, NetworkType network);

    /// <summary>Caches a custom endpoint for the session.</summary>
    void SetCustomEndpoint(ChainType chain, NetworkType network, string endpoint);

    /// <summary>Removes a custom endpoint from the session cache.</summary>
    void ClearCustomEndpoint(ChainType chain, NetworkType network);

    #endregion

    void ClearCache();
    void ClearWalletCache(int walletId);
}
