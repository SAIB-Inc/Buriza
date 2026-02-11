using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
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

        BurizaU5CProvider concrete = Assert.IsType<BurizaU5CProvider>(provider);
        Assert.Equal(NetworkType.Preprod, concrete.NetworkType);
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
}
