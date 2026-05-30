namespace MovieWatch.Pages;

public partial class UpdateProgressPage : ContentPage {
    private readonly string _downloadUrl;
    private CancellationTokenSource? _cts;

    public UpdateProgressPage(string downloadUrl) {
        InitializeComponent();
        _downloadUrl = downloadUrl;
    }

    protected override void OnAppearing() {
        base.OnAppearing();
        _ = StartDownloadAsync();
    }

    private async Task StartDownloadAsync() {
        _cts = new CancellationTokenSource();

        var progress = new Progress<double>(value =>
            MainThread.BeginInvokeOnMainThread(() => {
                UpdateProgressBar.Progress = value;
                StatusLabel.Text = $"Downloading... {value * 100:F0}%";
            }));

        try {
            await UpdateService.DownloadAndInstallAsync(_downloadUrl, progress, _cts.Token);

            // Hand-off to OS installer
            MainThread.BeginInvokeOnMainThread(() => {
                StatusLabel.Text = "Installing — follow the on-screen prompt.";
                CancelButton.IsEnabled = false;
            });
        } catch (OperationCanceledException) {
            await Navigation.PopModalAsync();
        } catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine($"[Update] Download error: {ex}");
            await MainThread.InvokeOnMainThreadAsync(async () => {
                await DisplayAlert(
                    "Update Failed",
                    "Could not download the update. Please try again later.",
                    "OK");
                await Navigation.PopModalAsync();
            });
        }
    }

    private void OnCancelClicked(object sender, EventArgs e) => _cts?.Cancel();
}