@echo off
setlocal enabledelayedexpansion

rem ============================================================
rem  OpenTypingPlus build script (.NET 10)
rem  - Output file: build\<Korean app name>.exe (single file)
rem  - Requires .NET SDK 10+ to build.
rem  - The produced exe needs the .NET 10 Desktop Runtime (x64)
rem    installed on the target machine.
rem  - IMPORTANT (2026-08-31, reproduced 5/5): the ASSEMBLY name must
rem    NOT contain '+'. WPF builds internal resource pack URIs from
rem    the assembly name, and '+' is a reserved URI character - with
rem    /p:AssemblyName=<name with '+'> every XAML window whose resources go
rem    through that path failed with ArgumentNullException(partUri)
rem    in single-file Release builds. So we keep the csproj's default
rem    assembly name (see STAGED_* below; same as Debug builds, proven
rem    fine for months) and only RENAME THE FILE when copying it to
rem    build\ - the single-file bundle does not care about its file
rem    name, and Process name checks use the file name anyway.
rem ============================================================

set "PROJECT=%~dp0OpenTyping\OpenTyping.csproj"
set "CONFIG=Release"
rem  IMPORTANT (2026-08-31, reproduced): this file must stay PURE ASCII.
rem  cmd.exe reads a .bat using the CONSOLE code page, so Korean text in a
rem  variable works at chcp 65001 but is MANGLED at chcp 949 - which is what
rem  Explorer double-click uses on Korean Windows. When the exe names lived
rem  here as `set "TARGET_NAME=<korean>"`, the staging-exe existence check
rem  silently failed at 949 and no exe was ever copied into build\ (the user
rem  hit exactly this). Passing the names back out of PowerShell through a
rem  pipe does not help either - that pipe is decoded with the same code page.
rem  So cmd never learns the Korean names at all: the existence check and the
rem  copy+rename are done entirely inside PowerShell (UTF-16 internally,
rem  code-page independent), which builds both names from Unicode code points.
rem      STAGED = the csproj AssemblyName (what dotnet publish emits)
rem      TARGET = the shipped file name   (see the '+' note above)
rem  Both are defined in ONE place: the $staged / $target lines of the
rem  PowerShell block below. Change them there, nowhere else.
rem  CAUTION: do not name these variables OUTDIR, OUTPUTPATH, etc.
rem  MSBuild imports environment variables as properties, so an
rem  env var named OUTDIR silently overrides the build output dir.
set "BUILD_DIR=%~dp0build"
set "STAGE_DIR=%~dp0OpenTyping\obj\publish-single"

echo(
echo === Build start (%CONFIG%) ===
echo(

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] dotnet CLI not found.
    echo         Install the .NET SDK 10 or later.
    exit /b 1
)

rem --- Remove stale exes first (build\ and staging) so an old or wrongly-named
rem     exe can never survive this run and get packaged by build-exe.bat.
rem     (A stale exe being silently packaged happened for real on 2026-08-31.)
if exist "%BUILD_DIR%" del /f /q "%BUILD_DIR%\*.exe" >nul 2>nul
if exist "%STAGE_DIR%" del /f /q "%STAGE_DIR%\*.exe" >nul 2>nul

rem --- Publish as a framework-dependent single file ---
rem  IncludeNativeLibrariesForSelfExtract bundles native DLLs
rem  (e.g. SkiaSharp) into the exe as well.
rem  The publish output goes to a staging folder first: the SDK
rem  writes loose DLLs next to the bundled exe there, so only the
rem  self-contained single exe is copied into the build folder.
rem  (the doubled trailing backslash: MSBuild's arg parser turns
rem  \\" into a single backslash + closing quote)
dotnet publish "%PROJECT%" -c %CONFIG% -r win-x64 --self-contained false ^
    /p:PublishDir="%STAGE_DIR%\\" ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:DebugType=None ^
    /v:minimal /nologo

if errorlevel 1 (
    echo(
    echo [FAILED] Build error occurred.
    exit /b 1
)

rem  Verify the published exe exists, then copy it into build\ under the
rem  shipped (Korean) name. Done entirely in PowerShell so that no Korean
rem  text has to survive cmd's code page - see the ASCII note in the header.
rem  $staged / $target are the ONE place these two names are defined.
if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$ErrorActionPreference='Stop';" ^
    "$staged = -join([char]0xC5F4,[char]0xB9B0,[char]0xD0C0,[char]0xC790,[char]0xD50C,[char]0xB7EC,[char]0xC2A4);" ^
    "$target = -join([char]0xC5F4,[char]0xB9B0,[char]0xD0C0,[char]0xC790,'+');" ^
    "$src = Join-Path '%STAGE_DIR%' ($staged + '.exe');" ^
    "$dst = Join-Path '%BUILD_DIR%' ($target + '.exe');" ^
    "if (-not (Test-Path -LiteralPath $src)) { Write-Host ('[FAILED] Published exe not found in staging folder: ' + $src); exit 1 };" ^
    "try { Copy-Item -LiteralPath $src -Destination $dst -Force }" ^
    "catch { Write-Host ('[FAILED] Could not copy exe to build folder: ' + $_.Exception.Message); exit 1 };" ^
    "if (-not (Test-Path -LiteralPath $dst)) { Write-Host '[FAILED] Copy reported success but the exe is missing.'; exit 1 };" ^
    "exit 0"
if errorlevel 1 exit /b 1

rem --- Copy runtime data folders next to the exe ---
rem  The app looks for "layouts" (key layout data) and "data"
rem  (practice data) in the exe directory at startup.
rem  Source folders live at the repo root so they are easy to
rem  edit by hand. robocopy /MIR mirrors them into the build
rem  output on every build: files you added, modified, renamed
rem  or deleted are all reflected exactly.
rem  (robocopy exit codes 0-7 mean success, 8+ mean failure)
robocopy "%~dp0layouts" "%BUILD_DIR%\layouts" /MIR /NFL /NDL /NJH /NJS >nul
if errorlevel 8 (
    echo [FAILED] Could not copy layouts folder.
    exit /b 1
)
rem  basic.json is a local test file; keep it out of the build.
robocopy "%~dp0data" "%BUILD_DIR%\data" /MIR /XF basic.json /NFL /NDL /NJH /NJS >nul
if errorlevel 8 (
    echo [FAILED] Could not copy data folder.
    exit /b 1
)

rem  Prompt word list (used by both stage practice and the acid-rain game,
rem  <260927_2>). The app reads it from "wordslist\words.json" next to the exe.
rem  Source lives inside the project folder; copy it directly from source (not
rem  the publish staging) so incremental single-file publish can never leave
rem  it out and blank the deployed copy (<260723_2>).
robocopy "%~dp0OpenTyping\wordslist" "%BUILD_DIR%\wordslist" /MIR /NFL /NDL /NJH /NJS >nul
if errorlevel 8 (
    echo [FAILED] Could not copy wordslist folder.
    exit /b 1
)

rem  Practice-stage definitions. The app reads "stages\*.json" next to the exe
rem  (editable), with an embedded-resource fallback. Copy directly from source
rem  (same anti-trap rationale as above) (<260724_1>(3)).
robocopy "%~dp0OpenTyping\stages" "%BUILD_DIR%\stages" /MIR /NFL /NDL /NJH /NJS >nul
if errorlevel 8 (
    echo [FAILED] Could not copy stages folder.
    exit /b 1
)

rem  Finger-layer hand-shape vectors (SVG). The app reads "hands\*.svg" next to
rem  the exe (FingerLayer.TryLoadHandSvg); falls back to the built-in ContourHand
rem  synthesis if a file is missing. Copy directly from source (same anti-trap
rem  rationale as above) (<260811_26>(2-1)).
robocopy "%~dp0OpenTyping\hands" "%BUILD_DIR%\hands" /MIR /NFL /NDL /NJH /NJS >nul
if errorlevel 8 (
    echo [FAILED] Could not copy hands folder.
    exit /b 1
)

echo(
echo [OK] Build complete
echo      Output: the app exe in "%BUILD_DIR%"
echo      Data  : "%BUILD_DIR%\layouts", "%BUILD_DIR%\data", "%BUILD_DIR%\wordslist", "%BUILD_DIR%\stages", "%BUILD_DIR%\hands"
echo(

endlocal
rem  IMPORTANT: exit explicitly with 0. Without this the script falls off the end
rem  carrying the LAST robocopy's exit code, which is 1-7 on success (1 = files
rem  copied). build-exe.bat then reads that as failure via `if errorlevel 1` and
rem  aborts a perfectly good build. Only shows up on clean builds (when robocopy
rem  actually copies something), so it hides during ordinary rebuilds.
exit /b 0

