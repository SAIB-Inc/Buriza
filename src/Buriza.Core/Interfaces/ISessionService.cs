using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Interfaces;

public interface ISessionService : IDisposable
{
    #region Address Cache

    string? GetCachedAddress(int walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange);
    void CacheAddress(int walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange, string address);
    bool HasCachedAddresses(int walletId, ChainInfo chainInfo, int accountIndex);

    #endregion

    #region Custom Provider Config Cache (session-only)

    /// <summary>Gets a cached custom API key for a chain. Returns null if not set.</summary>
    string? GetCustomApiKey(ChainInfo chainInfo);

    /// <summary>Caches a decrypted custom API key for the session.</summary>
    void SetCustomApiKey(ChainInfo chainInfo, string apiKey);

    /// <summary>Checks if a custom API key is cached for a chain.</summary>
    bool HasCustomApiKey(ChainInfo chainInfo);

    /// <summary>Removes a custom API key from the session cache.</summary>
    void ClearCustomApiKey(ChainInfo chainInfo);

    /// <summary>Gets a cached custom endpoint for a chain. Returns null if not set.</summary>
    string? GetCustomEndpoint(ChainInfo chainInfo);

    /// <summary>Caches a custom endpoint for the session.</summary>
    void SetCustomEndpoint(ChainInfo chainInfo, string endpoint);

    /// <summary>Removes a custom endpoint from the session cache.</summary>
    void ClearCustomEndpoint(ChainInfo chainInfo);

    #endregion

    #region Custom Data Service Config Cache (session-only)

    /// <summary>Gets a cached custom endpoint for a data service. Returns null if not set.</summary>
    string? GetDataServiceEndpoint(DataServiceType serviceType);

    /// <summary>Caches a custom endpoint for a data service.</summary>
    void SetDataServiceEndpoint(DataServiceType serviceType, string endpoint);

    /// <summary>Removes a custom endpoint for a data service from the session cache.</summary>
    void ClearDataServiceEndpoint(DataServiceType serviceType);

    /// <summary>Gets a cached custom API key for a data service. Returns null if not set.</summary>
    string? GetDataServiceApiKey(DataServiceType serviceType);

    /// <summary>Caches a custom API key for a data service.</summary>
    void SetDataServiceApiKey(DataServiceType serviceType, string apiKey);

    /// <summary>Removes a custom API key for a data service from the session cache.</summary>
    void ClearDataServiceApiKey(DataServiceType serviceType);

    #endregion

    void ClearCache();
    void ClearWalletCache(int walletId);
}
