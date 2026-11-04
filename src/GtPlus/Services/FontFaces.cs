using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using SkiaSharp;

namespace GtPlus.Services;

/// <summary>
/// the installed faces of a font family, each with its <b>true</b> weight: the <c>usWeightClass</c> in the font's own OS/2 table.
/// That is the number vMix (DirectWrite on Windows) matches a title's FontWeight against. macOS reports weights through
/// CoreText's -1…1 scale instead, which comes back as odd values (a Black face as 947, an ExtraBold as 867), so asking the
/// platform for "Black = 900" can land on the ExtraBold face. Everything here resolves a GT weight to the face vMix would pick
/// and hands the platform that face's own reported weight, so the editor draws the same face vMix does.
/// The title still stores GT's weight names; nothing about the file changes.
/// </summary>
public static class FontFaces
{
    public sealed record Face(string Family, string StyleName, int TrueWeight, FontWeight PlatformWeight, bool Italic, string PostScriptName);

    private static readonly ConcurrentDictionary<string, IReadOnlyList<Face>> _families = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, Face>? _byPostScript;
    private static readonly object _psLock = new();

    /// <summary>every face of an installed family, lightest first; empty when the family is not installed</summary>
    public static IReadOnlyList<Face> FacesOf(string family)
    {
        if (string.IsNullOrWhiteSpace(family)) return Array.Empty<Face>();
        return _families.GetOrAdd(family, Load);
    }

    private static IReadOnlyList<Face> Load(string family)
    {
        var list = new List<Face>();
        try
        {
            using var set = SKFontManager.Default.GetFontStyles(family);
            for (int i = 0; i < set.Count; i++)
            {
                using var tf = set.CreateTypeface(i);
                if (tf is null) continue;
                // a fallback face for a family that is not really installed comes back under another name
                if (!string.Equals(tf.FamilyName, family, StringComparison.OrdinalIgnoreCase)) continue;
                list.Add(new Face(tf.FamilyName, set.GetStyleName(i) ?? "", TrueWeight(tf), (FontWeight)tf.FontWeight,
                                  tf.FontSlant != SKFontStyleSlant.Upright, tf.PostScriptName ?? ""));
            }
        }
        catch
        {
            // no font manager (a test without a platform): the family reads as not installed
        }
        return list.OrderBy(f => f.TrueWeight).ThenBy(f => f.Italic).ToList();
    }

    /// <summary>usWeightClass from the OS/2 table (big-endian u16 at offset 4), falling back to what the platform reports</summary>
    private static int TrueWeight(SKTypeface tf)
    {
        try
        {
            uint tag = ('O' << 24) | ('S' << 16) | ('/' << 8) | '2';
            if (tf.TryGetTableData(tag, out var os2) && os2.Length >= 6)
            {
                int w = (os2[4] << 8) | os2[5];
                if (w is >= 1 and <= 1000) return w;
            }
        }
        catch { }
        return tf.FontWeight;
    }

    /// <summary>
    /// the face of <paramref name="family"/> that a GT weight and style select, by the CSS / DirectWrite rule on true weights:
    /// an exact weight wins; asking for more than 500 looks heavier first then lighter, 400-500 looks up to 500 then lighter
    /// then heavier, below 400 looks lighter first then heavier. Italic faces are preferred when italic is asked for and
    /// avoided otherwise. Null when the family is not installed
    /// </summary>
    public static Face? Pick(string family, FontWeight weight, FontStyle style)
    {
        var faces = FacesOf(family);
        if (faces.Count == 0) return null;
        bool italic = style != FontStyle.Normal;
        var pool = faces.Where(f => f.Italic == italic).ToList();
        if (pool.Count == 0) pool = faces.ToList();

        int want = (int)weight;
        var exact = pool.FirstOrDefault(f => f.TrueWeight == want);
        if (exact is not null) return exact;

        IEnumerable<Face> heavier = pool.Where(f => f.TrueWeight > want).OrderBy(f => f.TrueWeight);
        IEnumerable<Face> lighter = pool.Where(f => f.TrueWeight < want).OrderByDescending(f => f.TrueWeight);
        if (want > 500) return heavier.FirstOrDefault() ?? lighter.First();
        if (want >= 400)
            return pool.Where(f => f.TrueWeight > want && f.TrueWeight <= 500).OrderBy(f => f.TrueWeight).FirstOrDefault()
                ?? lighter.FirstOrDefault() ?? heavier.First();
        return lighter.FirstOrDefault() ?? heavier.First();
    }

    /// <summary>
    /// the typeface to draw a GT font with: the family and style as given, the weight swapped for the platform's own number for
    /// the face vMix would pick, so the right face is drawn. A family that is not installed passes through unchanged
    /// </summary>
    public static Typeface Typeface(string family, FontStyle style, FontWeight weight)
    {
        var face = Pick(family, weight, style);
        return new Typeface(new FontFamily(family), style, face?.PlatformWeight ?? weight);
    }

    /// <summary>an installed face by its exact PostScript name ("GFF-Latin-Black"), as a PSD names its fonts</summary>
    public static Face? ByPostScriptName(string postScriptName)
    {
        if (string.IsNullOrWhiteSpace(postScriptName)) return null;
        lock (_psLock)
        {
            if (_byPostScript is null)
            {
                var map = new Dictionary<string, Face>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var fam in SKFontManager.Default.FontFamilies.Distinct(StringComparer.OrdinalIgnoreCase))
                        foreach (var f in FacesOf(fam))
                            if (f.PostScriptName.Length > 0) map.TryAdd(f.PostScriptName, f);
                }
                catch { }
                _byPostScript = map;
            }
            return _byPostScript.TryGetValue(postScriptName, out var face) ? face : null;
        }
    }

    /// <summary>the GT weight to store for a face: its true weight, which the file writes as the nearest vMix weight name</summary>
    public static FontWeight GtWeight(Face face) => (FontWeight)face.TrueWeight;
}
