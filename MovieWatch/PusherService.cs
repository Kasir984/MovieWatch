using PusherClient;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

namespace MovieWatch {
    public class PusherService {
        private static PusherService? _instance;
        private static readonly HttpClient Http = new();
        public static PusherService Instance => _instance ??= new PusherService();

        private Pusher? _pusher;
        private Channel? _channel;

        private readonly string _deviceId = Guid.NewGuid().ToString();

        private const string AppId = "2160917";
        private const string AppKey = "18c815208a8ff3f759c2";
        private const string AppSecret = "b4180e26deb46877cf16";
        private const string Cluster = "ap2";
        private const string ChannelName = "robust-window-194.";

        // Presence
        public event Action? PartnerOnline;
        public event Action? PartnerOffline;

        // Video setup
        public event Action? PartnerPicking;
        public event Action<string, long>? ChecksumReceived;
        public event Action<bool>? ChecksumResultReceived;
        public event Action? PartnerReady;

        // Playback Sync Events
        public event Action<long, long>? PauseReceived;
        public event Action<long, long>? PlayReceived;
        public event Action<long, long>? SyncReceived;
        public event Action<long>? PingReceived;
        public event Action<long>? PongReceived;

        private PusherService() { }

        public async Task ConnectAsync() {
            if (_pusher is { State: ConnectionState.Connected }) return;

            _pusher = new Pusher(AppKey, new PusherOptions { Cluster = Cluster, Encrypted = true });

            _pusher.ConnectionStateChanged += (_, state) =>
                System.Diagnostics.Debug.WriteLine($"Pusher: {state}");
            _pusher.Error += (_, error) =>
                System.Diagnostics.Debug.WriteLine($"Pusher error: {error.Message}");

            await _pusher.ConnectAsync();
            _channel = await _pusher.SubscribeAsync(ChannelName);

            BindEvents();

            await AnnounceOnlineAsync();
            await TriggerPusherEventAsync(ChannelName, "status-request",
                new JsonObject { ["senderId"] = _deviceId });
        }

        private void BindEvents() {
            _channel?.Bind("partner-online", (PusherEvent e) => { if (GetSender(e) != _deviceId) PartnerOnline?.Invoke(); });
            _channel?.Bind("partner-offline", (PusherEvent e) => { if (GetSender(e) != _deviceId) PartnerOffline?.Invoke(); });
            _channel?.Bind("status-request", (PusherEvent e) => { if (GetSender(e) != _deviceId) _ = AnnounceOnlineAsync(); });
            _channel?.Bind("picker-selected", (PusherEvent e) => { if (GetSender(e) != _deviceId) PartnerPicking?.Invoke(); });
            _channel?.Bind("device-ready", (PusherEvent e) => { if (GetSender(e) != _deviceId) PartnerReady?.Invoke(); });

            _channel?.Bind("video-checksum", (PusherEvent e) => {
                if (GetSender(e) == _deviceId) return;
                var data = JsonDocument.Parse(e.Data).RootElement;
                ChecksumReceived?.Invoke(
                    data.GetProperty("checksum").GetString()!,
                    data.GetProperty("fileSize").GetInt64());
            });

            _channel?.Bind("checksum-result", (PusherEvent e) => {
                if (GetSender(e) == _deviceId) return;
                ChecksumResultReceived?.Invoke(
                    JsonDocument.Parse(e.Data).RootElement.GetProperty("match").GetBoolean());
            });

            _channel?.Bind("video-pause", (PusherEvent e) => {
                if (GetSender(e) == _deviceId) return;
                var data = JsonDocument.Parse(e.Data).RootElement;
                PauseReceived?.Invoke(
                    data.GetProperty("position").GetInt64(),
                    data.GetProperty("timestamp").GetInt64());
            });

            _channel?.Bind("video-play", (PusherEvent e) => {
                if (GetSender(e) == _deviceId) return;
                var data = JsonDocument.Parse(e.Data).RootElement;
                PlayReceived?.Invoke(
                    data.GetProperty("position").GetInt64(),
                    data.GetProperty("timestamp").GetInt64());
            });

            _channel?.Bind("video-sync", (PusherEvent e) => {
                if (GetSender(e) == _deviceId) return;
                var data = JsonDocument.Parse(e.Data).RootElement;
                SyncReceived?.Invoke(
                    data.GetProperty("position").GetInt64(),
                    data.GetProperty("timestamp").GetInt64());
            });

            _channel?.Bind("video-ping", (PusherEvent e) => {
                if (GetSender(e) == _deviceId) return;
                PingReceived?.Invoke(
                    JsonDocument.Parse(e.Data).RootElement.GetProperty("timestamp").GetInt64());
            });

            _channel?.Bind("video-pong", (PusherEvent e) => {
                if (GetSender(e) == _deviceId) return;
                PongReceived?.Invoke(
                    JsonDocument.Parse(e.Data).RootElement.GetProperty("originalTimestamp").GetInt64());
            });
        }

        private string? GetSender(PusherEvent e) =>
            JsonDocument.Parse(e.Data).RootElement.GetProperty("senderId").GetString();

        // ── Public send methods ─────────────────────────────────────────────────

        public async Task AnnounceOnlineAsync() =>
            await TriggerPusherEventAsync(ChannelName, "partner-online",
                new JsonObject { ["senderId"] = _deviceId });

        public async Task AnnounceOfflineAsync() =>
            await TriggerPusherEventAsync(ChannelName, "partner-offline",
                new JsonObject { ["senderId"] = _deviceId });

        public async Task AnnouncePickerAsync() =>
            await TriggerPusherEventAsync(ChannelName, "picker-selected",
                new JsonObject { ["senderId"] = _deviceId });

        public async Task AnnounceReadyAsync() =>
            await TriggerPusherEventAsync(ChannelName, "device-ready",
                new JsonObject { ["senderId"] = _deviceId });

        public async Task SendChecksumAsync(string checksum, long fileSize) =>
            await TriggerPusherEventAsync(ChannelName, "video-checksum",
                new JsonObject {
                    ["senderId"] = _deviceId,
                    ["checksum"] = checksum,
                    ["fileSize"] = fileSize
                });

        public async Task SendChecksumResultAsync(bool match) =>
            await TriggerPusherEventAsync(ChannelName, "checksum-result",
                new JsonObject {
                    ["senderId"] = _deviceId,
                    ["match"] = match
                });

        public async Task SendPauseAsync(long positionMs) =>
            await TriggerPusherEventAsync(ChannelName, "video-pause",
                new JsonObject {
                    ["senderId"] = _deviceId,
                    ["position"] = positionMs,
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });

        public async Task SendPlayAsync(long positionMs) =>
            await TriggerPusherEventAsync(ChannelName, "video-play",
                new JsonObject {
                    ["senderId"] = _deviceId,
                    ["position"] = positionMs,
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });

        public async Task SendSyncAsync(long positionMs) =>
            await TriggerPusherEventAsync(ChannelName, "video-sync",
                new JsonObject {
                    ["senderId"] = _deviceId,
                    ["position"] = positionMs,
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });

        public async Task SendPingAsync() =>
            await TriggerPusherEventAsync(ChannelName, "video-ping",
                new JsonObject {
                    ["senderId"] = _deviceId,
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });

        public async Task SendPongAsync(long originalTimestamp) =>
            await TriggerPusherEventAsync(ChannelName, "video-pong",
                new JsonObject {
                    ["senderId"] = _deviceId,
                    ["originalTimestamp"] = originalTimestamp
                });

        // ── Core HTTP trigger ───────────────────────────────────────────────────

        // data is now JsonObject instead of object — trim-safe, no reflection on user types
        private async Task TriggerPusherEventAsync(string channel, string eventName, JsonObject data) {
            var body = new JsonObject {
                ["name"] = eventName,
                ["channel"] = channel,
                ["data"] = data.ToJsonString()   // serialise inner payload to a JSON string
            }.ToJsonString();

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var bodyMd5 = GetMd5(body);
            var sortedParams = $"auth_key={AppKey}&auth_timestamp={timestamp}&auth_version=1.0&body_md5={bodyMd5}";
            var signature = GetHmac(AppSecret, $"POST\n/apps/{AppId}/events\n{sortedParams}");
            var url = $"https://api-{Cluster}.pusher.com/apps/{AppId}/events?{sortedParams}&auth_signature={signature}";

            // ReSharper disable once UnusedVariable
            var response = await Http.PostAsync(url,
                new StringContent(body, Encoding.UTF8, "application/json"));
            // System.Diagnostics.Debug.WriteLine($"Pusher: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
        }

        private static string GetMd5(string input) =>
            Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(input))).ToLower();

        private static string GetHmac(string secret, string data) =>
            Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secret),
                Encoding.UTF8.GetBytes(data))).ToLower();
    }
}