#!/bin/sh
# Installs Optimal GT Designer for the current user: the program into ~/.local/bin, the menu entry and the icon into
# ~/.local/share. Run it from the unpacked folder:  ./install.sh      Remove with:  ./install.sh --uninstall
set -e
here="$(cd "$(dirname "$0")" && pwd)"
bin="$HOME/.local/bin"; apps="$HOME/.local/share/applications"; icons="$HOME/.local/share/icons/hicolor/512x512/apps"
if [ "${1:-}" = "--uninstall" ]; then
  rm -f "$bin/optimal-gt-designer" "$apps/optimal-gt-designer.desktop" "$icons/optimal-gt-designer.png"
  echo "Optimal GT Designer removed."; exit 0
fi
mkdir -p "$bin" "$apps" "$icons"
install -m 755 "$here/optimal-gt-designer" "$bin/optimal-gt-designer"
install -m 644 "$here/optimal-gt-designer.png" "$icons/optimal-gt-designer.png"
sed "s|^Exec=optimal-gt-designer|Exec=$bin/optimal-gt-designer|" "$here/optimal-gt-designer.desktop" > "$apps/optimal-gt-designer.desktop"
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$apps" >/dev/null 2>&1 || true
echo "Installed. Optimal GT Designer is in your applications menu, or run: $bin/optimal-gt-designer"
