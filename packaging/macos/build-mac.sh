#!/bin/bash
# Builds "Optimal GT Designer.app" into dist/macos and zips it.
#   packaging/macos/build-mac.sh [version] [rid]   version e.g. 0.1.30 (default: Info.plist), rid osx-arm64 (default) or osx-x64 (Intel)
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
version="${1:-$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$here/Info.plist")}"
rid="${2:-osx-arm64}"
arch="$([ "$rid" = "osx-x64" ] && echo x64 || echo arm64)"
out="$root/dist/macos"
app="$out/Optimal GT Designer.app"

rm -rf "$app" "$out/publish"; mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
dotnet publish "$root/src/GtPlus/GtPlus.csproj" -c Release -r "$rid" --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none -p:DebugSymbols=false -p:Version="$version" -o "$out/publish"

cp "$out/publish/GtPlus" "$app/Contents/MacOS/GtPlus"
cp "$here/Info.plist" "$app/Contents/Info.plist"
cp "$here/AppIcon.icns" "$app/Contents/Resources/AppIcon.icns"
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $version" -c "Set :CFBundleVersion $version" "$app/Contents/Info.plist"
codesign --force --deep --sign - "$app"   # ad-hoc signature; not notarized
(cd "$out" && ditto -c -k --keepParent "Optimal GT Designer.app" "Optimal-GT-Designer-$version-mac-$arch.zip")
rm -rf "$out/publish"
echo "Built $app"
