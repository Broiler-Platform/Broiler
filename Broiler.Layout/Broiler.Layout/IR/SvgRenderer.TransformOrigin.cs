using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace Broiler.Layout.IR;

/// <summary>
/// CSS Transforms 1 §6/§8: <c>transform-box</c> and <c>transform-origin</c> on an SVG element.
/// </summary>
/// <remarks>
/// <para>
/// An SVG <c>transform</c> was applied about the user-space origin and nothing else — which is
/// what SVG 1.1 did, and is still right for the initial <c>transform-box: view-box</c>. What was
/// missing is the box-relative case: <c>transform-box: fill-box</c> makes the element's own fill
/// bounding box the reference box, and <c>transform-origin</c> then names a point inside it (the
/// centre, <c>50% 50%</c>, when it is not written). The element's own transform has to be
/// conjugated by that point — <c>T(o) · M · T(-o)</c> — and it was not, so a
/// <c>rotate(90)</c> about a box centre turned about the viewport corner instead and swung the
/// element clean off its intended place.
/// </para>
/// <para>
/// The 45 <c>css-transforms/transform-origin/svg-origin-*</c> tests of the 2026-08-21 run scored an
/// identical 97.14% on this: each declares <c>transform-box: fill-box</c> on a 150×150 rect, and
/// 150×150 is 2.86% of the canvas — the whole of their disagreement.
/// </para>
/// <para>
/// Only the box-relative <c>transform-box</c> values take this path. Under the initial
/// <c>view-box</c> the transform keeps the meaning it already had, so a document that never
/// mentions <c>transform-box</c> renders exactly as before.
/// </para>
/// </remarks>
internal static partial class SvgRenderer
{
    /// <summary>
    /// The element's own transform, conjugated by its <c>transform-origin</c> when
    /// <c>transform-box</c> makes a box the reference. <paramref name="fillBox"/> is the element's
    /// fill bounding box in user units.
    /// </summary>
    private static SvgTransform OwnTransformAbout(
        Dictionary<string, string> attrs, RectangleF fillBox)
    {
        var own = SvgTransform.Parse(attrs.GetValueOrDefault("transform"));

        if (own.IsIdentity || !UsesBoxRelativeTransformBox(attrs))
            return own;

        var origin = ResolveTransformOrigin(GetPresentationValue(attrs, "transform-origin"), fillBox);
        if (Math.Abs(origin.X) < 1e-6f && Math.Abs(origin.Y) < 1e-6f)
            return own;

        var toOrigin = new SvgTransform(1, 0, 0, 1, origin.X, origin.Y);
        var fromOrigin = new SvgTransform(1, 0, 0, 1, -origin.X, -origin.Y);
        return toOrigin.Concat(own).Concat(fromOrigin);
    }

    /// <summary>
    /// Whether <c>transform-box</c> names a box of the element rather than the viewport. The
    /// initial value is <c>view-box</c>, under which the transform keeps SVG 1.1's meaning; every
    /// other value makes the element's own box the reference. <c>stroke-box</c>, <c>content-box</c>
    /// and <c>border-box</c> differ from <c>fill-box</c> only by the stroke and by box decorations
    /// this renderer does not give a shape, so they resolve to the same rectangle here.
    /// </summary>
    private static bool UsesBoxRelativeTransformBox(Dictionary<string, string> attrs)
    {
        string box = GetPresentationValue(attrs, "transform-box")?.Trim();
        return !string.IsNullOrEmpty(box)
            && !box.Equals("view-box", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// CSS Transforms 1 §8: <c>transform-origin</c> as a point in user units, relative to
    /// <paramref name="box"/>. One value gives the x with the y centred; a keyword names an edge or
    /// the centre; a percentage resolves against the box. The z component a third value carries is
    /// dropped — nothing here is 3D. An absent or unreadable value is the initial
    /// <c>50% 50%</c>: the centre of the box.
    /// </summary>
    private static PointF ResolveTransformOrigin(string value, RectangleF box)
    {
        float centreX = box.X + box.Width / 2f;
        float centreY = box.Y + box.Height / 2f;

        if (string.IsNullOrWhiteSpace(value))
            return new PointF(centreX, centreY);

        var parts = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return new PointF(centreX, centreY);

        if (parts.Length == 1)
        {
            // One value sets that axis and centres the other. `top`/`bottom` name the vertical one,
            // so a lone one of those moves y rather than x.
            if (IsVerticalKeyword(parts[0]))
                return new PointF(centreX, ResolveOriginComponent(parts[0], box.Y, box.Height, horizontal: false, centreY));

            return IsHorizontalCompatible(parts[0])
                ? new PointF(ResolveOriginComponent(parts[0], box.X, box.Width, horizontal: true, centreX), centreY)
                : new PointF(centreX, centreY);
        }

        // The pair may be written the other way round — but only when *both* halves are keywords.
        // `top left` is the keyword form; `top 100%` is not a form at all, because a
        // length-percentage may not follow a vertical keyword. An invalid declaration is dropped
        // whole, leaving the initial `50% 50%` — which is what the twelve WPT
        // css-transforms/transform-origin/svg-origin-relative-length-invalid-* cases assert, and
        // what accepting `top 100%` as `100% top` got wrong.
        string first = parts[0], second = parts[1];
        if (IsVerticalKeyword(first))
        {
            if (!IsHorizontalKeyword(second))
                return new PointF(centreX, centreY);

            (first, second) = (second, first);
        }

        if (!IsHorizontalCompatible(first) || !IsVerticalCompatible(second))
            return new PointF(centreX, centreY);

        return new PointF(
            ResolveOriginComponent(first, box.X, box.Width, horizontal: true, centreX),
            ResolveOriginComponent(second, box.Y, box.Height, horizontal: false, centreY));
    }

    /// <summary>A keyword naming a position on the horizontal axis.</summary>
    private static bool IsHorizontalKeyword(string token) =>
        token.Equals("left", StringComparison.OrdinalIgnoreCase)
        || token.Equals("right", StringComparison.OrdinalIgnoreCase)
        || token.Equals("center", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the token may occupy the horizontal slot: a keyword for it, or a length.</summary>
    private static bool IsHorizontalCompatible(string token) =>
        IsHorizontalKeyword(token) || IsLengthOrPercentage(token);

    /// <summary>Whether the token may occupy the vertical slot: a keyword for it, or a length.</summary>
    private static bool IsVerticalCompatible(string token) =>
        IsVerticalKeyword(token)
        || token.Equals("center", StringComparison.OrdinalIgnoreCase)
        || IsLengthOrPercentage(token);

    private static bool IsLengthOrPercentage(string token)
    {
        var span = token.AsSpan().Trim();
        if (span.Length == 0)
            return false;

        if (span[^1] == '%')
            span = span[..^1];

        return float.TryParse(
            span.ToString().Replace("px", string.Empty), NumberStyles.Float,
            CultureInfo.InvariantCulture, out _);
    }

    private static bool IsVerticalKeyword(string token) =>
        token.Equals("top", StringComparison.OrdinalIgnoreCase)
        || token.Equals("bottom", StringComparison.OrdinalIgnoreCase);

    private static float ResolveOriginComponent(
        string token, float origin, float extent, bool horizontal, float fallback)
    {
        if (token.Equals("center", StringComparison.OrdinalIgnoreCase))
            return origin + extent / 2f;

        if (horizontal)
        {
            if (token.Equals("left", StringComparison.OrdinalIgnoreCase))
                return origin;
            if (token.Equals("right", StringComparison.OrdinalIgnoreCase))
                return origin + extent;
        }
        else
        {
            if (token.Equals("top", StringComparison.OrdinalIgnoreCase))
                return origin;
            if (token.Equals("bottom", StringComparison.OrdinalIgnoreCase))
                return origin + extent;
        }

        var span = token.AsSpan().Trim();
        if (span.Length > 0 && span[^1] == '%')
        {
            return float.TryParse(
                span[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float percent)
                ? origin + extent * percent / 100f
                : fallback;
        }

        // A bare number, or one carrying a unit this renderer treats as pixels — the same reading
        // GetLength gives an attribute length. The origin is measured from the box's own corner.
        return float.TryParse(
            token.Replace("px", string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture,
            out float length)
            ? origin + length
            : fallback;
    }
}
