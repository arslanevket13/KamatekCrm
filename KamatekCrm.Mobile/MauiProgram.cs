using Microsoft.Extensions.Logging;

namespace KamatekCrm.Mobile;

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
			});

		builder.Services.AddMauiBlazorWebView();

        // HTTP Client for API
        builder.Services.AddScoped(sp => new HttpClient
        {
            BaseAddress = new Uri(
                DeviceInfo.Platform == DevicePlatform.Android
                ? "http://10.0.2.2:5087"
                : "http://localhost:5087")
        });

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
