using Broiler.Graphics;

namespace Broiler.Layout.Engine;

/// <summary>
/// CSS 2.1 §8.5.3: <c>inset</c> and <c>outset</c> paint a 3D bevel — two sides in a darkened shade
/// of the border colour and two in the colour itself — rather than four flat sides.
/// </summary>
/// <remarks>
/// <para>
/// The spec leaves the shades to the UA ("the colors are based on the <c>border-color</c> property,
/// but UAs may choose their own algorithm"), so a reference implementation's numbers are the only
/// thing an engine can be measured against. These are Chromium's, read off it directly rather than
/// inferred from its source: the darkened side scales every channel so that the <em>largest</em>
/// one loses 0.33 of full intensity, and the lit side is the colour unchanged. Scaling by the
/// largest channel rather than subtracting per channel is what keeps the hue —
/// <c>rgb(200,100,50)</c> darkens to <c>rgb(116,58,29)</c>, all three at ×0.58, where a flat
/// subtraction would give <c>rgb(116,16,0)</c> and turn brown into red.
/// </para>
/// <para>
/// Black is the one colour with a special case on each side, and it needs one: it cannot be
/// darkened, so a black bevel would be four identical black sides. The dark side stays black and
/// the lit side becomes <c>#545454</c>, which is what Chromium paints for
/// <c>border: 2px inset black</c>.
/// </para>
/// <para>
/// <c>groove</c> and <c>ridge</c> are <em>not</em> handled here. They split each side lengthwise
/// into two halves carrying the two shades — measurably so: a 16px grey <c>groove</c> paints its
/// outer half <c>#2C2C2C</c> and its inner half <c>#808080</c>, where <c>inset</c> paints the whole
/// width <c>#2C2C2C</c>. That needs two rectangles per side rather than one colour per side, so
/// they keep painting flat until the paint path can express it.
/// </para>
/// <para>
/// <b>An unspecified border colour is the UA stylesheet's business, not this type's.</b> CSS makes
/// the initial <c>border-color</c> <c>currentColor</c>, which on black text bevels into two blacks;
/// browsers substitute a light grey so the bevel is visible. Broiler states that grey directly in
/// the UA stylesheet — <c>hr</c> and <c>iframe</c>, the two elements the HTML Standard renders with
/// a bevelled border, carry <c>border-color: #EEEEEE</c> — and this type shades it from there. The
/// rendering matches; the computed <c>border-color</c> differs from Chromium's, which keeps
/// <c>currentColor</c> and substitutes at paint time. An author element with a bevelled border and
/// no colour of its own therefore still bevels from black rather than from the grey.
/// </para>
/// </remarks>
internal static class BorderBevel
{
    /// <summary>
    /// How much of full intensity the darkened side loses, as a fraction — Chromium's
    /// <c>Color::Dark()</c> constant (0.33 ≈ 84/255).
    /// </summary>
    private const float DarkenAmount = 0.33f;

    /// <summary>
    /// What the lit side of a black bevel is painted, since black cannot be darkened away from
    /// itself. Chromium's <c>kLightenedBlack</c>.
    /// </summary>
    private static readonly BColor LightenedBlack = BColor.FromArgb(255, 0x54, 0x54, 0x54);

    /// <summary>Whether <paramref name="borderStyle"/> paints a bevel this type models.</summary>
    public static bool IsBevelled(string? borderStyle) =>
        IsStyle(borderStyle, "inset") || IsStyle(borderStyle, "outset");

    /// <summary>
    /// The colour one side of a bevelled border is painted, or <paramref name="color"/> unchanged
    /// when <paramref name="borderStyle"/> is not one this type bevels.
    /// </summary>
    /// <param name="borderStyle">The side's used <c>border-style</c>.</param>
    /// <param name="isTopOrLeft">Whether this is the top or left side.</param>
    /// <param name="color">The side's used <c>border-color</c>.</param>
    public static BColor SideColor(string? borderStyle, bool isTopOrLeft, BColor color)
    {
        if (!IsBevelled(borderStyle))
            return color;

        // `inset` sinks the box, so the light falls on its bottom and right; `outset` raises it and
        // lights the top and left instead.
        bool darkened = isTopOrLeft == IsStyle(borderStyle, "inset");
        return darkened ? Darken(color) : Lighten(color);
    }

    /// <summary>
    /// Chromium's <c>Color::Dark()</c>: scales every channel by <c>(v - 0.33) / v</c>, where
    /// <c>v</c> is the largest channel, so the hue is kept and a colour already darker than the
    /// step lands on black. Alpha is untouched.
    /// </summary>
    public static BColor Darken(BColor color)
    {
        float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f;
        float v = System.Math.Max(r, System.Math.Max(g, b));
        if (v <= 0f)
            return color;   // black has nothing left to darken

        float multiplier = System.Math.Max(0f, (v - DarkenAmount) / v);
        return BColor.FromArgb(color.A, Channel(r, multiplier), Channel(g, multiplier), Channel(b, multiplier));
    }

    /// <summary>
    /// The lit side: the colour itself, except black, which would otherwise make the bevel
    /// invisible against its own darkened side.
    /// </summary>
    public static BColor Lighten(BColor color) =>
        color.R == 0 && color.G == 0 && color.B == 0
            ? BColor.FromArgb(color.A, LightenedBlack.R, LightenedBlack.G, LightenedBlack.B)
            : color;

    private static bool IsStyle(string? borderStyle, string name) =>
        borderStyle is not null && borderStyle.Equals(name, System.StringComparison.OrdinalIgnoreCase);

    private static byte Channel(float value, float multiplier) =>
        (byte)System.Math.Clamp((int)System.Math.Round(value * multiplier * 255f), 0, 255);
}
