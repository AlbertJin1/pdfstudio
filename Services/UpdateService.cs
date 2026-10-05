using PdfStudio.UI;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace PdfStudio.Services;

/// <summary>A newer release found on GitHub.</summary>
public sealed record UpdateInfo(string Version, string Tag, string Notes, string AssetApiUrl, long Size, string? Sha256, string PageUrl);

/// <summary>Result of a check: Update is null when you are up to date; Error is set when the check itself failed.</summary>
public sealed record UpdateCheck(UpdateInfo? Update, string? Error);

/// <summary>
/// Self-update from GitHub Releases.
///
///  1. CheckAsync    asks the GitHub API for the latest release and compares its tag (v2.7.0) with the running version.
///  2. DownloadAsync saves the release's PdfStudio.exe next to the running exe as PdfStudio.exe.new and verifies it.
///  3. ApplyAndRestart  renames the running exe to PdfStudio.exe.old (Windows allows renaming a running exe, just not
///                      overwriting it), moves the new file into place and starts it. The new copy deletes the .old
///                      file on its next start (CleanupLeftovers).
///
/// Private repository: the GitHub API needs a token. Put a fine-grained, read-only token in <see cref="DefaultToken"/>,
/// or in %LOCALAPPDATA%\PdfStudio\settings.txt as   update_token=github_pat_...   (see UPDATES.md for the security notes).
/// Other settings keys: update_repo=owner/name   auto_update=1/0   update_skip=2.7.0
/// </summary>
public static class UpdateService
{
    // ── CHANGE THESE ───────────────────────────────────────────────────────────────────────────────────────────
    public const string DefaultRepo = "YOUR-GITHUB-USERNAME/YOUR-REPO";   // "owner/name"
    private const string DefaultToken = "";                               // leave empty for a public repo
    private const string AssetName = "PdfStudio.exe";                     // the file you attach to each release
    // ───────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static string Repo => AppSettings.Get("update_repo", DefaultRepo).Trim();
    private static string Token => AppSettings.Get("update_token", DefaultToken).Trim();

    public static bool AutoCheck => AppSettings.GetBool("auto_update", true);
    public static string SkippedVersion { get => AppSettings.Get("update_skip"); set => AppSettings.Set("update_skip", value); }

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        // Redirects are followed by hand: GitHub answers an asset download with a 302 to a pre-signed storage URL,
        // and that second request must NOT carry the Authorization header.
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        var c = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"PdfStudio/{AppInfo.Version}");
        return c;
    }

    private static HttpRequestMessage Api(string url, string accept)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        req.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        if (Token.Length > 0) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return req;
    }

    // ───────────────────────── paths / guards ─────────────────────────

    private static string? ExePath => Environment.ProcessPath;
    private static string NewPath => ExePath + ".new";
    private static string OldPath => ExePath + ".old";

    /// <summary>False when running from 'dotnet run' / the IDE, or from a folder the user cannot write to (e.g. Program Files).</summary>
    public static bool CanSelfUpdate(out string reason)
    {
        reason = "";
        var exe = ExePath;
        if (string.IsNullOrEmpty(exe) || string.Equals(Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase))
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
        var repo = Repo;
        if (!repo.Contains('/') || repo.StartsWith("YOUR-", StringComparison.OrdinalIgnoreCase))
            return new UpdateCheck(null, "Updates are not configured yet (set the repository in Services/UpdateService.cs).");
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            using var req = Api($"https://api.github.com/repos/{repo}/releases/latest", "application/vnd.github+json");
            using var res = await Http.SendAsync(req, cts.Token).ConfigureAwait(false);

            switch (res.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    return new UpdateCheck(null, Token.Length == 0
                        ? "No published release was found. If the repository is private, an access token is required."
                        : "No published release was found, or the access token cannot read this repository.");
                case HttpStatusCode.Unauthorized:
                    return new UpdateCheck(null, "GitHub rejected the access token (expired or revoked).");
                case HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests:
                    return new UpdateCheck(null, "GitHub is limiting requests right now. Please try again later.");
            }
            res.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false));
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!TryParse(tag, out var latest)) return new UpdateCheck(null, $"The latest release has an unrecognised version tag ('{tag}').");
            if (!TryParse(AppInfo.Version, out var current) || latest <= current) return new UpdateCheck(null, null);

            JsonElement? asset = null;
            if (root.TryGetProperty("assets", out var assets))
                foreach (var a in assets.EnumerateArray())
                    if (string.Equals(a.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase)) { asset = a; break; }
            if (asset is null) return new UpdateCheck(null, $"Release {tag} has no '{AssetName}' file attached.");

            string? sha = null;                                   // GitHub reports "sha256:<hex>" for uploaded assets
            if (asset.Value.TryGetProperty("digest", out var d) && d.GetString() is { } ds && ds.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                sha = ds[7..];

            return new UpdateCheck(new UpdateInfo(
                $"{latest.Major}.{latest.Minor}.{latest.Build}", tag,
                root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
                asset.Value.GetProperty("url").GetString()!,
                asset.Value.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                sha,
                root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : ""), null);
        }
        catch (OperationCanceledException) { return new UpdateCheck(null, "GitHub did not answer in time."); }
        catch (HttpRequestException) { return new UpdateCheck(null, "Could not reach GitHub. Check your internet connection."); }
        catch (Exception ex) { return new UpdateCheck(null, ex.Message); }
    }

    // ───────────────────────── 2. download ─────────────────────────

    private static async Task<HttpResponseMessage> OpenAssetAsync(string url, CancellationToken ct)
    {
        using var req = Api(url, "application/octet-stream");
        var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if ((int)res.StatusCode is >= 300 and < 400 && res.Headers.Location is { } loc)
        {
            res.Dispose();
            var next = loc.IsAbsoluteUri ? loc : new Uri(new Uri(url), loc);
            using var plain = new HttpRequestMessage(HttpMethod.Get, next);     // pre-signed URL: no Authorization header
            return await Http.SendAsync(plain, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        return res;
    }

    /// <summary>Downloads and verifies the new exe. Returns the path of PdfStudio.exe.new.</summary>
    public static async Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress, CancellationToken ct)
    {
        if (!CanSelfUpdate(out var why)) throw new InvalidOperationException(why);
        var target = NewPath;
        try
        {
            using var res = await OpenAssetAsync(update.AssetApiUrl, ct).ConfigureAwait(false);
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
