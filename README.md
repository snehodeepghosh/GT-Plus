# GT Plus

Standalone cross-platform editor for vMix GT Title Designer templates (`.gtzip` files).
Extends the functionality of the built-in vMix GT Title Designer.

Built with [Avalonia UI](https://avaloniaui.net/) - runs on Windows, macOS, and Linux.

## Optimal GT Designer

This fork is packaged as **Optimal GT Designer** by Optimal Studios. The full guide is in [docs/](docs/README.md).
On top of GT Plus it adds:

- a ribbon interface laid out like vMix GT Title Designer, with a dark theme
- Photoshop import (File › Import PSD, or drop a `.psd`/`.psb` on the canvas): groups become layers, text stays
  editable, and Photoshop clipping masks become GT masks
- masks drawn the way GT's engine draws them: the mask object's own pixels cut the masked object, not its bounding box
- Photoshop-style clipping in the Layers panel (Option-click between rows, or right-click › Create Clipping Mask),
  and clipped objects that can only be clicked where they show
- bulk font replacement, and Photoshop-style faux italic saved as GT's own Skew effect
- a reference image overlay (the PSD's own preview after an import) with opacity and difference modes, never saved
- PSD text rotation and slant imported as GT rotation and Skew
- Skew, Reflection and Texture Flip effects
- a Figma-style timeline that can be detached onto another monitor, with the working area highlighted
- drag-to-move on the canvas, a grid or black canvas background, and Cmd/Option shortcuts on macOS

To build every platform from one machine (macOS, Linux, or Windows with bash) into `dist/`:

```bash
packaging/build-all.sh 0.1.42
```

| Package | Contents |
| --- | --- |
| `Optimal-GT-Designer-<v>-mac-arm64.zip` / `-mac-x64.zip` | `Optimal GT Designer.app` for Apple Silicon / Intel Macs |
| `Optimal-GT-Designer-<v>-windows-x64.zip` / `-arm64.zip` | `Optimal GT Designer.exe` |
| `Optimal-GT-Designer-<v>-linux-x64.tar.gz` / `-arm64.tar.gz` | `optimal-gt-designer`, icon, menu entry and `install.sh` |

Every build is self-contained: nothing else (no .NET) needs installing on the target machine. Pass platform names to
build only some, for example `packaging/build-all.sh 0.1.42 windows linux`. The Mac packages need a Mac (for
`codesign`); see [docs/platforms.md](docs/platforms.md) for each platform's notes.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or newer

---

## Running (dev)

```bash
cd src/GtPlus
dotnet run
```

Then use **File › Open** to load a `.gtzip` file.

---

## Building a release binary

Run the build script from the repository root:

```powershell
./build.ps1
```

### Options

| Command | Effect |
| --- | --- |
| `./build.ps1` | Bump patch, build all platforms |
| `./build.ps1 -Bump Minor` | Bump minor (patch resets to 0) |
| `./build.ps1 -Bump Major` | Bump major (minor and patch reset to 0) |
| `./build.ps1 -Bump None` | Rebuild the current version in place |
| `./build.ps1 -SetVersion 1.0.0` | Build an exact version |
| `./build.ps1 -Rid win-x64` | Build a subset of platforms |
| `./build.ps1 -NoArchive` | Skip the `.zip` / `.tar.gz` step |
| `./build.ps1 -NoBundleNative` | Leave Skia/HarfBuzz beside the executable |

Windows cannot set the Unix executable bit, so the Linux and macOS binaries need
`chmod +x GTPlus-<version>` after extraction.

### Manual publish

```bash
dotnet publish src/GtPlus -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist/win-x64
```

Substitute `linux-x64`, `osx-x64` or `osx-arm64` for other platforms.

---

## GTZIP format

See [GTZIP-Format.md](GTZIP-Format.md) for full format documentation.

---