using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Avalonia.Media;
using GtPlus.Models;
using GtPlus.Services;

namespace GtPlus.Services.Psd;

/// <summary>what an import produced, for the status line</summary>
public sealed class PsdImportResult
{
    public required GtDocument Document { get; init; }
    public required GtAssetLibrary Assets { get; init; }
    public int Images { get; set; }
    public int Texts { get; set; }
    public int SkippedHidden { get; set; }
    public int SkippedEmpty { get; set; }

    /// <summary>Photoshop clipping masks carried over: layers that now carry a GT mask naming their base, or had a group base's shape cut into their pixels</summary>
    public int Clipped { get; set; }

    /// <summary>font families the PSD asks for that are not installed on this machine</summary>
    public SortedSet<string> MissingFonts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Warnings { get; } = new();

    /// <summary>Photoshop's flattened preview of the document as a PNG, for the editor's reference overlay; never part of the title</summary>
    public byte[]? ReferencePng { get; set; }

    /// <summary>true when the canvas is one of the sizes vMix's GT Title Designer accepts</summary>
    public bool StandardSize { get; set; } = true;
}

/// <summary>
/// turns a Photoshop document into a GT title the way vMix GT Title Designer's own PSD import does (its PSDImport helper):
/// <list type="bullet">
/// <item>every top-level group becomes a GT layer named after the group and cropped to its contents; runs of loose layers between groups share a layer, the first of them the document's default one</item>
/// <item>a type layer becomes an editable text object (text, font, size, weight, italic, alignment, underline, strikethrough, colour, paragraph box)</item>
/// <item>any other layer with pixels becomes a rectangle filled with its picture, clipped to the canvas</item>
/// <item>locked, visible and opacity carry over; names lose their dots and are cut to 15 characters, then made unique</item>
/// </list>
/// Where GT's importer loses what the designer sees, this one keeps it: nested groups are flattened into their top-level group
/// rather than dropped, hidden groups stay hidden, group and fill opacity are multiplied in, layer masks are applied, point
/// text is placed on its baseline rather than on the top of its glyphs, and any canvas size is accepted (with a warning when
/// vMix would refuse it). Blend modes, layer styles and adjustment layers are not carried over, as in GT
/// </summary>
public static class PsdImporter
{
    /// <summary>the canvas sizes GT Title Designer's importer accepts</summary>
    public static readonly (int W, int H)[] StandardSizes =
    {
        (1920, 1080), (1280, 720), (3840, 2160), (3412, 1920), (4096, 2160), (7680, 4320), (720, 576), (768, 576), (720, 480),
    };

    private const int MaxNameLength = 15;

    public static PsdImportResult Import(string path)
    {
        var psd = PsdReader.Read(path);
        return Import(psd, Path.GetFileNameWithoutExtension(path));
    }

    public static PsdImportResult Import(PsdFile psd, string documentName)
    {
        var doc = new GtDocument { Width = psd.Width, Height = psd.Height };
        var result = new PsdImportResult { Document = doc, Assets = new GtAssetLibrary() };
        result.Warnings.AddRange(psd.Warnings);
        result.StandardSize = StandardSizes.Contains((psd.Width, psd.Height));

        if (psd.Composite is { } composite && composite.Length == psd.Width * psd.Height * 4)
            result.ReferencePng = PngEncoder.Encode(composite, psd.Width, psd.Height);

        var ctx = new Context(psd, result, SafeAssetName(documentName));
        var root = BuildTree(psd.Layers);
        AssignClipBases(root);

        // GT starts from a document holding one empty layer and fills it with the first run of loose layers
        var defaultLayer = NewLayer(doc, ctx.UniqueName("Layer 1"));
        doc.Layers.Add(defaultLayer);
        GtLayer? current = defaultLayer;

        foreach (var node in root.Children)
        {
            if (node.Group is not null)
            {
                if (!node.Visible) { result.SkippedHidden += CountLeaves(node); continue; }

                var layer = NewLayer(doc, "");
                ImportInto(ctx, layer, node.Children, node.Opacity);
                if (layer.Elements.Count == 0) continue;

                layer.Name = ctx.UniqueName(FormatName(node.Name, "Group"));
                CropLayer(layer);
                doc.Layers.Add(layer);
                current = null;   // loose layers after a group start a layer of their own
            }
            else
            {
                if (current is null)
                {
                    current = NewLayer(doc, "");
                    doc.Layers.Add(current);
                }
                ImportInto(ctx, current, new[] { node }, 1.0);
            }
        }

        // name the loose-layer runs now that they exist; an empty default layer only stays if it is all there is
        foreach (var layer in doc.Layers.ToList())
        {
            if (layer.Elements.Count == 0 && doc.Layers.Count > 1) { doc.Layers.Remove(layer); continue; }
            if (string.IsNullOrEmpty(layer.Name)) layer.Name = ctx.UniqueName("Layer");
        }

        return result;
    }

    // ── layer tree ──────────────────────────────────────────────────────

    private sealed class Node
    {
        public PsdLayer? Layer;     // a leaf
        public PsdLayer? Group;     // a folder record
        public List<Node> Children { get; } = new();
        public string Name => (Group ?? Layer)?.Name ?? "";
        public bool Visible => (Group ?? Layer)?.Visible ?? true;
        public double Opacity => ((Group ?? Layer)?.Opacity ?? 255) / 255.0;

        /// <summary>for a layer clipped in Photoshop (Alt-click between layers), the layer or group it is clipped to: the nearest unclipped sibling below it</summary>
        public Node? ClipBase;
        /// <summary>what this node became, so layers clipped to it can name it as their mask</summary>
        public GtElement? Element;
        public GtLayer? ElementLayer;
    }

    /// <summary>
    /// finds every clipping group: a clipped layer is cut to the nearest unclipped layer or group below it in the same group,
    /// however many clipped layers are stacked on that base. A clipped layer with nothing under it is drawn whole, as Photoshop does
    /// </summary>
    private static void AssignClipBases(Node parent)
    {
        Node? lastBase = null;
        foreach (var child in parent.Children)
        {
            if (child.Layer is { Clipped: true }) child.ClipBase = lastBase;
            else lastBase = child;
            if (child.Group is not null) AssignClipBases(child);
        }
    }

    /// <summary>rebuilds the group hierarchy from Photoshop's flat list: reading bottom to top, an end marker opens a group and its folder record closes it</summary>
    private static Node BuildTree(List<PsdLayer> layers)
    {
        var root = new Node();
        var stack = new Stack<Node>();
        stack.Push(root);

        foreach (var layer in layers)
        {
            switch (layer.Section)
            {
                case PsdSection.GroupEnd:
                {
                    var group = new Node();
                    stack.Peek().Children.Add(group);
                    stack.Push(group);
                    break;
                }
                case PsdSection.OpenFolder:
                case PsdSection.ClosedFolder:
                    if (stack.Count > 1) stack.Pop().Group = layer;
                    else root.Children.Add(new Node { Group = layer });   // a folder with no end marker: an empty group
                    break;
                default:
                    stack.Peek().Children.Add(new Node { Layer = layer });
                    break;
            }
        }

        // a file whose markers do not balance still imports: any group left open simply keeps what it collected
        while (stack.Count > 1)
        {
            var open = stack.Pop();
            open.Group ??= new PsdLayer { Name = "Group" };
        }
        return root;
    }

    private static int CountLeaves(Node node) =>
        node.Layer is not null ? 1 : node.Children.Sum(CountLeaves);

    // ── objects ─────────────────────────────────────────────────────────

    private sealed class Context
    {
        public Context(PsdFile psd, PsdImportResult result, string assetBase)
        {
            Psd = psd;
            Result = result;
            AssetBase = assetBase;
        }

        public PsdFile Psd { get; }
        public PsdImportResult Result { get; }
        public string AssetBase { get; }
        public int AssetIndex;
        private readonly Dictionary<Node, byte[]> _groupAlpha = new();

        /// <summary>
        /// a group's combined coverage on the canvas (one byte per canvas pixel), the shape a layer clipped to it is cut to: every
        /// visible layer inside it, with its opacity, merged the way alpha composites (a over b = a + b - ab)
        /// </summary>
        public byte[] GroupAlpha(Node group)
        {
            if (_groupAlpha.TryGetValue(group, out var cached)) return cached;
            int W = Psd.Width, H = Psd.Height;
            var cover = new float[W * H];
            void Add(Node n, double opacity)
            {
                if (!n.Visible) return;
                if (n.Group is not null)
                {
                    foreach (var c in n.Children) Add(c, opacity * n.Opacity);
                    return;
                }
                var l = n.Layer!;
                if (l.Pixels is null || n.ClipBase is not null) return;   // clipped layers never reach outside their base
                double op = opacity * l.Opacity / 255.0 * l.FillOpacity / 255.0;
                int x0 = Math.Max(l.Left, 0), y0 = Math.Max(l.Top, 0), x1 = Math.Min(l.Right, W), y1 = Math.Min(l.Bottom, H);
                for (int y = y0; y < y1; y++)
                {
                    int src = ((y - l.Top) * l.Width + (x0 - l.Left)) * 4 + 3, dst = y * W + x0;
                    for (int x = x0; x < x1; x++, src += 4, dst++)
                    {
                        float a = (float)(l.Pixels[src] / 255.0 * op);
                        cover[dst] = cover[dst] + a - cover[dst] * a;
                    }
                }
            }
            foreach (var c in group.Children) Add(c, group.Opacity);
            var alpha = new byte[W * H];
            for (int i = 0; i < alpha.Length; i++) alpha[i] = (byte)Math.Round(Math.Clamp(cover[i], 0, 1) * 255);
            _groupAlpha[group] = alpha;
            return alpha;
        }
        private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>the name itself while it is free, otherwise the first free name with a number on the end; names have to be unique across the whole document because animations find their object by name</summary>
        public string UniqueName(string name)
        {
            if (_names.Add(name)) return name;
            var unique = NameService.UniqueName(_names, name, 2);
            _names.Add(unique);
            return unique;
        }
    }

    private static void ImportInto(Context ctx, GtLayer layer, IEnumerable<Node> nodes, double inheritedOpacity)
    {
        foreach (var node in nodes)
        {
            if (!node.Visible) { ctx.Result.SkippedHidden += CountLeaves(node); continue; }

            // a nested group is flattened into the layer of its top-level group; its opacity carries down to what is inside
            if (node.Group is not null)
            {
                ImportInto(ctx, layer, node.Children, inheritedOpacity * node.Opacity);
                continue;
            }

            var psdLayer = node.Layer!;
            var opacity = inheritedOpacity * psdLayer.Opacity / 255.0 * psdLayer.FillOpacity / 255.0;
            bool isText = psdLayer.Text is not null && !string.IsNullOrEmpty(psdLayer.Text.Content);

            // a clipped layer only shows where its base does: a hidden base hides it, an empty one leaves nothing to show through
            var clipBase = node.ClipBase;
            if (clipBase is not null)
            {
                if (!clipBase.Visible) { ctx.Result.SkippedHidden++; continue; }
                if (clipBase.Layer is not null && clipBase.Element is null) { ctx.Result.SkippedEmpty++; continue; }
            }

            // a base that is a group has no single object a GT mask could name, so its combined shape is cut into the pixels
            byte[]? groupAlpha = null;
            if (clipBase?.Group is not null)
            {
                if (isText) ctx.Result.Warnings.Add($"'{psdLayer.Name}' is clipped to the group '{clipBase.Name}'; text cannot be cut to a group, so it imports unclipped");
                else groupAlpha = ctx.GroupAlpha(clipBase);
            }

            GtElement? element = isText
                ? MakeText(ctx, psdLayer, psdLayer.Text!)
                : MakeImage(ctx, psdLayer, groupAlpha);

            if (element is null) { ctx.Result.SkippedEmpty++; continue; }

            element.Name    = ctx.UniqueName(FormatName(psdLayer.Name, element is GtTextBlock ? "Text" : "Image"));
            element.Locked  = psdLayer.Locked;
            element.Visible = true;
            element.Opacity = Math.Round(Math.Clamp(opacity, 0, 1), 4);

            // elements are layer-local; a fresh layer sits at the origin, CropLayer moves things afterwards
            element.Location = new GtPoint(element.Location.X - layer.Location.X, element.Location.Y - layer.Location.Y);
            layer.Elements.Add(element);
            node.Element = element;
            node.ElementLayer = layer;

            if (clipBase is not null)
            {
                // GT's own mask: the base object is the mask, drawn as itself, so its pixels, glyphs and opacity cut this one the
                // way Photoshop's clipping does, and it keeps cutting if the base is moved, edited or animated
                if (clipBase.Element is { } baseElement && ReferenceEquals(clipBase.ElementLayer, layer))
                {
                    element.MaskObject = baseElement.Name;
                    ctx.Result.Clipped++;
                }
                else if (groupAlpha is not null)
                {
                    ctx.Result.Clipped++;
                }
            }
        }
    }

    /// <summary>a pixel layer as GT imports one: a rectangle whose fill is the layer's picture, clipped to the canvas and trimmed of fully transparent edges</summary>
    private static GtElement? MakeImage(Context ctx, PsdLayer layer, byte[]? clipAlpha = null)
    {
        if (layer.Pixels is null || layer.Width <= 0 || layer.Height <= 0) return null;

        // cut to a clipping group's shape first, so the trim below tightens to what is left
        var pixels = layer.Pixels;
        if (clipAlpha is not null)
        {
            pixels = (byte[])pixels.Clone();
            for (int y = Math.Max(layer.Top, 0); y < Math.Min(layer.Bottom, ctx.Psd.Height); y++)
                for (int x = Math.Max(layer.Left, 0); x < Math.Min(layer.Right, ctx.Psd.Width); x++)
                {
                    int i = ((y - layer.Top) * layer.Width + (x - layer.Left)) * 4 + 3;
                    pixels[i] = (byte)((pixels[i] * clipAlpha[y * ctx.Psd.Width + x] + 127) / 255);
                }
        }

        // the part of the layer that lies on the canvas
        int x0 = Math.Max(layer.Left, 0), y0 = Math.Max(layer.Top, 0);
        int x1 = Math.Min(layer.Right, ctx.Psd.Width), y1 = Math.Min(layer.Bottom, ctx.Psd.Height);
        if (x1 <= x0 || y1 <= y0) return null;

        // then the smallest box holding every pixel that is not fully transparent
        int w = layer.Width;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = y0; y < y1; y++)
        {
            int row = (y - layer.Top) * w;
            for (int x = x0; x < x1; x++)
            {
                if (pixels[(row + x - layer.Left) * 4 + 3] == 0) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        if (maxX < 0) return null;

        int cw = maxX - minX + 1, ch = maxY - minY + 1;
        var crop = new byte[cw * ch * 4];
        for (int y = 0; y < ch; y++)
            Buffer.BlockCopy(pixels, ((minY + y - layer.Top) * w + (minX - layer.Left)) * 4, crop, y * cw * 4, cw * 4);

        var path = $"images/{ctx.AssetBase}_{++ctx.AssetIndex}.png";
        ctx.Result.Assets[path] = PngEncoder.Encode(crop, cw, ch);
        ctx.Result.Images++;

        return new GtRectangleElement
        {
            Location   = new GtPoint(minX, minY),
            Dimensions = new GtSize(cw, ch),
            Fill       = new GtBrush { Type = GtBrushType.Bitmap, BitmapSource = path },
            StrokeThickness = 0,
        };
    }

    /// <summary>a type layer as an editable text object</summary>
    private static GtElement MakeText(Context ctx, PsdLayer layer, PsdText t)
    {
        // the text transform (x' = xx*x + yx*y + tx, y' = xy*x + yy*y + ty) is split the way GT can draw it: a rotation (the
        // object's RotateZ), a slant (GT's own Skew effect) and a scale (folded into the font size). Photoshop's faux italic
        // is a 10° slant of its own, so it is added to the slant rather than left to the renderer's synthetic italic
        var m = t.Transform;
        var xf = TextTransform.Decompose(m[0], m[1], m[2], m[3]);
        double scaleX = xf.ScaleX, scaleY = xf.ScaleY;
        double shear  = xf.Shear + (t.FauxItalic ? -TextTransform.FauxItalicShear * scaleX / scaleY : 0);
        bool transformed = Math.Abs(xf.Rotation) > 1e-3 || Math.Abs(shear) > 1e-3;

        var font = FontMatcher.Resolve(t.FontPostScriptName);
        if (!font.Installed && !string.IsNullOrEmpty(font.Family)) ctx.Result.MissingFonts.Add(font.Family);

        var tb = new GtTextBlock
        {
            // Photoshop ends a paragraph with \r and a forced line break with \u0003; GT text breaks lines with \r\n
            Text          = t.Content.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\u0003', '\n').Replace("\n", "\r\n"),
            FontFamily    = font.Family.Length > 0 ? font.Family : "Arial",
            FontSize      = Math.Round(t.FontSize * scaleY, 2),
            FontWeight    = t.FauxBold && font.Weight < FontWeight.Bold ? FontWeight.Bold : font.Weight,
            FontStyle     = font.Italic ? FontStyle.Italic : FontStyle.Normal,
            Underline     = t.Underline,
            Strikethrough = t.Strikethrough,
            Uppercase     = t.FontCaps == 2,
            IgnoreOverhang = true,
            TextAlign = t.Justification switch
            {
                1 => GtTextAlign.Right,
                2 => GtTextAlign.Center,
                _ => GtTextAlign.Left,
            },
            Fill = new GtBrush
            {
                Type  = GtBrushType.Solid,
                Color = Color.FromArgb(ToByte(t.Color[0]), ToByte(t.Color[1]), ToByte(t.Color[2]), ToByte(t.Color[3])),
            },
        };
        if (tb.Uppercase) tb.Text = tb.Text.ToUpperInvariant();

        // an explicit leading is an absolute line height; Photoshop's Auto leading is left to the font's own line spacing
        if (!t.AutoLeading && t.Leading > 0) tb.LineSpacing = Math.Round(t.Leading * scaleY, 2);

        double size = tb.FontSize;
        var (ascent, descent) = FontMatcher.Metrics(tb.FontFamily, tb.FontWeight, tb.FontStyle, size);
        int lines = Math.Max(1, tb.Text.Split('\n').Length);
        double lineHeight = tb.LineSpacing > 2 ? tb.LineSpacing : ascent + descent;

        // GT draws the object's own picture slanted (Skew, about the box centre) and then turns it (RotateZ, about the centre
        // too): together the transform T = R·S, with the scale already in the font size
        if (transformed)
        {
            // the split gives a clockwise-on-screen angle; vMix turns a positive RotateZ counterclockwise
            tb.RotateZ = Math.Round(-xf.Rotation, 6);
            // GT's skew X is positive for a forward (italic) lean, which is a negative shear here
            if (Math.Abs(shear) > 1e-3) GtSkew.Set(tb, Math.Round(-Math.Atan(shear) * 180 / Math.PI, 3), 0);
        }
        (double X, double Y) T(double x, double y)
        {
            double cs = Math.Cos(xf.Rotation), sn = Math.Sin(xf.Rotation);
            double sx = x + shear * y;
            return (cs * sx - sn * y, sn * sx + cs * y);
        }

        if (t.IsBox && t.BoxBounds is { } b)
        {
            // paragraph text: the box itself, mapped through the transform, and the text wraps inside it as in Photoshop
            double w = (b[2] - b[0]) * scaleX, h = (b[3] - b[1]) * scaleY;
            tb.Dimensions = new GtSize(Math.Round(w, 2), Math.Round(h, 2));
            tb.AutoSize   = GtAutoSize.Fixed;

            // Photoshop sets a paragraph's first baseline one ascent below the top of its box; where this editor's layout
            // puts it differs by the line box's own padding, so the box moves by the difference and the text lands in place
            double dy = Controls.GtCanvasControl.FirstLineGeometry(tb) is { } para ? ascent - para.Baseline : 0;

            // the box centre in the document is where the transform puts the centre of Photoshop's box; the baseline nudge is
            // a move in the box's own (slanted, turned) frame
            double pcx = (b[0] + b[2]) / 2, pcy = (b[1] + b[3]) / 2;
            double cx = m[0] * pcx + m[2] * pcy + m[4], cy = m[1] * pcx + m[3] * pcy + m[5];
            var nudge = T(0, dy);
            tb.Location = new GtPoint(Math.Round(cx + nudge.X - w / 2, 2), Math.Round(cy + nudge.Y - h / 2, 2));

            ctx.Result.Texts++;
            return tb;
        }

        // point text: the origin is the first baseline, at the left edge, centre or right edge of the line depending on the
        // alignment. The box is as wide as the rendered glyphs plus GT's 8px of slack and grows with the text, except when centred
        double width = Math.Max(layer.Width, 1) + 8;
        tb.Dimensions = new GtSize(Math.Round(width, 2), Math.Round(ascent + descent + (lines - 1) * lineHeight, 2));
        tb.AutoSize   = tb.TextAlign == GtTextAlign.Center ? GtAutoSize.Fixed : GtAutoSize.Width;
        tb.NoWrap     = true;

        // a turned or slanted layer's pixel bounds are wider than its text, so the width comes from laying the text out
        if (transformed)
        {
            tb.Dimensions = new GtSize(100000, tb.Dimensions.Height);
            if (Controls.GtCanvasControl.FirstLineGeometry(tb) is { } wide)
            {
                width = Math.Ceiling(wide.LineWidth) + 8;
                tb.Dimensions = new GtSize(width, tb.Dimensions.Height);
            }
        }

        // ask the editor's own text layout where it puts the first line inside that box, then move the box so that line's
        // anchor sits on Photoshop's origin; font metrics alone would leave the text a few pixels off
        double anchorX, baseline;
        if (Controls.GtCanvasControl.FirstLineGeometry(tb) is { } first)
        {
            baseline = first.Baseline;
            anchorX = tb.TextAlign switch
            {
                GtTextAlign.Center => first.LineLeft + first.LineWidth / 2,
                GtTextAlign.Right  => first.LineLeft + first.LineWidth,
                _                  => first.LineLeft,
            };
        }
        else
        {
            baseline = ascent;
            anchorX = tb.TextAlign switch
            {
                GtTextAlign.Center => width / 2,
                GtTextAlign.Right  => width,
                _                  => 0,
            };
        }

        // the anchor has to land on Photoshop's origin after GT turns and slants the box about its centre:
        // L = origin - size/2 - T(anchor - size/2), which is just origin - anchor when there is no transform
        double hw = tb.Dimensions.Width / 2, hh = tb.Dimensions.Height / 2;
        var off = T(anchorX - hw, baseline - hh);
        tb.Location = new GtPoint(Math.Round(m[4] - hw - off.X, 2), Math.Round(m[5] - hh - off.Y, 2));
        ctx.Result.Texts++;
        return tb;
    }

    private static byte ToByte(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);

    // ── layers ──────────────────────────────────────────────────────────

    private static GtLayer NewLayer(GtDocument doc, string name) => new()
    {
        Name        = name,
        Location    = GtPoint.Zero,
        Dimensions  = new GtSize(doc.Width, doc.Height),
        InnerWidth  = doc.Width,
        InnerHeight = doc.Height,
    };

    /// <summary>GT's CropLayer: shrinks a group's layer to the box around what it holds and moves its contents to match, so the layer can be animated as one object</summary>
    private static void CropLayer(GtLayer layer)
    {
        if (layer.Elements.Count == 0) return;

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var el in layer.Elements)
        {
            minX = Math.Min(minX, el.Location.X);
            minY = Math.Min(minY, el.Location.Y);
            maxX = Math.Max(maxX, el.Location.X + el.Dimensions.Width);
            maxY = Math.Max(maxY, el.Location.Y + el.Dimensions.Height);
        }

        minX = Math.Floor(minX); minY = Math.Floor(minY);
        maxX = Math.Ceiling(maxX); maxY = Math.Ceiling(maxY);

        layer.Location    = new GtPoint(layer.Location.X + minX, layer.Location.Y + minY);
        layer.Dimensions  = new GtSize(maxX - minX, maxY - minY);
        layer.InnerWidth  = layer.Dimensions.Width;
        layer.InnerHeight = layer.Dimensions.Height;
        foreach (var el in layer.Elements)
            el.Location = new GtPoint(el.Location.X - minX, el.Location.Y - minY);
    }

    /// <summary>GT's rule: no dots, at most 15 characters</summary>
    private static string FormatName(string name, string fallback)
    {
        var n = name.Replace(".", "").Trim();
        if (n.Length > MaxNameLength) n = n.Substring(0, MaxNameLength).TrimEnd();
        return n.Length == 0 ? fallback : n;
    }

    private static string SafeAssetName(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name)
            sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_');
        var s = sb.ToString().Trim('_');
        return s.Length == 0 ? "psd" : s;
    }
}

/// <summary>splits a type layer's 2×2 transform into what GT can express: rotation, X slant and scale</summary>
internal static class TextTransform
{
    /// <summary>Photoshop's faux italic slant, measured on its own render: tan 10°</summary>
    public static readonly double FauxItalicShear = Math.Tan(10 * Math.PI / 180);

    /// <summary>
    /// QR split of [[xx, yx], [xy, yy]] (columns are where the text's x and y axes go) into R(rotation) · Shear · Scale:
    /// <c>Shear</c> is the x += k·y slant (negative leans the top right, like italic), rotation is in radians, clockwise
    /// on screen
    /// </summary>
    public static (double Rotation, double Shear, double ScaleX, double ScaleY) Decompose(double xx, double xy, double yx, double yy)
    {
        double sx = Math.Sqrt(xx * xx + xy * xy);
        if (sx <= 1e-9) return (0, 0, 1, 1);
        double rot = Math.Atan2(xy, xx);
        double c = Math.Cos(rot), s = Math.Sin(rot);
        double m  = yx * c + yy * s;        // part of the y axis along the turned x axis
        double sy = -yx * s + yy * c;       // the rest, the true vertical scale
        if (Math.Abs(sy) <= 1e-9) return (rot, 0, sx, sx);
        return (rot, m / sy, sx, sy);
    }
}

/// <summary>maps a PostScript font name from a PSD ("Montserrat-SemiBoldItalic", "ArialMT", "HelveticaNeue-Bold") to an installed family, weight and style</summary>
internal static class FontMatcher
{
    public readonly record struct Match(string Family, FontWeight Weight, bool Italic, bool Installed);

    private static Dictionary<string, string>? _installed;

    // longest first so "ExtraBold" is not read as "Bold"
    private static readonly (string Word, FontWeight Weight)[] Weights =
    {
        ("extrablack", (FontWeight)950), ("ultrablack", (FontWeight)950),
        ("extralight", FontWeight.ExtraLight), ("ultralight", FontWeight.ExtraLight),
        ("extrabold", FontWeight.ExtraBold), ("ultrabold", FontWeight.ExtraBold),
        ("semibold", FontWeight.SemiBold), ("demibold", FontWeight.SemiBold), ("semilight", FontWeight.SemiLight),
        ("hairline", FontWeight.Thin), ("medium", FontWeight.Medium), ("regular", FontWeight.Normal), ("normal", FontWeight.Normal),
        ("roman", FontWeight.Normal), ("heavy", FontWeight.ExtraBold), ("black", FontWeight.Black), ("light", FontWeight.Light),
        ("thin", FontWeight.Thin), ("book", (FontWeight)325), ("bold", FontWeight.Bold), ("demi", FontWeight.SemiBold),
    };

    private static readonly string[] VendorSuffixes = { "psmt", "mt", "ps", "std", "pro", "lt", "com" };

    public static Match Resolve(string postScriptName)
    {
        if (string.IsNullOrWhiteSpace(postScriptName)) return new Match("", FontWeight.Normal, false, false);

        // the exact installed face first: its PostScript name is what the PSD stores, and its own family name and true
        // weight are what vMix will look the font up by
        if (FontFaces.ByPostScriptName(postScriptName) is { } exact)
            return new Match(exact.Family, FontFaces.GtWeight(exact), exact.Italic, true);

        // otherwise split the name into family and style. A family can itself contain dashes ("GFF-Latin-Black" is the
        // family "GFF Latin"), so the longest dash-separated prefix that names an installed family wins
        var installedFamilies = InstalledFamilies();
        int dash = postScriptName.IndexOf('-');
        for (int d = postScriptName.LastIndexOf('-'); d > 0; d = postScriptName.LastIndexOf('-', d - 1))
            if (installedFamilies.ContainsKey(Normalize(postScriptName[..d]))) { dash = d; break; }
        string familyPart = dash > 0 ? postScriptName[..dash] : postScriptName;
        string stylePart  = dash > 0 ? postScriptName[(dash + 1)..] : "";

        string style = Normalize(stylePart);
        var weight = FontWeight.Normal;
        foreach (var (word, w) in Weights)
            if (style.Contains(word)) { weight = w; break; }
        bool italic = style.Contains("italic") || style.Contains("oblique") || style.EndsWith("it");

        var installed = InstalledFamilies();
        string key = Normalize(familyPart);
        if (installed.TryGetValue(key, out var family)) return new Match(family, weight, italic, true);

        // vendor tails: ArialMT, TimesNewRomanPSMT, MinionPro
        foreach (var suffix in VendorSuffixes)
            if (key.EndsWith(suffix) && installed.TryGetValue(key[..^suffix.Length], out family))
                return new Match(family, weight, italic, true);

        // a family whose own name carries the weight ("RobotoCondensed-Bold" is installed as "Roboto Condensed")
        foreach (var (word, w) in Weights)
            if (key.EndsWith(word) && installed.TryGetValue(key[..^word.Length], out family))
                return new Match(family, weight == FontWeight.Normal ? w : weight, italic, true);

        return new Match(SplitCamel(familyPart.Replace('-', ' ')), weight, italic, false);
    }

    /// <summary>ascent and descent in pixels for a family at <paramref name="size"/>, from the font's own metrics</summary>
    public static (double Ascent, double Descent) Metrics(string family, FontWeight weight, FontStyle style, double size)
    {
        try
        {
            var face = FontFaces.Typeface(family, style, weight).GlyphTypeface;
            double em = face.Metrics.DesignEmHeight;
            if (em > 0)
                return (-face.Metrics.Ascent * size / em, face.Metrics.Descent * size / em);
        }
        catch
        {
            // a missing font falls through to typical proportions
        }
        return (size * 0.905, size * 0.212);
    }

    private static Dictionary<string, string> InstalledFamilies()
    {
        if (_installed is not null) return _installed;
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var f in FontManager.Current.SystemFonts)
                map.TryAdd(Normalize(f.Name), f.Name);
        }
        catch
        {
            // no font manager (a test without a platform): every font reads as missing
        }
        return _installed = map;
    }

    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    /// <summary>"HelveticaNeue" -> "Helvetica Neue", for a font that is not installed so at least its name reads naturally</summary>
    private static string SplitCamel(string s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i]) && char.IsLower(s[i - 1])) sb.Append(' ');
            sb.Append(s[i]);
        }
        var name = sb.ToString();
        foreach (var suffix in new[] { "PSMT", "MT" })
            if (name.EndsWith(suffix, StringComparison.Ordinal)) name = name[..^suffix.Length];
        return name.Trim();
    }
}

/// <summary>a minimal PNG writer for straight RGBA, so imported layers are stored losslessly without going through the UI toolkit</summary>
internal static class PngEncoder
{
    public static byte[] Encode(byte[] rgba, int width, int height)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        var ihdr = new byte[13];
        WriteBE(ihdr, 0, width);
        WriteBE(ihdr, 4, height);
        ihdr[8] = 8;    // bit depth
        ihdr[9] = 6;    // RGBA
        Chunk(ms, "IHDR", ihdr);

        using (var raw = new MemoryStream())
        {
            using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
            {
                int stride = width * 4;
                for (int y = 0; y < height; y++)
                {
                    z.WriteByte(0);   // filter: none
                    z.Write(rgba, y * stride, stride);
                }
            }
            Chunk(ms, "IDAT", raw.ToArray());
        }

        Chunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBE(len, 0, data.Length);
        s.Write(len);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);

        uint crc = Crc32(typeBytes, 0xFFFFFFFF);
        crc = Crc32(data, crc) ^ 0xFFFFFFFF;
        var c = new byte[4];
        WriteBE(c, 0, (int)crc);
        s.Write(c);
    }

    private static void WriteBE(byte[] b, int o, int v)
    {
        b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
    }

    private static uint[]? _table;

    private static uint Crc32(byte[] data, uint crc)
    {
        _table ??= BuildTable();
        foreach (var b in data) crc = _table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildTable()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }
}
