using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Core.Providers;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Services;

/// <summary>
/// Registry for chain providers.
/// Caches providers by config to avoid recreating for same settings.
/// Checks session for custom endpoints/API keys before falling back to appsettings.
/// </summary>
public class ChainRegistry(ChainProviderSettings settings, ISessionService? sessionService = null) : IChainRegistry, IDisposable
{
    private readonly Dictionary<string, IChainProvider> _providerCache = [];
    private readonly object _lock = new();
    private readonly ChainProviderSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly ISessionService? _sessionService = sessionService;
    private bool _disposed;

    public IChainProvider GetProvider(ChainInfo chainInfo)
    {
        ProviderConfig config = GetConfig(chainInfo);
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

    /// <summary>
    /// Invalidates all cached providers. Call when config changes require reconnection.
    /// </summary>
    public void InvalidateCache()
    {
        lock (_lock)
        {
            foreach (IChainProvider provider in _providerCache.Values)
                provider.Dispose();
            _providerCache.Clear();
        }
    }

    /// <summary>
    /// Invalidates cached provider for a specific chain. Call when that chain's config changes.
    /// </summary>
    public void InvalidateCache(ChainInfo chainInfo)
    {
        ProviderConfig config = GetConfig(chainInfo);
        string cacheKey = GetCacheKey(config);

        lock (_lock)
        {
            if (_providerCache.TryGetValue(cacheKey, out IChainProvider? cached))
            {
                cached.Dispose();
                _providerCache.Remove(cacheKey);
            }
        }
    }

    private ProviderConfig GetConfig(ChainInfo chainInfo)
    {
        // Get session overrides (if any)
        string? customEndpoint = _sessionService?.GetCustomEndpoint(chainInfo);
        string? customApiKey = _sessionService?.GetCustomApiKey(chainInfo);

        return chainInfo.Chain switch
        {
            ChainType.Cardano => CardanoProvider.ResolveConfig(_settings.Cardano, chainInfo.Network, customEndpoint, customApiKey),
            _ => throw new NotSupportedException($"Chain {chainInfo.Chain} is not supported")
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
