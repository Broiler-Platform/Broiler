using Broiler.CSS;

namespace Broiler.Layout.Engine;

/// <summary>
/// CSS Fragmentation 3 — page breaks in the block flow: the forced ones (§3) and the automatic
/// decision for content that must not be split (§4.1).
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
/// <b>Automatic fragmentation costs almost nothing in this model, and that is the point.</b> A box
/// that runs past a boundary already continues at the top of the next band, because the bands are
/// cut from one continuous surface — splitting is what the geometry does by itself. The only thing
/// that has to be decided is what must <em>not</em> be cut, which is why the automatic half here is
/// a monolithic test and a nudge rather than a box-splitting engine.
/// </para>
/// <para>
/// <b>What is still missing before a paged render can use any of this.</b> The boundaries have to be
/// in the right place, and they are defined by <c>@page</c>: its <c>size</c> and its <c>margin</c>
/// give the page area, and nothing else does. Paginating at the viewport instead is not an
/// approximation of that — it is a different set of boundaries. Measured over the 409 print
/// reftests with the runner wired to the viewport: 252 → 228 passing, the losses concentrated in
/// <c>css/CSS2/pagination</c>, whose tests declare <c>@page { size: 5in 3in; margin: 0.5in }</c> —
/// a two-inch page area — and were being cut at 768px.
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
    /// Whether this box must not be split across a fragmentation boundary — "monolithic" in
    /// CSS Fragmentation 3 §4.1 — so a boundary falling inside it moves the whole box instead.
    /// </summary>
    /// <remarks>
    /// Everything else is breakable, and in this model breakable content needs no work at all: the
    /// surface is continuous and the pages are bands cut from it, so a box that runs past a
    /// boundary already continues at the top of the next band. Fragmenting it is what the geometry
    /// does by itself; the only thing that has to be *decided* is what must not be cut.
    /// </remarks>
    private bool IsMonolithicForFragmentation()
    {
        if (BreakInsideAvoids(BreakInside) || BreakInsideAvoids(PageBreakInside))
            return true;

        // A scroll container fragments as a unit — its content scrolls rather than continuing on
        // the next page.
        if (!string.IsNullOrEmpty(Overflow)
            && !Overflow.Equals(CssConstants.Visible, StringComparison.OrdinalIgnoreCase))
            return true;

        // Size containment makes a box's contents unobservable from outside, so it cannot be
        // fragmented — the css-break monolithic tests are built on exactly this.
        if (HasSizeContainment(Contain))
            return true;

        // Atomic inlines and replaced content are single, indivisible boxes.
        return Display is CssConstants.InlineBlock or "inline-flex" or "inline-grid"
                or CssConstants.InlineTable
            || IsImage;
    }

    private static bool BreakInsideAvoids(string value)
    {
        var v = value?.Trim();
        return v is not null
            && (v.Equals(CssConstants.Avoid, StringComparison.OrdinalIgnoreCase)
                || v.Equals("avoid-page", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether a <c>contain</c> value applies size containment.</summary>
    private static bool HasSizeContainment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        foreach (var token in value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("size", StringComparison.OrdinalIgnoreCase)
                || token.Equals("strict", StringComparison.OrdinalIgnoreCase)
                || token.Equals("content", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Moves an unbreakable <paramref name="child"/> off a page boundary that falls inside it, to
    /// the top of the next page. Returns the distance moved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only when the box would actually fit on a page of its own: one taller than the page has to
    /// be cut somewhere however monolithic it is, and pushing it would leave a blank page and then
    /// overflow anyway. That is the same judgement <c>BreakPage</c> already made, and the same
    /// reason it declines.
    /// </para>
    /// <para>
    /// Called from the block child loop, so it covers block-level children only. An atomic inline
    /// is monolithic as well, and so are grid and flex items — <c>css-break/grid/monolithic-overflow-print</c>
    /// is a grid of size-contained items and needs the grid path to do this at row granularity —
    /// but each is placed by its own layout path and none of those are hooked yet.
    /// </para>
    /// </remarks>
    internal double ApplyMonolithicPageFit(CssBox child)
    {
        if (LayoutEnvironment is not { } environment)
            return 0;

        double pageHeight = environment.PageSize.Height;
        if (pageHeight <= 0 || pageHeight >= UnpaginatedPageExtent)
            return 0;

        if (child.Position is CssConstants.Absolute or CssConstants.Fixed
            || child.Display == CssConstants.None
            || !child.IsMonolithicForFragmentation())
        {
            return 0;
        }

        double origin = environment.MarginTop;
        double top = child.Location.Y - origin;
        double height = child.ActualBottom - child.Location.Y;

        if (top < 0 || height <= 0 || height > pageHeight)
            return 0;

        double intoPage = top % pageHeight;
        double remaining = pageHeight - intoPage;

        // Fits in what is left of this page, or starts exactly on a boundary: nothing to do.
        if (intoPage <= 0.01 || height <= remaining + 0.01)
            return 0;

        child.OffsetTop(remaining);
        return remaining;
    }

    /// <summary>
    /// Moves <paramref name="child"/> to the top of the next page when a break is forced before it,
    /// or after the box that precedes it. Returns the distance moved, so the caller can carry it
    /// into the sibling that follows.
    /// </summary>
    /// <remarks>
    /// Applied after the child is laid out rather than before, because its position is only known
    /// then — and the whole laid-out subtree moves with it, which is what
    /// <see cref="OffsetTop"/> is for. Nothing else needs adjusting:
    /// <see cref="CssBoxProperties.ActualBottom"/> is derived from the box's origin
    /// (<c>Location.Y + Size.Height</c>), so moving the origin carries the bottom edge — the edge
    /// the next sibling positions itself from — with it. Advancing it as well, which this used to
    /// do, does not translate the box but *stretches* it: the setter resizes rather than moves, so
    /// every pushed box grew by the distance it was pushed and everything after it drifted. That is
    /// what made a two-page document paginate as five.
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
        return delta;
    }
}
