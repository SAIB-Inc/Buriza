using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Services;
using Buriza.Data.Models;
using Buriza.Data.Providers;

namespace Buriza.Data.Services;

/// <summary>
/// Factory for creating chain providers and key services.
/// Uses session-scoped overrides, falls back to ChainProviderSettings.
/// </summary>
public class BurizaChainProviderFactory(
    ChainProviderSettings settings) : IBurizaChainProviderFactory
{
    private readonly ChainProviderSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly Dictionary<string, ServiceConfig> _sessionConfigs = [];
    private bool _disposed;

    public IBurizaChainProvider CreateProvider(ChainInfo chainInfo)
    {
        ServiceConfig? customConfig = GetSessionConfig(chainInfo);
        string? endpoint = Normalize(customConfig?.Endpoint) ?? GetDefaultEndpoint(chainInfo);
        string? apiKey = Normalize(customConfig?.ApiKey) ?? GetDefaultApiKey(chainInfo);

        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException($"No endpoint configured for {chainInfo.Chain} {chainInfo.Network}");

        return new BurizaU5CProvider(endpoint, apiKey, chainInfo.Network);
    }

    public IBurizaChainProvider CreateProvider(string endpoint, string? apiKey = null)
    {
        if (string.IsNullOrEmpty(endpoint))
            throw new ArgumentException("Endpoint is required", nameof(endpoint));

        return new BurizaU5CProvider(endpoint, apiKey);
    }

    public IKeyService CreateKeyService(ChainInfo chainInfo)
    {
        return chainInfo.Chain switch
        {
            ChainType.Cardano => new CardanoKeyService(chainInfo.Network),
            _ => throw new NotSupportedException($"Chain type {chainInfo.Chain} is not supported")
        };
    }

    public IReadOnlyList<ChainType> GetSupportedChains() => [ChainType.Cardano];

    public async Task<bool> ValidateConnectionAsync(string endpoint, string? apiKey = null, CancellationToken ct = default)
    {
        try
        {
            using BurizaU5CProvider provider = new(endpoint, apiKey);
            return await provider.ValidateConnectionAsync(ct);
        }
        catch
        {
            return false;
        }
    }

    private string? GetDefaultEndpoint(ChainInfo chainInfo)
    {
        return chainInfo.Chain switch
        {
            ChainType.Cardano => chainInfo.Network switch
            {
                NetworkType.Mainnet => _settings.Cardano?.MainnetEndpoint,
                NetworkType.Preprod => _settings.Cardano?.PreprodEndpoint,
                NetworkType.Preview => _settings.Cardano?.PreviewEndpoint,
                _ => _settings.Cardano?.MainnetEndpoint
            },
            _ => null
        };
    }

    private string? GetDefaultApiKey(ChainInfo chainInfo)
    {
        return chainInfo.Chain switch
        {
            ChainType.Cardano => chainInfo.Network switch
            {
                NetworkType.Mainnet => _settings.Cardano?.MainnetApiKey,
                NetworkType.Preprod => _settings.Cardano?.PreprodApiKey,
                NetworkType.Preview => _settings.Cardano?.PreviewApiKey,
                _ => _settings.Cardano?.MainnetApiKey
            },
            _ => null
        };
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    public void SetChainConfig(ChainInfo chainInfo, ServiceConfig config)
    {
        _sessionConfigs[GetConfigKey(chainInfo)] = config;
    }

    public void ClearChainConfig(ChainInfo chainInfo)
    {
        _sessionConfigs.Remove(GetConfigKey(chainInfo));
    }

    private ServiceConfig? GetSessionConfig(ChainInfo chainInfo)
        => _sessionConfigs.TryGetValue(GetConfigKey(chainInfo), out ServiceConfig? config) ? config : null;

    private static string GetConfigKey(ChainInfo chainInfo)
        => $"{(int)chainInfo.Chain}:{(int)chainInfo.Network}";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
