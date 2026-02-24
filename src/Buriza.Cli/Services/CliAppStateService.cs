using Buriza.Core.Interfaces;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;

namespace Buriza.Cli.Services;

/// <summary>
/// Minimal in-memory app state for CLI sessions.
/// </summary>
public sealed class CliAppStateService : IBurizaAppStateService
{
    private readonly Dictionary<string, string> _addressCache = [];
    private readonly Dictionary<string, ServiceConfig> _chainConfigs = [];

    public string? GetCachedAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange)
    {
        string key = GetAddressKey(walletId, chainInfo, accountIndex, addressIndex, isChange);
        return _addressCache.TryGetValue(key, out string? value) ? value : null;
    }

    public void CacheAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange, string address)
    {
        string key = GetAddressKey(walletId, chainInfo, accountIndex, addressIndex, isChange);
        _addressCache[key] = address;
    }

    public bool HasCachedAddresses(Guid walletId, ChainInfo chainInfo, int accountIndex)
        => _addressCache.Keys.Any(k => k.StartsWith(GetAddressPrefix(walletId, chainInfo, accountIndex), StringComparison.Ordinal));

    public void ClearWalletCache(Guid walletId)
    {
        string prefix = $"address:{walletId:N}:";
        foreach (string key in _addressCache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            _addressCache.Remove(key);
    }

    public ServiceConfig? GetChainConfig(ChainInfo chainInfo)
        => _chainConfigs.TryGetValue(GetChainKey(chainInfo), out ServiceConfig? config) ? config : null;

    public void SetChainConfig(ChainInfo chainInfo, ServiceConfig config)
        => _chainConfigs[GetChainKey(chainInfo)] = config;

    public void ClearChainConfig(ChainInfo chainInfo)
        => _chainConfigs.Remove(GetChainKey(chainInfo));

    public void ClearCache()
    {
        _addressCache.Clear();
        _chainConfigs.Clear();
    }

    private static string GetChainKey(ChainInfo chainInfo)
        => $"{(int)chainInfo.Chain}:{chainInfo.Network}";

    private static string GetAddressPrefix(Guid walletId, ChainInfo chainInfo, int accountIndex)
        => $"address:{walletId:N}:{GetChainKey(chainInfo)}:{accountIndex}:";

    private static string GetAddressKey(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange)
        => $"{GetAddressPrefix(walletId, chainInfo, accountIndex)}{(isChange ? 1 : 0)}:{addressIndex}";
}
