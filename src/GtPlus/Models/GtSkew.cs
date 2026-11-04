using System;
using System.Collections.Generic;
using Avalonia;

namespace GtPlus.Models;

/// <summary>
/// GT's Skew effect: <c>&lt;Effect Type="Skew" /&gt;</c> in the Replace stage, with <see cref="GtEffect.Angle"/> holding the X and Y
/// shear in degrees and <see cref="GtEffect.CenterOffset"/> moving the point it shears about away from the object's centre.
/// GT's ribbon shows 0,0 for an object with no skew and deletes the effect when both angles go back to zero, so "no skew" is
/// always "no effect" rather than an effect sitting at 0,0. Note the constructed default <see cref="GtEffect.DefaultAngle"/> is
/// 30,0, which is why an <c>Angle</c> attribute absent from the file still means a 30° skew.
/// </summary>
public static class GtSkew
{
    /// <summary>the furthest either angle may go; tan() runs away towards 90° and the picture becomes a line</summary>
    public const double MaxAngle = 85;

    public static GtEffect? First(IReadOnlyList<GtEffect> effects)
    {
        foreach (var e in effects)
            if (e.Type == GtEffectType.Skew) return e;
        return null;
    }

    /// <summary>the element's skew in degrees, 0,0 when it has none</summary>
    public static (double X, double Y) Get(GtElement el) =>
        First(el.Effects) is { } s ? (s.Angle.X, s.Angle.Y) : (0, 0);

    /// <summary>
    /// sets the skew angles; 0,0 removes the effect, otherwise the first Skew effect is updated or a new one appended in the Replace
    /// stage. Any further Skew effects a hand-written file carried are left alone, only the first is edited, as GT's ribbon does
    /// </summary>
    public static void Set(GtElement el, double x, double y)
    {
        x = Math.Clamp(x, -MaxAngle, MaxAngle);
        y = Math.Clamp(y, -MaxAngle, MaxAngle);
        var existing = First(el.Effects);

        if (x == 0 && y == 0)
        {
            if (existing is not null) el.Effects.Remove(existing);
            return;
        }

        if (existing is null)
        {
            existing = new GtEffect { Type = GtEffectType.Skew, Mode = GtEffectMode.Replace };
            el.Effects.Add(existing);
        }
        existing.Angle = new GtPoint(x, y);
    }

    /// <summary>
    /// the shear every skew effect in the list contributes, about the centre of <paramref name="bounds"/> plus each effect's
    /// CenterOffset, composed in list order; identity when there is none. Same convention as D2D's Matrix3x2F::Skew:
    /// a positive X angle pushes the bottom of the object right, a positive Y angle pushes its right side down
    /// </summary>
    public static Matrix Matrix(IReadOnlyList<GtEffect> effects, Rect bounds)
    {
        var m = Avalonia.Matrix.Identity;
        foreach (var e in effects)
        {
            if (e.Type != GtEffectType.Skew) continue;
            double ax = Math.Clamp(e.Angle.X, -MaxAngle, MaxAngle) * Math.PI / 180.0;
            double ay = Math.Clamp(e.Angle.Y, -MaxAngle, MaxAngle) * Math.PI / 180.0;
            if (ax == 0 && ay == 0) continue;

            double cx = bounds.Center.X + e.CenterOffset.X;
            double cy = bounds.Center.Y + e.CenterOffset.Y;
            m *= Avalonia.Matrix.CreateTranslation(-cx, -cy)
               * Avalonia.Matrix.CreateSkew(ax, ay)
               * Avalonia.Matrix.CreateTranslation(cx, cy);
        }
        return m;
    }
}
