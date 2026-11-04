using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GtPlus.Models;

namespace GtPlus.Views;

/// <summary>
/// bulk font replacement: lists every font the title's text objects use (family, weight and style, with how many objects
/// use each and whether it is installed) and lets each be swapped for another in one step. It only rewrites the
/// <c>FontFamily</c>, <c>FontWeight</c> and <c>FontStyle</c> every text object already has, so the file stays plain GT
/// </summary>
public sealed class ReplaceFontsWindow : Window
{
    /// <summary>one font in use and what it becomes; a null replacement part keeps what the object had</summary>
    public sealed record Replacement(FontKey From, string Family, FontWeight? Weight, FontStyle? Style);

    public readonly record struct FontKey(string Family, FontWeight Weight, FontStyle Style)
    {
        public override string ToString()
        {
            var self = Weight;
            var weight = GtFontWeights.All.FirstOrDefault(w => w.Weight == self).Name ?? ((int)self).ToString();
            return $"{Family}  ·  {weight}{(Style == FontStyle.Normal ? "" : "  ·  Italic")}";
        }
    }

    private sealed class Row
    {
        public required FontKey Key;
        public required AutoCompleteBox FamilyBox;
        public required ComboBox WeightBox;
        public required ComboBox StyleBox;
    }

    private readonly IReadOnlyList<GtTextBlock> _all;
    private readonly IReadOnlyList<GtTextBlock> _selected;
    private readonly List<string> _installed;
    private readonly HashSet<string> _installedSet;
    private readonly StackPanel _rows = new() { Spacing = 6 };
    private readonly List<Row> _rowControls = new();
    private readonly RadioButton _scopeAll;
    private readonly RadioButton _scopeSelected;
    private readonly TextBlock _summary = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#9ccdf5")) };

    /// <summary>the objects to change and the replacements, set when the user presses Replace</summary>
    public (IReadOnlyList<GtTextBlock> Targets, IReadOnlyList<Replacement> Replacements)? Result { get; private set; }

    public ReplaceFontsWindow() : this(Array.Empty<GtTextBlock>(), Array.Empty<GtTextBlock>()) { }

    public ReplaceFontsWindow(IReadOnlyList<GtTextBlock> all, IReadOnlyList<GtTextBlock> selected)
    {
        _all = all;
        _selected = selected;

        Title = "Replace Fonts";
        Width = 760; Height = 520; MinWidth = 640; MinHeight = 320;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#1a1a1a"));
        FontSize = 12;

        try
        {
            _installed = FontManager.Current.SystemFonts.Select(f => f.Name).Distinct(StringComparer.OrdinalIgnoreCase)
                                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch { _installed = new List<string>(); }
        _installedSet = new HashSet<string>(_installed, StringComparer.OrdinalIgnoreCase);

        _scopeAll      = new RadioButton { Content = "Every text object in the title", GroupName = "scope", IsChecked = true };
        _scopeSelected = new RadioButton { Content = $"Selected text objects only ({selected.Count})", GroupName = "scope",
                                           IsEnabled = selected.Count > 0 };
        _scopeAll.IsCheckedChanged      += (_, _) => Rebuild();
        _scopeSelected.IsCheckedChanged += (_, _) => Rebuild();

        var header = new TextBlock
        {
            Text = "Pick a new font for any font in use. Rows you leave as they are stay unchanged.",
            FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#cccccc")), TextWrapping = TextWrapping.Wrap,
        };

        var replace = new Button { Content = "Replace", IsDefault = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancel  = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        replace.Click += (_, _) => Commit();
        cancel.Click  += (_, _) => Close();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(replace);

        var bottom = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(buttons, Dock.Right);
        bottom.Children.Add(buttons);
        bottom.Children.Add(_summary);

        var scope = new StackPanel { Spacing = 2 };
        scope.Children.Add(_scopeAll);
        scope.Children.Add(_scopeSelected);

        var top = new StackPanel { Spacing = 10, Margin = new Thickness(0, 0, 0, 10) };
        top.Children.Add(header);
        top.Children.Add(scope);

        var root = new DockPanel { Margin = new Thickness(18) };
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(bottom);
        root.Children.Add(new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        Content = root;

        Rebuild();
    }

    private IReadOnlyList<GtTextBlock> Targets => _scopeSelected.IsChecked == true ? _selected : _all;

    private void Rebuild()
    {
        _rows.Children.Clear();
        _rowControls.Clear();

        var groups = Targets
            .GroupBy(t => new FontKey(t.FontFamily, t.FontWeight, t.FontStyle))
            .OrderBy(g => g.Key.Family, StringComparer.OrdinalIgnoreCase).ThenBy(g => (int)g.Key.Weight)
            .ToList();

        if (groups.Count == 0)
        {
            _rows.Children.Add(new TextBlock { Text = "There is no text in the title.", Foreground = Brushes.Gray, FontSize = 12 });
            _summary.Text = "";
            return;
        }

        foreach (var g in groups)
        {
            bool installed = _installedSet.Contains(g.Key.Family);
            var label = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            label.Children.Add(new TextBlock { Text = g.Key.ToString(), FontSize = 12, Foreground = Brushes.White,
                                               TextTrimming = TextTrimming.CharacterEllipsis });
            label.Children.Add(new TextBlock
            {
                Text = $"{g.Count()} object{(g.Count() == 1 ? "" : "s")}{(installed ? "" : "  ·  not installed")}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse(installed ? "#888888" : "#ff7b6b")),
            });

            var family = new AutoCompleteBox
            {
                ItemsSource = _installed, Text = g.Key.Family, Width = 200, FilterMode = AutoCompleteFilterMode.Contains,
                MinimumPrefixLength = 0, MinimumPopulateDelay = TimeSpan.Zero, MaxDropDownHeight = 300,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var weight = new ComboBox { Width = 130, VerticalAlignment = VerticalAlignment.Center };
            weight.Items.Add("Keep weight");
            foreach (var w in GtFontWeights.All) weight.Items.Add($"{w.Name} ({(int)w.Weight})");
            weight.SelectedIndex = 0;
            var style = new ComboBox { Width = 110, VerticalAlignment = VerticalAlignment.Center };
            style.Items.Add("Keep style");
            style.Items.Add("Upright");
            style.Items.Add("Italic");
            style.SelectedIndex = 0;

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,24,Auto,6,Auto,6,Auto") };
            Grid.SetColumn(label, 0);
            var arrow = new TextBlock { Text = "→", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center,
                                        HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(arrow, 1);
            Grid.SetColumn(family, 2);
            Grid.SetColumn(weight, 4);
            Grid.SetColumn(style, 6);
            grid.Children.Add(label); grid.Children.Add(arrow); grid.Children.Add(family);
            grid.Children.Add(weight); grid.Children.Add(style);

            _rows.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse("#232323")), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 6), Child = grid,
            });
            _rowControls.Add(new Row { Key = g.Key, FamilyBox = family, WeightBox = weight, StyleBox = style });
        }

        int missing = groups.Count(g => !_installedSet.Contains(g.Key.Family));
        _summary.Text = $"{groups.Count} font{(groups.Count == 1 ? "" : "s")} in use" +
                        (missing > 0 ? $", {missing} not installed" : "");
    }

    /// <summary>an installed family matching what was typed (exact, then prefix, then substring), or the text as typed when nothing matches</summary>
    private string ResolveFamily(string? typed, string fallback)
    {
        typed = typed?.Trim();
        if (string.IsNullOrEmpty(typed)) return fallback;
        return _installed.FirstOrDefault(f => string.Equals(f, typed, StringComparison.OrdinalIgnoreCase))
            ?? _installed.FirstOrDefault(f => f.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            ?? _installed.FirstOrDefault(f => f.Contains(typed, StringComparison.OrdinalIgnoreCase))
            ?? typed;
    }

    private void Commit()
    {
        var list = new List<Replacement>();
        foreach (var row in _rowControls)
        {
            var family = ResolveFamily(row.FamilyBox.Text, row.Key.Family);
            FontWeight? weight = row.WeightBox.SelectedIndex > 0 ? GtFontWeights.All[row.WeightBox.SelectedIndex - 1].Weight : null;
            FontStyle? style = row.StyleBox.SelectedIndex switch { 1 => FontStyle.Normal, 2 => FontStyle.Italic, _ => null };

            bool changes = !string.Equals(family, row.Key.Family, StringComparison.Ordinal)
                        || (weight is { } w && w != row.Key.Weight)
                        || (style is { } s && s != row.Key.Style);
            if (changes) list.Add(new Replacement(row.Key, family, weight, style));
        }
        Result = (Targets, list);
        Close();
    }

    /// <summary>applies the replacements to the targets and returns each changed object's before/after, for undo</summary>
    public static List<(GtTextBlock Text, FontKey Before, FontKey After)> Apply(
        IReadOnlyList<GtTextBlock> targets, IReadOnlyList<Replacement> replacements)
    {
        var changed = new List<(GtTextBlock, FontKey, FontKey)>();
        foreach (var t in targets)
        {
            var key = new FontKey(t.FontFamily, t.FontWeight, t.FontStyle);
            var r = replacements.FirstOrDefault(x => x.From == key);
            if (r is null) continue;
            var after = new FontKey(r.Family, r.Weight ?? key.Weight, r.Style ?? key.Style);
            if (after == key) continue;
            Set(t, after);
            changed.Add((t, key, after));
        }
        return changed;
    }

    public static void Set(GtTextBlock t, FontKey k)
    {
        t.FontFamily = k.Family;
        t.FontWeight = k.Weight;
        t.FontStyle  = k.Style;
    }
}
