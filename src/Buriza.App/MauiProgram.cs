using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using MudBlazor.Services;
using Buriza.UI.Services;
using Buriza.Core.Interfaces.Security;
using Buriza.Core.Interfaces.Storage;
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
		builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

		// Buriza services
		builder.Services.AddSingleton<PlatformDeviceAuthService>();
		builder.Services.AddSingleton<IDeviceAuthService>(sp => sp.GetRequiredService<PlatformDeviceAuthService>());
		builder.Services.AddSingleton<BurizaAppStorageService>();
		builder.Services.AddSingleton<BurizaStorageBase>(sp => sp.GetRequiredService<BurizaAppStorageService>());
		builder.Services.AddSingleton<AppStateService>();
		builder.Services.AddSingleton<IBurizaAppStateService>(sp => sp.GetRequiredService<AppStateService>());
		ChainProviderSettings settings = builder.Configuration
			.GetSection("ChainProviderSettings")
			.Get<ChainProviderSettings>() ?? new ChainProviderSettings();
		builder.Services.AddSingleton(settings);
		builder.Services.AddSingleton<IBurizaChainProviderFactory, BurizaChainProviderFactory>();
		builder.Services.AddSingleton<WalletManagerService>();

		return builder.Build();
	}
}
