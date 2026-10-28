# Publishing the legacy V2.8.15 bridge

Run `release.bat auto` to prefer the LAN Monitor and fall back to the configured public endpoint if the LAN endpoint is unavailable. You can also select `local` or `public` explicitly.

The uploader verifies the EXE's Windows FileVersion, reads the matching release notes from `CHANGELOG.md`, compresses the EXE if ZIP is smaller, sends resumable 4 MiB chunks, uses base64 chunks when the remote Monitor advertises support, and verifies server completion. It explicitly publishes as the Monitor app id `pdfstudio`.

For the required V2-to-V3 rollout, publish this version **2.8.15 first**, then publish V3. If the Monitor's public zrok proxy returns an HTML 403 before returning API JSON, use `release.bat local` or `release.bat auto`; base64 chunks cannot bypass a proxy that blocks the initial POST.

Set `PDFSTUDIO_MONITOR_KEY` or create an ignored local `monitor-key.txt` with the publishing key. Existing source projects retain their source-constant fallback for backward compatibility. Do not add keys to BAT files or commits.
