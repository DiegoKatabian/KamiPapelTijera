@echo off
rem Starts the demo build from Builds\SteamDemo for testing, WITHOUT Steam relaunching it.
rem Why: without a steam_appid.txt next to the exe, SteamAPI.RestartAppIfNecessary hands the launch to Steam,
rem which starts the version installed in your library (an old one) instead of this build.
rem This copies steam_appid.txt in for the test; upload-demo.bat deletes it again before uploading.
rem Steam must be running and signed in to an account that owns the demo.
setlocal
set "BUILD=%~dp0..\..\Builds\SteamDemo"
if not exist "%BUILD%\KamiPapelTijera.exe" (
    echo No build found. In Unity run: Kami ^> Build ^> Steam Demo ^(Level 1^)
    exit /b 1
)
copy /y "%~dp0..\..\steam_appid.txt" "%BUILD%\steam_appid.txt" >nul
start "" "%BUILD%\KamiPapelTijera.exe"
