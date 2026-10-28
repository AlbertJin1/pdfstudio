from pathlib import Path

root = Path(__file__).resolve().parents[1]
ps1 = (root / 'publish_to_monitor.ps1').read_text(encoding='utf-8')
bat = (root / 'release.bat').read_text(encoding='utf-8')
updater = (root / 'Services' / 'UpdateService.cs').read_text(encoding='utf-8')
checks = {
    'uploader exposes auto/public/local targets': "[ValidateSet('auto', 'public', 'local')][string]$Target = 'auto'" in ps1,
    'local target defaults to the configured LAN Monitor host': "http://192.168.254.110:8080/pdfstudio-monitor/api" in ps1,
    'automatic mode switches to alternate endpoint after an HTML/authentication/gateway route failure': 'function Test-RouteFailure' in ps1 and 'function Switch-ToFallback' in ps1 and "[int]$result.Status -eq 401" in ps1 and "[int]$result.Status -eq 403" in ps1,
    'automatic route prioritizes LAN and probes it before upload': '$primaryBase = $localDefault' in ps1 and 'Test-MonitorTcpEndpoint $script:endpoint' in ps1,
    'automatic fallback is only attempted once': '$script:fallbackEndpoint = $null  # only switch once' in ps1,
    'uploader uses base64 for public routes when supported': "'base64'" in ps1 and "&enc=base64" in ps1 and 'chunk_encodings' in ps1 and 'public/remote routes' in ps1,
    'LAN route avoids base64 transfer overhead': "avoid base64's 33% transfer overhead" in ps1 and '$usingLanEndpoint' in ps1,
    'legacy monitors continue using raw chunk uploads': 'Older Monitor versions that do not advertise base64 always receive raw chunks.' in ps1,
    'release BAT offers public, local, and auto choices': '1. Public zrok URL' in bat and '2. Local Monitor' in bat and '3. Automatic - local first' in bat,
    'release BAT passes the selected route to PowerShell': '-Target "!TARGET!"' in bat,
    'release failure message recommends local fallback': 'publish_to_monitor.ps1 -Target local' in bat,
    'release stops when EXE product/version metadata is invalid': 'Release validation failed' in bat and "$info.ProductName -ne 'PDF Studio'" in bat and 'EXE and csproj versions differ' in bat,
    'release blocks publication without versioned changelog notes': 'CHANGELOG.md has no release heading for' in bat,
    'noupload flag works regardless of argument position': 'if "%NOUPLOAD%"=="1"' in bat and '"noupload" set "NOUPLOAD=1"' in bat,
    'legacy updater identifies the 2.x-to-3.x platform migration': 'public static bool IsPlatformMigration(UpdateInfo update)' in updater and 'current.Major < 3 && target.Major >= 3' in updater,
    'platform migration prefers compressed ZIP and falls back to the full V3 portable EXE': 'if (IsPlatformMigration(update))' in updater and 'await DownloadZipAsync(update, portablePath' in updater and 'The compressed migration package could not be used. Downloading the verified portable app' in updater and 'await DownloadFileAsync(update.DownloadUrl, portablePath' in updater,
    'legacy migration verifies downloaded executable integrity before applying': 'await VerifyExeAsync(portablePath, update, ct)' in updater and 'VerifyPortableCopy(replacement, update)' in updater and 'VerifyPortableCopy(fallbackTemp, update)' in updater,
    'protected installation migration can launch a per-user portable copy without elevation': 'PortableFallbackPath' in updater and 'MigrationMarkerPath' in updater and 'Continue into the fallback below' in updater,
    'legacy C# delta downloads are bypassed during the Electron platform migration': 'Delta reuse is intentionally not attempted across the C# -> Electron architecture boundary.' in updater,
}
failed = False
for label, ok in checks.items():
    print(('PASS' if ok else 'FAIL') + ': ' + label)
    failed |= not ok
raise SystemExit(1 if failed else 0)
