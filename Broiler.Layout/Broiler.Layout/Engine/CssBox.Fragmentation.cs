using Broiler.CSS;

namespace Broiler.Layout.Engine;

/// <summary>
/// CSS Fragmentation 3 — forced page breaks, as far as the block flow is concerned.
/// </summary>
/// <remarks>
/// <para>
/// The engine paints one continuous surface, and a paged render is that surface cut into
/// page-height strips: page <c>k</c> is the band from <c>k·H</c> to <c>(k+1)·H</c>. That is a real
/// model rather than a trick, because the container already separates the two sizes it needs —
/// <c>PageSize</c>, which is what <c>vw</c>/<c>vh</c> resolve against, from <c>MaxSize</c>, the
/// surface actually rasterised. A forced break is then a placement rule: move the box down to the
/// next band boundary and let the flow below it follow.
/// </para>
/// <para>
/// Inert unless a page size is in force. A screen render leaves <c>PageSize</c> at its
/// no-pagination default, so no boundary ever falls inside the content and every box stays where
/// the flow put it — which is why this can be applied unconditionally rather than behind a mode
/// flag.
/// </para>
/// <para>
/// What this does <em>not</em> do is break content that merely runs off the end of a page. That is
/// automatic fragmentation, and it needs boxes to be split rather than moved.
/// </para>
/// </remarks>
internal partial class CssBox
{
    /// <summary>
    /// A page size this large means "not paginated" — the value
    /// <c>HtmlContainer</c> installs for an ordinary screen render.
    /// </summary>
    private const double UnpaginatedPageExtent = 90000;

    /// <summary>
    /// Whether a <c>break-before</c>/<c>break-after</c> value forces a page break. The named page
    /// sides (<c>left</c>, <c>right</c>, <c>recto</c>, <c>verso</c>) force one and additionally
    /// constrain which side it lands on, which needs page numbering this does not have; forcing the
    /// break is the half that is representable, and it is the half these values share.
    /// </summary>
    private static bool ForcesPageBreak(string value)
    {
        var v = value?.Trim();
        return v is not null
            && (v.Equals("page", System.StringComparison.OrdinalIgnoreCase)
                || v.Equals("always", System.StringComparison.OrdinalIgnoreCase)
                || v.Equals(CssConstants.Left, System.StringComparison.OrdinalIgnoreCase)
                || v.Equals(CssConstants.Right, System.StringComparison.OrdinalIgnoreCase)
                || v.Equals("recto", System.StringComparison.OrdinalIgnoreCase)
                || v.Equals("verso", System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Moves <paramref name="child"/> to the top of the next page when a break is forced before it,
    /// or after the box that precedes it. Returns the distance moved, so the caller can carry it
    /// into the sibling that follows.
    /// </summary>
    /// <remarks>
    /// Applied after the child is laid out rather than before, because its position is only known
    /// then — and the whole laid-out subtree moves with it, which is what
    /// <see cref="OffsetTop"/> is for. <c>ActualBottom</c> is advanced by hand: <c>OffsetTop</c>
    /// moves the box's origin and its descendants but not its recorded bottom edge, and that edge
    /// is precisely what the next sibling positions itself from.
    /// </remarks>
    internal double ApplyForcedPageBreakBefore(CssBox child, CssBox? previous)
    {
        if (LayoutEnvironment is not { } environment)
            return 0;

        double pageHeight = environment.PageSize.Height;
        if (pageHeight <= 0 || pageHeight >= UnpaginatedPageExtent)
            return 0;

        if (child.Position is CssConstants.Absolute or CssConstants.Fixed
            || child.Float != CssConstants.None
            || child.Display == CssConstants.None)
        {
            return 0;
        }

        if (!ForcesPageBreak(child.BreakBefore)
            && !(previous is not null && ForcesPageBreak(previous.BreakAfter)))
        {
            return 0;
        }

        double origin = environment.MarginTop;
        double offsetInFlow = child.Location.Y - origin;
        if (offsetInFlow < 0)
            return 0;

        // Already at the top of a page: the break is satisfied and moving would insert a blank one.
        double intoPage = offsetInFlow % pageHeight;
        if (intoPage <= 0.01)
            return 0;

        double delta = pageHeight - intoPage;
        child.OffsetTop(delta);
        child.ActualBottom += delta;
        return delta;
    }
}
