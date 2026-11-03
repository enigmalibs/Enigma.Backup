@echo off
rem Builds the Windows installer of Enigma Backup, artifacts\Enigma.Backup.X.Y.Z.msi, from a cmd prompt:
rem it runs buildmsi.ps1, beside this script, with no PowerShell execution policy in the way.
rem
rem buildmsi.ps1 cleans the repository (git clean -dxf: every file git does not track is deleted),
rem publishes the app for win-x64 with publish.ps1, and builds the MSI with Enigma.Msi. Its help says
rem the rest: powershell -NoProfile -Command "Get-Help .\packaging\windows\buildmsi.ps1 -Full"

setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0buildmsi.ps1"

rem buildmsi.ps1's exit code is the script's, so a failed build fails the script.
exit /b %ERRORLEVEL%
