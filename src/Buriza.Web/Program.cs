using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Services;
using Buriza.UI.Services;
using Buriza.UI.Storage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Buriza.UI.Components.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
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

builder.Services.AddScoped<AppStateService>();
builder.Services.AddScoped<JavaScriptBridgeService>();
builder.Services.AddScoped<BurizaSnackbarService>();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Storage providers
builder.Services.AddScoped<IStorageProvider, BrowserStorageProvider>();
builder.Services.AddScoped<ISecureStorageProvider, BrowserStorageProvider>();
builder.Services.AddScoped<IWalletStorage, WalletStorageService>();

// Buriza.Core services
builder.Services.AddSingleton<IChainRegistry, ChainRegistry>();
builder.Services.AddSingleton<ISessionService, SessionService>();
builder.Services.AddSingleton<IKeyService, KeyService>();
builder.Services.AddScoped<IWalletManager, WalletManagerService>();

// Web platform: Password-only authentication
// No biometrics or PIN - use IWalletStorage.UnlockVaultAsync() directly

await builder.Build().RunAsync();
