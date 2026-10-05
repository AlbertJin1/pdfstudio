@echo off
setlocal EnableExtensions
REM PDF Studio - build and publish a GitHub release that installed copies will pick up as an update.
REM Usage:   release.bat 2.7.0
REM Needs:   the GitHub CLI (gh), signed in once with "gh auth login".
REM Notes:   put the release notes in release-notes.md next to this file (optional; otherwise GitHub generates them).

set "REPO=YOUR-GITHUB-USERNAME/YOUR-REPO"
cd /d "%~dp0"

set "VER=%~1"
if "%VER%"=="" (
    echo Usage: release.bat 2.7.0
    exit /b 1
)
where gh >nul 2>nul
if errorlevel 1 (
    echo ERROR: the GitHub CLI ^(gh^) was not found. Install it from https://cli.github.com and run "gh auth login".
    exit /b 1
)

REM The tag must match the version inside the exe, or the app will think it is always out of date.
for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "([xml](Get-Content -Raw 'PdfStudioCS.csproj')).Project.PropertyGroup.Version | Where-Object { $_ }"`) do set "CSPROJ_VER=%%v"
if not "%CSPROJ_VER%"=="%VER%" (
    echo ERROR: PdfStudioCS.csproj says version "%CSPROJ_VER%", not "%VER%".
    echo Update ^<Version^> and ^<FileVersion^> in the csproj, plus CHANGELOG.md and Services\ChangeLog.cs, then run again.
    exit /b 1
)

call "%~dp0build_publish.bat"
if errorlevel 1 exit /b 1

if exist "release-notes.md" ( set "NOTES=--notes-file release-notes.md" ) else ( set "NOTES=--generate-notes" )
gh release create "v%VER%" "publish\PdfStudio.exe" -R "%REPO%" --title "PDF Studio %VER%" %NOTES%
if errorlevel 1 (
    echo.
    echo Release upload failed.
    exit /b 1
)
echo.
echo Released v%VER%. Running copies will offer it the next time they start.
exit /b 0
