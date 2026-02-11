using Buriza.Cli.Services;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Services;
using Buriza.Core.Storage;
using Buriza.Data.Models;
using Buriza.Data.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Buriza.Cli;

public static class CliBootstrap
{
    public static ServiceProvider ConfigureServices()
    {
        ServiceCollection services = new();

        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton<BurizaCliStorageService>();
        services.AddSingleton<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaCliStorageService>());

        ChainProviderSettings settings = config
            .GetSection("ChainProviderSettings")
            .Get<ChainProviderSettings>() ?? new ChainProviderSettings();
        services.AddSingleton(settings);

        services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
        services.AddSingleton<IWalletManager, WalletManagerService>();

        return services.BuildServiceProvider();
    }
}
