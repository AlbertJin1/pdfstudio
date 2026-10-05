using PdfStudio.UI;
using System.Diagnostics;
using System.Text.Json;

namespace PdfStudio.Services;

/// <summary>
/// Sends anonymous usage statistics (app opened, how long it stays open) and user reviews to the PDF Studio Monitor
/// web app (PHP) through its ngrok address. No files, file names, PC name or user name are ever sent: only a random
/// install id, the app version, the Windows version and the open time.
///
/// Reliability: "open" and "close" events that cannot be delivered (offline, server down) are kept in a small queue file and
/// re-sent the next time the app starts. A "heartbeat" every minute carries the total open time, so even a crash or a
/// killed process still counts almost all of its time.
///
/// Settings (key=value lines in %LOCALAPPDATA%\PdfStudio\settings.txt):
///   usage_stats = 1 / 0      share anonymous usage statistics (default 1 = always on; the app has no checkbox for it, set 0 here only for testing)
///   monitor_url = https://…/pdfstudio-monitor/api   override the server address
///   monitor_key = …          override the API key (must equal 'api_key' in the server's config.php)
/// </summary>
public static class UsageTracker
{
    // ngrok serves HTTPS on the default port (443): ngrok forwards it to the PHP server that listens on :8080 on the host PC.
    public const string DefaultBaseUrl = "https://paternal-carefully-glucose.ngrok-free.dev/pdfstudio-monitor/api";
    // Must be the same value as 'api_key' in pdfstudio-monitor/config.php. Change both before you ship.
    private const string DefaultApiKey = "change-this-key";

    private const int HeartbeatMs = 60_000;
    private const int MaxQueued = 100;

    private static readonly HttpClient Http = CreateClient();
    private static readonly object Gate = new();
    private static readonly Stopwatch Clock = new();
    private static readonly string SessionId = Guid.NewGuid().ToString("N");
    private static long startedAt;
    private static System.Threading.Timer? heartbeat;
    private static bool started, stopped;

    private static string BaseUrl => AppSettings.Get("monitor_url", DefaultBaseUrl).Trim().TrimEnd('/');
    private static string ApiKey => AppSettings.Get("monitor_key", DefaultApiKey).Trim();
    private static string QueuePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfStudio", "usage-queue.jsonl");

    /// <summary>Whether anonymous usage statistics are shared. On by default and not exposed in the UI (reviews are sent when the user presses Send).</summary>
    public static bool Enabled => AppSettings.GetBool("usage_stats", true);
    public static void SetEnabled(bool on) => AppSettings.SetBool("usage_stats", on);

    private static string InstallId
    {
        get
        {
            var id = AppSettings.Get("install_id");
            if (id.Length < 16) { id = Guid.NewGuid().ToString("N"); AppSettings.Set("install_id", id); }
            return id;
        }
    }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"PdfStudio/{AppInfo.Version}");
        c.DefaultRequestHeaders.TryAddWithoutValidation("ngrok-skip-browser-warning", "1");   // skips ngrok's free-plan interstitial page
        c.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        return c;
    }

    // ───────────────────────── session ─────────────────────────

    /// <summary>Call once when the main window is shown: records one "open" and starts the minute heartbeat.</summary>
    public static void Start()
    {
        if (started || !Enabled) return;
        started = true;
        startedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Clock.Start();
        _ = Task.Run(async () =>
        {
            if (!await SendAsync(Build("open"), 8000).ConfigureAwait(false)) Enqueue(Build("open"));
            await FlushQueueAsync().ConfigureAwait(false);
        });
        heartbeat = new System.Threading.Timer(_ =>
        {
            if (stopped || !Enabled) return;
            _ = SendAsync(Build("heartbeat"), 8000);          // cumulative, so a missed one is harmless
        }, null, HeartbeatMs, HeartbeatMs);
    }

    /// <summary>Call when the main window has closed: sends the final open time (waits at most ~3 s).</summary>
    public static void Stop()
    {
        if (!started || stopped) return;
        stopped = true;
        try { heartbeat?.Dispose(); } catch { }
        Clock.Stop();
        var payload = Build("close");
        try { Task.Run(() => SendAsync(payload, 2500)).Wait(3000); } catch { }
        if (!sentClose) Enqueue(payload);      // not delivered in time: it is re-sent the next time the app starts
    }

    private static volatile bool sentClose;

    private static Dictionary<string, object?> Build(string kind) => new()
    {
        ["event"] = kind,
        ["session_id"] = SessionId,
        ["install_id"] = InstallId,
        ["version"] = AppInfo.Version,
        ["os"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        ["started_at"] = startedAt,
        ["seconds"] = (long)Clock.Elapsed.TotalSeconds
    };

    // ───────────────────────── reviews ─────────────────────────

    /// <summary>Sends a review. Returns (true, "") on success or (false, message to show the user).</summary>
    public static async Task<(bool Ok, string Error)> SubmitReviewAsync(int rating, string name, string message)
    {
        var payload = new Dictionary<string, object?>
        {
            ["event"] = "review",
            ["install_id"] = InstallId,
            ["version"] = AppInfo.Version,
            ["os"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ["rating"] = rating,
            ["name"] = name,
            ["message"] = message
        };
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            using var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/") { Content = content };
            req.Headers.TryAddWithoutValidation("X-Api-Key", ApiKey);
            using var res = await Http.SendAsync(req, cts.Token).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            if (res.IsSuccessStatusCode) return (true, "");
            return (false, ServerError(body) ?? $"The server answered with an error ({(int)res.StatusCode}). Please try again later.");
        }
        catch (TaskCanceledException) { return (false, "The server did not answer in time. Please try again in a moment."); }
        catch (HttpRequestException) { return (false, "Could not reach the PDF Studio server. Check your internet connection and try again."); }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private static string? ServerError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
        }
        catch { return null; }
    }

    // ───────────────────────── transport ─────────────────────────

    private static async Task<bool> SendAsync(Dictionary<string, object?> payload, int timeoutMs)
    {
        try { return await SendJsonAsync(JsonSerializer.Serialize(payload), timeoutMs, payload.TryGetValue("event", out var ev) && ev as string == "close").ConfigureAwait(false); }
        catch { return false; }
    }

    private static async Task<bool> SendJsonAsync(string json, int timeoutMs, bool isClose = false)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/") { Content = content };
            req.Headers.TryAddWithoutValidation("X-Api-Key", ApiKey);
            using var res = await Http.SendAsync(req, cts.Token).ConfigureAwait(false);
            // 2xx = stored. 4xx = the server rejected it for good (do not queue it forever). 5xx / network = try again later.
            var ok = res.IsSuccessStatusCode || ((int)res.StatusCode is >= 400 and < 500);
            if (ok && isClose) sentClose = true;
            return ok;
        }
        catch { return false; }
    }

    // ───────────────────────── offline queue ─────────────────────────

    private static void Enqueue(Dictionary<string, object?> payload)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(QueuePath)!);
                var lines = File.Exists(QueuePath) ? File.ReadAllLines(QueuePath).ToList() : new List<string>();
                lines.Add(JsonSerializer.Serialize(payload));
                if (lines.Count > MaxQueued) lines = lines.Skip(lines.Count - MaxQueued).ToList();
                File.WriteAllLines(QueuePath, lines);
            }
        }
        catch { }
    }

    private static async Task FlushQueueAsync()
    {
        string[] lines;
        lock (Gate)
        {
            try { lines = File.Exists(QueuePath) ? File.ReadAllLines(QueuePath) : Array.Empty<string>(); }
            catch { return; }
        }
        if (lines.Length == 0) return;
        var keep = new List<string>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!await SendJsonAsync(line, 6000).ConfigureAwait(false)) keep.Add(line);
        }
        lock (Gate)
        {
            try
            {
                // anything queued while we were sending stays; only what was delivered is dropped
                var now = File.Exists(QueuePath) ? File.ReadAllLines(QueuePath) : Array.Empty<string>();
                var extra = now.Skip(lines.Length);
                var left = keep.Concat(extra).ToList();
                if (left.Count == 0) File.Delete(QueuePath); else File.WriteAllLines(QueuePath, left);
            }
            catch { }
        }
    }
}
