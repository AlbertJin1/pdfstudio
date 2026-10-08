# In-app updates (from your PDF Studio Monitor)

PDF Studio checks your **PDF Studio Monitor** server (the PHP app behind your ngrok address) a few seconds after it
starts, and from **About → Check for updates**. If you have uploaded a newer `PdfStudio.exe`, the user sees a window with
the release notes and three choices:

| Button | What happens |
|---|---|
| **Update now** | downloads the new exe (progress bar), checks it, swaps it in and restarts PDF Studio |
| **Later** | closes the window; the user is asked again at the next start |
| **Skip this version** | never asks about *this* version again (the next, newer version is offered as usual) |

No GitHub, no tokens, nothing to configure: the updater uses the same server address and API key as the usage statistics
(`UsageTracker.DefaultBaseUrl` / `DefaultApiKey`, or `monitor_url` / `monitor_key` in settings.txt).

## Shipping an update

1. Raise `<Version>` **and** `<FileVersion>` in `PdfStudioCS.csproj` to the same number (e.g. `2.7.0` / `2.7.0.0`) and add the
   CHANGELOG.md and Services/ChangeLog.cs entries (the build already enforces this).
2. Run `release.bat`. It builds `publish\PdfStudio.exe`, shows the version inside it, then uploads a zip of it to the monitor.
3. Open the monitor → **Releases**, drop `PdfStudio.exe` (or a `.zip` containing it) in, optionally type *What's new*, press **Upload release**.

You never type a version number: the monitor reads it from the exe and stores the file as
`release/<version>/PdfStudio.exe` (e.g. `release/2.7.0/PdfStudio.exe`). The highest version is what the app is offered.
Only `major.minor.build` counts; the 4th number is ignored.

## How the app decides

* The app compares **its own exe's file version** (Windows → Properties → Details → File version) with the highest
  uploaded version. Both sides read the same field, so they cannot disagree.
* Versions are compared as numbers: `2.10.0` is newer than `2.9.0`.
* Skipping is remembered in `%LOCALAPPDATA%\PdfStudio\settings.txt` as `update_skip=2.7.0`.
* `auto_update=0` in the same file turns off the check at startup (the About button still works).

## Notes

* The exe must be in a folder the user can write to (not `C:\Program Files`); otherwise the app explains this and stops.
* The old exe is renamed to `PdfStudio.exe.old` and deleted by the new copy at its next start.
* Downloads are verified for size, the Windows "MZ" header and the SHA-256 the monitor computed when you uploaded the file.
* Running via `dotnet run` never self-updates.
* A version released here cannot be installed by copies **older than the first build that contains this updater**; those
  must be replaced by hand once.
* If the monitor is offline when the app starts, nothing is shown (a manual check from About shows the reason).
* **Smaller transfers (v2.7.0+).** `release.bat` zips the signed exe (deflate, smallest size) and uploads `PdfStudio.zip`, so the upload is
  about half the size. The monitor checks the zip's SHA-256, unpacks it, checks the exe inside (SHA-256, version, product name) and
  stores both `release/<version>/PdfStudio.exe` (older copies download this, unchanged) and `PdfStudio.zip` (current copies download
  this, unpack it to `PdfStudio.exe.new` and verify the exe's own SHA-256 before swapping). If the zip is missing or fails a check,
  the app silently downloads the full exe. The monitor builds the zip itself for releases uploaded as a plain exe or on the web page
  (also for older releases, on first request). `publish_to_monitor.ps1 -NoZip` uploads the plain exe. Needs only PHP's zlib extension.
  Copies older than 2.7.0 still download the full exe once, to get to 2.7.0 or later.
* Not done: binary delta patches (sending only what changed between two versions). They would shrink updates further but need a
  patch tool on both sides; the exe bundles the whole .NET runtime, so most bytes are unchanged between releases and it could be worth it later.
