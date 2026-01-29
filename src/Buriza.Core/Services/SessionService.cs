using System.Collections.Concurrent;
using Buriza.Core.Interfaces;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Services;

public class SessionService : ISessionService
{
    // Cache derived addresses: key = "walletId:chain:accountIndex:isChange:addressIndex"
    private readonly ConcurrentDictionary<string, string> _addressCache = new();

    // Cache decrypted custom API keys: key = "apikey:chain:network"
    private readonly ConcurrentDictionary<string, string> _apiKeyCache = new();

    // Cache custom endpoints: key = "endpoint:chain:network"
    private readonly ConcurrentDictionary<string, string> _endpointCache = new();

    // Lock for atomic cache clearing operations
    private readonly object _clearLock = new();

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
        // Lock to prevent race condition where new keys could be added between snapshot and removal
        lock (_clearLock)
        {
            string prefix = $"{walletId}:";
            foreach (string key in _addressCache.Keys.Where(k => k.StartsWith(prefix)).ToList())
            {
                _addressCache.TryRemove(key, out _);
            }
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

    public string? GetCustomEndpoint(ChainType chain, NetworkType network)
    {
        string key = BuildEndpointKey(chain, network);
        return _endpointCache.TryGetValue(key, out string? endpoint) ? endpoint : null;
    }

    public void SetCustomEndpoint(ChainType chain, NetworkType network, string endpoint)
    {
        string key = BuildEndpointKey(chain, network);
        _endpointCache[key] = endpoint;
    }

    public void ClearCustomEndpoint(ChainType chain, NetworkType network)
    {
        string key = BuildEndpointKey(chain, network);
        _endpointCache.TryRemove(key, out _);
    }

    private static string BuildApiKeyKey(ChainType chain, NetworkType network)
        => $"apikey:{(int)chain}:{(int)network}";

    private static string BuildEndpointKey(ChainType chain, NetworkType network)
        => $"endpoint:{(int)chain}:{(int)network}";

    #endregion

    public void ClearCache()
    {
        _addressCache.Clear();
        _apiKeyCache.Clear();
        _endpointCache.Clear();
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
