using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blazor.BrowserExtension;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Security;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Services;
using Buriza.Data.Models;
using Buriza.Data.Services;
using Buriza.Extension;
using Buriza.UI.Storage;
using MudBlazor;
using MudBlazor.Services;
using Buriza.UI.Services;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.HideTransitionDuration = 300;
    config.SnackbarConfiguration.ShowTransitionDuration = 300;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

builder.Services.AddSingleton<AppStateService>();
builder.Services.AddSingleton<IBurizaAppStateService>(sp => sp.GetRequiredService<AppStateService>());
builder.Services.AddScoped<JavaScriptBridgeService>();
builder.Services.AddScoped<BurizaSnackbarService>();

builder.UseBrowserExtension(browserExtension =>
{
    if (browserExtension.Mode == BrowserExtensionMode.Background)
    {
        builder.RootComponents.AddBackgroundWorker<BackgroundWorker>();
    }
    else
    {
        builder.RootComponents.Add<Buriza.UI.Components.App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");
    }
});

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Platform storage
builder.Services.AddScoped<IPlatformStorage, BrowserPlatformStorage>();

// Buriza.Core services
builder.Services.AddSingleton<ChainProviderSettings>();
builder.Services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
builder.Services.AddSingleton<IBiometricService, NullBiometricService>();
builder.Services.AddScoped<BurizaStorageService>();
builder.Services.AddScoped<IWalletManager, WalletManagerService>();

await builder.Build().RunAsync();
