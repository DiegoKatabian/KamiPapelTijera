@echo off
rem Uploads Builds\SteamDemo to the Level 1 demo app (3284350) through SteamPipe.
rem   upload-demo.bat preview   -> writes the manifest and logs only, uploads nothing (run this first)
rem   upload-demo.bat           -> the real upload
rem Steam asks for your password and Steam Guard code itself, in this window.
setlocal
set "SDK=C:\Users\Diego\OneDrive\Documents\GitHub\steamworks sdk"
set "HERE=%~dp0"

if not exist "%HERE%..\..\Builds\SteamDemo\KamiPapelTijera.exe" (
    echo No build found. In Unity run: Kami ^> Build ^> Steam Demo ^(Level 1^)
    exit /b 1
)
if not exist "%SDK%\tools\ContentBuilder\builder\steamcmd.exe" (
    echo steamcmd.exe not found under "%SDK%"
    exit /b 1
)

rem run-local.bat leaves a steam_appid.txt in the build: it must never be uploaded
if exist "%HERE%..\..\Builds\SteamDemo\steam_appid.txt" del "%HERE%..\..\Builds\SteamDemo\steam_appid.txt"

if /i "%~1"=="preview" (
    set "SCRIPT=%HERE%app_build_demo_preview.vdf"
) else (
    set "SCRIPT=%HERE%app_build_demo.vdf"
)

set /p STEAMUSER=Steam account name: 
"%SDK%\tools\ContentBuilder\builder\steamcmd.exe" +login %STEAMUSER% +run_app_build "%SCRIPT%" +quit
echo.
echo steamcmd finished. Logs: %HERE%..\..\Builds\SteamPipeOutput
pause
