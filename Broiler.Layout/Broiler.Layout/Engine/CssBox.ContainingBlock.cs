using Broiler.CSS;
using System.Drawing;


namespace Broiler.Layout.Engine;

internal partial class CssBox : CssBoxProperties, IDisposable
{
    public CssBox ContainingBlock
    {
        get
        {
            if (ParentBox == null)
                return this; //This is the initial containing block.

            var box = ParentBox;

            // CSS2.1 §10.1: The containing block for a box is the nearest
            // ancestor that is a block container.  Block containers include:
            //   - block-level boxes (display:block, flex, grid)
            //   - inline-block boxes (display:inline-block)
            //   - list-item boxes
            //   - table cells (display:table-cell)
            //   - table boxes (display:table)
            //   - table captions (display:table-caption)
            // Inline-block establishes a BFC (§9.4.1), so its block-level
            // children must use it as their containing block.  A table caption
            // is a block container that establishes an independent BFC
            // (CSS2.1 §17.4), so its in-flow children resolve their width and
            // position against the caption's content box, not a further-up
            // ancestor — critical when the caption is sized/rotated by its own
            // writing mode (its children must inherit its inline size, not the
            // viewport's).
            while (!box.IsBlock
                   && box.Display != CssConstants.InlineBlock
                   && box.Display != CssConstants.ListItem
                   && box.Display != CssConstants.Table
                   && box.Display != CssConstants.TableCell
                   && box.Display != CssConstants.TableCaption
                   && box.ParentBox != null)
            {
                box = box.ParentBox;
            }

            //Comment this following line to treat always superior box as block
            if (box == null)
                throw new Exception("There's no containing block on the chain");

            return box;
        }
    }

    /// <summary>
    /// CSS2.1 §10.1: For absolutely positioned elements, the containing
    /// block is the padding-box of the nearest ancestor with a computed
    /// position of <c>absolute</c>, <c>relative</c>, or <c>fixed</c>.
    /// Falls back to <see cref="ContainingBlock"/> if none is found.
    /// Also checks <see cref="SplitPositionedAncestor"/> which links back
    /// to positioned inlines that were restructured by the block-inside-
    /// inline correction (CSS2.1 §9.2.1.1).
    /// </summary>
    private CssBox FindPositionedContainingBlock()
    {
        var box = ParentBox;
        while (box != null)
        {
            if (box.Position is CssConstants.Relative or CssConstants.Absolute or CssConstants.Fixed || box.ParentBox == null)
                return box;

            // Native anchor mode (P5.8d.2b transform/contain CB expansion): a box that
            // establishes a containing block for absolutely-positioned descendants through a
            // non-position property — a non-none transform (CSS Transforms 1 §4) or
            // contain: layout/paint/strict/content (CSS Containment §2) — is that containing
            // block. On the baked path the bridge's EnsureContainingBlockPositioning pre-bakes
            // position:relative onto the same boxes, so the position check above already
            // returns them; recognising them here lets native mode drop that pre-bake. Gated
            // by the native-anchor lever so default-off layout is byte-identical.
            if (NativeAnchorPlacement.Enabled && box.EstablishesNonPositionAbsPosContainingBlock())
                return box;

            // RF-BRIDGE-1b Track 3.2: a nested browsing context's sub-viewport
            // (#subdoc-root) is the initial containing block for its subtree, so an
            // absolutely-positioned descendant with no positioned ancestor resolves
            // against it rather than climbing out into the top-level document.
            if (box.IsNestedViewportRoot)
                return box;

            // If the block-inside-inline correction split a positioned inline
            // and hoisted this branch out, SplitPositionedAncestor links back
            // to the original positioned inline ancestor.
            if (box.SplitPositionedAncestor is { } spa
                && spa.Position is CssConstants.Relative or CssConstants.Absolute or CssConstants.Fixed)
                return spa;

            box = box.ParentBox;
        }

        return ContainingBlock;
    }

    private bool IsInitialContainingBlock(CssBox cb) => cb.ParentBox == null && LayoutEnvironment != null;

    /// <summary>
    /// Whether this box establishes a containing block for absolutely-positioned
    /// descendants through a property other than <c>position</c> — a non-<c>none</c>
    /// <c>transform</c> (CSS Transforms 1 §4) or a <c>contain</c> value of
    /// <c>layout</c>/<c>paint</c>/<c>strict</c>/<c>content</c> (CSS Containment §2).
    /// Mirrors the subset of the bridge's <c>EstablishesContainingBlock</c> that the engine
    /// models natively; <c>will-change</c> is not projected onto the box, so a
    /// will-change-only containing block stays on the bridge path. Consulted by
    /// <see cref="FindPositionedContainingBlock"/> only under the native-anchor lever.
    /// </summary>
    internal bool EstablishesNonPositionAbsPosContainingBlock()
    {
        if (!string.IsNullOrWhiteSpace(Transform)
            && !string.Equals(Transform, "none", System.StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(Contain))
        {
            var c = Contain.ToLowerInvariant();
            if (c.Contains("layout") || c.Contains("paint") || c.Contains("strict") || c.Contains("content"))
                return true;
        }

        return false;
    }

    /// <summary>
    /// RF-BRIDGE-1b Track 3.2: the viewport a <c>position:fixed</c> descendant of this
    /// box resolves against — its used size (for inset percentages and viewport-basis
    /// sizing) and its origin (for placement). When the box is inside a nested browsing
    /// context (an <c>&lt;iframe&gt;</c> whose <c>#subdoc-root</c> is marked
    /// <see cref="IsNestedViewportRoot"/>), this is that frame's content box (the
    /// sub-viewport); otherwise it is the top-level viewport at the origin
    /// (<c>0,0</c> + <see cref="ILayoutEnvironment.ViewportSize"/>) — byte-identical to
    /// the previous behaviour for every box in the top-level document. The sub-viewport
    /// origin is the root's <em>pre-translate</em> content origin; the later
    /// <see cref="LayoutSubdocument"/> translate composes the whole subtree (fixed box
    /// included) onto the frame's content origin, so the fixed box lands at
    /// <c>frameContentOrigin + inset</c>.
    /// </summary>
    private RectangleF FixedPositioningViewport()
    {
        for (var box = ParentBox; box != null; box = box.ParentBox)
        {
            if (!box.IsNestedViewportRoot)
                continue;

            // Origin tracks the live Location (composed onto the frame content origin by
            // the LayoutSubdocument translate); size comes from the pinned
            // NestedViewportSize because the box's used Size is transiently 0 while its
            // own subtree lays out.
            double left = box.Location.X + box.ActualBorderLeftWidth + box.ActualPaddingLeft;
            double top = box.Location.Y + box.ActualBorderTopWidth + box.ActualPaddingTop;
            return new RectangleF((float)left, (float)top,
                box.NestedViewportSize.Width, box.NestedViewportSize.Height);
        }

        var vp = LayoutEnvironment?.ViewportSize ?? SizeF.Empty;
        return new RectangleF(0, 0, vp.Width, vp.Height);
    }

    private void GetAbsoluteContainingBlockPaddingBox(CssBox cb,
        out double cbPadLeft,
        out double cbPadTop,
        out double cbPadWidth,
        out double cbPadHeight)
    {
        // CSS Grid §9: an absolutely-positioned grid item's containing block is
        // the grid area the grid container's track-sizing pass resolved for it,
        // not the container's padding box. All abspos size/offset resolution
        // routes through here, so returning the area makes width/height/inset
        // percentages and the static position use it uniformly.
        if (GridAreaContainingBlock is { } gridArea)
        {
            cbPadLeft = gridArea.Left;
            cbPadTop = gridArea.Top;
            cbPadWidth = gridArea.Width;
            cbPadHeight = gridArea.Height;
            return;
        }

        if (IsInlineContainingBlock(cb))
        {
            var bbox = GetInlineBoundingBox(cb);
            if (bbox != RectangleF.Empty)
            {
                cbPadLeft = bbox.Left;
                cbPadTop = bbox.Top;
                cbPadWidth = bbox.Width;
                cbPadHeight = bbox.Height;
                return;
            }
        }

        if (IsInitialContainingBlock(cb))
        {
            cbPadLeft = 0;
            cbPadTop = 0;
            cbPadWidth = LayoutEnvironment.ViewportSize.Width;
            cbPadHeight = LayoutEnvironment.ViewportSize.Height;
            return;
        }

        // RF-BRIDGE-1b Track 3.2: a nested browsing context's sub-viewport acts as the
        // initial containing block for its subtree — an abspos descendant with no
        // positioned ancestor resolves against the frame content box. Use the box's own
        // (pre-translate) content box: its used Size is the sub-viewport, and its
        // Location is composed onto the frame's content origin by the later
        // LayoutSubdocument translate, exactly as the abspos static position is.
        if (cb.IsNestedViewportRoot)
        {
            cbPadLeft = cb.Location.X + cb.ActualBorderLeftWidth + cb.ActualPaddingLeft;
            cbPadTop = cb.Location.Y + cb.ActualBorderTopWidth + cb.ActualPaddingTop;
            cbPadWidth = cb.NestedViewportSize.Width;
            cbPadHeight = cb.NestedViewportSize.Height;
            return;
        }

        cbPadLeft = cb.Location.X + cb.ActualBorderLeftWidth;
        cbPadTop = cb.Location.Y + cb.ActualBorderTopWidth;
        cbPadWidth = cb.Size.Width - cb.ActualBorderLeftWidth - cb.ActualBorderRightWidth;
        cbPadHeight = (cb.ActualBottom - cb.Location.Y) - cb.ActualBorderTopWidth - cb.ActualBorderBottomWidth;

        // Block-axis self-alignment of an absolutely positioned descendant can
        // run before the containing block has resolved its own block size:
        // heights resolve bottom-up, yet abspos children are positioned during
        // the CB's layout, so cb.ActualBottom may still equal cb.Location.Y and
        // cbPadHeight collapses to ~0 — leaving align-self with no IMCB to work
        // within (the box stays at its static position).  Widths resolve
        // top-down, so cbPadWidth is already correct; this only patches the
        // height.  When the CB carries a definite (non-percentage) specified
        // height, derive the padding-box height from it directly.
        if (cbPadHeight <= 0
            && cb.Height != CssConstants.Auto && !string.IsNullOrEmpty(cb.Height)
            && !cb.Height.Contains('%'))
        {
            double cssHeight = CssLengthParser.ParseLength(cb.Height, 0, cb.GetEmHeight());
            double borderBoxHeight = cb.ResolveSpecifiedHeightToBorderBox(cssHeight);
            double candidate = borderBoxHeight - cb.ActualBorderTopWidth - cb.ActualBorderBottomWidth;

            if (candidate > cbPadHeight)
                cbPadHeight = candidate;
        }
    }

    /// <summary>
    /// CSS2.1 §10.1: When the containing block for an absolutely positioned
    /// element is formed by an inline-level element, the containing block is
    /// the bounding box around the padding boxes of the first and last inline
    /// boxes generated for that element.  Returns the bounding rectangle in
    /// absolute coordinates, or <see cref="RectangleF.Empty"/> if the inline
    /// has no line-box rectangles and no laid-out children.
    /// </summary>
    private static RectangleF GetInlineBoundingBox(CssBox cb)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        // Accumulate extents from one box (the original or a fragment).
        void AccumulateBox(CssBox box)
        {
            // Try the inline's own Rectangles (populated when the
            // inline element has direct text words).
            foreach (var rect in box.Rectangles.Values)
            {
                if (rect.Left < minX) minX = rect.Left;
                if (rect.Top < minY) minY = rect.Top;
                if (rect.Right > maxX) maxX = rect.Right;
                if (rect.Bottom > maxY) maxY = rect.Bottom;
            }

            // Also scan child boxes (inline-blocks etc.) for their
            // laid-out positions and sizes.
            foreach (var child in box.Boxes)
            {
                // CSS2.1 §10.1: the inline containing block's extent is the box
                // around the inline's own (in-flow) line boxes. An out-of-flow
                // (absolutely/fixed positioned) descendant is not part of that
                // extent — and while it is being positioned its transient static
                // Location would otherwise pollute the bounds it is measured
                // against (e.g. drag the CB top to 0), corrupting its own inset.
                if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                    continue;

                if (child.Size.Width <= 0 && child.Size.Height <= 0)
                    continue;

                float left = child.Location.X;
                float top = child.Location.Y;
                float right = left + child.Size.Width;
                float bottom = (float)child.ActualBottom;

                if (bottom <= top) bottom = top + child.Size.Height;

                if (left < minX) minX = left;
                if (top < minY) minY = top;
                if (right > maxX) maxX = right;
                if (bottom > maxY) maxY = bottom;
            }
        }

        // Scan the original box.
        AccumulateBox(cb);

        // If the positioned inline was split by the block-inside-inline
        // correction, also scan inline fragment copies that received its
        // children so the bounding box covers the full inline extent.
        // Only include fragments that are still inline — block-level
        // anonymous wrappers created during the split are structural
        // containers, not inline fragments.
        if (cb.SplitFragments != null)
        {
            foreach (var frag in cb.SplitFragments)
            {
                if (frag.Display == CssConstants.Inline)
                    AccumulateBox(frag);
            }
        }

        if (minX > maxX || minY > maxY)
            return RectangleF.Empty;

        return new RectangleF(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Returns <c>true</c> when the given box is a pure inline element
    /// (not inline-block/inline-table etc.) whose containing-block extent
    /// must be computed from its line-box rectangles per CSS2.1 §10.1.
    /// </summary>
    private static bool IsInlineContainingBlock(CssBox cb) => cb.Display == CssConstants.Inline;

    /// <summary>
    /// Returns true when <see cref="Height"/> is a percentage that resolves
    /// to auto because the containing block's height is not explicitly
    /// specified (CSS 2.1 §10.5).  Callers must still verify that Height is
    /// not auto/empty before using this — the check only tests whether a
    /// non-auto percentage value should be treated as auto.
    /// </summary>
    internal bool HeightPercentageResolvesToAuto()
    {
        if (!Height.Contains('%'))
            return false;

        // CSS 2.1 §10.5: "A percentage height on the root element is
        // relative to the initial containing block."  The initial
        // containing block always has a definite height (the viewport),
        // so percentage heights on the root element never resolve to auto.
        if (ContainingBlock?.ParentBox == null)
            return false;

        // CSS 2.1 §10.5: the "resolves to auto when the containing block's
        // height is indefinite" rule applies only when "this element is not
        // absolutely positioned".  An absolutely (or fixed) positioned box's
        // containing block is the padding box of its positioned ancestor (or
        // the viewport for the initial containing block), whose height is
        // always definite — so a percentage height never resolves to auto.
        if (Position == CssConstants.Absolute || Position == CssConstants.Fixed)
            return false;

        // CSS Sizing 4 §4: a containing block whose height is auto but that has a
        // preferred aspect-ratio and a definite used width has a definite used
        // block size (its transferred aspect-ratio height), so a percentage height
        // resolves against it rather than to auto — matching the reference browser,
        // which sizes a filling child to the aspect-ratio square.
        if (ContainingBlock.HasDefiniteAspectRatioBlockHeight())
            return false;

        return ContainingBlock.Height == CssConstants.Auto || string.IsNullOrEmpty(ContainingBlock.Height);
    }

    /// <summary>CSS Sizing 4 §4: <c>true</c> when this box's block (height) axis is
    /// <c>auto</c> but resolvable from its used width and preferred
    /// <c>aspect-ratio</c>, so its used height is definite for percentage-height
    /// descendants. Scoped to in-flow block-level boxes, matching
    /// <see cref="TryResolveAspectRatioBlockHeight"/>'s applicability.</summary>
    internal bool HasDefiniteAspectRatioBlockHeight() =>
        (Height == CssConstants.Auto || string.IsNullOrEmpty(Height))
        && Display == CssConstants.Block
        && Float == CssConstants.None
        && Position != CssConstants.Absolute && Position != CssConstants.Fixed
        && !IsImage
        && TryResolveAspectRatioBlockHeight(out _);

    /// <summary>
    /// CSS2.1 §10.5: the containing-block height a percentage <c>height</c>
    /// (or percentage <c>min-/max-height</c>) resolves against.  For
    /// fixed-position boxes this is the viewport; for other absolutely
    /// positioned boxes it is the height of the <em>positioned</em> containing
    /// block's padding box (the viewport when that is the initial containing
    /// block) — an abspos box's containing block always has a definite height,
    /// unlike the flow containing block, whose height may be auto/indefinite.
    /// Otherwise the flow containing block's used height is returned.
    /// </summary>
    private double PercentageHeightContainingBlockHeight()
    {
        if (Position == CssConstants.Fixed && LayoutEnvironment != null)
            return FixedPositioningViewport().Height;

        if (Position == CssConstants.Absolute)
        {
            var cb = FindPositionedContainingBlock();
            GetAbsoluteContainingBlockPaddingBox(cb, out _, out _, out _, out double cbHeight);
            return cbHeight;
        }

        if (ContainingBlock?.ParentBox == null && LayoutEnvironment != null)
            return LayoutEnvironment.ViewportSize.Height;

        // A flow containing block with a definite (non-auto, non-percentage)
        // specified height exposes that height to its percentage-height
        // children even before its own block size is applied. Block heights
        // resolve bottom-up — children lay out (and resolve their percentages)
        // before the containing block sets its used height — and a fixed-height
        // box, unlike a percentage-height one, is not pre-resolved into
        // Size.Height (see the §10.5 pre-resolution in the layout pass). Reading
        // Size.Height here would then yield 0 and collapse every percentage-height
        // child. Derive the basis straight from the specification instead, the
        // same way the abspos IMCB fallback does for a definite containing block.
        var flowCb = ContainingBlock;
        if (flowCb != null && flowCb.Height != CssConstants.Auto && !string.IsNullOrEmpty(flowCb.Height)
            && !flowCb.Height.Contains('%'))
        {
            double cssHeight = CssLengthParser.ParseLength(flowCb.Height, 0, flowCb.GetEmHeight());

            if (cssHeight > 0)
                return flowCb.ResolveSpecifiedHeightToBorderBox(cssHeight);
        }

        return flowCb?.Size.Height ?? 0;
    }

}
