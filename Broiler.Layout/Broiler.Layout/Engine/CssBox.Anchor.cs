using System.Collections.Generic;
using Broiler.CSS;

namespace Broiler.Layout.Engine;

/// <summary>
/// Native CSS anchor-positioning placement (Phase 5 item 3, P5.8c). A root
/// post-pass — modelled on <c>LayoutNestedBrowsingContexts</c>, run after the main
/// single-pass layout so every anchor already has final geometry — that builds an
/// <see cref="AnchorRegistry"/> from the box tree and re-places anchor-positioned
/// boxes using the geometry primitives promoted in P5.4–P5.7.
/// </summary>
/// <remarks>
/// Gated by <see cref="NativeAnchorPlacement.Enabled"/> (default off), so this is
/// inert until the P5.8d cutover. The pass handles <c>position-area</c> with an explicit
/// <c>position-anchor</c> and a registered anchor. For a childless, content-box box with
/// no percentage box properties it also applies the grid-derived used <b>size</b>
/// (fill-the-cell / explicit / percentage width+height — the P5.8d.2b sizing expansion);
/// every other box keeps the reposition-only behaviour (its already-laid-out size is
/// kept). <c>box-sizing: border-box</c>, percentage margin/padding/inset box props,
/// inline-CB promotion, scrolling, <c>anchor()</c> insets and position-try are still out
/// of scope and stay on the bridge path until later P5.8d expansions.
/// </remarks>
partial class CssBox
{
    /// <summary>
    /// Entry point for the anchor-placement post-pass, invoked from
    /// <c>PerformLayout</c> at the document root when the flag is on.
    /// </summary>
    internal static void RunNativeAnchorPlacement(CssBox root)
    {
        var registry = BuildAnchorRegistry(root);
        if (registry.Count == 0)
            return;
        ApplyNativeAnchorPlacement(root, registry);
    }

    /// <summary>
    /// Walks the box tree collecting every element carrying an <c>anchor-name</c> into
    /// a name → document-absolute border-box registry (last in tree order wins).
    /// </summary>
    internal static AnchorRegistry BuildAnchorRegistry(CssBox root)
    {
        var registry = new AnchorRegistry();
        CollectAnchors(root, registry);
        return registry;
    }

    private static void CollectAnchors(CssBox box, AnchorRegistry registry)
    {
        var name = box.AnchorName;
        if (!string.IsNullOrEmpty(name) && name != "none")
        {
            var b = box.Bounds;
            registry.Register(name, new AnchorRect(b.X, b.Y, b.Width, b.Height));
        }

        foreach (var child in box.Boxes)
            CollectAnchors(child, registry);
    }

    private static void ApplyNativeAnchorPlacement(CssBox box, AnchorRegistry registry)
    {
        if (box.PositionArea != "none" && box.TryResolvePositionAreaTarget(registry, out var target))
        {
            // Apply the used SIZE first (P5.8d.2b sizing slices) for the boxes the engine
            // can size without re-flowing a subtree: a childless box (no in-flow children
            // or words) with no percentage margin/padding/inset box properties (those
            // resolve against the cell and need the bridge's explicit pre-bake for now).
            // For such a box the grid-derived used size fills the cell / honours an explicit
            // or percentage length exactly as the baked bridge path does; setting its border
            // box repaints correctly with no children to re-flow. Every other box keeps the
            // reposition-only behaviour (its already-laid-out size is preserved).
            if (box.CanApplyNativeAnchorSize())
            {
                // The box's own padding + border, read font-free (px + the CSS
                // thin/medium/thick keyword map, style:none → 0 — matching the bridge's
                // ResolveBorderWidth and avoiding the layout font the Actual* getters
                // resolve; percentage padding is excluded by the gate).
                double padBorderW =
                    box.NativeBorderPx(box.BorderLeftWidth, box.BorderLeftStyle)
                    + box.NativePaddingPx(box.PaddingLeft) + box.NativePaddingPx(box.PaddingRight)
                    + box.NativeBorderPx(box.BorderRightWidth, box.BorderRightStyle);
                double padBorderH =
                    box.NativeBorderPx(box.BorderTopWidth, box.BorderTopStyle)
                    + box.NativePaddingPx(box.PaddingTop) + box.NativePaddingPx(box.PaddingBottom)
                    + box.NativeBorderPx(box.BorderBottomWidth, box.BorderBottomStyle);

                // box-sizing: content-box (default) — target.Width/Height are the content
                // size, so the border box adds padding+border. box-sizing: border-box —
                // target.Width/Height already ARE the (cell-clamped/filled) border box, so
                // use them directly, clamped to at least padding+border so the content box
                // stays non-negative (mirrors the bridge's BorderBoxToContentSize + re-add).
                bool borderBox = string.Equals(box.BoxSizing, "border-box", StringComparison.OrdinalIgnoreCase);
                double borderBoxW = borderBox
                    ? System.Math.Max(target.Width, padBorderW)
                    : target.Width + padBorderW;
                double borderBoxH = borderBox
                    ? System.Math.Max(target.Height, padBorderH)
                    : target.Height + padBorderH;
                box.Size = new System.Drawing.SizeF((float)borderBoxW, (float)borderBoxH);
            }

            // Reposition the box (and its subtree) so its border-box origin lands at the
            // grid-resolved position.
            box.OffsetLeft(target.Left - box.Location.X);
            box.OffsetTop(target.Top - box.Location.Y);
        }

        foreach (var child in box.Boxes)
            ApplyNativeAnchorPlacement(child, registry);
    }

    /// <summary>
    /// Whether the native placement post-pass may set this box's used size directly
    /// (rather than only repositioning). True only when doing so cannot mis-render:
    /// the box has no in-flow children or words to re-flow, and has no percentage
    /// margin/padding/inset box properties (which resolve against the position-area
    /// cell — a used-value computation still owned by the bridge's pre-bake path until a
    /// later expansion). Both <c>content-box</c> and <c>border-box</c> sizing are handled
    /// (the caller forms the border box accordingly). Matches the boxes for which the
    /// engine's grid-derived size equals the baked bridge result.
    /// </summary>
    private bool CanApplyNativeAnchorSize()
    {
        if (Boxes.Count != 0 || Words.Count != 0)
            return false;
        return !HasPercent(MarginTop) && !HasPercent(MarginRight)
            && !HasPercent(MarginBottom) && !HasPercent(MarginLeft)
            && !HasPercent(PaddingTop) && !HasPercent(PaddingRight)
            && !HasPercent(PaddingBottom) && !HasPercent(PaddingLeft)
            && !HasPercent(Top) && !HasPercent(Right)
            && !HasPercent(Bottom) && !HasPercent(Left);
    }

    private static bool HasPercent(string? value) => value != null && value.Contains('%');

    /// <summary>
    /// Font-free used border width for one side: <c>0</c> when the border style is
    /// absent/<c>none</c>; otherwise the CSS <c>thin</c>/<c>medium</c>/<c>thick</c>
    /// keywords map to <c>1</c>/<c>3</c>/<c>4</c> px (default <c>medium</c>) and a
    /// pixel length is taken as-is. Mirrors the bridge's <c>ResolveBorderWidth</c> and
    /// <c>CssLengthParser.GetActualBorderWidth</c> so the native and baked paths agree.
    /// </summary>
    private double NativeBorderPx(string? width, string? style)
    {
        if (string.IsNullOrEmpty(style) || string.Equals(style, "none", StringComparison.OrdinalIgnoreCase))
            return 0;
        var w = width?.Trim();
        if (string.IsNullOrEmpty(w) || string.Equals(w, "medium", StringComparison.OrdinalIgnoreCase))
            return 3;
        if (string.Equals(w, "thin", StringComparison.OrdinalIgnoreCase)) return 1;
        if (string.Equals(w, "thick", StringComparison.OrdinalIgnoreCase)) return 4;
        return ParsePxOnly(w) ?? 3;
    }

    /// <summary>Font-free used padding for one side: a pixel length (or bare number),
    /// else <c>0</c>. Percentage padding is excluded upstream by the size gate.</summary>
    private double NativePaddingPx(string? value) => ParsePxOnly(value) ?? 0;

    private static double? ParsePxOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value!.Trim();
        if (v.Contains('%')) return null;
        if (v.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            v = v[..^2];
        return double.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var px)
            ? px
            : null;
    }

    /// <summary>
    /// Computes where this <c>position-area</c> box's border box should sit against its
    /// named anchor. Returns <c>false</c> (leave the box where it is) when the box is
    /// not an MVP anchor-positioned box or its anchor is not registered.
    /// </summary>
    internal bool TryResolvePositionAreaTarget(AnchorRegistry registry, out PositionAreaBox target)
    {
        target = default;

        var area = PositionArea;
        if (string.IsNullOrEmpty(area) || area == "none")
            return false;

        var anchorName = PositionAnchor;
        if (string.IsNullOrEmpty(anchorName) || anchorName == "auto")
            return false; // MVP requires an explicit position-anchor.

        // Containing-block padding box, in document coordinates.
        var cb = FindPositionedContainingBlock();
        GetAbsoluteContainingBlockPaddingBox(cb, out double cbX, out double cbY, out double cbW, out double cbH);

        var parsed = PositionAreaValue.Parse(area);
        var cell = registry.ResolvePositionAreaCell(anchorName, parsed, cbX, cbY, cbW, cbH);
        if (cell is not { } c)
            return false; // Anchor not registered.

        // Width/height inputs to the grid resolution. For a size-apply box (childless,
        // content-box, no percentage box props — see CanApplyNativeAnchorSize) feed the
        // box's authored CSS width/height so the used size fills the cell / honours an
        // explicit or percentage length exactly as the baked bridge path does. For every
        // other box keep the reposition-only MVP inputs (the already-laid-out border-box
        // size), so its alignment position is byte-identical to the shipped behaviour.
        double? explicitW, percentW, explicitH, percentH;
        if (CanApplyNativeAnchorSize())
        {
            (explicitW, percentW) = ParseSizeComponent(Width);
            (explicitH, percentH) = ParseSizeComponent(Height);
        }
        else
        {
            (explicitW, percentW) = (Size.Width, null);
            (explicitH, percentH) = (Size.Height, null);
        }

        target = PositionAreaGrid.ResolveElementBox(
            c,
            ResolveInset(Top, c.Height), ResolveInset(Right, c.Width),
            ResolveInset(Bottom, c.Height), ResolveInset(Left, c.Width),
            explicitWidth: explicitW, percentWidth: percentW,
            explicitHeight: explicitH, percentHeight: percentH,
            parsed);
        return true;
    }

    /// <summary>
    /// Parses a CSS <c>width</c>/<c>height</c> component into the (explicit-px, percent)
    /// pair <see cref="PositionAreaGrid.ResolveElementBox"/> expects: a pixel length (or
    /// bare number) → <c>(px, null)</c>; a percentage like <c>50%</c> → <c>(null, 50)</c>;
    /// <c>auto</c>/unparseable/other units → <c>(null, null)</c> (fill the cell). Mirrors
    /// the bridge's <c>TryParsePx</c>/<c>TryParsePercent</c> exactly for parity.
    /// </summary>
    internal static (double? explicitPx, double? percent) ParseSizeComponent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (null, null);
        var v = value!.Trim();
        if (v.EndsWith('%'))
        {
            return double.TryParse(v[..^1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var pct)
                ? (null, pct)
                : (null, null);
        }
        if (v.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            v = v[..^2];
        return double.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var px)
            ? (px, null)
            : (null, null);
    }

    /// <summary>
    /// Resolves a single position-area inset value (<c>px</c> or <c>%</c> of
    /// <paramref name="basis"/>) to pixels; <c>auto</c>/unparseable/other units → 0
    /// (matching the bridge's inset handling for the MVP subset).
    /// </summary>
    internal static double ResolveInset(string value, double basis)
    {
        if (string.IsNullOrEmpty(value) || value == "auto")
            return 0;
        var len = new CssLength(value);
        if (len.HasError)
            return 0;
        // CssLength stores a percentage as a fraction (0.25 for "25%").
        if (len.IsPercentage)
            return basis * len.Number;
        return len.Unit == CssUnit.Px ? len.Number : 0;
    }
}
