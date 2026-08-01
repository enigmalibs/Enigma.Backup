#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Publishes Enigma.Backup as a self-contained, single-file desktop app and zips the result.

.DESCRIPTION
    Produces artifacts/Enigma.Backup-<version>-<rid>.zip from the staging directory
    artifacts/publish/<rid>. Both paths are gitignored.

    The version is read from <Version> in Directory.Build.props rather than passed in, so the
    zip name can never disagree with the assembly version inside it.

    Self-contained means the target machine needs no .NET runtime installed; single-file means
    the zip holds one executable.

    Caveat when cross-publishing linux-x64 from Windows: Compress-Archive stores no Unix file
    mode, so the extracted binary loses its executable bit and needs a chmod +x. Build the
    linux-x64 zip from Linux with publish.sh, which uses zip(1) and preserves the mode.

.PARAMETER Rid
    The runtime identifiers to publish. Defaults to both.

.EXAMPLE
    ./publish.ps1
    ./publish.ps1 -Rid win-x64
#>
[CmdletBinding()]
param(
    [ValidateSet('linux-x64', 'win-x64')]
    [string[]] $Rid = @('linux-x64', 'win-x64')
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'src/Enigma.Backup.Desktop/Enigma.Backup.Desktop.csproj'
$artifacts = Join-Path $root 'artifacts'

$version = (dotnet msbuild $project -getProperty:Version -nologo).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($version)) {
    throw "publish: could not read the Version property from $project"
}

Write-Host "Enigma.Backup $version"

foreach ($runtime in $Rid) {
    $stage = Join-Path $artifacts "publish/$runtime"
    $archive = Join-Path $artifacts "Enigma.Backup-$version-$runtime.zip"

    Write-Host ''
    Write-Host "==> publishing $runtime"
    if (Test-Path -Path $stage) { Remove-Item -Path $stage -Recurse -Force }
    dotnet publish $project `
        --configuration Release `
        --runtime $runtime `
        --self-contained true `
        --output $stage `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none
    if ($LASTEXITCODE -ne 0) {
        throw "publish: dotnet publish failed for $runtime"
    }

    Write-Host "==> zipping $(Split-Path -Path $archive -Leaf)"
    if (Test-Path -Path $archive) { Remove-Item -Path $archive -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -CompressionLevel Optimal

    $size = '{0:N1} MiB' -f ((Get-Item -Path $archive).Length / 1MB)
    Write-Host "    $archive ($size)"
}
