@echo off
rem Publishes Enigma.Backup for Windows x64. Runs, from wherever it is started:
rem
rem     dotnet publish .\src\Enigma.Backup.Desktop\ -c Release -r win-x64 -p:PublishReadyToRun=true
rem
rem A Release build for win-x64, compiled ReadyToRun so it starts faster, which runs on an installed
rem .NET 10 runtime. The result lands in
rem
rem     src\Enigma.Backup.Desktop\bin\Release\net10.0\win-x64\publish\
rem
rem and dotnet's last line names that folder. Start Enigma.Backup.Desktop.exe in it; app.ico beside it
rem is the icon for a shortcut.
rem
rem The same publish as publish.ps1, for a cmd prompt: no PowerShell execution policy applies. For the
rem self-contained, single-file release zips, use publish.ps1 at the repository root (docs\RELEASE.md).

setlocal

rem Resolved from this script rather than the current directory, so it runs from anywhere.
set "project=%~dp0..\..\src\Enigma.Backup.Desktop"

dotnet publish "%project%" -c Release -r win-x64 -p:PublishReadyToRun=true

rem dotnet's exit code is the script's, so a failed publish fails the script.
exit /b %ERRORLEVEL%
