<#
  PDF Studio - uploads publish\PdfStudio.exe to the PDF Studio Monitor (through zrok or the LAN endpoint) and sends the release
  notes of that version from CHANGELOG.md.  Called by release.bat; can also be run by hand:

      powershell -ExecutionPolicy Bypass -File publish_to_monitor.ps1 [-Target auto|public|local] [-Force]

  What it does
    1. Reads the version out of the exe (the same File version the monitor reads) and its notes from CHANGELOG.md.
    2. Asks the monitor what it has:
         NEW version             -> uploaded normally
         SAME version, exe changed -> uploaded and OVERWRITES the stored file
         SAME version, same exe  -> nothing is uploaded (release notes are refreshed); -Force uploads anyway
    3. Packs the exe into PdfStudio.zip (deflate, smallest size, about half the size) and sends THAT in 4 MB pieces, so the
       upload is roughly half as big. The monitor's web page shows an "upload in progress" bar while this runs.
       -NoZip sends the plain exe instead (needed only for a monitor that predates zip support).
    4. The monitor verifies the zip's SHA-256, unpacks it, verifies the exe inside (SHA-256, version, product name), stores
       the exe (for older app copies) and the zip (what current app copies download and unpack themselves), then publishes.

  Server API base comes from -BaseUrl / environment variables, then Services\UsageTracker.cs. The API key comes from -ApiKey,
  PDFSTUDIO_MONITOR_KEY, monitor-key.txt, the legacy source constant (compatibility only), or a secure prompt. Target auto
  tries the LAN Monitor first and falls back to the configured/public endpoint when the LAN service is unavailable; use -Target public
  to force the zrok route. Public uploads use base64 chunks when the Monitor advertises support. Set PDFSTUDIO_LOCAL_MONITOR_URL to override the LAN API base.
#>
param(
    [string]$ExePath   = (Join-Path $PSScriptRoot 'publish\PdfStudio.exe'),
    [string]$Changelog = (Join-Path $PSScriptRoot 'CHANGELOG.md'),
    [string]$BaseUrl   = '',
    [string]$ApiKey    = $env:PDFSTUDIO_MONITOR_KEY,
    [ValidateSet('auto', 'public', 'local')][string]$Target = 'auto',
    [switch]$Force,
    [switch]$NoZip
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Fail([string]$msg) { Write-Host ''; Write-Host ('UPLOAD FAILED: ' + $msg) -ForegroundColor Red; exit 1 }
function Mb([double]$n) { return ('{0:N1} MB' -f ($n / 1MB)) }

# ---------- settings / route selection ----------
$tracker = Join-Path $PSScriptRoot 'Services\UsageTracker.cs'
$src = ''
if (Test-Path $tracker) { $src = Get-Content -Raw $tracker }
$trackerBaseUrl = ''
if ($src -match 'DefaultBaseUrl\s*=\s*"([^"]+)"') { $trackerBaseUrl = $Matches[1] }

# Prefer an ignored local key file. Keep the embedded source constant only as a legacy compatibility fallback.
# New/recommended deployments should use PDFSTUDIO_MONITOR_KEY or monitor-key.txt.
$keyFile = Join-Path $PSScriptRoot 'monitor-key.txt'
if ([string]::IsNullOrWhiteSpace($ApiKey) -and (Test-Path -LiteralPath $keyFile -PathType Leaf)) {
    $ApiKey = [IO.File]::ReadAllText($keyFile).Trim()
}
if ([string]::IsNullOrWhiteSpace($ApiKey) -and $src -match 'DefaultApiKey\s*=\s*"([^"]+)"') { $ApiKey = $Matches[1] }
if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Host 'A Monitor publishing API key is required. It will not be added to the release package.' -ForegroundColor Yellow
    $secureKey = Read-Host 'Monitor API key' -AsSecureString
    $keyPtr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
    try { $ApiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPtr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPtr) }
}

$publicDefault = 'https://pdfstudio.shares.zrok.io/pdfstudio-monitor/api'
$localDefault = $env:PDFSTUDIO_LOCAL_MONITOR_URL
if ([string]::IsNullOrWhiteSpace($localDefault)) { $localDefault = 'http://192.168.254.110:8080/pdfstudio-monitor/api' }
$localDefault = $localDefault.Trim().TrimEnd('/')

# Explicit -BaseUrl wins, then the environment override, then the legacy source constant.
$configuredBase = $BaseUrl
if ([string]::IsNullOrWhiteSpace($configuredBase)) { $configuredBase = $env:PDFSTUDIO_MONITOR_URL }
if ([string]::IsNullOrWhiteSpace($configuredBase)) { $configuredBase = $trackerBaseUrl }
if ([string]::IsNullOrWhiteSpace($configuredBase)) { $configuredBase = $publicDefault }
$configuredBase = $configuredBase.Trim().TrimEnd('/')

switch ($Target) {
    'local'  { $primaryBase = $localDefault; $fallbackBase = $null }
    'public' { $primaryBase = $configuredBase; $fallbackBase = $null }
    default  {
        # Auto mode prefers the LAN endpoint to bypass public-tunnel POST filtering; fall back to the configured/public URL.
        $primaryBase = $localDefault
        if ($configuredBase -eq $localDefault) { $fallbackBase = $publicDefault }
        else { $fallbackBase = $configuredBase }
        if ($fallbackBase -eq $primaryBase) { $fallbackBase = $null }
    }
}
if ([string]::IsNullOrWhiteSpace($ApiKey)) { Fail 'No API key. Set PDFSTUDIO_MONITOR_KEY, create monitor-key.txt, or enter the key at the secure prompt.' }
$script:endpoint = $primaryBase.Trim().TrimEnd('/') + '/upload.php'
$script:fallbackEndpoint = if (-not [string]::IsNullOrWhiteSpace($fallbackBase)) { $fallbackBase.Trim().TrimEnd('/') + '/upload.php' } else { $null }

if (-not (Test-Path $ExePath)) { Fail ('Exe not found: ' + $ExePath) }
$ExePath = (Resolve-Path $ExePath).Path

# ---------- version ----------
$fv = (Get-Item $ExePath).VersionInfo.FileVersion
if ([string]::IsNullOrWhiteSpace($fv)) { Fail 'The exe has no File version. Set <Version> and <FileVersion> in PdfStudioCS.csproj.' }
$p = $fv.Split('.')
$version = '{0}.{1}.{2}' -f [int]$p[0], [int]$p[1], [int]$p[2]

# ---------- release notes (CHANGELOG.md -> plain text for the update window) ----------
if (-not (Test-Path $Changelog)) { Fail ('CHANGELOG.md not found: ' + $Changelog) }
$log = Get-Content -Raw -Encoding UTF8 $Changelog
$m = [regex]::Match($log, '(?ms)^## \[' + [regex]::Escape($version) + '\][^\r\n]*\r?\n(.*?)(?=^## \[|\z)')
if (-not $m.Success) { Fail ('CHANGELOG.md has no "## [' + $version + ']" section, so there are no release notes to send.') }
$lines = New-Object System.Collections.Generic.List[string]
foreach ($raw in ($m.Groups[1].Value -split '\r?\n')) {
    $l = $raw.TrimEnd()
    $l = [regex]::Replace($l, '\[([^\]]+)\]\([^)]*\)', '$1')   # [text](url) -> text
    $l = $l -replace '\*\*', '' -replace '`', ''
    if ($l -match '^###\s+(.*)$') { if ($lines.Count -gt 0) { $lines.Add('') }; $lines.Add($Matches[1].Trim().ToUpper()); continue }
    if ($l -match '^_(.*)_$')     { $lines.Add($Matches[1].Trim()); continue }
    if ($l.Trim() -eq '')         { continue }
    if ($l -match '^\s*[-*]\s+(.*)$') { $lines.Add([string][char]0x2022 + ' ' + $Matches[1].Trim()); continue }
    $lines.Add($l.Trim())
}
$notes = ($lines -join "`n").Trim()
if ($notes.Length -gt 3900) { $notes = $notes.Substring(0, 3900).TrimEnd() + "`n..." }
if ($notes -eq '') { Fail ('The CHANGELOG.md section for ' + $version + ' is empty.') }

$exeSize = (Get-Item $ExePath).Length
Write-Host ''
Write-Host 'Calculating SHA-256...'
$exeSha = (Get-FileHash -Algorithm SHA256 -Path $ExePath).Hash.ToLower()

# ---------- pack: PdfStudio.exe -> PdfStudio.zip (the signed exe is zipped AFTER signing, so the signature is untouched) ----------
$uploadPath = $ExePath
$package = ''
$tempZip = $null
if (-not $NoZip) {
    $tempZip = Join-Path ([IO.Path]::GetTempPath()) ('PdfStudio-' + $version + '-' + [Guid]::NewGuid().ToString('N') + '.zip')
    Write-Host 'Compressing the exe for upload...'
    $zip = [IO.Compression.ZipFile]::Open($tempZip, [IO.Compression.ZipArchiveMode]::Create)
    # Written by hand (CreateEntry + copy) instead of ZipFileExtensions.CreateEntryFromFile, whose overloads differ between
    # Windows PowerShell 5.1 and PowerShell 7.
    $level = [IO.Compression.CompressionLevel]::Optimal
    if ([Enum]::GetNames([IO.Compression.CompressionLevel]) -contains 'SmallestSize') { $level = [Enum]::Parse([IO.Compression.CompressionLevel], 'SmallestSize') }
    $inStream = $null; $entryStream = $null
    try {
        $entry = $zip.CreateEntry('PdfStudio.exe', $level)
        $entryStream = $entry.Open()
        $inStream = [IO.File]::OpenRead($ExePath)
        $inStream.CopyTo($entryStream, 1MB)
    }
    finally {
        if ($inStream) { $inStream.Dispose() }
        if ($entryStream) { $entryStream.Dispose() }
        $zip.Dispose()
    }
    $uploadPath = $tempZip
    $package = 'zip'
}
$size = (Get-Item $uploadPath).Length
$sha = (Get-FileHash -Algorithm SHA256 -Path $uploadPath).Hash.ToLower()

Write-Host ''
if ($package -eq 'zip') { Write-Host ('Uploading PDF Studio v' + $version + ' (' + (Mb $size) + ' zipped, the exe is ' + (Mb $exeSize) + ' - saves ' + [int](100 - 100 * $size / $exeSize) + '%)') -ForegroundColor Cyan }
else                    { Write-Host ('Uploading PDF Studio v' + $version + ' (' + (Mb $size) + ', not compressed)') -ForegroundColor Cyan }
Write-Host ('Monitor : ' + $script:endpoint + '  (target: ' + $Target + ')')
Write-Host 'Release notes that will be sent:' -ForegroundColor Cyan
foreach ($ln in ($notes -split "`n")) { Write-Host ('   ' + $ln) -ForegroundColor DarkGray }
Write-Host ''

# ---------- http ----------
$http = New-Object System.Net.Http.HttpClient
$http.Timeout = [TimeSpan]::FromMinutes(10)
$http.DefaultRequestHeaders.Add('X-Api-Key', $ApiKey)
$http.DefaultRequestHeaders.Add('ngrok-skip-browser-warning', '1')    # harmless on zrok; skips the ngrok free-plan interstitial when a tunnel is used

function Invoke-Monitor([string]$query, [System.Net.Http.HttpContent]$content) {
    $resp = $null
    try {
        $resp = $http.PostAsync($script:endpoint + $query, $content).GetAwaiter().GetResult()
        $text = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $json = $null
        try { $json = $text | ConvertFrom-Json } catch { }
        if ($null -eq $json) {
            $snippet = $text; if ($snippet.Length -gt 200) { $snippet = $snippet.Substring(0, 200) }
            $json = [pscustomobject]@{ ok = $false; error = ('Unexpected reply (HTTP ' + [int]$resp.StatusCode + '): ' + $snippet) }
        }
        return [pscustomobject]@{ Status = [int]$resp.StatusCode; Json = $json }
    }
    finally {
        if ($resp) { $resp.Dispose() }
        if ($content) { $content.Dispose() }
    }
}
function JsonBody($obj) {
    return New-Object System.Net.Http.StringContent(($obj | ConvertTo-Json -Compress), [Text.Encoding]::UTF8, 'application/json')
}
function Test-RouteFailure($result) {
    if ($null -eq $result) { return $true }
    if ([int]$result.Status -eq 401 -or [int]$result.Status -eq 403 -or [int]$result.Status -eq 404 -or [int]$result.Status -eq 408 -or [int]$result.Status -eq 429 -or [int]$result.Status -ge 500) { return $true }
    $err = [string]$result.Json.error
    return ($err -match '^Unexpected reply \(HTTP ')
}
function Switch-ToFallback([string]$reason) {
    if ([string]::IsNullOrWhiteSpace($script:fallbackEndpoint)) { return $false }
    $script:endpoint = $script:fallbackEndpoint
    $script:fallbackEndpoint = $null  # only switch once; do not loop between endpoints
    Write-Host ''
    Write-Host ('The first Monitor endpoint was blocked or returned an invalid response: ' + $reason) -ForegroundColor Yellow
    Write-Host ('Retrying the release upload through the alternate endpoint: ' + $script:endpoint) -ForegroundColor Yellow
    Write-Host 'If this is the LAN endpoint, ensure serve.bat is running on the Monitor host and the API key matches its private configuration.' -ForegroundColor DarkGray
    return $true
}
function Test-MonitorTcpEndpoint([string]$url, [int]$timeoutMs = 1800) {
    $client = $null
    $async = $null
    try {
        $uri = [System.Uri]$url
        $client = New-Object System.Net.Sockets.TcpClient
        $async = $client.BeginConnect($uri.Host, $uri.Port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne($timeoutMs, $false)) { return $false }
        $client.EndConnect($async)
        return $true
    } catch { return $false }
    finally {
        if ($async -and $async.AsyncWaitHandle) { $async.AsyncWaitHandle.Close() }
        if ($client) { $client.Close() }
    }
}

# ---------- begin: new / replace / identical ----------
if ($Target -eq 'auto' -and -not (Test-MonitorTcpEndpoint $script:endpoint)) {
    [void](Switch-ToFallback 'The LAN Monitor did not accept a TCP connection promptly.')
}
$beginPayload = @{ app = 'pdfstudio'; version = $version; size = $size; sha256 = $sha; notes = $notes; filename = 'PdfStudio.exe'; force = [bool]$Force; package = $package; exe_size = $exeSize; exe_sha256 = $exeSha }
$begin = $null
try {
    $begin = Invoke-Monitor '?action=begin' (JsonBody $beginPayload)
} catch {
    $firstError = $_.Exception.Message
    if (Switch-ToFallback $firstError) {
        try { $begin = Invoke-Monitor '?action=begin' (JsonBody $beginPayload) }
        catch { if ($tempZip) { Remove-Item -LiteralPath $tempZip -ErrorAction SilentlyContinue }; Fail ('Both Monitor endpoints failed. Alternate endpoint error: ' + $_.Exception.Message) }
    } else {
        if ($tempZip) { Remove-Item -LiteralPath $tempZip -ErrorAction SilentlyContinue }
        Fail ('Could not reach the monitor: ' + $firstError + '. Use -Target local if the public zrok endpoint is blocking POST requests.')
    }
}
if ($null -ne $begin -and -not $begin.Json.ok -and (Test-RouteFailure $begin) -and (Switch-ToFallback ([string]$begin.Json.error))) {
    try { $begin = Invoke-Monitor '?action=begin' (JsonBody $beginPayload) }
    catch { if ($tempZip) { Remove-Item -LiteralPath $tempZip -ErrorAction SilentlyContinue }; Fail ('The primary endpoint failed and the alternate endpoint could not be reached: ' + $_.Exception.Message) }
}
if ($null -eq $begin -or -not $begin.Json.ok) {
    if ($tempZip) { Remove-Item -LiteralPath $tempZip -ErrorAction SilentlyContinue }
    Fail ([string]$begin.Json.error)
}
$mode = [string]$begin.Json.mode

# A monitor that predates zip support ignores "package" and would treat the zip as an exe. It does not echo "package" back,
# so cancel that upload and send the plain exe instead (deploy the new monitor to get the smaller upload).
if ($package -eq 'zip' -and $mode -ne 'identical' -and [string]$begin.Json.package -ne 'zip') {
    Write-Host 'This monitor does not support zipped releases yet (it has not been updated) - uploading the plain exe instead.' -ForegroundColor Yellow
    try { [void](Invoke-Monitor ('?action=abort&upload_id=' + [string]$begin.Json.upload_id) (JsonBody @{})) } catch { }
    if ($tempZip) { Remove-Item -LiteralPath $tempZip -ErrorAction SilentlyContinue; $tempZip = $null }
    $uploadPath = $ExePath; $package = ''; $size = $exeSize; $sha = $exeSha
    try { $begin = Invoke-Monitor '?action=begin' (JsonBody @{ app = 'pdfstudio'; version = $version; size = $size; sha256 = $sha; notes = $notes; filename = 'PdfStudio.exe'; force = [bool]$Force }) }
    catch { Fail ('Could not reach the monitor: ' + $_.Exception.Message) }
    if (-not $begin.Json.ok) { Fail ([string]$begin.Json.error) }
    $mode = [string]$begin.Json.mode
}

if ($mode -eq 'identical') {
    if ($tempZip) { Remove-Item -LiteralPath $tempZip -ErrorAction SilentlyContinue }
    Write-Host ('v' + $version + ' is already on the monitor with the SAME file - nothing to upload.') -ForegroundColor Yellow
    if ($begin.Json.notes_updated) { Write-Host 'Release notes were refreshed from CHANGELOG.md.' -ForegroundColor Green }
    Write-Host 'To upload anyway, run publish_to_monitor.ps1 -Force, or bump the version.'
    exit 0
}
if ($mode -eq 'replace') { Write-Host ('v' + $version + ' already exists but the exe is DIFFERENT -> it will be OVERWRITTEN.') -ForegroundColor Yellow }
else                     { Write-Host ('v' + $version + ' is a NEW version -> uploading as a new release.') -ForegroundColor Green }

# ---------- chunks ----------
$id = [string]$begin.Json.upload_id
$chunk = [int]$begin.Json.chunk_size
# The current Monitor advertises accepted encodings. On public/remote routes, prefer base64 so a tunnel/WAF
# does not inspect raw ZIP bytes. On the LAN, keep raw 4 MiB chunks to avoid base64's 33% transfer overhead.
# Older Monitor versions that do not advertise base64 always receive raw chunks.
$useBase64Chunks = $false
$usingLanEndpoint = $false
try {
    $activeUri = [System.Uri]$script:endpoint
    $lanUri = [System.Uri]$localDefault
    $usingLanEndpoint = ($activeUri.Host -eq $lanUri.Host -and $activeUri.Port -eq $lanUri.Port)
} catch { }
if (-not $usingLanEndpoint -and $null -ne $begin.Json.chunk_encodings) {
    foreach ($encoding in @($begin.Json.chunk_encodings)) { if ([string]$encoding -eq 'base64') { $useBase64Chunks = $true } }
}
if ($useBase64Chunks) { Write-Host 'Upload transport: base64-encoded chunks (public tunnel/WAF compatible).' -ForegroundColor DarkGray }
else { Write-Host 'Upload transport: raw 4 MiB chunks (LAN/legacy-compatible, lower overhead).' -ForegroundColor DarkGray }
$fs = [IO.File]::OpenRead($uploadPath)
$buf = New-Object byte[] $chunk
$offset = [long]0
$clock = [Diagnostics.Stopwatch]::StartNew()
try {
    while ($offset -lt $size) {
        $fs.Position = $offset
        $n = $fs.Read($buf, 0, $chunk)
        if ($n -le 0) { throw 'Could not read the file to upload.' }
        $sent = $false
        for ($try = 1; $try -le 6 -and -not $sent; $try++) {
            try {
                if ($useBase64Chunks) {
                    $encoded = [Convert]::ToBase64String($buf, 0, $n)
                    $body = New-Object System.Net.Http.StringContent($encoded, [Text.Encoding]::ASCII, 'text/plain')
                    $chunkQuery = '?action=chunk&upload_id=' + $id + '&offset=' + $offset + '&enc=base64'
                } else {
                    $body = New-Object System.Net.Http.ByteArrayContent($buf, 0, $n)
                    $body.Headers.ContentType = New-Object System.Net.Http.Headers.MediaTypeHeaderValue('application/octet-stream')
                    $chunkQuery = '?action=chunk&upload_id=' + $id + '&offset=' + $offset
                }
                $r = Invoke-Monitor $chunkQuery $body
                if ($r.Json.ok) { $offset = [long]$r.Json.received; $sent = $true }
                elseif ($r.Status -eq 409 -and $null -ne $r.Json.expected_offset) { $offset = [long]$r.Json.expected_offset; $sent = $true }   # resume where the server is
                elseif ($r.Status -eq 403) { throw (New-Object InvalidOperationException ('HTTP 403 during upload. The public proxy may be rejecting POST requests; restart with -Target local or release.bat auto. Response: ' + [string]$r.Json.error)) }
                elseif ($r.Status -ge 400 -and $r.Status -lt 500 -and $r.Status -ne 408 -and $r.Status -ne 429) { throw (New-Object InvalidOperationException ([string]$r.Json.error)) }
                else { throw (New-Object Net.WebException ([string]$r.Json.error)) }
            } catch [InvalidOperationException] {
                throw
            } catch {
                if ($try -ge 6) { throw }
                Write-Host ('  piece failed (' + $_.Exception.Message + '), retrying ' + $try + '/5...') -ForegroundColor Yellow
                Start-Sleep -Seconds ([Math]::Min(10, 2 * $try))
            }
        }
        $pct = [int][Math]::Floor($offset * 100 / $size)
        $rate = if ($clock.Elapsed.TotalSeconds -gt 0) { $offset / $clock.Elapsed.TotalSeconds } else { 0 }
        Write-Progress -Activity ('Uploading PDF Studio v' + $version + ' to the monitor') -Status ('{0}%  ({1} of {2})  {3}/s' -f $pct, (Mb $offset), (Mb $size), (Mb $rate)) -PercentComplete $pct
    }
} catch {
    $fs.Dispose()
    if ($tempZip) { Remove-Item -LiteralPath $tempZip -ErrorAction SilentlyContinue }
    try { [void](Invoke-Monitor ('?action=abort&upload_id=' + $id) (JsonBody @{})) } catch { }
    Write-Progress -Activity 'Uploading' -Completed
    Fail $_.Exception.Message
}
$fs.Dispose()
if ($tempZip) { Remove-Item -LiteralPath $tempZip -ErrorAction SilentlyContinue }
Write-Progress -Activity 'Uploading' -Completed
Write-Host ('Uploaded {0} in {1:N0} s. Verifying and publishing on the monitor...' -f (Mb $size), $clock.Elapsed.TotalSeconds)

# ---------- finish ----------
try { $fin = Invoke-Monitor ('?action=finish&upload_id=' + $id) (JsonBody @{}) } catch { Fail ('Could not finish: ' + $_.Exception.Message) }
if (-not $fin.Json.ok) { Fail ([string]$fin.Json.error) }
Write-Host ''
if ($fin.Json.replaced) { Write-Host ('DONE - v' + $version + ' was OVERWRITTEN on the monitor, with its release notes.') -ForegroundColor Green }
else                    { Write-Host ('DONE - v' + $version + ' was PUBLISHED on the monitor as a new release, with its release notes.') -ForegroundColor Green }
foreach ($w in @($fin.Json.warnings)) { if ($w) { Write-Host ('Note: ' + $w) -ForegroundColor Yellow } }
exit 0
