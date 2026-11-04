using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;

namespace GtPlus.Services;

/// <summary>
/// the modifier the editor's shortcuts and drag gestures use: Cmd on macOS, Ctrl everywhere else.
/// Option already arrives as <see cref="KeyModifiers.Alt"/> on macOS, so Alt needs no mapping, only its label.
/// </summary>
public static partial class PlatformKeys
{
    public static readonly bool IsMac = OperatingSystem.IsMacOS();

    /// <summary>Cmd on macOS (Ctrl+click is a right-click there), Ctrl elsewhere</summary>
    public static readonly KeyModifiers Command = IsMac ? KeyModifiers.Meta : KeyModifiers.Control;

    /// <summary>true when the platform's command modifier is held</summary>
    public static bool HasCommand(KeyModifiers modifiers) => modifiers.HasFlag(Command);

    /// <summary>
    /// rewrites a Windows-style shortcut label for the current platform; on macOS "Ctrl+Shift+N" becomes "⇧⌘N",
    /// "Ctrl+drag" becomes "Cmd+drag" and a bare "Alt" becomes "Option". Elsewhere the text is returned unchanged.
    /// </summary>
    public static string Label(string text)
    {
        if (!IsMac || string.IsNullOrEmpty(text)) return text;

        text = ComboRegex().Replace(text, m =>
        {
            var mods = m.Groups["mods"].Value;
            var key  = m.Groups["key"].Value;
            bool ctrl = mods.Contains("Ctrl"), alt = mods.Contains("Alt"), shift = mods.Contains("Shift");

            // a gesture word reads better spelled out than as a glyph run
            if (key.Length > 1 && char.IsLower(key[0]))
            {
                var words = new List<string>();
                if (alt)   words.Add("Option");
                if (shift) words.Add("Shift");
                if (ctrl)  words.Add("Cmd");
                return string.Join("+", words) + "+" + key;
            }

            // Apple's order is Option, Shift, Command, then the key
            var sb = new StringBuilder();
            if (alt)   sb.Append('⌥');
            if (shift) sb.Append('⇧');
            if (ctrl)  sb.Append('⌘');
            return sb.Append(key).ToString();
        });

        text = BareCtrlRegex().Replace(text, "Cmd");
        text = BareAltRegex().Replace(text, "Option");
        return text;
    }

    /// <summary>relabels every string tooltip under <paramref name="root"/> for the current platform</summary>
    public static void LocalizeToolTips(ILogical root)
    {
        if (!IsMac) return;
        foreach (var node in root.GetSelfAndLogicalDescendants())
            if (node is Control c && ToolTip.GetTip(c) is string tip)
            {
                var mac = Label(tip);
                if (!ReferenceEquals(mac, tip) && mac != tip) ToolTip.SetTip(c, mac);
            }
    }

    [GeneratedRegex(@"(?<mods>(?:(?:Ctrl|Alt|Shift)\+)+)(?<key>[A-Za-z]+|[^\s\)]+)")]
    private static partial Regex ComboRegex();

    [GeneratedRegex(@"\bCtrl\b")]
    private static partial Regex BareCtrlRegex();

    [GeneratedRegex(@"\bAlt\b")]
    private static partial Regex BareAltRegex();
}
