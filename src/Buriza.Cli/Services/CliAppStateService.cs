using System.Collections.Concurrent;
using Buriza.Core.Interfaces;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;

namespace Buriza.Cli.Services;

public sealed class CliAppStateService : IBurizaAppStateService
{
    private readonly ConcurrentDictionary<string, string> _addressCache = new();
    private readonly ConcurrentDictionary<string, ServiceConfig> _chainConfigs = new();

    public string? GetCachedAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange)
    {
        string key = BuildAddressKey(walletId, chainInfo, accountIndex, addressIndex, isChange);
        return _addressCache.TryGetValue(key, out string? address) ? address : null;
    }

    public void CacheAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange, string address)
    {
        string key = BuildAddressKey(walletId, chainInfo, accountIndex, addressIndex, isChange);
        _addressCache[key] = address;
    }

    public bool HasCachedAddresses(Guid walletId, ChainInfo chainInfo, int accountIndex)
        => _addressCache.Keys.Any(k => k.StartsWith(BuildAddressPrefix(walletId, chainInfo, accountIndex), StringComparison.Ordinal));

    public void ClearWalletCache(Guid walletId)
    {
        string prefix = $"{walletId:N}|";
        foreach (string key in _addressCache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
            _addressCache.TryRemove(key, out _);
    }

    public ServiceConfig? GetChainConfig(ChainInfo chainInfo)
    {
        string key = BuildChainConfigKey(chainInfo);
        return _chainConfigs.TryGetValue(key, out ServiceConfig? config) ? config : null;
    }

    public void SetChainConfig(ChainInfo chainInfo, ServiceConfig config)
    {
        string key = BuildChainConfigKey(chainInfo);
        _chainConfigs[key] = config;
    }

    public void ClearChainConfig(ChainInfo chainInfo)
    {
        string key = BuildChainConfigKey(chainInfo);
        _chainConfigs.TryRemove(key, out _);
    }

    public void ClearCache()
    {
        _addressCache.Clear();
        _chainConfigs.Clear();
    }

    private static string BuildAddressKey(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange)
        => $"{BuildAddressPrefix(walletId, chainInfo, accountIndex)}|{addressIndex}|{(isChange ? 1 : 0)}";

    private static string BuildAddressPrefix(Guid walletId, ChainInfo chainInfo, int accountIndex)
        => $"{walletId:N}|{(int)chainInfo.Chain}|{(int)chainInfo.Network}|{accountIndex}";

    private static string BuildChainConfigKey(ChainInfo chainInfo)
        => $"{(int)chainInfo.Chain}:{(int)chainInfo.Network}";
}
