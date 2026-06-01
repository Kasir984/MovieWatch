using System.Diagnostics;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

namespace MovieWatch.Pages;

public partial class VideoPlayer {
    private volatile bool _suppressEventSend;
    private CancellationTokenSource? _monitorCts;

    // --- Sync Fields ---
    private readonly List<double> _rttMeasurements = new();
    private double _averageRttMs = 50;  // reasonable default before first pong
    private long _lastPingSentTimestamp;
    private bool _isResettingSpeed;

    public VideoPlayer() {
        InitializeComponent();
    }

    public VideoPlayer(string filePath) : this() {
        try {
            if (!string.IsNullOrEmpty(filePath)) {
                MediaElement.Source = new Uri(filePath);
                MediaElement.ShouldAutoPlay = false;
            }
        } catch (Exception ex) {
            Debug.WriteLine($"Error setting media source: {ex}");
        }
    }

    protected override async void OnAppearing() {
        try {
            base.OnAppearing();
            await InitPusherAsync();

            // Subscribe to all remote events
            PusherService.Instance.PauseReceived += OnRemotePauseReceived;
            PusherService.Instance.PlayReceived += OnRemotePlayReceived;
            PusherService.Instance.SyncReceived += OnRemoteSyncReceived;
            PusherService.Instance.PingReceived += OnRemotePingReceived;
            PusherService.Instance.PongReceived += OnRemotePongReceived;

            // Cancel and dispose any previous monitor before creating a new one.
            // _monitorCts is null on first entry — the previous code used `?.CancelAsync()!`
            // which would await a null Task and throw NullReferenceException.
            if (_monitorCts != null) {
                await _monitorCts.CancelAsync();
                _monitorCts.Dispose();
            }
            _monitorCts = new CancellationTokenSource();
            _ = MonitorPlaybackAsync(_monitorCts.Token);

            await Task.Delay(500); // let media element settle before playing
            Debug.WriteLine($"MediaElement Source: {MediaElement.Source}");
            Debug.WriteLine($"MediaElement CurrentState: {MediaElement.CurrentState}");
            Debug.WriteLine("About to call Play()");
            MediaElement.Play();
            Debug.WriteLine($"Play() called, CurrentState now: {MediaElement.CurrentState}");
        } catch (Exception e) {
            Debug.WriteLine($"Exception Caught: {e}");
        }
    }

    protected override void OnDisappearing() {
        base.OnDisappearing();

        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;

        // Unsubscribe from all events
        PusherService.Instance.PauseReceived -= OnRemotePauseReceived;
        PusherService.Instance.PlayReceived -= OnRemotePlayReceived;
        PusherService.Instance.SyncReceived -= OnRemoteSyncReceived;
        PusherService.Instance.PingReceived -= OnRemotePingReceived;
        PusherService.Instance.PongReceived -= OnRemotePongReceived;
    }

    // --- Remote Event Handlers ---

    private void OnRemotePauseReceived(long positionMs, long timestampMs) {
        MainThread.BeginInvokeOnMainThread(() => {
            try {
                var target = TimeSpan.FromMilliseconds(positionMs + (_averageRttMs / 2));
                SeekMediaElement(MediaElement, target);
                _suppressEventSend = true;
                MediaElement.Pause();
            } catch (Exception ex) { Debug.WriteLine($"Failed to apply remote pause: {ex}"); }
        });
    }

    private void OnRemotePlayReceived(long positionMs, long timestampMs) {
        MainThread.BeginInvokeOnMainThread(() => {
            try {
                var target = TimeSpan.FromMilliseconds(positionMs + (_averageRttMs / 2));
                SeekMediaElement(MediaElement, target);
                _suppressEventSend = true;
                MediaElement.Play();
            } catch (Exception ex) { Debug.WriteLine($"Failed to apply remote play: {ex}"); }
        });
    }

    private void OnRemoteSyncReceived(long positionMs, long timestampMs) {
        if (MediaElement.CurrentState != MediaElementState.Playing) return;

        MainThread.BeginInvokeOnMainThread(() => {
            var remoteExpected = positionMs + (_averageRttMs / 2);
            var localPosition = MediaElement.Position.TotalMilliseconds;
            var drift = localPosition - remoteExpected;

            if (Math.Abs(drift) > 250 && Math.Abs(drift) < 2000) {
                MediaElement.Speed = drift > 0 ? 0.98 : 1.02;  // slow down or speed up
                _ = ResetSpeedAfterDelay(TimeSpan.FromSeconds(3));
            }
        });
    }

    private void OnRemotePingReceived(long timestamp) =>
        _ = PusherService.Instance.SendPongAsync(timestamp);

    private void OnRemotePongReceived(long originalTimestamp) {
        var rtt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - originalTimestamp;
        if (rtt <= 0) return;

        _rttMeasurements.Add(rtt);
        if (_rttMeasurements.Count > 10)
            _rttMeasurements.RemoveAt(0);
        _averageRttMs = _rttMeasurements.Average();
    }

    // --- Background Monitor ---

    private async Task MonitorPlaybackAsync(CancellationToken token) {
        var lastState = MediaElementState.Stopped;

        while (!token.IsCancellationRequested) {
            try {
                await Task.Delay(250, token);

                var currentState = MediaElement.CurrentState;
                var currentPosition = MediaElement.Position;

                // Detect state transitions (pause / play)
                if (currentState != lastState) {
                    if (!_suppressEventSend) {
                        if (currentState == MediaElementState.Paused)
                            await PusherService.Instance.SendPauseAsync((long)currentPosition.TotalMilliseconds);
                        else if (currentState == MediaElementState.Playing)
                            await PusherService.Instance.SendPlayAsync((long)currentPosition.TotalMilliseconds);
                    }
                    lastState = currentState;
                }

                // Periodic sync while playing
                if (currentState == MediaElementState.Playing && !_suppressEventSend)
                    await PusherService.Instance.SendSyncAsync((long)currentPosition.TotalMilliseconds);

                // Periodic ping for RTT measurement
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lastPingSentTimestamp > 5000) {
                    _lastPingSentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    await PusherService.Instance.SendPingAsync();
                }

                // Reset suppression after a short cooldown
                if (_suppressEventSend) {
                    await Task.Delay(500, token);
                    _suppressEventSend = false;
                }
            } catch (OperationCanceledException) { break; } catch (Exception ex) { Debug.WriteLine($"Monitor failed: {ex}"); }
        }
    }

    // --- Helpers ---

    private async Task ResetSpeedAfterDelay(TimeSpan delay) {
        if (_isResettingSpeed) return;
        _isResettingSpeed = true;
        await Task.Delay(delay);
        MediaElement.Speed = 1.0;
        _isResettingSpeed = false;
    }

    private static async Task InitPusherAsync() {
        try { await PusherService.Instance.ConnectAsync(); } catch { /* ignored — already connected */ }
    }

    private static void SeekMediaElement(MediaElement media, TimeSpan position) {
        var duration = media.Duration;
        if (duration > TimeSpan.Zero && position > duration) position = duration;
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        media.SeekTo(position);
    }
}