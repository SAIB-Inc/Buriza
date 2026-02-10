using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Services;
using Buriza.Data.Models;
using Buriza.Data.Services;
using Buriza.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Buriza.UI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBurizaServices(this IServiceCollection services, ServiceLifetime coreLifetime)
    {
        services.AddSingleton<AppStateService>();
        services.AddSingleton<IBurizaAppStateService>(sp => sp.GetRequiredService<AppStateService>());

        services.AddSingleton<ChainProviderSettings>();
        services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();

        switch (coreLifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<BurizaStorageService>();
                services.AddSingleton<IWalletManager, WalletManagerService>();
                break;
            case ServiceLifetime.Scoped:
                services.AddScoped<BurizaStorageService>();
                services.AddScoped<IWalletManager, WalletManagerService>();
                break;
            default:
                services.AddTransient<BurizaStorageService>();
                services.AddTransient<IWalletManager, WalletManagerService>();
                break;
        }

        return services;
    }
}
