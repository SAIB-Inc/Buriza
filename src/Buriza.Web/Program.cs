using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Services;
using Buriza.Core.Storage;
using Buriza.UI.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Buriza.UI.Components.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddMudServices();

builder.Services.AddScoped<AppStateService>();
builder.Services.AddScoped<JavaScriptBridgeService>();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Buriza.Core services
builder.Services.AddScoped<IWalletStorage, BrowserWalletStorage>();
builder.Services.AddSingleton<IChainRegistry, ChainRegistry>();
builder.Services.AddSingleton<ISessionService, SessionService>();
builder.Services.AddSingleton<IKeyService, KeyService>();
builder.Services.AddScoped<IWalletManager, WalletManagerService>();

await builder.Build().RunAsync();
