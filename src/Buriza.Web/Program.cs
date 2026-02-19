using Buriza.Core.Interfaces;
using Buriza.Core.Services;
using Buriza.Core.Storage;
using Buriza.Data.Models;
using Buriza.Data.Services;
using Buriza.UI.Services;
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

builder.Services.AddScoped<JavaScriptBridgeService>();
builder.Services.AddScoped<BurizaSnackbarService>();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Platform storage
builder.Services.AddScoped<BurizaWebStorageService>();
builder.Services.AddScoped<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaWebStorageService>());

// Buriza services
builder.Services.AddSingleton<AppStateService>();
builder.Services.AddSingleton<IBurizaAppStateService>(sp => sp.GetRequiredService<AppStateService>());
builder.Services.AddSingleton<ChainProviderSettings>();
builder.Services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
builder.Services.AddScoped<WalletManagerService>();

await builder.Build().RunAsync();
