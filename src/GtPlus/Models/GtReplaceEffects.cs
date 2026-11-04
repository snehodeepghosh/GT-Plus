using System;
using System.Collections.Generic;
using Avalonia;

namespace GtPlus.Models;

/// <summary>
/// GT's Texture Flip: <c>&lt;Effect Type="FlipX" /&gt;</c> mirrors the object's texture left-to-right (the ribbon's "Horizontal"),
/// <c>FlipY</c> top-to-bottom ("Vertical"). Both live in the Replace stage and carry no settings of their own; an unticked box is
/// no effect at all. Two of the same kind in a hand-written file cancel, which is what chaining them in GT does
/// </summary>
public static class GtTextureFlip
{
    public static bool Has(IReadOnlyList<GtEffect> effects, GtEffectType type)
    {
        bool on = false;
        foreach (var e in effects)
            if (e.Type == type) on = !on;
        return on;
    }

    /// <summary>the ribbon's two boxes for an element</summary>
    public static (bool Horizontal, bool Vertical) Get(GtElement el) =>
        (Has(el.Effects, GtEffectType.FlipX), Has(el.Effects, GtEffectType.FlipY));

    /// <summary>turns one flip on or off; every effect of that kind is dropped first so the result is exactly one or none</summary>
    public static void Set(GtElement el, GtEffectType type, bool on)
    {
        if (type is not (GtEffectType.FlipX or GtEffectType.FlipY))
            throw new ArgumentOutOfRangeException(nameof(type));

        el.Effects.RemoveAll(e => e.Type == type);
        if (on) el.Effects.Add(new GtEffect { Type = type, Mode = GtEffectMode.Replace });
    }
}

/// <summary>
/// GT's Reflection: <c>&lt;Effect Type="Reflection" Mode="Finish" Depth="0.5" /&gt;</c>, a flat mirror image of the finished object
/// drawn under its bottom edge. vMix documents it only as a 2D reflection that ignores 3D; this editor reads <see cref="GtEffect.Depth"/>
/// as the share of the object's height the reflection reaches before it has faded out. An absent <c>Depth</c> means GT's
/// constructed 0.5
/// </summary>
public static class GtReflection
{
    /// <summary>alpha of the reflection where it meets the object; it fades linearly to nothing at the full depth</summary>
    public const double StartAlpha = 0.5;

    /// <summary>quick picks offered by this editor's ribbon; GT's own gallery values are not documented, so these are this editor's</summary>
    public static readonly IReadOnlyList<(string Name, double Depth)> Presets = new (string, double)[]
    {
        ("Short", 0.25),
        ("Half",  0.5),
        ("Full",  1.0),
    };

    public static GtEffect? First(IReadOnlyList<GtEffect> effects)
    {
        foreach (var e in effects)
            if (e.Type == GtEffectType.Reflection) return e;
        return null;
    }

    /// <summary>reflection depth as a 0-1 share of the object's height, or null when there is none</summary>
    public static double? Get(GtElement el) => First(el.Effects)?.Depth;

    /// <summary>null or a depth at or below zero removes the reflection; anything else sets the first one, adding it in the Finish stage if needed</summary>
    public static void Set(GtElement el, double? depth)
    {
        var existing = First(el.Effects);
        if (depth is not { } d || d <= 0)
        {
            if (existing is not null) el.Effects.Remove(existing);
            return;
        }

        if (existing is null)
        {
            existing = new GtEffect { Type = GtEffectType.Reflection, Mode = GtEffectMode.Finish };
            el.Effects.Add(existing);
        }
        existing.Depth = Math.Min(d, 1.0);
    }

    /// <summary>mirror about the bottom edge of <paramref name="bounds"/></summary>
    public static Matrix MirrorMatrix(Rect bounds) =>
        Matrix.CreateTranslation(0, -bounds.Bottom) * Matrix.CreateScale(1, -1) * Matrix.CreateTranslation(0, bounds.Bottom);
}

/// <summary>the Replace-stage effects that reshape the object's own picture, composed in list order: texture flips and skew</summary>
public static class GtReplaceEffects
{
    /// <summary>
    /// the transform the object is drawn through; flips mirror about the centre of <paramref name="bounds"/> and skews shear about it
    /// (plus their CenterOffset), each applied in the order the file lists them, as GT chains its effect graph
    /// </summary>
    public static Matrix Matrix(IReadOnlyList<GtEffect> effects, Rect bounds)
    {
        var m = Avalonia.Matrix.Identity;
        var c = bounds.Center;
        foreach (var e in effects)
        {
            switch (e.Type)
            {
                case GtEffectType.FlipX:
                    m *= Avalonia.Matrix.CreateTranslation(-c.X, -c.Y) * Avalonia.Matrix.CreateScale(-1, 1)
                       * Avalonia.Matrix.CreateTranslation(c.X, c.Y);
                    break;
                case GtEffectType.FlipY:
                    m *= Avalonia.Matrix.CreateTranslation(-c.X, -c.Y) * Avalonia.Matrix.CreateScale(1, -1)
                       * Avalonia.Matrix.CreateTranslation(c.X, c.Y);
                    break;
                case GtEffectType.Skew:
                    m *= GtSkew.Matrix(new[] { e }, bounds);
                    break;
            }
        }
        return m;
    }
}
