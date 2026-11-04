using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace GtPlus.Controls;

/// <summary>
/// Ribbon chrome for the properties panel: GT's File / Home / Styles / Format / Effects / Animations / Data
/// tabs, plus slots the host window fills with its own command groups. The slots are plain object
/// properties so MainWindow can declare their content in its own AXAML, keeping the named controls
/// (undo, clipboard, guides, timeline...) in the window's name scope where its code-behind expects them.
/// </summary>
public partial class PropertiesPanelControl
{
    public static readonly StyledProperty<object?> FileContentProperty =
        AvaloniaProperty.Register<PropertiesPanelControl, object?>(nameof(FileContent));
    public static readonly StyledProperty<object?> QuickAccessContentProperty =
        AvaloniaProperty.Register<PropertiesPanelControl, object?>(nameof(QuickAccessContent));
    public static readonly StyledProperty<object?> HomeStartContentProperty =
        AvaloniaProperty.Register<PropertiesPanelControl, object?>(nameof(HomeStartContent));
    public static readonly StyledProperty<object?> HomeEndContentProperty =
        AvaloniaProperty.Register<PropertiesPanelControl, object?>(nameof(HomeEndContent));
    public static readonly StyledProperty<object?> AnimationsContentProperty =
        AvaloniaProperty.Register<PropertiesPanelControl, object?>(nameof(AnimationsContent));
    public static readonly StyledProperty<object?> DataContentProperty =
        AvaloniaProperty.Register<PropertiesPanelControl, object?>(nameof(DataContent));
    public static readonly StyledProperty<object?> ViewContentProperty =
        AvaloniaProperty.Register<PropertiesPanelControl, object?>(nameof(ViewContent));

    /// <summary>left of the tabs: the File menu</summary>
    public object? FileContent        { get => GetValue(FileContentProperty);        set => SetValue(FileContentProperty, value); }
    /// <summary>right end of the tab strip: quick undo / redo / help</summary>
    public object? QuickAccessContent { get => GetValue(QuickAccessContentProperty); set => SetValue(QuickAccessContentProperty, value); }
    /// <summary>Home tab, before Font: Undo and Clipboard groups</summary>
    public object? HomeStartContent   { get => GetValue(HomeStartContentProperty);   set => SetValue(HomeStartContentProperty, value); }
    /// <summary>Home tab, after Align: Arrange / layer groups</summary>
    public object? HomeEndContent     { get => GetValue(HomeEndContentProperty);     set => SetValue(HomeEndContentProperty, value); }
    public object? AnimationsContent  { get => GetValue(AnimationsContentProperty);  set => SetValue(AnimationsContentProperty, value); }
    /// <summary>Data tab, after Options</summary>
    public object? DataContent        { get => GetValue(DataContentProperty);        set => SetValue(DataContentProperty, value); }
    public object? ViewContent        { get => GetValue(ViewContentProperty);        set => SetValue(ViewContentProperty, value); }

    /// <summary>name of the ribbon tab showing, one of Home Styles Format Effects Animations Data View</summary>
    public string SelectedRibbonTab { get; private set; } = "Home";

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        ContentControl? host =
            change.Property == FileContentProperty        ? FileHost :
            change.Property == QuickAccessContentProperty ? QuickAccessHost :
            change.Property == HomeStartContentProperty   ? HomeStartHost :
            change.Property == HomeEndContentProperty     ? HomeEndHost :
            change.Property == AnimationsContentProperty  ? AnimationsHost :
            change.Property == DataContentProperty        ? DataHost :
            change.Property == ViewContentProperty        ? ViewHost : null;

        if (host is not null)
            host.Content = change.NewValue;
    }

    /// <summary>shows one tab's groups; also expands a collapsed ribbon, as clicking a tab does in GT</summary>
    public void SelectRibbonTab(string name)
    {
        SelectedRibbonTab = name;

        foreach (var child in TabStrip.Children)
            if (child is ToggleButton tb)
                tb.IsChecked = (string?)tb.Tag == name;

        HomePage.IsVisible       = name == "Home";
        StylesPage.IsVisible     = name == "Styles";
        FormatPage.IsVisible     = name == "Format";
        EffectsPage.IsVisible    = name == "Effects";
        AnimationsPage.IsVisible = name == "Animations";
        DataPage.IsVisible       = name == "Data";
        ViewPage.IsVisible       = name == "View";

        SetRibbonCollapsed(false);
    }

    public bool RibbonCollapsed => !RibbonBody.IsVisible;

    public void SetRibbonCollapsed(bool collapsed)
    {
        RibbonBody.IsVisible = !collapsed;
        // chevron points up while open ("collapse"), down while collapsed ("expand")
        RibbonCollapseGlyph.Data = Avalonia.Media.Geometry.Parse(collapsed ? "M 6,9 L 12,15 L 18,9" : "M 6,15 L 12,9 L 18,15");
        ToolTip.SetTip(RibbonCollapseButton, collapsed ? "Expand the ribbon" : "Collapse the ribbon (double-click a tab does the same)");
    }

    private void RibbonTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: string name })
            SelectRibbonTab(name);
    }

    private void RibbonTab_DoubleTapped(object? sender, TappedEventArgs e)
    {
        SetRibbonCollapsed(!RibbonCollapsed);
        // the double-tap's second click already re-checked the tab; keep the strip consistent
        if (sender is ToggleButton tb) tb.IsChecked = true;
    }

    private void RibbonCollapse_Click(object? sender, RoutedEventArgs e) =>
        SetRibbonCollapsed(!RibbonCollapsed);
}
