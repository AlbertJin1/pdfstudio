# PDF Studio V2.8.15 release upload — HTML HTTP 403 fix

The previous uploader always posted directly to the public zrok URL and treated an HTML response as a fatal error. The Monitor's PHP upload endpoint returns JSON for API/application errors; an HTML `403 Forbidden` is usually generated before the PHP upload handler processes the request.

## Recommended use

Run `release.bat`, then choose **3. Automatic**. It tries the LAN Monitor first, bypassing public-tunnel filtering for the upload. A short TCP probe prevents a long wait when the LAN endpoint is offline. If the LAN request fails with HTTP 403, HTTP 404, a server error, an HTML/non-JSON response, or a connection exception, it retries the `begin` request against the configured public endpoint. The local Monitor address is:

`http://192.168.254.110:8080/pdfstudio-monitor/api/upload.php`

The route is selected before chunk transfer begins, so a failed route negotiation does not leave a half-uploaded package. You can also bypass zrok directly with:

```bat
release.bat local
```

or:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\publish_to_monitor.ps1 -Target local
```

The uploader keeps the same `X-Api-Key` authentication. It reads the key from `PDFSTUDIO_MONITOR_KEY` or the existing source configuration. The Monitor's private `MONITOR_API_KEY` must match. An actual API-key mismatch normally returns JSON `401`; do not disable authentication to work around a 403.

## Chunk transfer

When the Monitor advertises `chunk_encodings: ["raw", "base64"]`, public/remote uploads use base64-encoded chunks (`enc=base64`) to avoid raw ZIP-body filtering by tunnels/WAFs. The local LAN route keeps raw 4 MiB chunks to avoid a 33% base64 size overhead. Older Monitor releases that do not advertise base64 continue to receive raw chunks.

## Commands

- `release.bat` — build and choose an upload target.
- `release.bat auto` — LAN first, then configured/public fallback.
- `release.bat local` — send directly to the LAN Monitor.
- `release.bat public` — use the configured public endpoint only.
- `release.bat -Force` — prompt for target and force replacement of an identical release.
- `release.bat noupload` — build only.

If the LAN attempt fails too, ensure the Monitor's `serve.bat` is running on `192.168.254.110:8080`, that the build machine can reach that host on the private network, and that the server and uploader use the same API key.
