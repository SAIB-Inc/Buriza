using Buriza.Core.Interfaces;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;

namespace Buriza.Tests.Mocks;

public sealed class TestAppStateService : IBurizaAppStateService
{
    private readonly Dictionary<string, ServiceConfig> _chainConfigs = [];

    public string? GetCachedAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange) => null;
    public void CacheAddress(Guid walletId, ChainInfo chainInfo, int accountIndex, int addressIndex, bool isChange, string address) { }
    public bool HasCachedAddresses(Guid walletId, ChainInfo chainInfo, int accountIndex) => false;
    public void ClearWalletCache(Guid walletId) { }

    public ServiceConfig? GetChainConfig(ChainInfo chainInfo)
        => _chainConfigs.TryGetValue(GetChainKey(chainInfo), out ServiceConfig? config) ? config : null;

    public void SetChainConfig(ChainInfo chainInfo, ServiceConfig config)
        => _chainConfigs[GetChainKey(chainInfo)] = config;

    public void ClearChainConfig(ChainInfo chainInfo)
        => _chainConfigs.Remove(GetChainKey(chainInfo));

    public void ClearCache() => _chainConfigs.Clear();

    private static string GetChainKey(ChainInfo chainInfo)
        => $"{(int)chainInfo.Chain}:{(int)chainInfo.Network}";
}
