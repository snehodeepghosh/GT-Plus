# Debugging

When something looks wrong on one machine and not another, the app can collect what's needed to work it out.

## Diagnostic report

**File › Debug › Save Diagnostic Report…** saves one zip. Nothing is sent anywhere; you choose where it goes. It holds:

| File | What's in it |
|---|---|
| `report.txt` | App version; OS, machine model, chip, memory; the graphics backend in use and every screen's size and scale; the open title's layers and objects; a decode check of every picture in the title |
| `optimal-gt-designer.log` | This session's log |
| `optimal-gt-designer.previous.log` | The last session's log, for a session that crashed |
| `canvas-reference.png` | The open title drawn by the CPU renderer |

If `canvas-reference.png` looks right but the screen doesn't, the graphics backend is at fault. On macOS, start the app
with a different one to compare (see [macOS › Graphics](macos.md#graphics)).

A picture marked `FAILED` in `report.txt` can't be decoded, so the canvas leaves it out; its error is listed beside it.

## The log

**File › Debug › Show Log File** shows it in Finder or Explorer. Each launch starts a new log and keeps the last one.

| System | Folder |
|---|---|
| macOS | `~/Library/Logs/Optimal GT Designer` |
| Windows | `%LOCALAPPDATA%\Optimal GT Designer\Logs` |
| Linux | `~/.local/state/optimal-gt-designer` |

It records the app and system versions, the graphics backend, every title opened or imported, pictures that couldn't be
drawn, and any error with its full stack trace.

**File › Debug › Verbose Logging** adds detailed steps, such as every file read, until the app closes. To have it on
from launch, start the app with `OPTIMAL_GT_DEBUG=1`.

Before 0.1.62 the log was `vmix-gt-plus.log` next to the app's executable, overwritten on every launch.
