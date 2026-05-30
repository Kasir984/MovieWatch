using MovieWatch.Pages;

namespace MovieWatch {
    public partial class App {
        public App() {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
            => new Window(new MainPage());

        protected override void OnStart() {
            base.OnStart();
            // Fire-and-forget; errors are caught inside
            _ = RunUpdateCheckAsync();
        }

        private static async Task RunUpdateCheckAsync() {
            // Wait for the main page to finish rendering before showing a dialog
            await Task.Delay(2000);

            try {
                var update = await UpdateService.CheckForUpdateAsync();
                if (update is null) return;

                var page = Application.Current?.Windows[0].Page;
                if (page is null) return;

                var confirmed = await page.DisplayAlert(
                    "Update Available 🎉",
                    $"Version {update.Version} is available.\nWould you like to download and install it now?",
                    "Update Now",
                    "Later");

                if (!confirmed) return;

                // Push the progress page modally so the user sees download progress
                var progressPage = new UpdateProgressPage(update.DownloadUrl);
                await page.Navigation.PushModalAsync(progressPage);
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[Update] Flow error: {ex}");
            }
        }
    }
}