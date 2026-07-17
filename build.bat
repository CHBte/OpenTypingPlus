@echo off
setlocal enabledelayedexpansion

rem ============================================================
rem  OpenTypingPlus build script
rem  - Output file: build\OpenTypingPlus.exe
rem  - Leaves the .csproj untouched; overrides the output
rem    assembly name and output folder here only.
rem ============================================================

set "PROJECT=%~dp0OpenTyping\OpenTyping.csproj"
set "ASSEMBLY_NAME=OpenTypingPlus"
set "CONFIG=Release"
set "OUTDIR=%~dp0build"

echo(
echo === Build start (%CONFIG% / %ASSEMBLY_NAME%.exe) ===
echo(

rem --- Locate MSBuild via vswhere ---
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo [ERROR] vswhere.exe not found.
    echo         Make sure Visual Studio or Build Tools is installed.
    exit /b 1
)

set "MSBUILD="
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%i"

if not defined MSBUILD (
    echo [ERROR] MSBuild.exe not found.
    exit /b 1
)

echo MSBuild : "%MSBUILD%"
echo Project : "%PROJECT%"
echo(

rem --- Build (override output name and output folder) ---
rem  Note: the doubled trailing backslash in OutputPath is intentional.
rem  MSBuild's arg parser turns \\" into a single backslash + closing
rem  quote, so OutputPath ends with exactly one backslash.
"%MSBUILD%" "%PROJECT%" /p:Configuration=%CONFIG% /p:AssemblyName=%ASSEMBLY_NAME% /p:OutputPath="%OUTDIR%\\" /v:minimal /nologo

if errorlevel 1 (
    echo(
    echo [FAILED] Build error occurred.
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
robocopy "%~dp0layouts" "%OUTDIR%\layouts" /MIR /NFL /NDL /NJH /NJS >nul
if errorlevel 8 (
    echo [FAILED] Could not copy layouts folder.
    exit /b 1
)
rem  basic.json is a local test file; keep it out of the build.
robocopy "%~dp0data" "%OUTDIR%\data" /MIR /XF basic.json /NFL /NDL /NJH /NJS >nul
if errorlevel 8 (
    echo [FAILED] Could not copy data folder.
    exit /b 1
)

echo(
echo [OK] Build complete
echo      Output: "%OUTDIR%\%ASSEMBLY_NAME%.exe"
echo      Data  : "%OUTDIR%\layouts", "%OUTDIR%\data"
echo(

endlocal
