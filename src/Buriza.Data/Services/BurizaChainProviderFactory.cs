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
    IBurizaAppStateService appState,
    ChainProviderSettings settings) : IBurizaChainProviderFactory
{
    private readonly IBurizaAppStateService _appState = appState ?? throw new ArgumentNullException(nameof(appState));
    private readonly ChainProviderSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private bool _disposed;

    public IBurizaChainProvider CreateProvider(ChainInfo chainInfo)
    {
        ServiceConfig? customConfig = _appState.GetChainConfig(chainInfo);
        string? endpoint = Normalize(customConfig?.Endpoint) ?? GetDefaultEndpoint(chainInfo);
        string? apiKey = Normalize(customConfig?.ApiKey) ?? GetDefaultApiKey(chainInfo);

        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException($"No endpoint configured for {chainInfo.Chain} {chainInfo.Network}");

        ValidateEndpointSecurity(endpoint);
        return new BurizaU5CProvider(endpoint, apiKey, chainInfo.Network);
    }

    public IBurizaChainProvider CreateProvider(string endpoint, string? apiKey = null)
    {
        if (string.IsNullOrEmpty(endpoint))
            throw new ArgumentException("Endpoint is required", nameof(endpoint));

        ValidateEndpointSecurity(endpoint);
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
        catch (Exception)
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

    private void ValidateEndpointSecurity(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
            throw new InvalidOperationException("Invalid provider endpoint configured.");

        if (uri.Scheme != "http" && uri.Scheme != "https")
            throw new InvalidOperationException("Provider endpoint must use HTTP or HTTPS.");

        if (uri.Scheme == "http" && !uri.IsLoopback)
            throw new InvalidOperationException("HTTP endpoints are allowed only for localhost/loopback.");

        List<string> allowedHosts = [.. _settings.Cardano?.AllowedEndpointHosts?
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim()) ?? []];

        // Allow local development regardless of allowlist.
        if (uri.IsLoopback || allowedHosts.Count == 0)
            return;

        bool isAllowed = allowedHosts.Any(host =>
            string.Equals(host, uri.Host, StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
            throw new InvalidOperationException(
                $"Provider endpoint host '{uri.Host}' is not in the configured allowlist.");
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    public void SetChainConfig(ChainInfo chainInfo, ServiceConfig config)
        => _appState.SetChainConfig(chainInfo, config);

    public void ClearChainConfig(ChainInfo chainInfo)
        => _appState.ClearChainConfig(chainInfo);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
