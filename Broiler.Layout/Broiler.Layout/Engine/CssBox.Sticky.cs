using System;
using Broiler.CSS;

namespace Broiler.Layout.Engine;

/// <summary>
/// Native <c>position: sticky</c> placement (HtmlBridge complexity-reduction roadmap
/// Phase 5, P5.8d.2b sticky expansion). A root post-pass — run after
/// <see cref="CssBox.RunScrollSimulation"/> so it sees the scrolled content, before
/// <see cref="CssBox.RunNativeAnchorPlacement"/> — that pins each <c>position: sticky</c>
/// box to its nearest scroll container's scrollport edges (physical <c>top</c>/
/// <c>bottom</c>/<c>left</c>/<c>right</c> insets), clamped so the box stays within its
/// containing block's content box (CSS Positioned Layout 3 §6.3). This is the engine
/// equivalent of the bridge's <c>ResolveStickyPositioning</c> pre-bake (which rewrites
/// <c>sticky</c> to <c>relative</c> + a computed offset for the static renderer).
/// </summary>
/// <remarks>
/// <para>Gated by <see cref="NativeAnchorPlacement.Enabled"/> (default off), so this is
/// inert until the cutover. It only fires for a box the bridge left <c>position: sticky</c>
/// on: the bridge hands off only a sticky box whose scroll container is a
/// <em>non-document</em> clipping element on a <em>no-anchor</em> page
/// (<c>DomBridge.IsMvpNativeStickyBox</c>), the same scope as the native scroll handoff, so
/// the engine's scroll post-pass has the offset (via <c>data-broiler-scroll-*</c>) and never
/// crosses the anchor-scroll / position-visibility machinery. A sticky box pinned to the
/// document scrolling element (page scroll) stays on the bridge path — the engine has no
/// document-scroll model.</para>
///
/// <para>First increment: the physical-inset "pin to an edge" case, reposition-only (the
/// box keeps its laid-out size). A sticky box is laid out in flow (the renderer treats the
/// unrecognised <c>sticky</c> value as <c>static</c>); this pass then offsets it.</para>
/// </remarks>
partial class CssBox
{
    /// <summary>
    /// Entry point for the sticky post-pass, invoked from <c>PerformLayout</c> at the
    /// document root when the flag is on.
    /// </summary>
    internal static void RunStickyPositioning(CssBox root) => ApplyStickyPositioning(root);

    private static void ApplyStickyPositioning(CssBox box)
    {
        if (string.Equals(box.Position, "sticky", StringComparison.OrdinalIgnoreCase))
            box.ApplyStickyOffset();

        // The offset only moved box + subtree; the child list is unchanged, so a simple
        // recursion is safe. Nested sticky boxes compose (each pins within its own scroll
        // container / containing block, on top of any shift an ancestor already applied).
        foreach (var child in box.Boxes)
            ApplyStickyPositioning(child);
    }

    private void ApplyStickyOffset()
    {
        var scrollContainer = FindStickyScrollContainer();
        if (scrollContainer == null)
            return; // No clipping ancestor → the viewport/document scroll, not handled natively.

        // The box's containing block (its nearest block-container ancestor) bounds how far a
        // sticky box may travel (CSS Positioned Layout 3 §6.3).
        var cb = ContainingBlock;

        // Both axes read the box's current (post-scroll) geometry before either offset is
        // applied — they are independent (OffsetTop touches only Y, OffsetLeft only X), so
        // computing both up front is order-independent.
        double dy = ComputeStickyShift(scrollContainer, cb, vertical: true);
        double dx = ComputeStickyShift(scrollContainer, cb, vertical: false);

        if (dy != 0)
            OffsetTop(dy);
        if (dx != 0)
            OffsetLeft(dx);
    }

    /// <summary>The nearest ancestor that clips its content (a scroll container), or
    /// <c>null</c> when there is none (the box scrolls with the viewport, which the engine
    /// does not model natively).</summary>
    private CssBox? FindStickyScrollContainer()
    {
        for (var p = ParentBox; p != null; p = p.ParentBox)
            if (p.IsScrollClipContainer())
                return p;
        return null;
    }

    /// <summary>
    /// The shift (px) needed to keep the box pinned within its scroll container's scrollport
    /// along one axis. Positive = toward the end (down/right), negative = toward the start
    /// (up/left). Mirrors the bridge's <c>ComputeStickyShift</c>: a start (<c>top</c>/
    /// <c>left</c>) inset pins the box down/right when it would scroll above the inset line;
    /// an end (<c>bottom</c>/<c>right</c>) inset pins it up/left when it would scroll past the
    /// far edge. The result is clamped so the box never leaves its containing block.
    /// </summary>
    private double ComputeStickyShift(CssBox scrollContainer, CssBox cb, bool vertical)
    {
        string startInset = vertical ? Top : Left;
        string endInset = vertical ? Bottom : Right;
        bool hasStart = HasStickyInset(startInset);
        bool hasEnd = HasStickyInset(endInset);
        if (!hasStart && !hasEnd)
            return 0;

        // The scrollport is the scroll container's content box (document coords). The
        // container box itself is not translated by the scroll post-pass — only its content
        // is — so its content-box edges are stable references.
        double portStart = vertical ? scrollContainer.ClientTop : scrollContainer.ClientLeft;
        double portEnd = vertical ? scrollContainer.ClientBottom : scrollContainer.ClientRight;
        double portSize = portEnd - portStart;

        // The box's current (post-scroll) border-box position within the scrollport, and its
        // border-box size along the axis.
        double boxStart = vertical ? Bounds.Y : Bounds.X;
        double size = vertical ? Bounds.Height : Bounds.Width;
        double posInScrollport = boxStart - portStart;

        double shift = 0;
        if (hasStart)
        {
            double inset = ParseStickyInset(startInset, portSize);
            double needed = inset - posInScrollport; // push toward the end (down/right)
            if (needed > 0)
                shift = needed;
        }
        if (shift == 0 && hasEnd)
        {
            double inset = ParseStickyInset(endInset, portSize);
            double overflow = (posInScrollport + size) - (portSize - inset);
            if (overflow > 0)
                shift = -overflow; // push toward the start (up/left)
        }
        if (shift == 0)
            return 0;

        // Clamp so the box's border box stays within the containing block's content box
        // (a sticky box never leaves its containing block).
        double cbStart = vertical ? cb.ClientTop : cb.ClientLeft;
        double cbEnd = vertical ? cb.ClientBottom : cb.ClientRight;
        double cbExtent = cbEnd - cbStart;
        double naturalInCb = boxStart - cbStart;
        double minShift = -naturalInCb;
        double maxShift = cbExtent - size - naturalInCb;
        if (maxShift < minShift)
            maxShift = minShift;

        return Math.Clamp(shift, minShift, maxShift);
    }

    /// <summary>Whether a physical inset participates in sticky pinning (present and not
    /// <c>auto</c>).</summary>
    private static bool HasStickyInset(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value.Trim(), CssConstants.Auto, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves a sticky inset to px. A percentage resolves against the scrollport size
    /// along the axis (<see cref="CssLength"/> stores a percentage as a fraction, so
    /// <c>basis * Number</c>). Non-px/percent units are not resolved here (font-free, first
    /// increment — matching the anchor primitives' px/percent scope) and yield 0.
    /// </summary>
    internal static double ParseStickyInset(string? value, double percentBasis)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;
        var len = new CssLength(value);
        if (len.HasError)
            return 0;
        if (len.IsPercentage)
            return percentBasis * len.Number;
        return len.Unit == CssUnit.Px ? len.Number : 0;
    }
}
