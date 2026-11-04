using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using GtPlus.Models;
using GtPlus.Services;

namespace GtPlus.Controls;

/// <summary>
/// Photoshop's Free Transform (Cmd/Ctrl+T) for one object: a box that turns with the object, eight handles that scale it
/// (corners keep the proportions, Shift frees them; Option/Alt scales from the centre), a drag inside moves it and a drag
/// anywhere outside the box rotates it about its centre (Shift snaps to 15°). Enter or a double-click inside applies,
/// Esc puts everything back; the whole session is one undo step. It only edits Location, Dimensions and RotateZ, so the
/// file stays plain GT. Also home to the Photoshop-style cursors the ordinary resize handles use
/// </summary>
public partial class GtCanvasControl
{
    private sealed class FreeTransformSession
    {
        public required GtLayer Layer;
        public required GtElement Element;
        public required GtPoint Location;
        public required GtSize Dimensions;
        public required double RotateZ;
    }

    private enum FreeTransformDrag { None, Move, Scale, Rotate }

    private FreeTransformSession? _ft;
    private FreeTransformDrag _ftDrag;
    private ResizeHandle _ftHandle;
    private Point _ftStart;
    private Rect _ftStartBox;
    private double _ftStartAngle;
    private double _ftStartScreenRz;
    private GtPoint _ftStartLocation;

    /// <summary>how far outside the box (in screen pixels) a press still counts as grabbing a handle</summary>
    private const double FtHandleReach = 8;

    /// <summary>rotation step while Shift is held, as in Photoshop</summary>
    private const double FtRotateStep = Math.PI / 12;

    public bool IsFreeTransforming => _ft is not null;

    /// <summary>raised when Free Transform starts or ends, so the window can show or clear its hint</summary>
    public event EventHandler? FreeTransformChanged;

    /// <summary>the object a Free Transform would act on: exactly one selected, unlocked object on an unlocked layer</summary>
    public GtElement? FreeTransformCandidate()
    {
        if (_document is null || IsPreviewing || _selectedElements.Count != 1) return null;
        foreach (var el in _selectedElements)
            if (!el.Locked && !IsLayerLocked(el) && LayerOf(el) is not null) return el;
        return null;
    }

    /// <summary>starts Free Transform on the single selected object; false when there is not exactly one unlocked object selected</summary>
    public bool BeginFreeTransform()
    {
        if (_ft is not null) return true;
        if (FreeTransformCandidate() is not { } el || LayerOf(el) is not { } layer) return false;

        CommitNudge();
        if (ActiveTool != CanvasTool.Edit) ActiveTool = CanvasTool.Edit;
        _ft = new FreeTransformSession
        {
            Layer = layer, Element = el, Location = el.Location, Dimensions = el.Dimensions, RotateZ = el.RotateZ,
        };
        _ftDrag = FreeTransformDrag.None;
        InvalidateVisual();
        FreeTransformChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>applies the transform: one undo step from where the session started to where the object is now</summary>
    public void CommitFreeTransform()
    {
        if (_ft is not { } s) return;
        _ft = null;
        _ftDrag = FreeTransformDrag.None;
        var el = s.Element;
        var after = (el.Location, el.Dimensions, el.RotateZ);
        var before = (s.Location, s.Dimensions, s.RotateZ);

        if (after != before && History is not null)
        {
            void Set((GtPoint Loc, GtSize Dim, double Rz) v)
            {
                el.Location = v.Loc; el.Dimensions = v.Dim; el.RotateZ = v.Rz;
                InvalidateVisual();
                TransformLive?.Invoke(this, EventArgs.Empty);
            }
            History.Push(new PropertyChangeAction($"Free transform ({el.Name})",
                undo: () => Set(before), redo: () => Set(after)));
        }

        Cursor = Cursor.Default;
        InvalidateVisual();
        TransformLive?.Invoke(this, EventArgs.Empty);
        FreeTransformChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>drops the transform and puts the object back exactly as it was</summary>
    public void CancelFreeTransform()
    {
        if (_ft is not { } s) return;
        _ft = null;
        _ftDrag = FreeTransformDrag.None;
        s.Element.Location = s.Location;
        s.Element.Dimensions = s.Dimensions;
        s.Element.RotateZ = s.RotateZ;
        Cursor = Cursor.Default;
        InvalidateVisual();
        TransformLive?.Invoke(this, EventArgs.Empty);
        FreeTransformChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>arrow-key nudge while transforming: moves the object without its own undo step, the session's step covers it</summary>
    public void NudgeFreeTransform(double dx, double dy)
    {
        if (_ft is not { } s) return;
        s.Element.Location = new GtPoint(s.Element.Location.X + dx, s.Element.Location.Y + dy);
        InvalidateVisual();
        TransformLive?.Invoke(this, EventArgs.Empty);
    }

    // ── geometry ────────────────────────────────────────────────────────

    private Rect FtBox() => ElementAbsBounds(_ft!.Layer, _ft.Element);

    /// <summary>the box's screen-space turn, the same angle the renderer rotates the object by</summary>
    private double FtAngle() => ScreenRz(_ft!.Element.RotateZ);

    private static Point Rotate(Point p, Point c, double angle)
    {
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        double x = p.X - c.X, y = p.Y - c.Y;
        return new Point(c.X + x * cos - y * sin, c.Y + x * sin + y * cos);
    }

    /// <summary>which handle (if any) is under <paramref name="docPt"/>, measured on the turned box</summary>
    private ResizeHandle FtHandleAt(Point docPt)
    {
        var box = FtBox();
        double angle = FtAngle();
        double reach = FtHandleReach / Zoom;
        foreach (var (pt, handle) in GetHandlePoints(box))
        {
            var p = Rotate(pt, box.Center, angle);
            if (Math.Abs(p.X - docPt.X) <= reach && Math.Abs(p.Y - docPt.Y) <= reach) return handle;
        }
        return ResizeHandle.None;
    }

    private bool FtInside(Point docPt)
    {
        var box = FtBox();
        return box.Contains(Rotate(docPt, box.Center, -FtAngle()));
    }

    // ── pointer ─────────────────────────────────────────────────────────

    /// <summary>takes every left press while transforming: a handle scales, inside moves, outside rotates, a double-click inside applies</summary>
    private bool FtPressed(PointerPressedEventArgs e, Point docPt)
    {
        if (_ft is null) return false;
        if (IsPreviewing) { CommitFreeTransform(); return false; }

        var handle = FtHandleAt(docPt);
        if (e.ClickCount >= 2 && handle == ResizeHandle.None && FtInside(docPt))
        {
            CommitFreeTransform();
            e.Handled = true;
            return true;
        }

        _ftStart = docPt;
        _ftStartBox = FtBox();
        _ftStartScreenRz = FtAngle();
        _ftStartLocation = _ft.Element.Location;
        if (handle != ResizeHandle.None) { _ftDrag = FreeTransformDrag.Scale; _ftHandle = handle; }
        else if (FtInside(docPt)) _ftDrag = FreeTransformDrag.Move;
        else
        {
            _ftDrag = FreeTransformDrag.Rotate;
            var c = _ftStartBox.Center;
            _ftStartAngle = Math.Atan2(docPt.Y - c.Y, docPt.X - c.X);
        }
        e.Pointer.Capture(this);
        e.Handled = true;
        return true;
    }

    private bool FtMoved(PointerEventArgs e, Point docPt)
    {
        if (_ft is not { } s) return false;
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt   = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        var el = s.Element;

        switch (_ftDrag)
        {
            case FreeTransformDrag.Move:
            {
                double nx = _ftStartLocation.X + docPt.X - _ftStart.X;
                double ny = _ftStartLocation.Y + docPt.Y - _ftStart.Y;
                if (SnapToPixelGrid) { nx = Math.Round(nx); ny = Math.Round(ny); }
                el.Location = new GtPoint(nx, ny);
                break;
            }
            case FreeTransformDrag.Scale:
                FtScale(docPt, shift, alt);
                break;
            case FreeTransformDrag.Rotate:
            {
                var c = _ftStartBox.Center;
                double now = Math.Atan2(docPt.Y - c.Y, docPt.X - c.X);
                double screen = _ftStartScreenRz + (now - _ftStartAngle);
                if (shift) screen = Math.Round(screen / FtRotateStep) * FtRotateStep;
                // ScreenRz is a plain sign flip, so the model angle is its negative, kept in (-180°, 180°]
                double rz = -screen;
                rz = Math.IEEERemainder(rz, 2 * Math.PI);
                if (Math.Abs(rz) < 1e-9) rz = 0;
                el.RotateZ = rz;
                Cursor = RotateCursorFor(docPt);
                break;
            }
            default:
                Cursor = FtHoverCursor(docPt);
                return true;
        }

        InvalidateVisual();
        TransformLive?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
        return true;
    }

    /// <summary>
    /// scales in the box's own turned frame: the pointer's travel is turned back into that frame, the box is rebuilt from
    /// the opposite side (or its centre with Option/Alt), and the new centre is turned back out so the opposite side stays
    /// put on screen. Corners keep the proportions unless Shift is held, as in Photoshop; side handles stretch one axis
    /// </summary>
    private void FtScale(Point docPt, bool shift, bool alt)
    {
        var s = _ft!;
        var box = _ftStartBox;
        var c = box.Center;
        double angle = _ftStartScreenRz;
        var d = Rotate(new Point(docPt.X - _ftStart.X, docPt.Y - _ftStart.Y), default, -angle);

        int sx = _ftHandle is ResizeHandle.NW or ResizeHandle.W or ResizeHandle.SW ? -1
               : _ftHandle is ResizeHandle.NE or ResizeHandle.E or ResizeHandle.SE ? 1 : 0;
        int sy = _ftHandle is ResizeHandle.NW or ResizeHandle.N or ResizeHandle.NE ? -1
               : _ftHandle is ResizeHandle.SW or ResizeHandle.S or ResizeHandle.SE ? 1 : 0;

        double w0 = Math.Max(box.Width, 1e-6), h0 = Math.Max(box.Height, 1e-6);
        double grow = alt ? 2 : 1;
        double w = sx == 0 ? w0 : w0 + grow * sx * d.X;
        double h = sy == 0 ? h0 : h0 + grow * sy * d.Y;

        if (sx != 0 && sy != 0 && !shift)
        {
            // proportional: follow whichever side the pointer has pulled further
            if (Math.Abs(w / w0) >= Math.Abs(h / h0)) h = h0 * w / w0;
            else                                      w = w0 * h / h0;
        }
        w = Math.Max(1, w);
        h = Math.Max(1, h);
        if (SnapToPixelGrid) { w = Math.Round(w); h = Math.Round(h); }

        // the fixed point, in the unturned frame: the opposite side or corner, or the centre with Alt; a side handle keeps the other axis centred
        double ax = alt || sx == 0 ? c.X : (sx > 0 ? box.Left : box.Right);
        double ay = alt || sy == 0 ? c.Y : (sy > 0 ? box.Top : box.Bottom);
        double left = alt || sx == 0 ? ax - w / 2 : (sx > 0 ? ax : ax - w);
        double top  = alt || sy == 0 ? ay - h / 2 : (sy > 0 ? ay : ay - h);
        var newCenter = Rotate(new Point(left + w / 2, top + h / 2), c, angle);

        double absX = newCenter.X - w / 2, absY = newCenter.Y - h / 2;
        double lx = absX - s.Layer.Location.X, ly = absY - s.Layer.Location.Y;
        if (SnapToPixelGrid) { lx = Math.Round(lx); ly = Math.Round(ly); }
        s.Element.Location   = new GtPoint(lx, ly);
        s.Element.Dimensions = new GtSize(w, h);
        Cursor = ArrowCursor(HandleAngle(_ftHandle) + FtAngle());
    }

    private bool FtReleased(PointerReleasedEventArgs e)
    {
        if (_ft is null || _ftDrag == FreeTransformDrag.None || e.InitialPressMouseButton != MouseButton.Left) return false;
        _ftDrag = FreeTransformDrag.None;
        e.Pointer.Capture(null);
        Cursor = FtHoverCursor(ToDocPoint(e.GetPosition(this)));
        InvalidateVisual();
        e.Handled = true;
        return true;
    }

    private Cursor FtHoverCursor(Point docPt)
    {
        var handle = FtHandleAt(docPt);
        if (handle != ResizeHandle.None) return ArrowCursor(HandleAngle(handle) + FtAngle());
        if (FtInside(docPt)) return Cursor.Default;
        return RotateCursorFor(docPt);
    }

    private Cursor RotateCursorFor(Point docPt)
    {
        var c = FtBox().Center;
        return RotateCursor(Math.Atan2(docPt.Y - c.Y, docPt.X - c.X));
    }

    // ── drawing ─────────────────────────────────────────────────────────

    /// <summary>the turned box with its eight handles and the centre reference point, as Photoshop draws it</summary>
    private void RenderFreeTransform(DrawingContext ctx)
    {
        if (_ft is null) return;
        var box = FtBox();
        var c = box.Center;
        var turn = Matrix.CreateTranslation(-c.X, -c.Y) * Matrix.CreateRotation(FtAngle()) * Matrix.CreateTranslation(c.X, c.Y);

        using (ctx.PushTransform(turn))
        {
            var line = new Pen(new SolidColorBrush(Color.FromArgb(235, 80, 160, 255)), 1.0 / Zoom);
            ctx.DrawRectangle(null, line, box);

            double size = 7.0 / Zoom, half = size / 2;
            var fill = Brushes.White;
            foreach (var (pt, _) in GetHandlePoints(box))
                ctx.DrawRectangle(fill, line, new Rect(pt.X - half, pt.Y - half, size, size));

            // reference point: the rotation centre
            double r = 4.0 / Zoom, arm = 7.0 / Zoom;
            var dark = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), 1.0 / Zoom);
            ctx.DrawEllipse(null, new Pen(Brushes.White, 2.5 / Zoom), c, r, r);
            ctx.DrawEllipse(null, dark, c, r, r);
            ctx.DrawLine(dark, new Point(c.X - arm, c.Y), new Point(c.X + arm, c.Y));
            ctx.DrawLine(dark, new Point(c.X, c.Y - arm), new Point(c.X, c.Y + arm));
        }
    }

    // ── cursors ─────────────────────────────────────────────────────────

    /// <summary>direction a handle pulls in, on the unturned box: 0 = horizontal, π/2 = vertical, π/4 = down-right diagonal</summary>
    private static double HandleAngle(ResizeHandle h) => h switch
    {
        ResizeHandle.E or ResizeHandle.W   => 0,
        ResizeHandle.N or ResizeHandle.S   => Math.PI / 2,
        ResizeHandle.NW or ResizeHandle.SE => Math.PI / 4,
        ResizeHandle.NE or ResizeHandle.SW => -Math.PI / 4,
        _                                  => 0,
    };

    private static readonly Dictionary<int, Cursor> _arrowCursors = new();
    private static readonly Dictionary<int, Cursor> _rotateCursors = new();

    /// <summary>buckets an angle into 10° steps, folded to a half turn when the cursor reads the same both ways</summary>
    private static int Bucket(double angle, int steps)
    {
        double turns = angle / (2 * Math.PI) * steps;
        return (int)(((Math.Round(turns) % steps) + steps) % steps);
    }

    /// <summary>Photoshop's double-headed resize arrow, turned to <paramref name="angle"/> (screen radians, 0 = horizontal)</summary>
    private static Cursor ArrowCursor(double angle)
    {
        int key = Bucket(angle, 36) % 18;   // a double arrow reads the same turned half way round
        if (_arrowCursors.TryGetValue(key, out var cached)) return cached;
        var shape = Geometry.Parse("M -9,0 L 9,0 M -9,0 L -5,-4 M -9,0 L -5,4 M 9,0 L 5,-4 M 9,0 L 5,4");
        return _arrowCursors[key] = MakeCursor(shape, key * Math.PI / 18, StandardCursorType.SizeAll);
    }

    /// <summary>Photoshop's curved rotate arrow, bent around the corner the pointer sits beyond (<paramref name="angle"/> = direction from the centre)</summary>
    private static Cursor RotateCursor(double angle)
    {
        int key = Bucket(angle, 36);
        if (_rotateCursors.TryGetValue(key, out var cached)) return cached;
        // a quarter arc bulging away from the centre (toward +x), with a solid arrowhead at each end pointing along the arc
        var arc   = Geometry.Parse("M 0,-8 A 10,10 0 0 1 0,8");
        var heads = Geometry.Parse("M -3.6,-10.6 L 2.2,-11 L -2.2,-5.2 Z M -3.6,10.6 L 2.2,11 L -2.2,5.2 Z");
        return _rotateCursors[key] = MakeCursor(arc, key * Math.PI / 18, StandardCursorType.Hand, heads);
    }

    /// <summary>a 32px cursor of <paramref name="shape"/> (drawn about 0,0) turned by <paramref name="angle"/>, black over a white halo like the system cursors, hotspot in the middle</summary>
    private static Cursor MakeCursor(Geometry shape, double angle, StandardCursorType fallback, Geometry? solid = null)
    {
        try
        {
            const int size = 32;
            var rtb = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
            using (var ctx = rtb.CreateDrawingContext())
            using (ctx.PushTransform(Matrix.CreateRotation(angle) * Matrix.CreateTranslation(size / 2.0, size / 2.0)))
            {
                ctx.DrawGeometry(null, new Pen(Brushes.White, 4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), shape);
                if (solid is not null)
                    ctx.DrawGeometry(Brushes.Black, new Pen(Brushes.White, 3, lineJoin: PenLineJoin.Round), solid);
                ctx.DrawGeometry(null, new Pen(Brushes.Black, 1.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), shape);
                if (solid is not null)
                    ctx.DrawGeometry(Brushes.Black, null, solid);
            }
            return new Cursor(rtb, new PixelPoint(size / 2, size / 2));
        }
        catch
        {
            return new Cursor(fallback);
        }
    }
}
