using GtPlus.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using GtPlus.Models;

namespace GtPlus.Controls;

/// <summary>
/// custom-drawn storyboard track area laid out the way Figma's motion timeline is: one collapsible row per object carrying a
/// summary bar over everything it does, and under it one row per animation drawn as a start and end keyframe joined by a line.
/// The time ruler and the name gutter are pinned to the visible area (the host feeds the scroll position into
/// <see cref="ScrollOffset"/>), so neither scrolls away. Purely a view over <see cref="GtStoryboard"/>: it never touches the
/// document model outside the drag gestures the user performs
/// </summary>
public class TimelineTrackControl : Control
{
    public const double GutterWidth = 188;
    public const double RulerHeight = 22;
    public const double RowHeight   = 24;
    private const double BarInset   = 4;

    /// <summary>room between the gutter and time zero, so a keyframe sitting on the first frame shows its whole diamond</summary>
    public const double LeadIn = 12;

    /// <summary>half-width of the grab zone around a keyframe diamond or a summary bar's end</summary>
    private const double EdgeGrab   = 6;

    /// <summary>keyframe diamond, edge to edge</summary>
    private const double DiamondSize = 9;

    /// <summary>mute eye at the right of each gutter row, and the gap it keeps from the edge</summary>
    private const double MuteIconSize  = 13;
    private const double MuteIconInset = 8;

    /// <summary>direction arrow shown beside an animation's name in the gutter</summary>
    private const double IconSize = 12;

    /// <summary>gutter indents: segment children, object chevron, animation names</summary>
    private const double SegmentIndent = 10;
    private const double ChevronWidth  = 16;
    private const double ChildIndent   = 22;

    private static readonly IBrush RowBrush      = new SolidColorBrush(Color.FromRgb(0x1b, 0x1b, 0x1b));
    private static readonly IBrush ObjectRowBrush = new SolidColorBrush(Color.FromRgb(0x1f, 0x1f, 0x1f));
    private static readonly IBrush GutterBrush   = new SolidColorBrush(Color.FromRgb(0x17, 0x17, 0x17));
    private static readonly IBrush RulerBrush    = new SolidColorBrush(Color.FromRgb(0x1b, 0x1b, 0x1b));
    private static readonly IBrush LabelBrush    = new SolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe6));
    private static readonly IBrush SubLabelBrush = new SolidColorBrush(Color.FromRgb(0xa6, 0xa6, 0xa6));
    private static readonly IBrush DimLabelBrush = new SolidColorBrush(Color.FromRgb(0x7a, 0x7a, 0x7a));
    private static readonly Color  SelectedColor = Color.FromRgb(0x4d, 0x9a, 0xe0);
    private static readonly IBrush SelectedBrush = new SolidColorBrush(SelectedColor);
    private static readonly IBrush SelectedRowBrush = new SolidColorBrush(SelectedColor, 0.12);
    private static readonly IPen   GridPen       = new Pen(new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26)), 1);
    private static readonly IPen   TickPen       = new Pen(new SolidColorBrush(Color.FromRgb(0x4a, 0x4a, 0x4a)), 1);
    private static readonly IPen   PlayheadPen   = new Pen(new SolidColorBrush(Color.FromRgb(0xff, 0x60, 0x60)), 1.5);
    private static readonly IPen   BorderPen     = new Pen(new SolidColorBrush(Color.FromRgb(0x2e, 0x2e, 0x2e)), 1);
    private static readonly IPen   TreePen       = new Pen(new SolidColorBrush(Color.FromRgb(0x4a, 0x4a, 0x4a)), 1);

    // Figma's neutral group bar: a rounded grey slab with a small grip at each end
    private static readonly IBrush SummaryFill   = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38));
    private static readonly IPen   SummaryPen    = new Pen(new SolidColorBrush(Color.FromRgb(0x48, 0x48, 0x48)), 1);
    private static readonly IPen   SummaryGrip   = new Pen(new SolidColorBrush(Color.FromRgb(0x7a, 0x7a, 0x7a)), 1);
    private static readonly IPen   KeyLinePen    = new Pen(new SolidColorBrush(Color.FromRgb(0x6a, 0x6a, 0x6a)), 1);

    // time zero: where the clip actually starts playing; only drawn once the track shows pre-roll, since without negative delays it coincides with the left edge anyway
    private static readonly IPen   ZeroPen       = new Pen(new SolidColorBrush(Color.FromRgb(0xe0, 0xa2, 0x4d)), 1);
    private static readonly IBrush ZeroLabelBrush = new SolidColorBrush(Color.FromRgb(0xe0, 0xa2, 0x4d));

    /// <summary>wash over everything left of zero, so pre-roll reads as "already running"</summary>
    private static readonly IBrush PreRollBrush  = new SolidColorBrush(Colors.Black, 0.30);

    // the marquee is drawn over everything, so it stays thin and mostly transparent
    private static readonly IPen   MarqueePen  = new Pen(SelectedBrush, 1, new DashStyle(new double[] { 3, 3 }, 0));
    private static readonly IBrush MarqueeFill = new SolidColorBrush(SelectedColor, 0.12);

    // cursors are shared: OnPointerMoved fires constantly and allocating one per move would churn native cursor handles
    private static readonly Cursor ResizeCursor = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor HandCursor   = new(StandardCursorType.Hand);
    private static readonly Cursor CrossCursor  = new(StandardCursorType.Cross);

    private static readonly IBrush HeaderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x26, 0x2b));

    // the working area: wherever something is animating, a bar along the ruler's foot and a faint wash down the rows
    private static readonly IBrush WorkAreaBar   = new SolidColorBrush(SelectedColor, 0.85);
    private static readonly IBrush WorkAreaRuler = new SolidColorBrush(SelectedColor, 0.14);
    private static readonly IBrush WorkAreaWash  = new SolidColorBrush(SelectedColor, 0.05);
    private const double WorkAreaBarHeight = 3;

    private IReadOnlyList<GtTimelineSegment> _segments = Array.Empty<GtTimelineSegment>();

    /// <summary>everything selected, the last entry is the primary whose values fill the strip</summary>
    private readonly List<GtAnimation> _selection = new();

    /// <summary>flattened draw order: per segment an optional header, then per object its group row and (unless collapsed) one row per animation; rebuilt whenever the model or a fold changes so rendering and hit-testing always agree on what sits in a given row</summary>
    private readonly List<Row> _rows = new();

    /// <summary>objects whose animation rows are folded away, per storyboard</summary>
    private readonly HashSet<(GtStoryboard Storyboard, string Object)> _collapsed = new();

    private enum RowKind { Header, Object, Animation }

    private sealed class Row
    {
        public required RowKind Kind { get; init; }
        public required GtTimelineSegment Segment { get; init; }

        /// <summary>the animation an Animation row draws</summary>
        public GtAnimation? Animation { get; init; }

        /// <summary>the object an Object or Animation row belongs to</summary>
        public string ObjectName { get; init; } = "";

        /// <summary>every animation of an Object row, in storyboard order, folded or not</summary>
        public List<GtAnimation> Children { get; } = new();

        public bool IsHeader => Kind == RowKind.Header;
    }

    /// <summary>an animation's timing before a drag started, for the history entry</summary>
    public readonly struct AnimationTiming
    {
        public AnimationTiming(GtAnimation animation, double delay, double? duration)
        {
            Animation = animation;
            Delay     = delay;
            Duration  = duration;
        }

        public GtAnimation Animation { get; }
        public double Delay { get; }
        public double? Duration { get; }
    }

    private enum DragMode { None, Playhead, MoveBar, ResizeStart, ResizeEnd, Marquee }

    private DragMode _drag;
    private GtAnimation? _dragAnim;
    private GtTimelineSegment? _dragSegment;
    private double _dragGrabOffset;       // seconds between pointer and bar start
    private double _dragOrigDelay;
    private double? _dragOrigDuration;

    /// <summary>pre-drag timing of every selected bar, so a group drag moves as one</summary>
    private readonly List<AnimationTiming> _dragEntries = new();

    /// <summary>start of the visible range held still for the duration of a bar drag; dragging a clip into the negative grows the track leftwards, which would otherwise re-map every X coordinate mid-gesture and make the bar fight the pointer</summary>
    private double? _frozenStart;

    private Point _marqueeAnchor;
    private Point _marqueeCurrent;
    private bool _marqueeAdditive;

    public static readonly StyledProperty<double> PixelsPerSecondProperty =
        AvaloniaProperty.Register<TimelineTrackControl, double>(nameof(PixelsPerSecond), 70.0);

    public double PixelsPerSecond
    {
        get => GetValue(PixelsPerSecondProperty);
        set => SetValue(PixelsPerSecondProperty, value);
    }

    public static readonly StyledProperty<double> CurrentTimeProperty =
        AvaloniaProperty.Register<TimelineTrackControl, double>(nameof(CurrentTime));

    public double CurrentTime
    {
        get => GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    public static readonly StyledProperty<Vector> ScrollOffsetProperty =
        AvaloniaProperty.Register<TimelineTrackControl, Vector>(nameof(ScrollOffset));

    /// <summary>the host scroller's offset; the ruler is drawn at its Y and the gutter at its X so both stay on screen</summary>
    public Vector ScrollOffset
    {
        get => GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    /// <summary>fires while the user scrubs the playhead</summary>
    public event Action<double>? TimeScrubbed;

    /// <summary>fires when the selection changes, with the primary animation and its owning storyboard (null = cleared); the whole selection is read back from <see cref="SelectedAnimations"/></summary>
    public event Action<GtAnimation?, GtStoryboard?>? AnimationSelected;

    /// <summary>fires when a row's mute eye is clicked, after the flag has been flipped; an object row's eye fires it once per animation it changed</summary>
    public event Action<GtAnimation>? MuteToggled;

    /// <summary>fires once per completed drag, with every dragged bar's pre-drag timing</summary>
    public event Action<IReadOnlyList<AnimationTiming>>? AnimationsTimingChanged;

    /// <summary>fires when the track grows or shrinks at its left edge, with the pixels the existing content moved right by; the host adds it to the scroll offset so the picture stays put</summary>
    public event Action<double>? ContentShifted;

    /// <summary>storyboards shown on the timeline, each at its own offset</summary>
    public IReadOnlyList<GtTimelineSegment> Segments
    {
        get => _segments;
        set
        {
            _segments = value ?? Array.Empty<GtTimelineSegment>();
            _selection.Clear();
            Refresh();
        }
    }

    /// <summary>primary selection: the bar whose values the property strip shows</summary>
    public GtAnimation? SelectedAnimation
    {
        get => _selection.Count == 0 ? null : _selection[_selection.Count - 1];
        set
        {
            _selection.Clear();
            if (value is not null) _selection.Add(value);
            InvalidateVisual();
        }
    }

    /// <summary>every selected bar, in the order it was added</summary>
    public IReadOnlyList<GtAnimation> SelectedAnimations => _selection;

    /// <summary>replaces the selection outright, the last item becomes the primary</summary>
    public void SetSelection(IEnumerable<GtAnimation> animations)
    {
        _selection.Clear();
        foreach (var anim in animations)
            if (!IsSelected(anim)) _selection.Add(anim);
        InvalidateVisual();
    }

    /// <summary>true when the object's animation rows are folded away</summary>
    public bool IsCollapsed(GtStoryboard storyboard, string objectName) =>
        _collapsed.Contains((storyboard, objectName));

    /// <summary>folds or unfolds one object's animation rows</summary>
    public void SetCollapsed(GtStoryboard storyboard, string objectName, bool collapsed)
    {
        if (collapsed) _collapsed.Add((storyboard, objectName));
        else           _collapsed.Remove((storyboard, objectName));
        Refresh();
    }

    /// <summary>total seconds the track spans: the last segment's end plus a little headroom</summary>
    public double TimelineLength
    {
        get
        {
            double end = 0;
            foreach (var segment in _segments)
                if (segment.EndTime > end) end = segment.EndTime;
            return Math.Max(end, 1.0) + 1.0;
        }
    }

    /// <summary>leftmost second the track draws; zero unless a clip carries a negative delay (vMix computes those before the title goes live) in which case the track is extended leftwards far enough to show the whole clip; frozen while a bar is being dragged</summary>
    public double TimelineStart => _frozenStart ?? ComputeStart();

    /// <summary>earliest clip start across the track, never above zero</summary>
    private double ComputeStart()
    {
        double start = 0;
        foreach (var (segment, anim) in AllAnimations())
        {
            double t = anim.Delay + segment.Offset;
            if (t < start) start = t;
        }
        return Math.Round(start, 3);
    }

    /// <summary>every animation on the track with its segment, folded rows included</summary>
    private IEnumerable<(GtTimelineSegment Segment, GtAnimation Animation)> AllAnimations()
    {
        foreach (var segment in _segments)
            foreach (var anim in segment.Storyboard.Animations)
                if (!anim.IsPlaceholder) yield return (segment, anim);
    }

    /// <summary>seconds from the left edge of the track to the right, pre-roll included</summary>
    public double TimelineSpan => TimelineLength - TimelineStart;

    public void Refresh()
    {
        RebuildRows();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private static string ObjectKey(GtAnimation anim) =>
        string.IsNullOrEmpty(anim.Object) ? "(no object)" : anim.Object;

    private void RebuildRows()
    {
        _rows.Clear();

        // headers only earn their row when there is more than one storyboard to tell apart
        bool headers = _segments.Count > 1;
        foreach (var segment in _segments)
        {
            if (headers) _rows.Add(new Row { Kind = RowKind.Header, Segment = segment });

            // objects in the order their first animation appears, so the tree follows the file
            var groups = new List<Row>();
            var byName = new Dictionary<string, Row>(StringComparer.Ordinal);
            foreach (var anim in segment.Storyboard.Animations)
            {
                // GT's <None> placeholders animate nothing, so they get no row
                if (anim.IsPlaceholder) continue;

                var key = ObjectKey(anim);
                if (!byName.TryGetValue(key, out var group))
                {
                    group = new Row { Kind = RowKind.Object, Segment = segment, ObjectName = key };
                    byName[key] = group;
                    groups.Add(group);
                }
                group.Children.Add(anim);
            }

            foreach (var group in groups)
            {
                _rows.Add(group);
                if (_collapsed.Contains((segment.Storyboard, group.ObjectName))) continue;
                foreach (var anim in group.Children)
                    _rows.Add(new Row { Kind = RowKind.Animation, Segment = segment, Animation = anim, ObjectName = group.ObjectName });
            }
        }

        // an animation deleted out from under the selection must not linger in it; a folded one is still on the track and stays
        var live = new HashSet<GtAnimation>(AllAnimations().Select(x => x.Animation), ReferenceEqualityComparer.Instance);
        _selection.RemoveAll(a => !live.Contains(a));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CurrentTimeProperty || change.Property == ScrollOffsetProperty) InvalidateVisual();
        else if (change.Property == PixelsPerSecondProperty) { InvalidateMeasure(); InvalidateVisual(); }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(GutterWidth + LeadIn + TimelineSpan * PixelsPerSecond,
                        RulerHeight + Math.Max(_rows.Count, 1) * RowHeight);
    }

    private double TimeToX(double seconds) => GutterWidth + LeadIn + (seconds - TimelineStart) * PixelsPerSecond;

    private double XToTime(double x) => TimelineStart + (x - GutterWidth - LeadIn) / PixelsPerSecond;

    /// <summary>left edge of the pinned gutter in content coordinates</summary>
    private double GutterLeft => ScrollOffset.X;

    /// <summary>first x right of the pinned gutter; anything drawn left of it is hidden under the gutter</summary>
    private double GutterRight => ScrollOffset.X + GutterWidth;

    /// <summary>top of the pinned ruler in content coordinates</summary>
    private double RulerTop => ScrollOffset.Y;

    private static double RowTop(int index) => RulerHeight + index * RowHeight;

    private bool InRuler(Point p) => p.Y < RulerTop + RulerHeight;

    private bool InGutter(Point p) => p.X < GutterRight;

    private int RowAt(double y)
    {
        if (y < RulerTop + RulerHeight || y < RulerHeight) return -1;
        return (int)((y - RulerHeight) / RowHeight);
    }

    /// <summary>the mute eye's box in row <paramref name="index"/>, at the pinned gutter's right edge</summary>
    private Rect MuteRect(int index) =>
        new(GutterRight - MuteIconSize - MuteIconInset,
            RowTop(index) + (RowHeight - MuteIconSize) / 2,
            MuteIconSize, MuteIconSize);

    /// <summary>the fold chevron's box on an object row</summary>
    private Rect ChevronRect(int index) =>
        new(GutterLeft + ObjectIndent, RowTop(index), ChevronWidth, RowHeight);

    private double ObjectIndent => _segments.Count > 1 ? SegmentIndent : 2;

    /// <summary>start-to-end extent of an animation row's keyframes, or an object row's summary bar, in content coordinates</summary>
    private Rect BarRect(Row row, int index)
    {
        double offset = row.Segment.Offset;
        double start, end;
        if (row.Kind == RowKind.Animation)
        {
            start = row.Animation!.Delay;
            end   = row.Animation.EndTime;
        }
        else
        {
            start = double.MaxValue;
            end   = double.MinValue;
            foreach (var a in row.Children)
            {
                start = Math.Min(start, a.Delay);
                end   = Math.Max(end, a.EndTime);
            }
        }

        double x0 = TimeToX(start + offset);
        double x1 = TimeToX(end + offset);
        return new Rect(x0, RowTop(index) + BarInset, Math.Max(x1 - x0, 1), RowHeight - BarInset * 2);
    }

    private static readonly Dictionary<string, Bitmap?> IconCache = new();

    /// <summary>arrow for an animation's direction, pointing the way it travels, the same arrows the direction pad shows so a row reads as the pad button that set it; types that carry no direction and GT's Center fall back to the no-direction dot</summary>
    public static string IconFileFor(GtAnimDirection direction, GtAnimationType type)
    {
        if (!GtAnimation.SupportsDirection(type)) return "no-direction.png";
        return IconFileFor(direction);
    }

    /// <summary>arrow file for a direction on its own, also drives the direction pad's buttons</summary>
    public static string IconFileFor(GtAnimDirection direction) => direction switch
    {
        GtAnimDirection.TopLeft     => "arrow-down-right.png",
        GtAnimDirection.Top         => "arrow-down.png",
        GtAnimDirection.TopRight    => "arrow-down-left.png",
        GtAnimDirection.Left        => "arrow-right.png",
        GtAnimDirection.Right       => "arrow-left.png",
        GtAnimDirection.BottomLeft  => "arrow-up-right.png",
        GtAnimDirection.Bottom      => "arrow-up.png",
        GtAnimDirection.BottomRight => "arrow-up-left.png",
        _                           => "no-direction.png",
    };

    /// <summary>loads an icon once per name, a missing file caches as null and draws nothing</summary>
    public static Bitmap? Icon(string filename)
    {
        if (IconCache.TryGetValue(filename, out var cached)) return cached;

        Bitmap? bitmap = null;
        try
        {
            var uri = new Uri($"avares://GtPlus/Assets/Icons/{filename}");
            using var stream = AssetLoader.Open(uri);
            bitmap = new Bitmap(stream);
        }
        catch
        {
            // icons are decoration, a missing file must not take the timeline down with it
        }

        IconCache[filename] = bitmap;
        return bitmap;
    }

    public override void Render(DrawingContext ctx)
    {
        double width  = Bounds.Width;
        double height = Bounds.Height;

        // row backgrounds across the full width; group rows sit a shade lighter than their animations, as Figma's do
        for (int i = 0; i < Math.Max(_rows.Count, 1); i++)
        {
            var y = RowTop(i);
            IBrush brush = RowBrush;
            if (i < _rows.Count)
                brush = _rows[i].Kind switch
                {
                    RowKind.Header => HeaderBrush,
                    RowKind.Object => ObjectRowBrush,
                    _              => RowBrush,
                };
            ctx.DrawRectangle(brush, null, new Rect(0, y, width, RowHeight));
            if (i < _rows.Count && RowIsSelected(_rows[i]))
                ctx.DrawRectangle(SelectedRowBrush, null, new Rect(0, y, width, RowHeight));
            ctx.DrawLine(GridPen, new Point(0, y + RowHeight - 0.5), new Point(width, y + RowHeight - 0.5));
        }

        var workArea = WorkingArea();
        foreach (var (t0, t1) in workArea)
        {
            double x0 = TimeToX(t0), x1 = TimeToX(t1);
            ctx.DrawRectangle(WorkAreaWash, null, new Rect(x0, RulerHeight, Math.Max(x1 - x0, 1), height - RulerHeight));
        }

        for (int i = 0; i < _rows.Count; i++)
            DrawRow(ctx, _rows[i], i);

        DrawPreRoll(ctx, height);
        DrawZeroMarker(ctx, height);
        DrawPlayheadLine(ctx, height);

        // the gutter is pinned to the left of the visible area and drawn over the bars, so they scroll under it
        ctx.DrawRectangle(GutterBrush, null, new Rect(GutterLeft, RulerHeight, GutterWidth, height - RulerHeight));
        for (int i = 0; i < _rows.Count; i++)
        {
            if (RowIsSelected(_rows[i]))
                ctx.DrawRectangle(SelectedRowBrush, null, new Rect(GutterLeft, RowTop(i), GutterWidth, RowHeight));
            ctx.DrawLine(GridPen, new Point(GutterLeft, RowTop(i) + RowHeight - 0.5),
                         new Point(GutterRight, RowTop(i) + RowHeight - 0.5));
            DrawGutterLabel(ctx, _rows[i], i);
        }
        ctx.DrawLine(BorderPen, new Point(GutterRight, 0), new Point(GutterRight, height));

        if (_rows.Count == 0)
            DrawText(ctx, "No animations in this storyboard", GutterRight + 8, RulerTop + RulerHeight + 5, DimLabelBrush, 11);

        // the ruler is pinned to the top of the visible area and drawn last-but-one, over the rows scrolled up beneath it
        DrawRuler(ctx, width, workArea);
        DrawPlayheadHead(ctx);
        DrawMarquee(ctx);
    }

    /// <summary>dims everything before time zero: those frames are computed, never shown live</summary>
    private void DrawPreRoll(DrawingContext ctx, double height)
    {
        if (TimelineStart >= 0) return;

        double zero = TimeToX(0);
        if (zero <= GutterWidth) return;
        ctx.DrawRectangle(PreRollBrush, null,
                          new Rect(GutterWidth, RulerHeight, zero - GutterWidth, height - RulerHeight));
    }

    /// <summary>marks where the clip actually starts, once pre-roll pushes it off the left edge</summary>
    private void DrawZeroMarker(DrawingContext ctx, double height)
    {
        if (TimelineStart >= 0) return;

        double x = TimeToX(0);
        if (x < GutterRight) return;
        ctx.DrawLine(ZeroPen, new Point(x, RulerHeight), new Point(x, height));
        DrawText(ctx, "0", x + 3, RulerTop + RulerHeight + 1, ZeroLabelBrush, 9);
    }

    /// <summary>
    /// the stretches of timeline time during which at least one unmuted animation is running, merged and in order; this is the
    /// part of the timeline that actually does something, and the ruler marks it. Gaps where every object is at rest stay unmarked
    /// </summary>
    public IReadOnlyList<(double Start, double End)> WorkingArea()
    {
        var spans = new List<(double Start, double End)>();
        foreach (var (segment, anim) in AllAnimations())
        {
            if (anim.Muted) continue;
            double a = segment.Offset + anim.Delay, b = segment.Offset + anim.EndTime;
            if (b > a) spans.Add((a, b));
        }
        spans.Sort((x, y) => x.Start.CompareTo(y.Start));

        var merged = new List<(double Start, double End)>();
        foreach (var span in spans)
        {
            if (merged.Count > 0 && span.Start <= merged[^1].End + 1e-6)
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, span.End));
            else
                merged.Add(span);
        }
        return merged;
    }

    private void DrawRuler(DrawingContext ctx, double width, IReadOnlyList<(double Start, double End)> workArea)
    {
        double top = RulerTop;
        ctx.DrawRectangle(RulerBrush, null, new Rect(0, top, width, RulerHeight));

        // the working area tints the ruler and runs a bar along its foot, under the ticks so they stay readable
        foreach (var (t0, t1) in workArea)
        {
            double x0 = Math.Max(TimeToX(t0), GutterRight), x1 = TimeToX(t1);
            if (x1 <= x0) continue;
            ctx.DrawRectangle(WorkAreaRuler, null, new Rect(x0, top, x1 - x0, RulerHeight));
            ctx.DrawRectangle(WorkAreaBar, null,
                              new Rect(x0, top + RulerHeight - WorkAreaBarHeight - 1, x1 - x0, WorkAreaBarHeight));
        }
        ctx.DrawLine(BorderPen, new Point(0, top + RulerHeight - 0.5), new Point(width, top + RulerHeight - 0.5));

        // choose a tick spacing that stays readable at the current zoom
        double step = PixelsPerSecond switch
        {
            >= 400 => 0.05,
            >= 160 => 0.1,
            >= 60  => 0.5,
            >= 25  => 1.0,
            _      => 5.0
        };

        // counted in whole ticks rather than accumulating a double, so "every Nth tick is major" stays exact however long the timeline is
        int majorEvery = step >= 1 ? 1 : step < 0.1 ? 10 : 5;
        int first = (int)Math.Floor(TimelineStart / step);
        // ticks run to the right edge of the control, which the host stretches to the panel, not just to the end of the storyboard
        int ticks = (int)Math.Floor(Math.Max(TimelineLength, XToTime(width)) / step);
        for (int i = first; i <= ticks; i++)
        {
            double t = i * step;
            double x = TimeToX(t);
            if (x > width) break;
            if (x < GutterRight) continue;

            bool major = ((i % majorEvery) + majorEvery) % majorEvery == 0;
            ctx.DrawLine(TickPen, new Point(x, top + (major ? RulerHeight - 8 : RulerHeight - 4)),
                         new Point(x, top + RulerHeight));
            if (major)
                DrawText(ctx, FormatTime(t), x + 3, top + 3, SubLabelBrush, 10);
        }

        // the corner over the gutter carries the playhead's time, the way Figma's does
        ctx.DrawRectangle(RulerBrush, null, new Rect(GutterLeft, top, GutterWidth, RulerHeight));
        ctx.DrawLine(BorderPen, new Point(GutterLeft, top + RulerHeight - 0.5), new Point(GutterRight, top + RulerHeight - 0.5));
        ctx.DrawLine(BorderPen, new Point(GutterRight, top), new Point(GutterRight, top + RulerHeight));
        DrawText(ctx, $"{CurrentTime:0.00}s", GutterLeft + 8, top + 4, LabelBrush, 11);
    }

    private void DrawRow(DrawingContext ctx, Row row, int index)
    {
        double y = RowTop(index);

        switch (row.Kind)
        {
            case RowKind.Header:
            {
                // the header's span shows where its storyboard sits on the shared timeline
                double x0 = TimeToX(row.Segment.Offset);
                double x1 = TimeToX(row.Segment.EndTime);
                ctx.DrawRectangle(new SolidColorBrush(SelectedColor, 0.20), null,
                                  new Rect(x0, y + 7, Math.Max(x1 - x0, 2), RowHeight - 14));
                return;
            }

            case RowKind.Object:
            {
                if (row.Children.Count == 0) return;
                var rect = BarRect(row, index);
                bool allMuted = row.Children.All(a => a.Muted);
                bool selected = RowIsSelected(row);

                using (ctx.PushOpacity(allMuted ? 0.4 : 1.0))
                {
                    ctx.DrawRectangle(SummaryFill, selected ? new Pen(SelectedBrush, 1.5) : SummaryPen,
                                      new RoundedRect(rect, 4));

                    // a small grip at each end, as on Figma's group bars, where it is wide enough to hold one
                    if (rect.Width >= 16)
                    {
                        double gy0 = rect.Y + 4, gy1 = rect.Bottom - 4;
                        ctx.DrawLine(SummaryGrip, new Point(rect.X + 5, gy0), new Point(rect.X + 5, gy1));
                        ctx.DrawLine(SummaryGrip, new Point(rect.Right - 5, gy0), new Point(rect.Right - 5, gy1));
                    }
                }
                return;
            }

            default:
            {
                var anim     = row.Animation!;
                var rect     = BarRect(row, index);
                bool selected = IsSelected(anim);
                double cy    = y + RowHeight / 2;
                var accent   = AccentFor(anim.Type);

                using (ctx.PushOpacity(anim.Muted ? 0.35 : 1.0))
                {
                    // the span between the two keyframes; a selected animation lights up in the selection blue
                    var line = selected ? new Pen(SelectedBrush, 1.5) : KeyLinePen;
                    ctx.DrawLine(line, new Point(rect.X, cy), new Point(rect.Right, cy));

                    // a reverse animation settles at its start, so its start diamond is the hollow one
                    DrawDiamond(ctx, rect.X,     cy, accent, selected, hollow: anim.Reverse);
                    DrawDiamond(ctx, rect.Right, cy, accent, selected, hollow: false);
                }
                return;
            }
        }
    }

    private static void DrawDiamond(DrawingContext ctx, double x, double cy, Color accent, bool selected, bool hollow)
    {
        double r = DiamondSize / 2;
        var geo = new PolylineGeometry(new[]
        {
            new Point(x, cy - r), new Point(x + r, cy), new Point(x, cy + r), new Point(x - r, cy),
        }, true);

        IBrush fill = hollow ? new SolidColorBrush(Color.FromRgb(0x1b, 0x1b, 0x1b)) : new SolidColorBrush(accent);
        var pen = selected ? new Pen(Brushes.White, 1.5) : new Pen(new SolidColorBrush(accent), 1);
        ctx.DrawGeometry(fill, pen, geo);
    }

    private void DrawGutterLabel(DrawingContext ctx, Row row, int index)
    {
        double y = RowTop(index);

        if (row.IsHeader)
        {
            ctx.DrawRectangle(HeaderBrush, null, new Rect(GutterLeft, y, GutterWidth, RowHeight));
            var title = row.Segment.Offset > 0
                ? $"{row.Segment.Storyboard.DisplayName}  +{row.Segment.Offset:0.##}s"
                : row.Segment.Storyboard.DisplayName;
            using (ctx.PushClip(new Rect(GutterLeft, y, GutterWidth - 4, RowHeight)))
                DrawText(ctx, title, GutterLeft + 6, y + 4, SelectedBrush, 11);
            return;
        }

        var eye = MuteRect(index);

        if (row.Kind == RowKind.Object)
        {
            bool collapsed = IsCollapsed(row.Segment.Storyboard, row.ObjectName);
            bool allMuted  = row.Children.All(a => a.Muted);

            // fold chevron: right-pointing when folded, down when open
            var chev = ChevronRect(index);
            double cx = chev.X + chev.Width / 2, cy = y + RowHeight / 2;
            var points = collapsed
                ? new[] { new Point(cx - 2, cy - 4), new Point(cx + 2, cy), new Point(cx - 2, cy + 4) }
                : new[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) };
            ctx.DrawGeometry(null, new Pen(SubLabelBrush, 1.3, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round),
                             new PolylineGeometry(points, false));

            DrawEye(ctx, eye, allMuted);

            double textX = chev.Right + 2;
            using (ctx.PushClip(new Rect(textX, y, eye.X - 4 - textX, RowHeight)))
                DrawText(ctx, row.ObjectName, textX, y + 4, allMuted ? DimLabelBrush : LabelBrush, 11, FontWeight.SemiBold);

            // a folded group says how many animations it hides
            if (collapsed && row.Children.Count > 1)
                DrawText(ctx, row.Children.Count.ToString(), eye.X - 16, y + 5, DimLabelBrush, 10);
            return;
        }

        var anim = row.Animation!;

        // tree connector from the object's chevron down to this row, Figma's └
        double treeX = GutterLeft + ObjectIndent + ChevronWidth / 2;
        bool last = index + 1 >= _rows.Count || _rows[index + 1].Kind != RowKind.Animation
                    || !ReferenceEquals(_rows[index + 1].Segment, row.Segment)
                    || _rows[index + 1].ObjectName != row.ObjectName;
        ctx.DrawLine(TreePen, new Point(treeX, y), new Point(treeX, last ? y + RowHeight / 2 : y + RowHeight));
        ctx.DrawLine(TreePen, new Point(treeX, y + RowHeight / 2), new Point(treeX + 7, y + RowHeight / 2));

        DrawEye(ctx, eye, anim.Muted);

        double nameX = GutterLeft + ObjectIndent + ChildIndent;
        double nameRight = eye.X - 4;

        // the direction arrow sits just left of the eye, the way it sat on the old bar's right end
        if (GtAnimation.SupportsDirection(anim.Type) && Icon(IconFileFor(anim.Direction, anim.Type)) is { } arrow)
        {
            var iconRect = new Rect(eye.X - IconSize - 6, y + (RowHeight - IconSize) / 2, IconSize, IconSize);
            using (ctx.PushOpacity(anim.Muted ? 0.35 : 0.8))
                ctx.DrawImage(arrow, iconRect);
            nameRight = iconRect.X - 4;
        }

        var label = anim.Reverse ? "◀ " + anim.TypeName : anim.TypeName;
        using (ctx.PushClip(new Rect(nameX, y, Math.Max(0, nameRight - nameX), RowHeight)))
            DrawText(ctx, label, nameX, y + 5, anim.Muted ? DimLabelBrush : SubLabelBrush, 11);
    }

    private static void DrawEye(DrawingContext ctx, Rect box, bool muted)
    {
        var icon = Icon(muted ? "eye-off.png" : "eye.png");
        if (icon is null) return;
        using (ctx.PushOpacity(muted ? 0.9 : 0.45))
            ctx.DrawImage(icon, box);
    }

    private void DrawPlayheadLine(DrawingContext ctx, double height)
    {
        double x = TimeToX(CurrentTime);
        if (x < GutterRight) return;
        ctx.DrawLine(PlayheadPen, new Point(x, RulerHeight), new Point(x, height));
    }

    /// <summary>the head rides on the pinned ruler, so it is drawn after it</summary>
    private void DrawPlayheadHead(DrawingContext ctx)
    {
        double x = TimeToX(CurrentTime);
        if (x < GutterRight) return;
        double top = RulerTop;
        ctx.DrawLine(PlayheadPen, new Point(x, top), new Point(x, top + RulerHeight));
        ctx.DrawGeometry(PlayheadPen.Brush, null, new PolylineGeometry(
            new[] { new Point(x - 5, top), new Point(x + 5, top), new Point(x + 5, top + 6), new Point(x, top + 11), new Point(x - 5, top + 6) }, true));
    }

    private void DrawMarquee(DrawingContext ctx)
    {
        if (_drag != DragMode.Marquee) return;
        ctx.DrawRectangle(MarqueeFill, MarqueePen, MarqueeRect());
    }

    private Rect MarqueeRect() => new(_marqueeAnchor, _marqueeCurrent);

    private bool IsSelected(GtAnimation anim)
    {
        foreach (var a in _selection)
            if (ReferenceEquals(a, anim)) return true;
        return false;
    }

    /// <summary>an animation row is selected with its animation; an object row once every animation under it is</summary>
    private bool RowIsSelected(Row row) => row.Kind switch
    {
        RowKind.Animation => IsSelected(row.Animation!),
        RowKind.Object    => row.Children.Count > 0 && row.Children.All(IsSelected),
        _                 => false,
    };

    private static Color AccentFor(GtAnimationType type) => type switch
    {
        GtAnimationType.Fade              => Color.FromRgb(0x4d, 0x9a, 0xe0),
        GtAnimationType.Fly               => Color.FromRgb(0x5c, 0xb8, 0x5c),
        GtAnimationType.Bounce            => Color.FromRgb(0x7c, 0xc8, 0x4c),
        GtAnimationType.Expand            => Color.FromRgb(0x9c, 0xb8, 0x3c),
        GtAnimationType.Reveal            => Color.FromRgb(0xd0, 0x8b, 0x3c),
        GtAnimationType.Rotate            => Color.FromRgb(0xc8, 0x6c, 0x6c),
        GtAnimationType.RotateContinuous  => Color.FromRgb(0xc8, 0x6c, 0x6c),
        GtAnimationType.Blink             => Color.FromRgb(0x8a, 0x8a, 0xb0),
        GtAnimationType.Zoom              => Color.FromRgb(0xa8, 0x77, 0xd0),
        GtAnimationType.ZoomFade          => Color.FromRgb(0x7d, 0x88, 0xdc),
        GtAnimationType.Scroll            => Color.FromRgb(0x3c, 0x8b, 0xd0),
        GtAnimationType.Hidden            => Color.FromRgb(0x70, 0x70, 0x70),
        GtAnimationType.ImageSequence     => Color.FromRgb(0xd0, 0x5c, 0x8b),
        GtAnimationType.ImageSequenceLoop => Color.FromRgb(0xd0, 0x5c, 0x8b),
        GtAnimationType.FillOffset        => Color.FromRgb(0x3c, 0xa8, 0xa8),
        GtAnimationType.StrokeOffset      => Color.FromRgb(0x3c, 0xa8, 0xa8),
        _                                 => Color.FromRgb(0x88, 0x88, 0x88)
    };

    private static string FormatTime(double t) =>
        t >= 10 ? t.ToString("0") + "s" : t.ToString("0.##") + "s";

    private static void DrawText(DrawingContext ctx, string text, double x, double y, IBrush brush, double size,
                                 FontWeight weight = FontWeight.Normal)
    {
        var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, weight), size, brush);
        ctx.DrawText(ft, new Point(x, y));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed) return;

        bool ctrl  = PlatformKeys.HasCommand(e.KeyModifiers);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        e.Pointer.Capture(this);

        // the pinned ruler scrubs wherever it currently sits
        if (InRuler(p))
        {
            _drag = DragMode.Playhead;
            if (!InGutter(p)) ScrubTo(p.X);
            return;
        }

        int index = RowAt(p.Y);
        var row = index >= 0 && index < _rows.Count ? _rows[index] : null;

        if (InGutter(p))
        {
            HandleGutterPress(p, index, row, ctrl || shift);
            return;
        }

        var hit = HitTestBar(p);
        if (hit is null)
        {
            // Ctrl+drag rubber-bands a selection, the same gesture the canvas uses; Shift keeps what is already selected, without it the band replaces the selection
            if (ctrl)
            {
                _drag            = DragMode.Marquee;
                _marqueeAnchor   = p;
                _marqueeCurrent  = p;
                _marqueeAdditive = shift;
                if (!shift) _selection.Clear();
                InvalidateVisual();
                return;
            }

            // clicking empty track space still scrubs, matches NLE behaviour
            _drag = DragMode.Playhead;
            ScrubTo(p.X);
            return;
        }

        var (hitRow, rect) = hit.Value;
        var targets = hitRow.Kind == RowKind.Object ? hitRow.Children : new List<GtAnimation> { hitRow.Animation! };
        var anim    = hitRow.Kind == RowKind.Object ? targets.OrderBy(a => a.Delay).First() : hitRow.Animation!;

        if (ctrl || shift)
        {
            // toggling is a selection gesture only, it never starts a drag; a group toggles as one
            bool allIn = targets.All(IsSelected);
            foreach (var t in targets)
            {
                if (allIn) _selection.RemoveAll(a => ReferenceEquals(a, t));
                else if (!IsSelected(t)) _selection.Add(t);
            }

            _drag = DragMode.None;
            InvalidateVisual();
            RaiseSelectionChanged();
            return;
        }

        // pressing inside the selection keeps it, so a whole group can be dragged as one
        if (!targets.All(IsSelected))
        {
            _selection.Clear();
            _selection.AddRange(targets);
        }
        // the pressed animation becomes the primary, so the strip shows what was clicked
        _selection.RemoveAll(a => ReferenceEquals(a, anim));
        _selection.Add(anim);

        InvalidateVisual();
        RaiseSelectionChanged();

        _dragAnim         = anim;
        _dragSegment      = hitRow.Segment;
        _dragOrigDelay    = anim.Delay;
        _dragOrigDuration = anim.Duration;
        _frozenStart      = ComputeStart();
        CaptureDragEntries();

        // a group bar only moves: its ends belong to different animations, so there is no one duration to stretch
        if (hitRow.Kind == RowKind.Animation && Math.Abs(p.X - rect.X) <= EdgeGrab && rect.Width > EdgeGrab)
            _drag = DragMode.ResizeStart;
        else if (hitRow.Kind == RowKind.Animation && Math.Abs(rect.Right - p.X) <= EdgeGrab)
            _drag = DragMode.ResizeEnd;
        else
        {
            _drag = DragMode.MoveBar;
            // delays stay storyboard-relative, so the segment offset comes back out here
            _dragGrabOffset = XToTime(p.X) - hitRow.Segment.Offset - anim.Delay;
        }
    }

    /// <summary>gutter clicks: an eye mutes, an object row's chevron folds, any other press on a row selects what it stands for</summary>
    private void HandleGutterPress(Point p, int index, Row? row, bool toggle)
    {
        _drag = DragMode.None;

        if (row is null || row.IsHeader)
        {
            if (!toggle)
            {
                _selection.Clear();
                InvalidateVisual();
                AnimationSelected?.Invoke(null, null);
            }
            return;
        }

        // grown a little: the eye is 13px, and a pixel-exact target is fussy to hit
        if (MuteRect(index).Inflate(3).Contains(p))
        {
            var targets = row.Kind == RowKind.Object ? row.Children : new List<GtAnimation> { row.Animation! };
            // a group's eye mutes everything under it unless everything already is, in which case it brings it all back
            bool mute = !targets.All(a => a.Muted);
            foreach (var t in targets)
            {
                if (t.Muted == mute) continue;
                t.Muted = mute;
                MuteToggled?.Invoke(t);
            }
            InvalidateVisual();
            return;
        }

        if (row.Kind == RowKind.Object && ChevronRect(index).Contains(p))
        {
            SetCollapsed(row.Segment.Storyboard, row.ObjectName,
                         !IsCollapsed(row.Segment.Storyboard, row.ObjectName));
            return;
        }

        var picked = row.Kind == RowKind.Object ? row.Children : new List<GtAnimation> { row.Animation! };
        if (toggle)
        {
            bool allIn = picked.All(IsSelected);
            foreach (var t in picked)
            {
                if (allIn) _selection.RemoveAll(a => ReferenceEquals(a, t));
                else if (!IsSelected(t)) _selection.Add(t);
            }
        }
        else
        {
            _selection.Clear();
            _selection.AddRange(picked);
        }

        InvalidateVisual();
        RaiseSelectionChanged();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);

        if (_drag == DragMode.None)
        {
            Cursor = CursorFor(p, PlatformKeys.HasCommand(e.KeyModifiers));
            return;
        }

        if (_drag == DragMode.Playhead) { ScrubTo(p.X); return; }

        if (_drag == DragMode.Marquee)
        {
            _marqueeCurrent = p;
            InvalidateVisual();
            return;
        }

        if (_dragAnim is null) return;

        // work in the dragged animation's own storyboard time, not timeline time
        double t = Snap(XToTime(p.X) - (_dragSegment?.Offset ?? 0));

        // vMix accepts a negative delay, the animation is computed before the title goes live so it is already part-way through at time zero; it is deliberate rather than a slip so it takes Shift, without it every bar snaps to the zero mark instead
        bool preRoll = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (_drag)
        {
            case DragMode.MoveBar:
            {
                // the whole selection shifts by the grabbed bar's delta, clamped so nothing in the group is pushed past zero
                double grabbed = Snap(t - _dragGrabOffset);
                double delta   = (preRoll ? grabbed : Math.Max(0, grabbed)) - _dragOrigDelay;
                if (!preRoll)
                    foreach (var entry in _dragEntries) delta = Math.Max(delta, -entry.Delay);

                foreach (var entry in _dragEntries)
                    entry.Animation.Delay = Snap(entry.Delay + delta);
                break;
            }

            case DragMode.ResizeStart:
            {
                // dragging the start keyframe keeps the end fixed, so duration absorbs the change
                double end   = _dragOrigDelay + (_dragOrigDuration ?? GtAnimation.DefaultDuration);
                double start = Math.Min(t, end - 0.05);
                if (!preRoll) start = Math.Max(start, 0);
                double delta = start - _dragOrigDelay;
                foreach (var entry in _dragEntries)
                {
                    if (!preRoll) delta = Math.Max(delta, -entry.Delay);
                    delta = Math.Min(delta, (entry.Duration ?? GtAnimation.DefaultDuration) - 0.05);
                }

                foreach (var entry in _dragEntries)
                {
                    entry.Animation.Delay    = Snap(entry.Delay + delta);
                    entry.Animation.Duration = Math.Round((entry.Duration ?? GtAnimation.DefaultDuration) - delta, 3);
                }
                break;
            }

            case DragMode.ResizeEnd:
            {
                double delta = Math.Max(0.05, Math.Round(t - _dragOrigDelay, 3))
                               - (_dragOrigDuration ?? GtAnimation.DefaultDuration);
                foreach (var entry in _dragEntries)
                    delta = Math.Max(delta, 0.05 - (entry.Duration ?? GtAnimation.DefaultDuration));

                foreach (var entry in _dragEntries)
                    entry.Animation.Duration =
                        Math.Round((entry.Duration ?? GtAnimation.DefaultDuration) + delta, 3);
                break;
            }
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);

        if (_drag == DragMode.Marquee)
        {
            ApplyMarquee();
            _drag = DragMode.None;
            InvalidateVisual();
            RaiseSelectionChanged();
            return;
        }

        if (_drag is DragMode.MoveBar or DragMode.ResizeStart or DragMode.ResizeEnd)
        {
            var changed = _dragEntries
                .Where(entry => entry.Animation.Delay != entry.Delay ||
                                entry.Animation.Duration != entry.Duration)
                .ToList();

            ReleaseStartFreeze();
            if (changed.Count > 0) AnimationsTimingChanged?.Invoke(changed);
        }

        _drag = DragMode.None;
        _dragAnim = null;
        _dragSegment = null;
        _dragEntries.Clear();
    }

    /// <summary>snapshots the timing of every selected bar so a group drag can move as one</summary>
    private void CaptureDragEntries()
    {
        _dragEntries.Clear();
        foreach (var anim in _selection)
            _dragEntries.Add(new AnimationTiming(anim, anim.Delay, anim.Duration));
    }

    /// <summary>selects every animation the rubber band touches; a folded group is touched through its summary bar</summary>
    private void ApplyMarquee()
    {
        var box = MarqueeRect();
        if (!_marqueeAdditive) _selection.Clear();

        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            if (row.IsHeader) continue;
            if (!box.Intersects(BarRect(row, i).Inflate(new Thickness(EdgeGrab, 0)))) continue;

            if (row.Kind == RowKind.Animation)
            {
                if (!IsSelected(row.Animation!)) _selection.Add(row.Animation!);
            }
            else if (IsCollapsed(row.Segment.Storyboard, row.ObjectName))
            {
                foreach (var a in row.Children)
                    if (!IsSelected(a)) _selection.Add(a);
            }
        }
    }

    private void RaiseSelectionChanged()
    {
        var primary = SelectedAnimation;
        GtStoryboard? storyboard = null;
        if (primary is not null)
            foreach (var (segment, anim) in AllAnimations())
                if (ReferenceEquals(anim, primary)) { storyboard = segment.Storyboard; break; }

        AnimationSelected?.Invoke(primary, storyboard);
    }

    /// <summary>lets the track go back to sizing itself and reports how far the picture moved: growing pre-roll pushes everything right, and the host cancels that out with the scroll offset</summary>
    private void ReleaseStartFreeze()
    {
        if (_frozenStart is not { } frozen) return;

        _frozenStart = null;
        double shift = (frozen - ComputeStart()) * PixelsPerSecond;

        InvalidateMeasure();
        InvalidateVisual();
        if (Math.Abs(shift) > 0.5) ContentShifted?.Invoke(shift);
    }

    /// <summary>the playhead reaches back over the pre-roll so the frames vMix computes before the title goes live can be inspected; playback itself still starts at zero</summary>
    private void ScrubTo(double x)
    {
        var t = Math.Clamp(XToTime(Math.Max(x, GutterRight)), TimelineStart, TimelineLength);
        CurrentTime = t;
        TimeScrubbed?.Invoke(t);
    }

    /// <summary>rounds to 10ms, holding no modifier is precise enough for storyboard work</summary>
    private static double Snap(double seconds) => Math.Round(seconds, 2);

    /// <summary>the animation or group bar under <paramref name="p"/>, with the bar's rect; the grab zone reaches a little past each end so the keyframe diamonds, which straddle the ends, stay fully grabbable</summary>
    private (Row Row, Rect Rect)? HitTestBar(Point p)
    {
        int index = RowAt(p.Y);
        if (index < 0 || index >= _rows.Count) return null;

        var row = _rows[index];
        if (row.IsHeader || (row.Kind == RowKind.Object && row.Children.Count == 0)) return null;

        var rect = BarRect(row, index);
        return rect.Inflate(new Thickness(EdgeGrab, 0, EdgeGrab, 0)).Contains(p) ? (row, rect) : null;
    }

    private Cursor CursorFor(Point p, bool ctrl)
    {
        if (InRuler(p)) return InGutter(p) ? Cursor.Default : ResizeCursor;
        if (InGutter(p))
        {
            int index = RowAt(p.Y);
            if (index < 0 || index >= _rows.Count || _rows[index].IsHeader) return Cursor.Default;
            return HandCursor;
        }

        var hit = HitTestBar(p);
        if (hit is null) return ctrl ? CrossCursor : Cursor.Default;

        var (row, rect) = hit.Value;
        if (row.Kind == RowKind.Animation &&
            (Math.Abs(p.X - rect.X) <= EdgeGrab || Math.Abs(rect.Right - p.X) <= EdgeGrab))
            return ResizeCursor;
        return HandCursor;
    }
}
