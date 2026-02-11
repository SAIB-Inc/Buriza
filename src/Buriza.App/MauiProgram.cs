using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;
using Buriza.UI.Services;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Services;
using Buriza.Core.Storage;
using Buriza.Data.Models;
using Buriza.Data.Services;
using Buriza.App.Services;
using Buriza.App.Services.Security;
using Buriza.Core.Interfaces;

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

		// MAUI Essentials
		builder.Services.AddSingleton(Preferences.Default);

		// Buriza services
		builder.Services.AddSingleton<PlatformBiometricService>();
		builder.Services.AddSingleton<BurizaAppStorageService>();
		builder.Services.AddSingleton<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaAppStorageService>());
		builder.Services.AddSingleton<AppStateService>();
		builder.Services.AddSingleton<ChainProviderSettings>();
		builder.Services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
		builder.Services.AddSingleton<IWalletManager, WalletManagerService>();

		return builder.Build();
	}
}
