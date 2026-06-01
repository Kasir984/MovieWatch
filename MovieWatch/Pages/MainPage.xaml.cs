#pragma warning disable CS0618  // suppress obsolete-API warnings for legacy MAUI gesture / navigation calls


namespace MovieWatch.Pages {
    public partial class MainPage {
        private bool _iAmPicker;
        private string? _partnerChecksum;
        private long _partnerFileSize;
        private bool _iPressedReady;
        private bool _partnerPressedReady;
        private bool _setupStarted;
        private bool _initialized;
        private string? _selectedFilePath;
        public StarFieldDrawable Drawable { get; }
        private const bool IsAnimating = true;

        private readonly Star[] _stars;

        public MainPage() {
            InitializeComponent();

            var existingStars = new Star[]
            {
                new(501, 811, 0.5f), new(1450, 1324, 1.2f), new(1093, 1780, 0.8f),
                new(1469, 678, 2.0f), new(904, 741, 0.3f), new(1160, 781, 1.5f),
                new(1841, 1962, 0.9f), new(1630, 1667, 1.1f), new(1788, 676, 0.6f),
                new(367, 1734, 1.4f), new(1343, 156, 0.4f), new(1283, 1142, 1.7f),
                new(1062, 378, 0.5f), new(1395, 467, 1.2f), new(1017, 1891, 0.8f),
                new(137, 1114, 2.1f), new(1767, 1403, 1.3f), new(1543, 11, 0.2f),
                new(1078, 181, 0.7f), new(1189, 1574, 1.6f),
                // Add your remaining points inside 'new Star(X, Y, Speed)' format
            };

            _stars = existingStars.Concat(GenerateRandomStars(80)).ToArray();
            
            _ = InitializePusher();
            Drawable = new StarFieldDrawable(_stars);
            StarCanvas.Drawable = Drawable;
            StartAnimationLoop();
        }

        private static Star[] GenerateRandomStars(int count)
        {
            var random = new Random();
            var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
            var screenWidth = (int)displayInfo.Width;
            var screenHeight = (int)displayInfo.Height;

            var stars = new Star[count];
            for (var i = 0; i < count; i++)
            {
                stars[i] = new Star(
                    random.Next(0, screenWidth),
                    random.Next(0, screenHeight),
                    (float)(random.NextDouble() * 2.0 + 0.2f)
                );
            }

            return stars;
        }
        

        private async void StartAnimationLoop()
        {
            try
            {
                while (IsAnimating) {

                    foreach (var star in _stars)
                    {
                        star.Y += star.Speed;

                        if (star.Y > 2000)
                        {
                            star.Y = 0;
                        }
                    }
                    StarCanvas.Invalidate();

                    await Task.Delay(16);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Exception Caught: {e}");
            }
        }

        private async Task InitializePusher() {
            if (_initialized) return;
            _initialized = true;

            var pusher = PusherService.Instance;
            pusher.PartnerOnline += OnPartnerOnline;
            pusher.PartnerOffline += OnPartnerOffline;
            pusher.PartnerPicking += OnPartnerPicking;
            pusher.ChecksumReceived += OnChecksumReceived;
            pusher.ChecksumResultReceived += OnChecksumResultReceived;
            pusher.PartnerReady += OnPartnerReady;

            System.Diagnostics.Debug.WriteLine("Connecting to Pusher...");
            await pusher.ConnectAsync();
            System.Diagnostics.Debug.WriteLine("Pusher connected successfully.");
        }

        private void OnButtonClicked(object sender, EventArgs e) {
            _ = PusherService.Instance.AnnounceOnlineAsync();
        }

        // ── Pusher handlers ────────────────────────────────────────────────────

        private void OnPartnerOnline() {
            MainThread.BeginInvokeOnMainThread(() => {
                LblResult.IsVisible = true;
                LblResult.Text = "✅ Partner is online";
                LblResult.TextColor = Colors.Green;

                if (!_setupStarted)
                    ShowPanel(WhoPicksPanel);
            });
        }

        private void OnPartnerOffline() {
            MainThread.BeginInvokeOnMainThread(() => {
                LblResult.IsVisible = true;
                LblResult.Text = "❌ Partner is offline";
                LblResult.TextColor = Colors.Red;
                VideoSetupOverlay.IsVisible = false;
                _setupStarted = false;
            });
        }

        private void OnPartnerPicking() {
            MainThread.BeginInvokeOnMainThread(() => {
                _setupStarted = true;
                _iAmPicker = false;
                WaitingLabel.Text = "Partner is selecting the video...";
                ShowPanel(WaitingPanel);
            });
        }

        private void OnChecksumReceived(string checksum, long fileSize) {
            MainThread.BeginInvokeOnMainThread(() => {
                _partnerChecksum = checksum;
                _partnerFileSize = fileSize;
                PickVideoLabel.Text = "Select the same video file";
                PickVideoSubLabel.Text = "Partner has selected their file";
                ShowPanel(PickVideoPanel);
            });
        }

        private void OnChecksumResultReceived(bool match) {
            MainThread.BeginInvokeOnMainThread(() => {
                if (match) {
                    ReadyStatusLabel.Text = "Press Ready when you are ready";
                    ShowPanel(ReadyPanel);
                } else {
                    WaitingLabel.Text = "Partner is re-selecting the file...";
                    ShowPanel(WaitingPanel);
                }
            });
        }

        private void OnPartnerReady() {
            MainThread.BeginInvokeOnMainThread(() => {
                System.Diagnostics.Debug.WriteLine($"OnPartnerReady called. _iPressedReady={_iPressedReady}");
                _partnerPressedReady = true;
                if (_iPressedReady) {
                    System.Diagnostics.Debug.WriteLine("Both ready, calling OnBothReady()");
                    OnBothReady();
                } else {
                    System.Diagnostics.Debug.WriteLine("Partner ready but I haven't pressed yet.");
                    ReadyStatusLabel.Text = "Partner is ready! Press Ready when you are.";
                }
            });
        }

        private void OnBothReady() {
            VideoSetupOverlay.IsVisible = false;
            try {
                var newPage = !string.IsNullOrEmpty(_selectedFilePath)
                    ? new VideoPlayer(_selectedFilePath)
                    : new VideoPlayer();

                // Use Windows[0].Page — Application.MainPage setter is deprecated in .NET 9+
                if (Application.Current?.Windows is { Count: > 0 } windows)
                    windows[0].Page = newPage;
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to open VideoPlayer page: {ex}");
            }
        }

        // ── Who picks ──────────────────────────────────────────────────────────

        private async void OnMeClicked(object sender, EventArgs e) {
            try {
                _setupStarted = true;
                _iAmPicker = true;
                await PusherService.Instance.AnnouncePickerAsync();
                PickVideoLabel.Text = "Select the video file";
                PickVideoSubLabel.Text = "";
                ShowPanel(PickVideoPanel);
            } catch (Exception error) {
                System.Diagnostics.Debug.WriteLine($"Caught Exception: {error}");
            }
        }

        private void OnPartnerClicked(object sender, EventArgs e) {
            _setupStarted = true;
            _iAmPicker = false;
            WaitingLabel.Text = "Waiting for partner to select video...";
            ShowPanel(WaitingPanel);
        }

        // ── File picker ────────────────────────────────────────────────────────

        private async void OnBrowseClicked(object sender, EventArgs e) {
            try {
                // FilePicker.Default is the current API; static FilePicker.PickAsync is deprecated
                var result = await FilePicker.Default.PickAsync(new PickOptions {
                    PickerTitle = "Select a video file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI,   [".mp4", ".mkv", ".avi", ".mov", ".wmv"] },
                        { DevicePlatform.Android, ["video/*"] },
                        { DevicePlatform.iOS,     ["public.movie"] }
                    })
                });

                if (result == null) return;
                await ProcessVideoFile(result.FullPath);
            } catch (Exception ex) {
                await DisplayAlertAsync("Error", ex.Message, "OK");
            }
        }

        private async Task ProcessVideoFile(string filePath) {
            _selectedFilePath = filePath;

            VerifyingLabel.Text = "Reading file chunks...";
            ShowPanel(VerifyingPanel);

            var progress = new Progress<string>(msg =>
                MainThread.BeginInvokeOnMainThread(() => VerifyingLabel.Text = msg));

            var (hash, fileSize) = await VideoVerifier.ComputeAsync(filePath, progress);

            if (_iAmPicker) {
                VerifyingLabel.Text = "Sending to partner...";
                await PusherService.Instance.SendChecksumAsync(hash, fileSize);
                WaitingLabel.Text = "Waiting for partner to verify...";
                ShowPanel(WaitingPanel);
            } else {
                if (hash == _partnerChecksum && fileSize == _partnerFileSize) {
                    await PusherService.Instance.SendChecksumResultAsync(true);
                    ReadyStatusLabel.Text = "Press Ready when you are ready";
                    ShowPanel(ReadyPanel);
                } else {
                    await PusherService.Instance.SendChecksumResultAsync(false);
                    ShowPanel(MismatchPanel);
                }
            }
        }

        // ── Ready ──────────────────────────────────────────────────────────────

        private async void OnReadyClicked(object sender, EventArgs e) {
            try {
                System.Diagnostics.Debug.WriteLine($"OnReadyClicked. _partnerPressedReady={_partnerPressedReady}");
                _iPressedReady = true;
                ReadyButton.IsEnabled = false;
                await PusherService.Instance.AnnounceReadyAsync();
                System.Diagnostics.Debug.WriteLine(
                    $"AnnounceReadyAsync completed. _partnerPressedReady={_partnerPressedReady}");

                if (_partnerPressedReady) {
                    System.Diagnostics.Debug.WriteLine("Both ready! Calling OnBothReady()");
                    OnBothReady();
                } else {
                    System.Diagnostics.Debug.WriteLine("Partner not ready yet. Waiting...");
                    ReadyStatusLabel.Text = "Waiting for partner to press Ready...";
                }
            } catch (Exception error) {
                System.Diagnostics.Debug.WriteLine($"Exception Caught: {error.Message}");
            }
        }

        // ── Panel helper ───────────────────────────────────────────────────────

        private void ShowPanel(VisualElement panel) {
            WhoPicksPanel.IsVisible = false;
            WaitingPanel.IsVisible = false;
            PickVideoPanel.IsVisible = false;
            VerifyingPanel.IsVisible = false;
            MismatchPanel.IsVisible = false;
            ReadyPanel.IsVisible = false;

            panel.IsVisible = true;
            VideoSetupOverlay.IsVisible = true;
        }

        protected override void OnDisappearing() {
            base.OnDisappearing();
            _ = PusherService.Instance.AnnounceOfflineAsync();
        }
    }
}

#pragma warning restore CS0618