# macOS

## Shortcuts

Every shortcut that uses **Ctrl** on Windows uses **Cmd** (⌘) on a Mac, and **Alt** is the **Option** (⌥) key.
Tooltips show the Mac symbols, for example ⇧⌘N rather than Ctrl+Shift+N.

| Action | Mac | Windows |
| --- | --- | --- |
| Undo | ⌘Z | Ctrl+Z |
| Redo | ⇧⌘Z or ⌘Y | Ctrl+Y |
| Save / Save As | ⌘S / ⇧⌘S | Ctrl+S / Ctrl+Shift+S |
| Copy / Paste / Duplicate | ⌘C / ⌘V / ⌘D | Ctrl+C / Ctrl+V / Ctrl+D |
| Rulers | ⌘R | Ctrl+R |
| Show guides | ⌘; | Ctrl+; |
| Snap | ⇧⌘; | Ctrl+Shift+; |
| Lock guides | ⌥⌘; | Ctrl+Alt+; |
| Selection box | ⌘-drag | Ctrl-drag |
| Zoom | ⌘-scroll | Ctrl-scroll |

Single-key tool shortcuts (S Select, E Edit, T Text, R Rectangle, K Ticker, W Web Page, I Image) are the same on
every platform.

## App name and icon

On macOS the app shows as **Optimal GT Designer**, in the menu bar, window titles and Dock, with the Optimal Studios
icon. The bundle identifier is `in.optimalstudios.gtdesigner`.

## Building the app

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Xcode's command-line tools (for
`codesign`). Then run, from the repository root:

```bash
packaging/macos/build-mac.sh 0.1.30
```

This builds a self-contained Apple Silicon (`osx-arm64`) app into `dist/macos/Optimal GT Designer.app`, plus a zip
next to it. Leave out the version to use the one in `packaging/macos/Info.plist`.

The app is ad-hoc signed and not notarized. The first time you open it on another Mac, right-click it and choose
**Open**.

The files the script uses are in `packaging/macos/`: `Info.plist`, `AppIcon.icns` and `build-mac.sh`.
