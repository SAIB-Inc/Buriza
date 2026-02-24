using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Data.Models;
using Buriza.Data.Providers;
using Buriza.Data.Services;
using Buriza.Tests.Mocks;

namespace Buriza.Tests.Unit.Services;

public class BurizaChainProviderFactoryTests
{
    [Fact]
    public void CreateProvider_UsesChainNetwork()
    {
        ChainProviderSettings settings = new()
        {
            Cardano = new CardanoSettings
            {
                MainnetEndpoint = "https://test-mainnet.example.com",
                PreprodEndpoint = "https://test-preprod.example.com",
                PreviewEndpoint = "https://test-preview.example.com"
            }
        };

        BurizaChainProviderFactory factory = new(new TestAppStateService(), settings);

        using IBurizaChainProvider provider = factory.CreateProvider(ChainRegistry.CardanoPreprod);

        BurizaUtxoRpcProvider concrete = Assert.IsType<BurizaUtxoRpcProvider>(provider);
        Assert.Equal("preprod", concrete.NetworkType);
    }

    [Fact]
    public void CreateProvider_EmptyCustomEndpoint_FallsBackToDefaults()
    {
        ChainProviderSettings settings = new()
        {
            Cardano = new CardanoSettings
            {
                MainnetEndpoint = "https://test-mainnet.example.com"
            }
        };

        BurizaChainProviderFactory factory = new(new TestAppStateService(), settings);
        factory.SetChainConfig(ChainRegistry.CardanoMainnet, new ServiceConfig(string.Empty, null));

        using IBurizaChainProvider provider = factory.CreateProvider(ChainRegistry.CardanoMainnet);
        Assert.NotNull(provider);
    }

    [Fact]
    public void CreateProvider_NoEndpoint_Throws()
    {
        ChainProviderSettings settings = new()
        {
            Cardano = new CardanoSettings()
        };

        BurizaChainProviderFactory factory = new(new TestAppStateService(), settings);
        factory.SetChainConfig(ChainRegistry.CardanoMainnet, new ServiceConfig(string.Empty, null));

        Assert.Throws<InvalidOperationException>(() => factory.CreateProvider(ChainRegistry.CardanoMainnet));
    }

    [Fact]
    public void CreateProvider_WithAllowlist_AllowsConfiguredHost()
    {
        ChainProviderSettings settings = new()
        {
            Cardano = new CardanoSettings
            {
                MainnetEndpoint = "https://allowed.example.com",
                AllowedEndpointHosts = ["allowed.example.com", "backup.example.com"]
            }
        };

        BurizaChainProviderFactory factory = new(new TestAppStateService(), settings);

        using IBurizaChainProvider provider = factory.CreateProvider(ChainRegistry.CardanoMainnet);
        Assert.NotNull(provider);
    }

    [Fact]
    public void CreateProvider_WithAllowlist_BlocksUnconfiguredHost()
    {
        ChainProviderSettings settings = new()
        {
            Cardano = new CardanoSettings
            {
                MainnetEndpoint = "https://blocked.example.com",
                AllowedEndpointHosts = ["allowed.example.com"]
            }
        };

        BurizaChainProviderFactory factory = new(new TestAppStateService(), settings);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            factory.CreateProvider(ChainRegistry.CardanoMainnet));

        Assert.Contains("allowlist", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateProvider_WithAllowlist_AllowsLoopbackHttp()
    {
        ChainProviderSettings settings = new()
        {
            Cardano = new CardanoSettings
            {
                MainnetEndpoint = "http://127.0.0.1:50051",
                AllowedEndpointHosts = ["allowed.example.com"]
            }
        };

        BurizaChainProviderFactory factory = new(new TestAppStateService(), settings);

        using IBurizaChainProvider provider = factory.CreateProvider(ChainRegistry.CardanoMainnet);
        Assert.NotNull(provider);
    }
}
