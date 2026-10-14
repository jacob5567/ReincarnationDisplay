#!/usr/bin/env bash
# Builds the plugin and packages it as a Thunderstore zip in dist/.
set -euo pipefail
cd "$(dirname "$0")"

version=$(python3 -c 'import json; print(json.load(open("thunderstore/manifest.json"))["version_number"])')

dotnet build -c Release

stage=dist/package
rm -rf "$stage"
mkdir -p "$stage/plugins/ReincarnationDisplay"
cp thunderstore/manifest.json thunderstore/icon.png README.md CHANGELOG.md "$stage/"
cp bin/Release/netstandard2.1/ReincarnationDisplay.dll "$stage/plugins/ReincarnationDisplay/"

zip_path="dist/ReincarnationDisplay-$version.zip"
rm -f "$zip_path"
(cd "$stage" && zip -qr "../$(basename "$zip_path")" .)
echo "Packaged $zip_path"
