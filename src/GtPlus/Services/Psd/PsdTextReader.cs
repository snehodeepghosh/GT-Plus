using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GtPlus.Services.Psd;

/// <summary>
/// reads a type layer's 'TySh' block: the text transform, the text descriptor (which carries the string itself) and the
/// EngineData inside it, a PostScript-like dictionary holding the fonts, style runs and paragraph settings Photoshop lays the
/// text out with. Only the first style run and paragraph run are used, as GT's own importer does
/// </summary>
internal static class PsdTextReader
{
    public static PsdText ReadTypeTool(BigEndianReader r, long end)
    {
        var text = new PsdText();

        int version = r.ReadUInt16();
        if (version != 1) throw new InvalidDataException($"type tool version {version}");

        var t = new double[6];
        for (int i = 0; i < 6; i++) t[i] = r.ReadDouble();
        text.Transform = t;

        r.ReadUInt16();   // text version (50)
        r.ReadUInt32();   // descriptor version (16)
        var desc = PsdDescriptor.Read(r);

        if (desc.TryGetValue("Txt ", out var txt) && txt is string s)
            text.Content = s.TrimEnd('\0');

        if (desc.TryGetValue("EngineData", out var ed) && ed is byte[] engine)
            ApplyEngineData(text, EngineDataParser.Parse(engine));

        return text;
    }

    private static void ApplyEngineData(PsdText text, Dictionary<string, object?> root)
    {
        var engine = Dict(root, "EngineDict");
        var resources = Dict(root, "ResourceDict");

        // defaults first: the document's normal style sheet, which a style run only overrides where it differs
        var fonts = List(resources, "FontSet");
        Dictionary<string, object?>? defaults = null;
        var sheets = List(resources, "StyleSheetSet");
        if (sheets is { Count: > 0 } && sheets[0] is Dictionary<string, object?> sheet0)
            defaults = Dict(sheet0, "StyleSheetData");

        Dictionary<string, object?>? run = null;
        var runs = List(Dict(engine, "StyleRun"), "RunArray");
        if (runs is { Count: > 0 } && runs[0] is Dictionary<string, object?> run0)
            run = Dict(Dict(run0, "StyleSheet"), "StyleSheetData");

        object? Style(string key) =>
            run is not null && run.TryGetValue(key, out var v) ? v :
            defaults is not null && defaults.TryGetValue(key, out var d) ? d : null;

        text.FontSize      = Num(Style("FontSize"), 12);
        text.AutoLeading   = Bool(Style("AutoLeading"), true);
        text.Leading       = Num(Style("Leading"), 0);
        text.FauxBold      = Bool(Style("FauxBold"), false);
        text.FauxItalic    = Bool(Style("FauxItalic"), false);
        text.Underline     = Bool(Style("Underline"), false);
        text.Strikethrough = Bool(Style("Strikethrough"), false);
        text.FontCaps      = (int)Num(Style("FontCaps"), 0);

        if (Style("FillColor") is Dictionary<string, object?> fill && List(fill, "Values") is { Count: >= 4 } values)
            text.Color = new[] { Num(values[0], 1), Num(values[1], 0), Num(values[2], 0), Num(values[3], 0) };

        int fontIndex = (int)Num(Style("Font"), 0);
        if (fonts is not null && fontIndex >= 0 && fontIndex < fonts.Count &&
            fonts[fontIndex] is Dictionary<string, object?> font && font.TryGetValue("Name", out var fname) && fname is string name)
            text.FontPostScriptName = name;

        var paras = List(Dict(engine, "ParagraphRun"), "RunArray");
        if (paras is { Count: > 0 } && paras[0] is Dictionary<string, object?> para0)
        {
            var props = Dict(Dict(para0, "ParagraphSheet"), "Properties");
            text.Justification = (int)Num(props?.GetValueOrDefault("Justification"), 0);
        }

        // paragraph text keeps its box in the rendered shape description: ShapeType 1 with BoxBounds, point text is ShapeType 0
        var shapes = List(Dict(Dict(engine, "Rendered"), "Shapes"), "Children");
        if (shapes is { Count: > 0 } && shapes[0] is Dictionary<string, object?> shape0)
        {
            var photoshop = Dict(Dict(shape0, "Cookie"), "Photoshop");
            if (photoshop is not null && (int)Num(photoshop.GetValueOrDefault("ShapeType"), 0) == 1 &&
                List(photoshop, "BoxBounds") is { Count: >= 4 } box)
            {
                text.IsBox = true;
                text.BoxBounds = new[] { Num(box[0], 0), Num(box[1], 0), Num(box[2], 0), Num(box[3], 0) };
            }
        }
    }

    private static Dictionary<string, object?>? Dict(Dictionary<string, object?>? d, string key) =>
        d is not null && d.TryGetValue(key, out var v) ? v as Dictionary<string, object?> : null;

    private static List<object?>? List(Dictionary<string, object?>? d, string key) =>
        d is not null && d.TryGetValue(key, out var v) ? v as List<object?> : null;

    private static double Num(object? v, double fallback) => v switch
    {
        double d => d,
        int i    => i,
        bool b   => b ? 1 : 0,
        _        => fallback,
    };

    private static bool Bool(object? v, bool fallback) => v switch
    {
        bool b   => b,
        double d => d != 0,
        _        => fallback,
    };
}

/// <summary>Photoshop's "action descriptor" structure: a class id and a list of typed key/value items; returned as a dictionary keyed by item key</summary>
internal static class PsdDescriptor
{
    public static Dictionary<string, object?> Read(BigEndianReader r)
    {
        ReadUnicode(r);   // class name
        ReadId(r);        // class id
        int count = (int)r.ReadUInt32();
        var items = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            var key = ReadId(r);
            var type = r.ReadAscii(4);
            items[key] = ReadValue(r, type);
        }
        return items;
    }

    private static object? ReadValue(BigEndianReader r, string type)
    {
        switch (type)
        {
            case "Objc":
            case "GlbO":
                return Read(r);
            case "VlLs":
            {
                int n = (int)r.ReadUInt32();
                var list = new List<object?>(n);
                for (int i = 0; i < n; i++) list.Add(ReadValue(r, r.ReadAscii(4)));
                return list;
            }
            case "doub": return r.ReadDouble();
            case "UntF": r.Skip(4); return r.ReadDouble();
            case "UnFl":
            {
                r.Skip(4);
                int n = (int)r.ReadUInt32();
                var list = new List<object?>(n);
                for (int i = 0; i < n; i++) list.Add(r.ReadDouble());
                return list;
            }
            case "TEXT": return ReadUnicode(r);
            case "enum": ReadId(r); return ReadId(r);
            case "long": return r.ReadInt32();
            case "comp": return r.ReadInt64();
            case "bool": return r.ReadByte() != 0;
            case "type":
            case "GlbC": ReadUnicode(r); return ReadId(r);
            case "alis":
            case "tdta":
            case "Pth ":
            {
                long n = r.ReadUInt32();
                return r.ReadBytes(n);
            }
            case "obj ":
            {
                int n = (int)r.ReadUInt32();
                for (int i = 0; i < n; i++) ReadReferenceItem(r, r.ReadAscii(4));
                return null;
            }
            default:
                throw new InvalidDataException($"unknown descriptor type '{type}'");
        }
    }

    private static void ReadReferenceItem(BigEndianReader r, string type)
    {
        switch (type)
        {
            case "prop": ReadUnicode(r); ReadId(r); ReadId(r); break;
            case "Clss": ReadUnicode(r); ReadId(r); break;
            case "Enmr": ReadUnicode(r); ReadId(r); ReadId(r); ReadId(r); break;
            case "rele": ReadUnicode(r); ReadId(r); r.ReadUInt32(); break;
            case "Idnt": r.ReadUInt32(); break;
            case "indx": r.ReadUInt32(); break;
            case "name": ReadUnicode(r); ReadId(r); ReadUnicode(r); break;
            default: throw new InvalidDataException($"unknown reference type '{type}'");
        }
    }

    /// <summary>a class or key id: a length, or 0 meaning a 4-character code follows</summary>
    private static string ReadId(BigEndianReader r)
    {
        int len = (int)r.ReadUInt32();
        return r.ReadAscii(len == 0 ? 4 : len);
    }

    private static string ReadUnicode(BigEndianReader r)
    {
        int chars = (int)r.ReadUInt32();
        return Encoding.BigEndianUnicode.GetString(r.ReadBytes(chars * 2));
    }
}

/// <summary>parses EngineData, Photoshop's PostScript-flavoured text engine dictionary: &lt;&lt; /Key value &gt;&gt;, [ arrays ], (strings) in UTF-16 with a BOM, numbers and true/false</summary>
internal static class EngineDataParser
{
    public static Dictionary<string, object?> Parse(byte[] data)
    {
        int pos = 0;
        SkipSpace(data, ref pos);
        return ParseValue(data, ref pos) as Dictionary<string, object?> ?? new Dictionary<string, object?>();
    }

    private static object? ParseValue(byte[] d, ref int p)
    {
        SkipSpace(d, ref p);
        if (p >= d.Length) return null;

        if (d[p] == '<' && p + 1 < d.Length && d[p + 1] == '<')
        {
            p += 2;
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            while (true)
            {
                SkipSpace(d, ref p);
                if (p >= d.Length) break;
                if (d[p] == '>' && p + 1 < d.Length && d[p + 1] == '>') { p += 2; break; }
                if (d[p] != '/') { p++; continue; }
                var key = ReadName(d, ref p);
                dict[key] = ParseValue(d, ref p);
            }
            return dict;
        }

        if (d[p] == '[')
        {
            p++;
            var list = new List<object?>();
            while (true)
            {
                SkipSpace(d, ref p);
                if (p >= d.Length) break;
                if (d[p] == ']') { p++; break; }
                list.Add(ParseValue(d, ref p));
            }
            return list;
        }

        if (d[p] == '(') return ReadString(d, ref p);
        if (d[p] == '/') return ReadName(d, ref p);

        // a bare token: number, true/false, or anything else read as text
        int start = p;
        while (p < d.Length && !IsDelimiter(d[p])) p++;
        var token = Encoding.ASCII.GetString(d, start, p - start);
        if (token == "true") return true;
        if (token == "false") return false;
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var num)) return num;
        if (p == start) p++;   // never stall on an unexpected byte
        return token;
    }

    private static string ReadName(byte[] d, ref int p)
    {
        p++;   // '/'
        int start = p;
        while (p < d.Length && !IsDelimiter(d[p])) p++;
        return Encoding.ASCII.GetString(d, start, p - start);
    }

    private static string ReadString(byte[] d, ref int p)
    {
        p++;   // '('
        var bytes = new List<byte>();
        while (p < d.Length)
        {
            byte b = d[p++];
            if (b == '\\' && p < d.Length) { bytes.Add(d[p++]); continue; }
            if (b == ')') break;
            bytes.Add(b);
        }
        var arr = bytes.ToArray();
        if (arr.Length >= 2 && arr[0] == 0xFE && arr[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(arr, 2, arr.Length - 2);
        return Encoding.Latin1.GetString(arr);
    }

    private static bool IsDelimiter(byte b) =>
        b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'/' or (byte)'[' or (byte)']' or (byte)'<' or (byte)'>' or (byte)'(' or (byte)')' or 0;

    private static void SkipSpace(byte[] d, ref int p)
    {
        while (p < d.Length && d[p] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0) p++;
    }
}
