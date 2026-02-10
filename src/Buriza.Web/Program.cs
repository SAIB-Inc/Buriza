using Buriza.Core.Interfaces.Security;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Services;
using Buriza.UI.Extensions;
using Buriza.UI.Services;
using Buriza.UI.Storage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection;

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
builder.Services.AddScoped<IPlatformStorage, BrowserPlatformStorage>();
builder.Services.AddScoped<IPlatformSecureStorage, NullPlatformSecureStorage>();
builder.Services.AddSingleton(new BurizaStorageOptions { Mode = StorageMode.VaultEncryption });

// Buriza services
builder.Services.AddSingleton<IBiometricService, NullBiometricService>();
builder.Services.AddBurizaServices(ServiceLifetime.Scoped);

await builder.Build().RunAsync();
