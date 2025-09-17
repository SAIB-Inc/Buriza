using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Buriza.UI.Services;

namespace Buriza.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
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

		return builder.Build();
	}
}
