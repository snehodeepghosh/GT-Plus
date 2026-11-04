#!/bin/bash
# Builds "Optimal GT Designer.app" (Apple Silicon) into dist/macos and zips it.
#   packaging/macos/build-mac.sh [version]     e.g. 0.1.30; defaults to the version in Info.plist
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
version="${1:-$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$here/Info.plist")}"
out="$root/dist/macos"
app="$out/Optimal GT Designer.app"

rm -rf "$out"; mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
dotnet publish "$root/src/GtPlus/GtPlus.csproj" -c Release -r osx-arm64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none -p:DebugSymbols=false -p:Version="$version" -o "$out/publish"

cp "$out/publish/GtPlus" "$app/Contents/MacOS/GtPlus"
cp "$here/Info.plist" "$app/Contents/Info.plist"
cp "$here/AppIcon.icns" "$app/Contents/Resources/AppIcon.icns"
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $version" -c "Set :CFBundleVersion $version" "$app/Contents/Info.plist"
codesign --force --deep --sign - "$app"   # ad-hoc signature; not notarized
(cd "$out" && ditto -c -k --keepParent "Optimal GT Designer.app" "Optimal-GT-Designer-$version-arm64.zip")
rm -rf "$out/publish"
echo "Built $app"
