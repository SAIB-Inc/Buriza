using System.Collections.Concurrent;
using Buriza.Core.Interfaces;
using Buriza.Data.Models.Enums;
using Buriza.Data.Services;

namespace Buriza.Core.Services;

public class SessionService : ISessionService, IDataServiceSessionProvider
{
    // Single cache for all session data
    // Key patterns:
    //   - address:{walletId}:{chain}:{accountIndex}:{isChange}:{addressIndex}
    //   - chainEndpoint:{chain}:{network}
    //   - chainApiKey:{chain}:{network}
    //   - dataEndpoint:{serviceType}
    //   - dataApiKey:{serviceType}
    private readonly ConcurrentDictionary<string, string> _cache = new();

    private bool _disposed;

    #region Cache Helpers

    private string? Get(string key)
        => _cache.TryGetValue(key, out string? value) ? value : null;

    private void Set(string key, string value)
        => _cache[key] = value;

    private bool TryAdd(string key, string value)
        => _cache.TryAdd(key, value);

    private void Remove(string key)
        => _cache.TryRemove(key, out _);

    private void RemoveByPrefix(string prefix)
    {
        foreach (string key in _cache.Keys.Where(k => k.StartsWith(prefix)).ToList())
        {
            _cache.TryRemove(key, out _);
        }
    }

    #endregion

    #region Address Cache

    public string? GetCachedAddress(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange)
        => Get($"address:{walletId}:{(int)chain}:{accountIndex}:{(isChange ? 1 : 0)}:{addressIndex}");

    public void CacheAddress(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange, string address)
        => TryAdd($"address:{walletId}:{(int)chain}:{accountIndex}:{(isChange ? 1 : 0)}:{addressIndex}", address);

    public bool HasCachedAddresses(int walletId, ChainType chain, int accountIndex)
        => _cache.Keys.Any(k => k.StartsWith($"address:{walletId}:{(int)chain}:{accountIndex}:"));

    public void ClearWalletCache(int walletId)
        => RemoveByPrefix($"address:{walletId}:");

    #endregion

    #region Chain Provider Config Cache

    public string? GetCustomApiKey(ChainType chain, NetworkType network)
        => Get($"chainApiKey:{(int)chain}:{(int)network}");

    public void SetCustomApiKey(ChainType chain, NetworkType network, string apiKey)
        => Set($"chainApiKey:{(int)chain}:{(int)network}", apiKey);

    public bool HasCustomApiKey(ChainType chain, NetworkType network)
        => _cache.ContainsKey($"chainApiKey:{(int)chain}:{(int)network}");

    public void ClearCustomApiKey(ChainType chain, NetworkType network)
        => Remove($"chainApiKey:{(int)chain}:{(int)network}");

    public string? GetCustomEndpoint(ChainType chain, NetworkType network)
        => Get($"chainEndpoint:{(int)chain}:{(int)network}");

    public void SetCustomEndpoint(ChainType chain, NetworkType network, string endpoint)
        => Set($"chainEndpoint:{(int)chain}:{(int)network}", endpoint);

    public void ClearCustomEndpoint(ChainType chain, NetworkType network)
        => Remove($"chainEndpoint:{(int)chain}:{(int)network}");

    #endregion

    #region Data Service Config Cache

    public string? GetDataServiceEndpoint(DataServiceType serviceType)
        => Get($"dataEndpoint:{(int)serviceType}");

    public void SetDataServiceEndpoint(DataServiceType serviceType, string endpoint)
        => Set($"dataEndpoint:{(int)serviceType}", endpoint);

    public void ClearDataServiceEndpoint(DataServiceType serviceType)
        => Remove($"dataEndpoint:{(int)serviceType}");

    public string? GetDataServiceApiKey(DataServiceType serviceType)
        => Get($"dataApiKey:{(int)serviceType}");

    public void SetDataServiceApiKey(DataServiceType serviceType, string apiKey)
        => Set($"dataApiKey:{(int)serviceType}", apiKey);

    public void ClearDataServiceApiKey(DataServiceType serviceType)
        => Remove($"dataApiKey:{(int)serviceType}");

    #endregion

    public void ClearCache() => _cache.Clear();

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _cache.Clear();
        }

        _disposed = true;
    }
}
