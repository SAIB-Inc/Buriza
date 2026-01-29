using System.Collections.Concurrent;
using Buriza.Core.Interfaces;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Services;

public class SessionService : ISessionService
{
    // Cache derived addresses: key = "walletId:chain:accountIndex:isChange:addressIndex"
    private readonly ConcurrentDictionary<string, string> _addressCache = new();

    // Cache decrypted custom API keys: key = "chain:network"
    private readonly ConcurrentDictionary<string, string> _apiKeyCache = new();

    private bool _disposed;

    #region Address Cache

    public string? GetCachedAddress(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange)
    {
        string key = BuildAddressKey(walletId, chain, accountIndex, addressIndex, isChange);
        return _addressCache.TryGetValue(key, out string? address) ? address : null;
    }

    public void CacheAddress(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange, string address)
    {
        string key = BuildAddressKey(walletId, chain, accountIndex, addressIndex, isChange);
        _addressCache.TryAdd(key, address);
    }

    public bool HasCachedAddresses(int walletId, ChainType chain, int accountIndex)
    {
        string prefix = $"{walletId}:{(int)chain}:{accountIndex}:";
        return _addressCache.Keys.Any(k => k.StartsWith(prefix));
    }

    public void ClearWalletCache(int walletId)
    {
        string prefix = $"{walletId}:";
        foreach (string key in _addressCache.Keys.Where(k => k.StartsWith(prefix)).ToList())
        {
            _addressCache.TryRemove(key, out _);
        }
    }

    private static string BuildAddressKey(int walletId, ChainType chain, int accountIndex, int addressIndex, bool isChange)
        => $"{walletId}:{(int)chain}:{accountIndex}:{(isChange ? 1 : 0)}:{addressIndex}";

    #endregion

    #region Custom API Key Cache

    public string? GetCustomApiKey(ChainType chain, NetworkType network)
    {
        string key = BuildApiKeyKey(chain, network);
        return _apiKeyCache.TryGetValue(key, out string? apiKey) ? apiKey : null;
    }

    public void SetCustomApiKey(ChainType chain, NetworkType network, string apiKey)
    {
        string key = BuildApiKeyKey(chain, network);
        _apiKeyCache[key] = apiKey;
    }

    public bool HasCustomApiKey(ChainType chain, NetworkType network)
    {
        string key = BuildApiKeyKey(chain, network);
        return _apiKeyCache.ContainsKey(key);
    }

    public void ClearCustomApiKey(ChainType chain, NetworkType network)
    {
        string key = BuildApiKeyKey(chain, network);
        _apiKeyCache.TryRemove(key, out _);
    }

    private static string BuildApiKeyKey(ChainType chain, NetworkType network)
        => $"apikey:{(int)chain}:{(int)network}";

    #endregion

    public void ClearCache()
    {
        _addressCache.Clear();
        _apiKeyCache.Clear();
    }

    public virtual void Dispose()
    {
        if (!_disposed)
        {
            ClearCache();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
