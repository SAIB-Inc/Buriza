using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Core.Services;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

using ChainRegistryData = Buriza.Data.Models.Common.ChainRegistry;

namespace Buriza.Tests.Unit.Services;

public class ChainRegistryTests : IDisposable
{
    private readonly Buriza.Core.Services.ChainRegistry _registry;
    private readonly ChainProviderSettings _settings;

    public ChainRegistryTests()
    {
        _settings = new ChainProviderSettings
        {
            Cardano = new CardanoSettings
            {
                MainnetEndpoint = "https://test-mainnet.example.com",
                PreprodEndpoint = "https://test-preprod.example.com",
                PreviewEndpoint = "https://test-preview.example.com",
                MainnetApiKey = "test-mainnet-key",
                PreprodApiKey = "test-preprod-key",
                PreviewApiKey = "test-preview-key"
            }
        };
        _registry = new Buriza.Core.Services.ChainRegistry(_settings);
    }

    public void Dispose()
    {
        _registry.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GetProvider Tests

    [Fact]
    public void GetProvider_WithCardanoChainInfo_ReturnsProvider()
    {
        // Act
        IChainProvider provider = _registry.GetProvider(ChainRegistryData.CardanoPreview);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal(ChainType.Cardano, provider.ChainInfo.Chain);
    }

    [Fact]
    public void GetProvider_SameChainInfoTwice_ReturnsCachedProvider()
    {
        // Act
        IChainProvider provider1 = _registry.GetProvider(ChainRegistryData.CardanoPreview);
        IChainProvider provider2 = _registry.GetProvider(ChainRegistryData.CardanoPreview);

        // Assert
        Assert.Same(provider1, provider2);
    }

    [Fact]
    public void GetProvider_DifferentNetworks_ReturnsDifferentProviders()
    {
        // Act
        IChainProvider provider1 = _registry.GetProvider(ChainRegistryData.CardanoPreview);
        IChainProvider provider2 = _registry.GetProvider(ChainRegistryData.CardanoPreprod);

        // Assert
        Assert.NotSame(provider1, provider2);
    }

    #endregion

    #region Provider Config Tests

    [Fact]
    public void GetProvider_Cardano_ConfigHasCorrectValues()
    {
        // Act
        IChainProvider provider = _registry.GetProvider(ChainRegistryData.CardanoMainnet);

        // Assert
        Assert.Equal(ChainType.Cardano, provider.Config.Chain);
        Assert.Equal(NetworkType.Mainnet, provider.Config.Network);
        Assert.Contains("mainnet", provider.Config.Endpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("test-mainnet-key", provider.Config.ApiKey);
    }

    [Fact]
    public void GetProvider_CardanoPreprod_ConfigHasCorrectValues()
    {
        // Act
        IChainProvider provider = _registry.GetProvider(ChainRegistryData.CardanoPreprod);

        // Assert
        Assert.Equal(ChainType.Cardano, provider.Config.Chain);
        Assert.Equal(NetworkType.Preprod, provider.Config.Network);
        Assert.Contains("preprod", provider.Config.Endpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("test-preprod-key", provider.Config.ApiKey);
    }

    [Fact]
    public void GetProvider_UnsupportedChain_ThrowsNotSupportedException()
    {
        // Act & Assert
        ChainInfo unsupportedChain = new()
        {
            Chain = (ChainType)999,
            Network = NetworkType.Mainnet,
            Symbol = "UNKNOWN",
            Name = "Unknown",
            Decimals = 0
        };
        Assert.Throws<NotSupportedException>(() =>
            _registry.GetProvider(unsupportedChain));
    }

    #endregion

    #region GetSupportedChains Tests

    [Fact]
    public void GetSupportedChains_IncludesCardano()
    {
        // Act
        IReadOnlyList<ChainType> chains = _registry.GetSupportedChains();

        // Assert
        Assert.Contains(ChainType.Cardano, chains);
    }

    #endregion
}
