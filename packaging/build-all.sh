#!/bin/bash
# Builds Optimal GT Designer for every platform from any one machine (macOS, Linux or Windows with bash) into dist/:
#   macOS    Optimal-GT-Designer-<v>-mac-arm64.zip / -mac-x64.zip     the .app bundle (ad-hoc signed when built on a Mac)
#   Windows  Optimal-GT-Designer-<v>-windows-x64.zip / -arm64.zip     "Optimal GT Designer.exe", self-contained
#   Linux    Optimal-GT-Designer-<v>-linux-x64.tar.gz / -arm64.tar.gz  binary + icon + menu entry + install.sh
#
#   packaging/build-all.sh [version] [platforms...]     platforms: mac windows linux (default: all three)
# Every build is self-contained (no .NET needed on the target) and a single executable.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/.." && pwd)"
version="${1:-$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$here/macos/Info.plist" 2>/dev/null || echo 0.0.0)}"
shift || true
platforms=("$@"); [ ${#platforms[@]} -eq 0 ] && platforms=(mac windows linux)
dist="$root/dist"; mkdir -p "$dist"

publish() {   # rid outdir
  dotnet publish "$root/src/GtPlus/GtPlus.csproj" -c Release -r "$1" --self-contained \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
    -p:DebugType=none -p:DebugSymbols=false -p:Version="$version" -o "$2" -nologo -v quiet
  find "$2" -name '*.pdb' -delete   # debug symbols that ship inside the SkiaSharp/HarfBuzz packages
}

for p in "${platforms[@]}"; do
  case "$p" in
    mac)
      if [ "$(uname)" != "Darwin" ]; then echo "skipping mac: the .app is signed with codesign, which needs macOS"; continue; fi
      for rid in osx-arm64 osx-x64; do
        "$here/macos/build-mac.sh" "$version" "$rid"
        mv "$dist/macos/"Optimal-GT-Designer-"$version"-mac-*.zip "$dist/"
      done ;;
    windows)
      for arch in x64 arm64; do
        work="$dist/work/win-$arch/Optimal GT Designer"; rm -rf "$dist/work/win-$arch"; mkdir -p "$work"
        publish "win-$arch" "$work"
        mv "$work/GtPlus.exe" "$work/Optimal GT Designer.exe"
        rm -f "$dist/Optimal-GT-Designer-$version-windows-$arch.zip"
        (cd "$dist/work/win-$arch" && zip -qr -X "$dist/Optimal-GT-Designer-$version-windows-$arch.zip" "Optimal GT Designer")
        echo "Built windows-$arch"
      done ;;
    linux)
      for arch in x64 arm64; do
        name="optimal-gt-designer-$version-linux-$arch"
        work="$dist/work/$name"; rm -rf "$work"; mkdir -p "$work"
        publish "linux-$arch" "$work"
        mv "$work/GtPlus" "$work/optimal-gt-designer"; chmod 755 "$work/optimal-gt-designer"
        cp "$here/linux/optimal-gt-designer.desktop" "$here/linux/optimal-gt-designer.png" "$here/linux/install.sh" "$work/"
        chmod 755 "$work/install.sh"
        tar -C "$dist/work" -czf "$dist/Optimal-GT-Designer-$version-linux-$arch.tar.gz" "$name"
        echo "Built linux-$arch"
      done ;;
    *) echo "unknown platform '$p' (use mac, windows, linux)"; exit 1 ;;
  esac
done
rm -rf "$dist/work"
echo; ls -lh "$dist"/Optimal-GT-Designer-"$version"-* 2>/dev/null
