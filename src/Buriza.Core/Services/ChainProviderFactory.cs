using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Core.Providers;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Services;

/// <summary>
/// Factory for creating chain providers from configuration.
/// Caches providers by config to avoid recreating for same settings.
/// </summary>
public class ChainProviderFactory(ChainProviderSettings settings) : IChainProviderFactory, IDisposable
{
    private readonly Dictionary<string, IChainProvider> _providerCache = [];
    private readonly object _lock = new();
    private readonly ChainProviderSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private bool _disposed;

    public IChainProvider Create(ProviderConfig config)
    {
        string cacheKey = GetCacheKey(config);

        lock (_lock)
        {
            if (_providerCache.TryGetValue(cacheKey, out IChainProvider? cached))
                return cached;

            IChainProvider provider = CreateProvider(config);
            _providerCache[cacheKey] = provider;
            return provider;
        }
    }

    public ProviderConfig GetDefaultConfig(ChainType chain, NetworkType network = NetworkType.Mainnet)
    {
        return chain switch
        {
            ChainType.Cardano => GetCardanoConfig(network),
            _ => throw new NotSupportedException($"Chain {chain} is not supported")
        };
    }

    private ProviderConfig GetCardanoConfig(NetworkType network)
    {
        string? apiKey = network switch
        {
            NetworkType.Mainnet => _settings.Cardano?.MainnetApiKey,
            NetworkType.Preprod => _settings.Cardano?.PreprodApiKey,
            NetworkType.Preview => _settings.Cardano?.PreviewApiKey,
            _ => _settings.Cardano?.MainnetApiKey
        };

        return network switch
        {
            NetworkType.Mainnet => ProviderPresets.Cardano.DemeterMainnet(apiKey ?? ""),
            NetworkType.Preprod => ProviderPresets.Cardano.DemeterPreprod(apiKey ?? ""),
            NetworkType.Preview => ProviderPresets.Cardano.DemeterPreview(apiKey ?? ""),
            _ => ProviderPresets.Cardano.DemeterMainnet(apiKey ?? "")
        };
    }

    public IReadOnlyList<ChainType> GetSupportedChains()
        => [ChainType.Cardano];

    public async Task<bool> ValidateConfigAsync(ProviderConfig config, CancellationToken ct = default)
    {
        try
        {
            using IChainProvider provider = CreateProvider(config);
            return await provider.ValidateConnectionAsync(ct);
        }
        catch
        {
            return false;
        }
    }

    private static IChainProvider CreateProvider(ProviderConfig config)
    {
        return config.Chain switch
        {
            ChainType.Cardano => new CardanoProvider(config.Endpoint, config.Network, config.ApiKey),
            _ => throw new NotSupportedException($"Chain {config.Chain} is not supported")
        };
    }

    private static string GetCacheKey(ProviderConfig config)
        => $"{config.Chain}:{config.Endpoint}:{config.Network}:{config.ApiKey ?? ""}";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (IChainProvider provider in _providerCache.Values)
            {
                provider.Dispose();
            }
            _providerCache.Clear();
        }

        GC.SuppressFinalize(this);
    }
}
