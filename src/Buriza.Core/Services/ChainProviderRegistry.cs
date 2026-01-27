using Buriza.Core.Interfaces.Chain;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Services;

public class ChainProviderRegistry
{
    private readonly Dictionary<ChainType, IChainProvider> _providers = [];

    public void Register(IChainProvider provider)
    {
        _providers[provider.ChainInfo.Chain] = provider;
    }

    public IChainProvider GetProvider(ChainType chain)
    {
        if (_providers.TryGetValue(chain, out IChainProvider? provider))
        {
            return provider;
        }

        throw new NotSupportedException($"Chain {chain} is not registered");
    }

    public bool HasProvider(ChainType chain)
    {
        return _providers.ContainsKey(chain);
    }

    public IEnumerable<IChainProvider> GetAllProviders()
    {
        return _providers.Values;
    }
}
