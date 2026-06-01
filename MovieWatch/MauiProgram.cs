using CommunityToolkit.Maui;

namespace MovieWatch {
    public static class MauiProgram {
        public static MauiApp CreateMauiApp() {
            var builder = MauiApp.CreateBuilder()
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit();

#if ANDROID && NET10_0_OR_GREATER
            if (DeviceInfo.Current.Platform == DevicePlatform.Android &&
                OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                builder.UseMauiCommunityToolkitMediaElement(false);
            }
#else
            builder.UseMauiCommunityToolkitMediaElement(false);
#endif

            builder.ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("Rajdhani-SemiBold.ttf", "Rajdhani Semibold");
                fonts.AddFont("Rajdhani-Medium.ttf", "Rajdhani Medium");
                fonts.AddFont("Rajdhani-Bold.ttf", "Rajdhani Bold");
                fonts.AddFont("JetBrainsMono-SemiBold.ttf", "Jetbrains Mono SemiBold");
                fonts.AddFont("LobsterTwo-Bold.ttf", "LobsterTwo Bold");
                fonts.AddFont("LobsterTwo-Medium.ttf", "LobsterTwo Medium");
                fonts.AddFont("LobsterTwo-Regular.ttf", "LobsterTwo Regular");
            });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
