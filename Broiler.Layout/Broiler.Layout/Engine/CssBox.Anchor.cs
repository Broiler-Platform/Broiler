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
/// inert until the P5.8d cutover. The current pass handles the MVP subset:
/// <c>position-area</c> with an explicit <c>position-anchor</c>, a registered anchor,
/// and a box with a definite size — for which placement is a pure reposition (the
/// grid cell + inset-modified containing block + within-cell alignment decide the
/// box's origin; its already-laid-out size is kept). Auto/fill-the-cell sizing,
/// box-sizing, inline-CB promotion, scrolling, <c>anchor()</c> insets and position-try
/// are out of MVP and stay on the bridge path until later P5.8d expansions.
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
            // Reposition the box (and its subtree) so its border-box origin lands at the
            // grid-resolved position. Size is left as laid out (MVP: definite-size boxes).
            box.OffsetLeft(target.Left - box.Location.X);
            box.OffsetTop(target.Top - box.Location.Y);
        }

        foreach (var child in box.Boxes)
            ApplyNativeAnchorPlacement(child, registry);
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

        // Reposition-only MVP: keep the box's already-laid-out border-box size, resolve
        // insets against the cell, and align the box within the cell.
        target = PositionAreaGrid.ResolveElementBox(
            c,
            ResolveInset(Top, c.Height), ResolveInset(Right, c.Width),
            ResolveInset(Bottom, c.Height), ResolveInset(Left, c.Width),
            explicitWidth: Size.Width, percentWidth: null,
            explicitHeight: Size.Height, percentHeight: null,
            parsed);
        return true;
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
