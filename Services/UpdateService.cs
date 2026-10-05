using PdfStudio.UI;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace PdfStudio.Services;

/// <summary>A newer version offered by the monitor server.</summary>
public sealed record UpdateInfo(string Version, string Notes, string DownloadUrl, long Size, string? Sha256);

/// <summary>Result of a check: Update is null when you are up to date; Error is set when the check itself failed.</summary>
public sealed record UpdateCheck(UpdateInfo? Update, string? Error);

/// <summary>
/// Self-update from the PDF Studio Monitor server (the PHP app behind the ngrok address, see UsageTracker.DefaultBaseUrl).
/// The exe you upload on the monitor's "Releases" page is what every copy of PDF Studio is offered.
///
///  1. CheckAsync       GET  {monitor}/update.php?action=latest   -> newest uploaded version, notes, size and SHA-256.
///  2. DownloadAsync    GET  {monitor}/update.php?action=download -> saves it next to the running exe as PdfStudio.exe.new and verifies it.
///  3. ApplyAndRestart  renames the running exe to PdfStudio.exe.old (Windows allows renaming a running exe, just not
///                      overwriting it), moves the new file into place and starts it. The new copy deletes the .old
///                      file on its next start (CleanupLeftovers).
///
/// The app asks the user (update now / later / skip this version) a few seconds after it starts; see MainForm.CheckForUpdatesAsync.
/// Settings (key=value lines in %LOCALAPPDATA%\PdfStudio\settings.txt):
///   monitor_url / monitor_key   the same server address and API key the usage statistics use
///   auto_update = 0             turn off the check at startup (Help / About → "Check for updates" still works)
///   update_skip = 2.7.0         the version the user chose to skip
/// </summary>
public static class UpdateService
{
    public static bool AutoCheck => AppSettings.GetBool("auto_update", true);
    public static string SkippedVersion { get => AppSettings.Get("update_skip"); set => AppSettings.Set("update_skip", value); }

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };    // each call sets its own limit; a big download must not be cut off
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"PdfStudio/{AppInfo.Version}");
        c.DefaultRequestHeaders.TryAddWithoutValidation("ngrok-skip-browser-warning", "1");   // skips ngrok's free-plan interstitial page
        return c;
    }

    private static HttpRequestMessage Get(string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("X-Api-Key", UsageTracker.ApiKey);
        return req;
    }

    private static string Endpoint(string query) => $"{UsageTracker.BaseUrl}/update.php?{query}";

    // ───────────────────────── paths / guards ─────────────────────────

    private static string? ExePath => Environment.ProcessPath;
    private static string NewPath => ExePath + ".new";
    private static string OldPath => ExePath + ".old";

    private static bool IsDevHost(string? exe) =>
        string.IsNullOrEmpty(exe) || string.Equals(Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The version this copy is compared with the server's. It is read from the exe's own Windows file version, which is exactly
    /// what the monitor reads when you upload an exe, so the two can never disagree. Falls back to the assembly version.
    /// </summary>
    public static string CurrentVersion
    {
        get
        {
            try
            {
                if (!IsDevHost(ExePath))
                {
                    var v = FileVersionInfo.GetVersionInfo(ExePath!);
                    if (v.FileMajorPart > 0 || v.FileMinorPart > 0 || v.FileBuildPart > 0)
                        return $"{v.FileMajorPart}.{v.FileMinorPart}.{v.FileBuildPart}";
                }
            }
            catch { }
            return AppInfo.Version;
        }
    }

    /// <summary>False when running from 'dotnet run' / the IDE, or from a folder the user cannot write to (e.g. Program Files).</summary>
    public static bool CanSelfUpdate(out string reason)
    {
        reason = "";
        var exe = ExePath;
        if (IsDevHost(exe))
        { reason = "This copy is running from the development environment, so it cannot replace itself."; return false; }
        try
        {
            var probe = Path.Combine(Path.GetDirectoryName(exe)!, ".write-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "x"); File.Delete(probe);
            return true;
        }
        catch
        {
            reason = "PDF Studio is installed in a folder that needs administrator rights (" + Path.GetDirectoryName(exe) + "). " +
                     "Move PdfStudio.exe to a normal folder such as your Documents or Desktop, or install the update manually.";
            return false;
        }
    }

    /// <summary>Call once at startup: removes the previous version left behind by an update (retries while the old process is still closing).</summary>
    public static void CleanupLeftovers()
    {
        if (string.IsNullOrEmpty(ExePath)) return;
        var old = OldPath; var nw = NewPath;
        _ = Task.Run(async () =>
        {
            for (var i = 0; i < 10; i++)
            {
                try { if (File.Exists(old)) File.Delete(old); if (File.Exists(nw)) File.Delete(nw); return; }
                catch { await Task.Delay(1000).ConfigureAwait(false); }
            }
        });
    }

    // ───────────────────────── 1. check ─────────────────────────

    private static bool TryParse(string text, out Version v)
    {
        text = text.Trim().TrimStart('v', 'V');
        var cut = text.IndexOfAny(new[] { '-', '+' });          // 2.7.0-beta -> 2.7.0
        if (cut >= 0) text = text[..cut];
        if (Version.TryParse(text, out var parsed)) { v = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build)); return true; }
        v = new Version(0, 0, 0); return false;
    }

    public static async Task<UpdateCheck> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            using var req = Get(Endpoint("action=latest&version=" + Uri.EscapeDataString(CurrentVersion)));
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            using var res = await Http.SendAsync(req, cts.Token).ConfigureAwait(false);

            switch (res.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    return new UpdateCheck(null, "The update server did not accept this app's access key.");
                case HttpStatusCode.NotFound:
                    return new UpdateCheck(null, "The update server was reached, but it has no update service. Is the monitor up to date?");
            }
            res.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false));
            var root = doc.RootElement;
            if (!root.TryGetProperty("latest", out var latest) || latest.ValueKind != JsonValueKind.Object)
                return new UpdateCheck(null, null);               // nothing uploaded yet: you are up to date

            var version = latest.GetProperty("version").GetString() ?? "";
            if (!TryParse(version, out var newest)) return new UpdateCheck(null, $"The update server sent an unrecognised version ('{version}').");
            if (!TryParse(CurrentVersion, out var current) || newest <= current) return new UpdateCheck(null, null);

            var clean = $"{newest.Major}.{newest.Minor}.{newest.Build}";
            return new UpdateCheck(new UpdateInfo(
                clean,
                latest.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "",
                Endpoint("action=download&version=" + Uri.EscapeDataString(clean)),
                latest.TryGetProperty("size", out var s) && s.TryGetInt64(out var size) ? size : 0,
                latest.TryGetProperty("sha256", out var h) ? h.GetString() : null), null);
        }
        catch (OperationCanceledException) { return new UpdateCheck(null, "The update server did not answer in time."); }
        catch (HttpRequestException) { return new UpdateCheck(null, "Could not reach the update server. Check your internet connection."); }
        catch (JsonException) { return new UpdateCheck(null, "The update server sent an unexpected reply."); }
        catch (Exception ex) { return new UpdateCheck(null, ex.Message); }
    }

    // ───────────────────────── 2. download ─────────────────────────

    /// <summary>Downloads and verifies the new exe. Returns the path of PdfStudio.exe.new.</summary>
    public static async Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress, CancellationToken ct)
    {
        if (!CanSelfUpdate(out var why)) throw new InvalidOperationException(why);
        var target = NewPath;
        try
        {
            using var req = Get(update.DownloadUrl);
            using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (res.StatusCode == HttpStatusCode.NotFound) throw new IOException("This version is no longer available on the update server.");
            if (res.StatusCode == HttpStatusCode.Unauthorized) throw new IOException("The update server did not accept this app's access key.");
            res.EnsureSuccessStatusCode();

            var total = res.Content.Headers.ContentLength ?? update.Size;
            await using (var src = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buf = new byte[81920]; long done = 0; int n;
                while ((n = await src.ReadAsync(buf, ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
                    done += n;
                    if (total > 0) progress?.Report(Math.Min(1.0, (double)done / total));
                }
            }

            var len = new FileInfo(target).Length;
            if (update.Size > 0 && len != update.Size) throw new IOException("The download is incomplete. Please try again.");

            await using (var fs = File.OpenRead(target))
            {
                var head = new byte[2];
                if (await fs.ReadAsync(head, ct).ConfigureAwait(false) != 2 || head[0] != (byte)'M' || head[1] != (byte)'Z')
                    throw new IOException("The downloaded file is not a Windows program.");
                if (!string.IsNullOrEmpty(update.Sha256))
                {
                    fs.Position = 0;
                    var actual = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false));
                    if (!string.Equals(actual, update.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("The downloaded file failed its integrity check and was discarded.");
                }
            }
            return target;
        }
        catch (HttpRequestException)
        {
            try { if (File.Exists(target)) File.Delete(target); } catch { }
            throw new IOException("Could not download the update. Check your internet connection and try again.");
        }
        catch
        {
            try { if (File.Exists(target)) File.Delete(target); } catch { }
            throw;
        }
    }

    // ───────────────────────── 3. apply ─────────────────────────

    /// <summary>Swaps the files and starts the new version. The caller must then close the app.</summary>
    public static void ApplyAndRestart(string newFile)
    {
        var exe = ExePath ?? throw new InvalidOperationException("Cannot locate the running program.");
        var old = OldPath;
        try { if (File.Exists(old)) File.Delete(old); } catch { }
        File.Move(exe, old);                                        // allowed while the program is running
        try { File.Move(newFile, exe); }
        catch { try { File.Move(old, exe); } catch { } throw; }     // roll back so the app is never left without its exe
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! });
    }
}
