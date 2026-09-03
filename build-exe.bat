@echo off
setlocal enabledelayedexpansion

rem ============================================================
rem  OpenTypingPlus installer build script (<260830_2>)
rem  - Produces a portable "location-selectable SFX" installer
rem    exe via Inno Setup: build-exe\OpenTypingPlus_Setup.exe
rem  - This is for producing a distributable installer only.
rem    Routine dev/verification builds should keep using
rem    build.bat, not this script.
rem  - Requires Inno Setup 6 (ISCC.exe) to be installed.
rem    https://jrsoftware.org/isinfo.php
rem ============================================================

set "ROOT=%~dp0"
set "CSPROJ=%ROOT%OpenTyping\OpenTyping.csproj"
set "INSTALLER_DIR=%ROOT%installer"
set "ISS_FILE=%INSTALLER_DIR%\installer.iss"
set "CACHE_DIR=%ROOT%OpenTyping\obj\installer-cache"
set "DOTNET_INSTALLER_NAME=windowsdesktop-runtime-10.0.11-win-x64.exe"
set "DOTNET_INSTALLER_URL=https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.11/windowsdesktop-runtime-10.0.11-win-x64.exe"
set "DOTNET_INSTALLER_CACHE=%CACHE_DIR%\%DOTNET_INSTALLER_NAME%"

echo(
echo === build-exe: installer build start ===
echo(

rem --- Step 0: delete any previous installer output first, so that if THIS run
rem     fails partway, no stale OpenTypingPlus_Setup.exe is left around to be
rem     run by mistake (a stale installer being run happened for real on 2026-08-31).
if exist "%ROOT%build-exe\OpenTypingPlus_Setup.exe" del /f /q "%ROOT%build-exe\OpenTypingPlus_Setup.exe"

rem --- Step 1: produce the app exe + data folders via build.bat ---
call "%ROOT%build.bat"
if errorlevel 1 (
    echo [FAILED] build.bat failed; aborting installer build.
    exit /b 1
)

rem --- Step 1.5: freshness guard - the app exe must have been rebuilt BY THIS RUN.
rem     Verifies build\ contains exactly one exe and it was written in the last
rem     10 minutes. Catches stale leftovers and wrongly-named duplicates alike.
powershell -NoProfile -Command ^
    "$e=Get-ChildItem -Path '%ROOT%build' -Filter *.exe; if($e.Count -ne 1){Write-Host ('[GUARD] unexpected exe count in build folder: '+$e.Count); exit 1}; $age=((Get-Date)-$e[0].LastWriteTime).TotalMinutes; if($age -gt 10){Write-Host ('[GUARD] stale app exe (age '+[int]$age+' min): '+$e[0].Name); exit 1}; exit 0"
if errorlevel 1 (
    echo [FAILED] Freshness guard: build\ does not contain a freshly built app exe.
    exit /b 1
)

rem --- Step 2: make sure the .NET 10 Desktop Runtime installer is cached ---
rem  Bundled offline into the SFX so the target PC needs no internet access
rem  at install time (only this build machine needs internet, right now).
if not exist "%CACHE_DIR%" mkdir "%CACHE_DIR%"
if exist "%DOTNET_INSTALLER_CACHE%" (
    echo [OK] .NET runtime installer already cached: "%DOTNET_INSTALLER_CACHE%"
) else (
    echo Downloading .NET 10 Desktop Runtime ^(x64^) installer ^(~57MB^)...
    powershell -NoProfile -Command ^
        "try { Invoke-WebRequest -Uri '%DOTNET_INSTALLER_URL%' -OutFile '%DOTNET_INSTALLER_CACHE%' -UseBasicParsing; exit 0 } catch { Write-Host $_.Exception.Message; exit 1 }"
    if errorlevel 1 (
        echo [FAILED] Could not download .NET runtime installer.
        if exist "%DOTNET_INSTALLER_CACHE%" del /f /q "%DOTNET_INSTALLER_CACHE%"
        exit /b 1
    )
    if not exist "%DOTNET_INSTALLER_CACHE%" (
        echo [FAILED] Download reported success but file is missing.
        exit /b 1
    )
    echo [OK] Downloaded and cached.
)

rem --- Step 3: read DisplayVersion from OpenTyping.csproj so the installer's
rem     AppVersion always matches the app, without manual syncing ---
set "APP_VERSION="
for /f "usebackq tokens=3 delims=<>" %%V in (`findstr /C:"<DisplayVersion>" "%CSPROJ%"`) do (
    if not defined APP_VERSION set "APP_VERSION=%%V"
)
if not defined APP_VERSION (
    echo [FAILED] Could not read DisplayVersion from "%CSPROJ%".
    exit /b 1
)
echo [OK] App version: %APP_VERSION%

rem --- Step 4: locate ISCC.exe (Inno Setup command-line compiler) ---
set "ISCC="
if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined ISCC (
    where ISCC.exe >nul 2>nul
    if not errorlevel 1 set "ISCC=ISCC.exe"
)
if not defined ISCC (
    echo [FAILED] ISCC.exe ^(Inno Setup 6^) not found.
    echo          Install Inno Setup 6: https://jrsoftware.org/isinfo.php
    exit /b 1
)
echo [OK] Using compiler: "%ISCC%"

rem --- Step 5: compile the installer ---
echo(
echo === Compiling installer ^(Inno Setup^) ===
"%ISCC%" "%ISS_FILE%" /DMyAppVersion=%APP_VERSION% /DDotNetInstallerPath="%DOTNET_INSTALLER_CACHE%" /Q
if errorlevel 1 (
    echo [FAILED] Inno Setup compilation failed.
    exit /b 1
)

if not exist "%ROOT%build-exe\OpenTypingPlus_Setup.exe" (
    echo [FAILED] Expected output not found: "%ROOT%build-exe\OpenTypingPlus_Setup.exe"
    exit /b 1
)

echo(
echo [OK] Installer build complete
echo      Output: "%ROOT%build-exe\OpenTypingPlus_Setup.exe"
echo(

endlocal
