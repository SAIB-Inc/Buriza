using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Buriza.UI.Services;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Interfaces.Security;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Services;
using Buriza.App.Storage;
using Buriza.App.Services.Security;

namespace Buriza.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		MauiAppBuilder builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif
		builder.Services.AddMudServices();
		builder.Services.AddSingleton<AppStateService>();
		builder.Services.AddScoped<JavaScriptBridgeService>();

		// MAUI Essentials
		builder.Services.AddSingleton(Preferences.Default);

		// Storage providers
		builder.Services.AddSingleton<IStorageProvider, MauiStorageProvider>();
		builder.Services.AddSingleton<ISecureStorageProvider, MauiStorageProvider>();
		builder.Services.AddSingleton<IWalletStorage, WalletStorageService>();

		// Buriza.Core services
		builder.Services.AddSingleton<IChainRegistry, ChainRegistry>();
		builder.Services.AddSingleton<ISessionService, SessionService>();
		builder.Services.AddSingleton<IKeyService, KeyService>();
		builder.Services.AddSingleton<IWalletManager, WalletManagerService>();

		// Authentication services
		builder.Services.AddSingleton<IBiometricService, PlatformBiometricService>();
		builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();

		return builder.Build();
	}
}
