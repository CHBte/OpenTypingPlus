@echo off
setlocal enabledelayedexpansion

rem ============================================================
rem  OpenTypingPlus build script (.NET 10)
rem  - Output file: build\OpenTypingPlus.exe (single file)
rem  - Requires .NET SDK 10+ to build.
rem  - The produced exe needs the .NET 10 Desktop Runtime (x64)
rem    installed on the target machine.
rem  - Leaves the .csproj untouched; overrides the output
rem    assembly name and output folder here only.
rem ============================================================

set "PROJECT=%~dp0OpenTyping\OpenTyping.csproj"
set "ASSEMBLY_NAME=OpenTypingPlus"
set "CONFIG=Release"
rem  CAUTION: do not name these variables OUTDIR, OUTPUTPATH, etc.
rem  MSBuild imports environment variables as properties, so an
rem  env var named OUTDIR silently overrides the build output dir.
set "BUILD_DIR=%~dp0build"
set "STAGE_DIR=%~dp0OpenTyping\obj\publish-single"

echo(
echo === Build start (%CONFIG% / %ASSEMBLY_NAME%.exe) ===
echo(

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] dotnet CLI not found.
    echo         Install the .NET SDK 10 or later.
    exit /b 1
)

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
    /p:AssemblyName=%ASSEMBLY_NAME% ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:DebugType=None ^
    /v:minimal /nologo

if errorlevel 1 (
    echo(
    echo [FAILED] Build error occurred.
    exit /b 1
)

if not exist "%STAGE_DIR%\%ASSEMBLY_NAME%.exe" (
    echo [FAILED] Published exe not found in staging folder.
    exit /b 1
)

if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"
copy /y "%STAGE_DIR%\%ASSEMBLY_NAME%.exe" "%BUILD_DIR%\%ASSEMBLY_NAME%.exe" >nul
if errorlevel 1 (
    echo [FAILED] Could not copy exe to build folder.
    exit /b 1
)

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

echo(
echo [OK] Build complete
echo      Output: "%BUILD_DIR%\%ASSEMBLY_NAME%.exe"
echo      Data  : "%BUILD_DIR%\layouts", "%BUILD_DIR%\data"
echo(

endlocal

