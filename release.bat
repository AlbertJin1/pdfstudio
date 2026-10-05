@echo off
setlocal EnableExtensions
REM PDF Studio - build the release exe and get it ready to upload to the monitor's "Releases" page.
REM Usage:   release.bat
REM Then:    open the monitor (see MONITOR below), go to Releases, and upload publish\PdfStudio.exe.
REM          The monitor reads the version out of the exe itself; installed copies offer the update on their next start.

set "MONITOR=https://paternal-carefully-glucose.ngrok-free.dev/pdfstudio-monitor/releases.php"
cd /d "%~dp0"

call "%~dp0build_publish.bat"
if errorlevel 1 exit /b 1

echo.
powershell -NoProfile -Command "$e=(Get-Item 'publish\PdfStudio.exe').VersionInfo; $p=([xml](Get-Content -Raw 'PdfStudioCS.csproj')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1; Write-Host ('Version inside the exe : ' + $e.FileVersion + '   (this is what the monitor will read)'); Write-Host ('<Version> in csproj    : ' + $p); if (([version]$e.FileVersion).ToString(3) -ne ([version]$p).ToString(3)) { Write-Host 'WARNING: they differ. Set <Version> and <FileVersion> to the same number in PdfStudioCS.csproj and build again.' -ForegroundColor Yellow }"

echo.
echo Next: upload  %~dp0publish\PdfStudio.exe
echo       at      %MONITOR%
explorer /select,"%~dp0publish\PdfStudio.exe"
exit /b 0
