# In-app updates (GitHub Releases)

PDF Studio checks GitHub for a newer release a few seconds after it starts (and from **About → Check for updates**).
If a newer one exists, it shows the release notes, downloads `PdfStudio.exe`, swaps it in and restarts.

## One-time setup
1. In `Services/UpdateService.cs` set `DefaultRepo` (`owner/name`) — and `DefaultToken` only if the repo is private (see below).
2. In `release.bat` set `REPO` to the same value.
3. Install the GitHub CLI (https://cli.github.com) and run `gh auth login`.

## Shipping an update
1. Raise `<Version>` and `<FileVersion>` in `PdfStudioCS.csproj`; add the CHANGELOG.md and Services/ChangeLog.cs entries (the build already enforces this).
2. Optional: write `release-notes.md` — it is what users see in the update window.
3. Run `release.bat 2.7.0` (builds, then creates tag `v2.7.0` with `PdfStudio.exe` attached).

The release **tag must equal the csproj version** (`v2.7.0` ↔ `2.7.0`); release.bat checks this. Draft and pre-release
entries are ignored by the app. The attached file **must be named `PdfStudio.exe`**.

## Where the repository can live — read this
The app is distributed to other people, so anything inside it can be extracted.

* **Public "releases only" repo (recommended).** Keep the source in your private repo; create a second, public repo that
  holds nothing but releases (exe + notes). No token is needed at all.
* **Private repo + token.** Works, but the token ships with the exe. GitHub requires *Contents: read* to download release
  assets, which also lets the holder read your source code. Use a fine-grained token limited to that one repo, read-only,
  and accept that anyone with the exe can pull it out. Tokens expire: once it does, installed copies can no longer
  update themselves and must be replaced by hand.
* **Your own server as a relay.** Keep the token on the PHP monitor server you already run and let the app ask that
  server for the latest version/download. Safest for a private repo, but needs a small server endpoint.

## Settings overrides (`%LOCALAPPDATA%\PdfStudio\settings.txt`)
`update_repo=owner/name` · `update_token=github_pat_…` · `auto_update=0` (turn off the startup check) · `update_skip=2.7.0`

## Notes
* The exe must be in a folder the user can write to (not `C:\Program Files`); otherwise the app explains this and stops.
* The old exe is renamed to `PdfStudio.exe.old` and deleted by the new copy at its next start.
* Downloads are checked for size, the Windows "MZ" header and — when GitHub provides it — the SHA-256 of the asset.
* Running via `dotnet run` never self-updates.
