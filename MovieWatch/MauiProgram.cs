using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;

namespace MovieWatch {
    public static class MauiProgram {
        public static MauiApp CreateMauiApp() {
            var builder = MauiApp.CreateBuilder()
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseMauiCommunityToolkitMediaElement(false);

            builder.ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("Rajdhani-SemiBold.ttf", "Rajdhani Semibold");
                fonts.AddFont("Rajdhani-Medium.ttf", "Rajdhani Medium");
                fonts.AddFont("Rajdhani-Bold.ttf", "Rajdhani Bold");
                fonts.AddFont("JetBrainsMono-SemiBold.ttf", "Jetbrains Mono SemiBold");
            });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
