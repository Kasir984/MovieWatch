using System.Text.Json.Nodes;

namespace MovieWatch;

public static class UpdateService {
    // ── ✏️  Change these to match your GitHub repo ─────────────────────────────
    private const string GitHubOwner = "YOUR_GITHUB_USERNAME";
    private const string GitHubRepo = "YOUR_REPO_NAME";

    // Name of the asset attached to each GitHub release:
    //   Android → upload the .apk as "MovieWatch.apk"
    //   Windows → upload your setup/msix as "MovieWatch-Setup.exe"
#if ANDROID
    private const string AssetName = "MovieWatch.apk";
#elif WINDOWS
    private const string AssetName = "MovieWatch-Setup.exe";
#else
    private const string AssetName = "";
#endif
    // ───────────────────────────────────────────────────────────────────────────

    private static readonly HttpClient Http = new();

    static UpdateService() {
        // GitHub API requires a User-Agent header
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("MovieWatch-Updater/1.0");
    }

    public record UpdateInfo(string Version, string DownloadUrl);

    // ── Check for a newer release ──────────────────────────────────────────────
    public static async Task<UpdateInfo?> CheckForUpdateAsync() {
        try {
            var apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
            var json = await Http.GetStringAsync(apiUrl);
            var root = JsonNode.Parse(json)!;

            var tagName = root["tag_name"]?.GetValue<string>() ?? string.Empty;
            var remoteVersion = Version.Parse(tagName.TrimStart('v'));
            var localVersion = AppInfo.Current.Version;

            System.Diagnostics.Debug.WriteLine(
                $"[Update] Local={localVersion}  Remote={remoteVersion}");

            if (remoteVersion <= localVersion) return null;   // already up to date

            // Find the right asset for this platform
            var asset = root["assets"]?.AsArray()
                .FirstOrDefault(a => a!["name"]?.GetValue<string>() == AssetName);

            var url = asset?["browser_download_url"]?.GetValue<string>();
            return url is null ? null : new UpdateInfo(tagName, url);
        } catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine($"[Update] Check failed: {ex.Message}");
            return null;
        }
    }

    // ── Download then launch the installer ────────────────────────────────────
    public static async Task DownloadAndInstallAsync(
        string downloadUrl,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default) {
        var ext = Path.GetExtension(AssetName);   // .apk or .exe
        var filePath = Path.Combine(FileSystem.Current.CacheDirectory,
                                    "MovieWatch_update" + ext);

        // --- Stream download with progress ---
        using var response = await Http.GetAsync(
            downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;

        await using var src = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var dest = new FileStream(
            filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long received = 0;
        int read;

        while ((read = await src.ReadAsync(buffer, cancellationToken)) > 0) {
            await dest.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            if (total > 0) progress?.Report((double)received / total);
        }

        await dest.FlushAsync(cancellationToken);
        progress?.Report(1.0);

        // --- Launch the installer ---
        LaunchInstaller(filePath);
    }

    private static void LaunchInstaller(string filePath) {
#if ANDROID
        var context = Android.App.Application.Context;
        var file = new Java.IO.File(filePath);

        // FileProvider turns the file-system path into a content:// URI that
        // the package-installer activity is allowed to read.
        var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
            context,
            context.PackageName + ".fileprovider",   // must match android:authorities in Manifest
            file);

        var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
        intent.SetDataAndType(uri, "application/vnd.android.package-archive");
        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
        intent.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission);
        context.StartActivity(intent);

#elif WINDOWS
        // On Windows, just launch the installer executable
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = filePath,
            UseShellExecute = true
        });
#endif
    }
}