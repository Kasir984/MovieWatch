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
                fonts.AddFont("Rajdhani-SemiBold.ttf", "RajdhaniSemiBold");
                fonts.AddFont("Rajdhani-Medium.ttf", "RajdhaniMedium");
                fonts.AddFont("Rajdhani-Bold.ttf", "RajdhaniBold");
                fonts.AddFont("JetBrainsMono-SemiBold.ttf", "JetBrainsMonoSemiBold");
                fonts.AddFont("LobsterTwo-Bold.ttf", "LobsterTwoBold");
                fonts.AddFont("LobsterTwo-Regular.ttf", "LobsterTwoRegular");
            });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
