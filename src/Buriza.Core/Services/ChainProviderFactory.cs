using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Core.Providers;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Services;

/// <summary>
/// Factory for creating chain providers from configuration.
/// Caches providers by config to avoid recreating for same settings.
/// Checks session for custom API keys before falling back to appsettings defaults.
/// </summary>
public class ChainProviderFactory(ChainProviderSettings settings, ISessionService? sessionService = null) : IChainProviderFactory, IDisposable
{
    private readonly Dictionary<string, IChainProvider> _providerCache = [];
    private readonly object _lock = new();
    private readonly ChainProviderSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly ISessionService? _sessionService = sessionService;
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
        // Check session for custom endpoint
        string? customEndpoint = _sessionService?.GetCustomEndpoint(ChainType.Cardano, network);

        // Check session for custom API key
        string? apiKey = _sessionService?.GetCustomApiKey(ChainType.Cardano, network);

        // Fall back to appsettings default for API key
        apiKey ??= network switch
        {
            NetworkType.Mainnet => _settings.Cardano?.MainnetApiKey,
            NetworkType.Preprod => _settings.Cardano?.PreprodApiKey,
            NetworkType.Preview => _settings.Cardano?.PreviewApiKey,
            _ => _settings.Cardano?.MainnetApiKey
        };

        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException($"API key for Cardano {network} not configured");

        // Use custom endpoint if set, otherwise use preset
        if (!string.IsNullOrEmpty(customEndpoint))
        {
            return new ProviderConfig
            {
                Chain = ChainType.Cardano,
                Network = network,
                Endpoint = customEndpoint,
                ApiKey = apiKey
            };
        }

        return network switch
        {
            NetworkType.Mainnet => ProviderPresets.Cardano.DemeterMainnet(apiKey),
            NetworkType.Preprod => ProviderPresets.Cardano.DemeterPreprod(apiKey),
            NetworkType.Preview => ProviderPresets.Cardano.DemeterPreview(apiKey),
            _ => ProviderPresets.Cardano.DemeterMainnet(apiKey)
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
    {
        // Hash the API key to avoid exposing it in cache keys (prevents leakage via logging/debugging)
        string apiKeyHash = string.IsNullOrEmpty(config.ApiKey)
            ? string.Empty
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(config.ApiKey)))[..16];
        return $"{config.Chain}:{config.Endpoint}:{config.Network}:{apiKeyHash}";
    }

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
