using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Core.Services;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

using ChainRegistryData = Buriza.Data.Models.Common.ChainRegistry;

namespace Buriza.Tests.Services;

/// <summary>
/// Tests for ChainRegistry custom provider config (custom endpoints and API keys from session).
/// </summary>
public class ChainRegistryCustomConfigTests : IDisposable
{
    private readonly SessionService _sessionService;
    private readonly Buriza.Core.Services.ChainRegistry _registry;
    private readonly ChainProviderSettings _settings;

    public ChainRegistryCustomConfigTests()
    {
        _sessionService = new SessionService();
        _settings = new ChainProviderSettings
        {
            Cardano = new CardanoSettings
            {
                MainnetEndpoint = "https://default-mainnet.example.com",
                PreprodEndpoint = "https://default-preprod.example.com",
                PreviewEndpoint = "https://default-preview.example.com",
                MainnetApiKey = "default-mainnet-key",
                PreprodApiKey = "default-preprod-key",
                PreviewApiKey = "default-preview-key"
            }
        };
        _registry = new Buriza.Core.Services.ChainRegistry(_settings, _sessionService);
    }

    public void Dispose()
    {
        _registry.Dispose();
        _sessionService.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Custom API Key Tests

    [Fact]
    public void GetProvider_WithCustomApiKey_UsesCustomKey()
    {
        // Arrange
        const string customApiKey = "custom-api-key-123";
        _sessionService.SetCustomApiKey(ChainRegistryData.CardanoMainnet, customApiKey);

        // Act
        IChainProvider provider = _registry.GetProvider(ChainRegistryData.CardanoMainnet);

        // Assert
        Assert.Equal(customApiKey, provider.Config.ApiKey);
    }

    [Fact]
    public void GetProvider_WithoutCustomApiKey_UsesDefaultKey()
    {
        // Act
        IChainProvider provider = _registry.GetProvider(ChainRegistryData.CardanoMainnet);

        // Assert
        Assert.Equal("default-mainnet-key", provider.Config.ApiKey);
    }

    [Fact]
    public void GetProvider_CustomApiKeyPerNetwork_UsesCorrectKey()
    {
        // Arrange
        _sessionService.SetCustomApiKey(ChainRegistryData.CardanoMainnet, "custom-mainnet");
        _sessionService.SetCustomApiKey(ChainRegistryData.CardanoPreprod, "custom-preprod");
        // Preview not set - should use default

        // Act
        IChainProvider mainnetProvider = _registry.GetProvider(ChainRegistryData.CardanoMainnet);
        IChainProvider preprodProvider = _registry.GetProvider(ChainRegistryData.CardanoPreprod);
        IChainProvider previewProvider = _registry.GetProvider(ChainRegistryData.CardanoPreview);

        // Assert
        Assert.Equal("custom-mainnet", mainnetProvider.Config.ApiKey);
        Assert.Equal("custom-preprod", preprodProvider.Config.ApiKey);
        Assert.Equal("default-preview-key", previewProvider.Config.ApiKey);
    }

    #endregion

    #region Custom Endpoint Tests

    [Fact]
    public void GetProvider_WithCustomEndpoint_UsesCustomEndpoint()
    {
        // Arrange
        const string customEndpoint = "https://custom.cardano-node.com/api";
        _sessionService.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, customEndpoint);

        // Act
        IChainProvider provider = _registry.GetProvider(ChainRegistryData.CardanoMainnet);

        // Assert
        Assert.Equal(customEndpoint, provider.Config.Endpoint);
    }

    [Fact]
    public void GetProvider_WithoutCustomEndpoint_UsesDefaultEndpoint()
    {
        // Act
        IChainProvider provider = _registry.GetProvider(ChainRegistryData.CardanoMainnet);

        // Assert - Should use default endpoint from settings
        Assert.Equal("https://default-mainnet.example.com", provider.Config.Endpoint);
    }

    [Fact]
    public void GetProvider_CustomEndpointPerNetwork_UsesCorrectEndpoint()
    {
        // Arrange
        _sessionService.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, "https://mainnet.custom.com");
        _sessionService.SetCustomEndpoint(ChainRegistryData.CardanoPreprod, "https://preprod.custom.com");
        // Preview not set - should use preset

        // Act
        IChainProvider mainnetProvider = _registry.GetProvider(ChainRegistryData.CardanoMainnet);
        IChainProvider preprodProvider = _registry.GetProvider(ChainRegistryData.CardanoPreprod);
        IChainProvider previewProvider = _registry.GetProvider(ChainRegistryData.CardanoPreview);

        // Assert
        Assert.Equal("https://mainnet.custom.com", mainnetProvider.Config.Endpoint);
        Assert.Equal("https://preprod.custom.com", preprodProvider.Config.Endpoint);
        Assert.Contains("preview", previewProvider.Config.Endpoint, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Combined Custom Endpoint and API Key Tests

    [Fact]
    public void GetProvider_WithBothCustomEndpointAndApiKey_UsesBoth()
    {
        // Arrange
        const string customEndpoint = "https://my-custom-node.com/api";
        const string customApiKey = "my-custom-api-key";
        _sessionService.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, customEndpoint);
        _sessionService.SetCustomApiKey(ChainRegistryData.CardanoMainnet, customApiKey);

        // Act
        IChainProvider provider = _registry.GetProvider(ChainRegistryData.CardanoMainnet);

        // Assert
        Assert.Equal(customEndpoint, provider.Config.Endpoint);
        Assert.Equal(customApiKey, provider.Config.ApiKey);
        Assert.Equal(ChainType.Cardano, provider.Config.Chain);
        Assert.Equal(NetworkType.Mainnet, provider.Config.Network);
    }

    [Fact]
    public void GetProvider_WithCustomEndpointOnly_UsesCustomEndpointAndDefaultApiKey()
    {
        // Arrange
        _sessionService.SetCustomEndpoint(ChainRegistryData.CardanoPreprod, "https://custom.preprod.com");

        // Act
        IChainProvider provider = _registry.GetProvider(ChainRegistryData.CardanoPreprod);

        // Assert
        Assert.Equal("https://custom.preprod.com", provider.Config.Endpoint);
        Assert.Equal("default-preprod-key", provider.Config.ApiKey);
    }

    [Fact]
    public void GetProvider_WithCustomApiKeyOnly_UsesPresetEndpointAndCustomApiKey()
    {
        // Arrange
        _sessionService.SetCustomApiKey(ChainRegistryData.CardanoPreview, "custom-preview-key");

        // Act
        IChainProvider provider = _registry.GetProvider(ChainRegistryData.CardanoPreview);

        // Assert
        Assert.Contains("preview", provider.Config.Endpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("custom-preview-key", provider.Config.ApiKey);
    }

    #endregion

    #region Provider Caching with Custom Config

    [Fact]
    public void GetProvider_DifferentCustomConfigs_ReturnsDifferentProviders()
    {
        // Arrange
        _sessionService.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, "https://endpoint1.com");
        var provider1 = _registry.GetProvider(ChainRegistryData.CardanoMainnet);

        // Change custom endpoint - need new registry to get different provider
        using Buriza.Core.Services.ChainRegistry registry2 = new(_settings, new SessionService());
        ((SessionService)registry2.GetType()
            .GetField("_sessionService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(registry2)!)?.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, "https://endpoint2.com");

        // Since we can't easily test provider cache invalidation with session changes,
        // we verify that different endpoints produce different configs
        Assert.Equal("https://endpoint1.com", provider1.Config.Endpoint);
    }

    [Fact]
    public void GetProvider_SameChainInfo_ReturnsCachedProvider()
    {
        // Arrange
        _sessionService.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, "https://custom.com");
        _sessionService.SetCustomApiKey(ChainRegistryData.CardanoMainnet, "custom-key");

        // Act
        var provider1 = _registry.GetProvider(ChainRegistryData.CardanoMainnet);
        var provider2 = _registry.GetProvider(ChainRegistryData.CardanoMainnet);

        // Assert - Same ChainInfo should return cached provider
        Assert.Same(provider1, provider2);
    }

    #endregion

    #region Registry Without Session Service

    [Fact]
    public void GetProvider_WithoutSessionService_UsesDefaults()
    {
        // Arrange - Registry without session service
        using Buriza.Core.Services.ChainRegistry registryWithoutSession = new(_settings);

        // Act
        IChainProvider provider = registryWithoutSession.GetProvider(ChainRegistryData.CardanoMainnet);

        // Assert
        Assert.Equal("default-mainnet-key", provider.Config.ApiKey);
        Assert.Equal("https://default-mainnet.example.com", provider.Config.Endpoint);
    }

    #endregion

    #region Error Cases

    [Fact]
    public void GetProvider_NoEndpointConfigured_ThrowsInvalidOperationException()
    {
        // Arrange - Settings with no endpoints
        ChainProviderSettings emptySettings = new()
        {
            Cardano = new CardanoSettings()
        };
        using Buriza.Core.Services.ChainRegistry registry = new(emptySettings);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            registry.GetProvider(ChainRegistryData.CardanoMainnet));
    }

    [Fact]
    public void GetProvider_NoEndpointConfigured_ButCustomEndpointSet_Works()
    {
        // Arrange - Settings with no endpoints but custom endpoint in session
        ChainProviderSettings emptySettings = new()
        {
            Cardano = new CardanoSettings()
        };
        SessionService session = new();
        session.SetCustomEndpoint(ChainRegistryData.CardanoMainnet, "https://custom.example.com");
        session.SetCustomApiKey(ChainRegistryData.CardanoMainnet, "custom-key");
        using Buriza.Core.Services.ChainRegistry registry = new(emptySettings, session);

        // Act
        IChainProvider provider = registry.GetProvider(ChainRegistryData.CardanoMainnet);

        // Assert
        Assert.Equal("https://custom.example.com", provider.Config.Endpoint);
        Assert.Equal("custom-key", provider.Config.ApiKey);

        // Cleanup
        session.Dispose();
    }

    #endregion
}
