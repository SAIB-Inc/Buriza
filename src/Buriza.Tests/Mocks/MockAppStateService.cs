using System.Collections.Concurrent;
using Buriza.Core.Interfaces;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;

namespace Buriza.Tests.Mocks;

/// <summary>
/// In-memory implementation of IBurizaAppStateService for testing.
/// </summary>
public class MockAppStateService : IBurizaAppStateService
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    private static string GetChainKey(ChainInfo chainInfo) => $"{(int)chainInfo.Chain}:{(int)chainInfo.Network}";

    #region Address Cache

    public string? GetCachedAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange)
    {
        string key = $"address:{walletId}:{GetChainKey(chainInfo)}:{accountIndex}:{(isChange ? 1 : 0)}:{addressIndex}";
        return _cache.TryGetValue(key, out string? value) ? value : null;
    }

    public void CacheAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange, string address)
    {
        string key = $"address:{walletId}:{GetChainKey(chainInfo)}:{accountIndex}:{(isChange ? 1 : 0)}:{addressIndex}";
        _cache.TryAdd(key, address);
    }

    public bool HasCachedAddresses(Guid walletId, ChainInfo chainInfo, int accountIndex)
        => _cache.Keys.Any(k => k.StartsWith($"address:{walletId}:{GetChainKey(chainInfo)}:{accountIndex}:"));

    public void ClearWalletCache(Guid walletId)
    {
        foreach (string key in _cache.Keys.Where(k => k.StartsWith($"address:{walletId}:")).ToList())
            _cache.TryRemove(key, out _);
    }

    #endregion

    #region Custom Chain Config

    public ServiceConfig? GetChainConfig(ChainInfo chainInfo)
    {
        string key = $"chain:{GetChainKey(chainInfo)}";
        _cache.TryGetValue($"{key}:endpoint", out string? endpoint);
        _cache.TryGetValue($"{key}:apiKey", out string? apiKey);
        return endpoint is null && apiKey is null ? null : new ServiceConfig(endpoint, apiKey);
    }

    public void SetChainConfig(ChainInfo chainInfo, ServiceConfig config)
    {
        string key = $"chain:{GetChainKey(chainInfo)}";
        if (config.Endpoint is not null)
            _cache[$"{key}:endpoint"] = config.Endpoint;
        else
            _cache.TryRemove($"{key}:endpoint", out _);

        if (config.ApiKey is not null)
            _cache[$"{key}:apiKey"] = config.ApiKey;
        else
            _cache.TryRemove($"{key}:apiKey", out _);
    }

    public void ClearChainConfig(ChainInfo chainInfo)
    {
        string key = $"chain:{GetChainKey(chainInfo)}";
        _cache.TryRemove($"{key}:endpoint", out _);
        _cache.TryRemove($"{key}:apiKey", out _);
    }

    #endregion

    public void ClearCache() => _cache.Clear();
}
