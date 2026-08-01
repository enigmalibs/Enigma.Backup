#!/usr/bin/env bash
#
# Publishes Enigma.Backup as a self-contained, single-file desktop app and zips the result.
#
#   ./publish.sh                    # both runtimes: linux-x64 and win-x64
#   ./publish.sh linux-x64          # one of them
#   ./publish.sh linux-x64 win-x64  # both, spelled out
#
# Produces artifacts/Enigma.Backup-<version>-<rid>.zip from the staging directory
# artifacts/publish/<rid>. Both paths are gitignored.
#
# The version is read from <Version> in Directory.Build.props rather than passed in, so the
# zip name can never disagree with the assembly version inside it.
#
# Self-contained means the target machine needs no .NET runtime installed; single-file means
# the zip holds one executable. Cross-publishing works in both directions (a win-x64 zip built
# here is a normal Windows build), but see publish.ps1 for the one caveat that runs the other
# way: Compress-Archive drops the Unix executable bit, so build the linux-x64 zip from Linux.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project="$root/src/Enigma.Backup.Desktop/Enigma.Backup.Desktop.csproj"
artifacts="$root/artifacts"

if [ "$#" -eq 0 ]; then
    rids=(linux-x64 win-x64)
else
    rids=("$@")
fi

for rid in "${rids[@]}"; do
    case "$rid" in
        linux-x64 | win-x64) ;;
        *)
            echo "publish: unsupported runtime '$rid' (expected linux-x64 or win-x64)" >&2
            exit 1
            ;;
    esac
done

version="$(dotnet msbuild "$project" -getProperty:Version -nologo | tr -d '[:space:]')"
if [ -z "$version" ]; then
    echo "publish: could not read the Version property from $project" >&2
    exit 1
fi

if ! command -v zip > /dev/null 2>&1; then
    echo "publish: 'zip' is required but was not found on PATH" >&2
    exit 1
fi

echo "Enigma.Backup $version"

for rid in "${rids[@]}"; do
    stage="$artifacts/publish/$rid"
    archive="$artifacts/Enigma.Backup-$version-$rid.zip"

    echo
    echo "==> publishing $rid"
    rm -rf "$stage"
    dotnet publish "$project" \
        --configuration Release \
        --runtime "$rid" \
        --self-contained true \
        --output "$stage" \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:EnableCompressionInSingleFile=true \
        -p:DebugType=none

    echo "==> zipping $(basename "$archive")"
    rm -f "$archive"
    (cd "$stage" && zip --quiet --recurse-paths "$archive" .)

    printf '    %s (%s)\n' "$archive" "$(du -h "$archive" | cut -f1)"
done
