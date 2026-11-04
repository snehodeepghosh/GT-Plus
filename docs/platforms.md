# Platforms

Optimal GT Designer runs on macOS, Windows and Linux. `packaging/build-all.sh` builds all of them from one machine;
every package is self-contained, so the target machine needs nothing else installed.

## macOS

- `Optimal-GT-Designer-<v>-mac-arm64.zip` is for Apple Silicon Macs, `-mac-x64.zip` for Intel Macs.
- Unzip it and drag **Optimal GT Designer.app** into Applications.
- The app is ad-hoc signed, not notarized. The first time, right-click it and choose **Open**.
- Shortcuts use Cmd and Option; see [macOS](macos.md).

## Windows

- `Optimal-GT-Designer-<v>-windows-x64.zip` is for ordinary 64-bit PCs, `-arm64.zip` for Windows on ARM.
- Unzip it anywhere and run **Optimal GT Designer.exe**. There's no installer.
- Windows SmartScreen may warn about an unrecognised app the first time; choose **More info › Run anyway**.
- The window and taskbar show the app icon. The `.exe` file's own icon in Explorer is only embedded when the app is
  built on Windows (a .NET limitation). A build made on a Mac or Linux shows a generic file icon there, and the
  window and taskbar icon are unaffected.
- Shortcuts use Ctrl and Alt.

## Linux

- `Optimal-GT-Designer-<v>-linux-x64.tar.gz` is for 64-bit PCs, `-arm64.tar.gz` for ARM machines.
- Unpack it and run `./optimal-gt-designer`, or run `./install.sh` to install it for your user. That puts the program
  in `~/.local/bin` and an **Optimal GT Designer** entry with its icon in your applications menu.
  `./install.sh --uninstall` removes it again.
- It needs an X11 or XWayland desktop and the usual graphics libraries (fontconfig, libX11, libICE/libSM), which
  desktop distributions already have.
- Shortcuts use Ctrl and Alt.

## Fonts on every platform

Titles store font names, so the fonts a title uses must be installed on the machine that edits it, as they must on the
vMix machine that plays it. Font weights are matched by each face's true weight from the font file on every platform;
see [Fonts](fonts.md).

## Testing status

The Apple Silicon Mac build is the one used day to day. The Windows, Linux and Intel Mac packages are built and checked
to be the right kind of executable for their platform, but haven't yet been launched on those machines.
