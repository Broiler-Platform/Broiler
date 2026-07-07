using Broiler.CSS;
using System.Drawing;


namespace Broiler.Layout.Engine;

internal partial class CssBox : CssBoxProperties, IDisposable
{
    /// <summary>
    /// CSS Box Model 4 §6.2: Applies <c>margin-trim</c> to this box by zeroing
    /// the block-start margin of its first in-flow block-level child and/or the
    /// block-end margin of its last in-flow block-level child, as requested by
    /// the property value (<c>block</c>, <c>block-start</c>, <c>block-end</c>).
    /// Inline-axis trimming is not yet supported.
    /// </summary>
    private void ApplyMarginTrim()
    {
        if (string.IsNullOrEmpty(MarginTrim) || MarginTrim == CssConstants.None)
            return;

        bool trimBlockStart = false;
        bool trimBlockEnd = false;

        foreach (var token in MarginTrim.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
        {
            switch (token.ToLowerInvariant())
            {
                case "block":
                    trimBlockStart = true;
                    trimBlockEnd = true;
                    break;

                case "block-start":
                    trimBlockStart = true;
                    break;

                case "block-end":
                    trimBlockEnd = true;
                    break;
            }
        }

        if (!trimBlockStart && !trimBlockEnd)
            return;

        CssBox first = null;
        CssBox last = null;

        foreach (var child in Boxes)
        {
            if (child.Display == CssConstants.None
                || child.Position == CssConstants.Absolute
                || child.Position == CssConstants.Fixed
                || child.Float != CssConstants.None
                || child.IsInline)
                continue;

            first ??= child;
            last = child;
        }

        if (trimBlockStart && first != null)
            first.MarginTop = "0";

        if (trimBlockEnd && last != null)
            last.MarginBottom = "0";
    }

    protected double MarginTopCollapse(CssBoxProperties prevSibling)
    {
        double value;

        if (prevSibling != null)
        {
            // CSS2.1 §8.3.1: When the previous sibling is an "empty" box
            // (zero content height, no borders/padding, height auto/0), its
            // own top and bottom margins — and its children's margins —
            // collapse through.  The resulting collapsed margin participates
            // in collapsing with this element's top margin.
            if (prevSibling is CssBox prevBox && CssBoxHelper.IsEmptyCollapsible(prevBox))
            {
                double maxPos = Math.Max(ActualMarginTop, 0);
                double maxNeg = Math.Min(ActualMarginTop, 0);
                CssBoxHelper.CollectEmptyBoxMargins(prevBox, ref maxPos, ref maxNeg);
                double collapsed = maxPos + maxNeg; // maxNeg <= 0

                // Subtract the portion of the collapsed margin already
                // consumed when positioning the empty box itself (its
                // CollapsedMarginTop was recorded during its own layout).
                value = collapsed - prevBox.CollapsedMarginTop;
            }
            else
            {
                // CSS2.1 §8.3.1: Adjoining vertical margins collapse.
                // When both are positive → max(m1, m2).
                // When one is negative  → max(positives,0) + min(negatives,0).
                // When both are negative → 0 + min(m1,m2) = most-negative.
                // The general formula covers all three cases.
                // Use GetPropagatedMarginBottom so that a last-child's
                // bottom margin propagates through its parent when the
                // parent has no bottom border/padding and auto height
                // (CSS 2.1 §8.3.1 parent-child bottom-margin collapse).
                double prevMb = (prevSibling is CssBox prevSibBox)
                    ? CssBoxHelper.GetPropagatedMarginBottom(prevSibBox)
                    : prevSibling.ActualMarginBottom;
                double maxPos = Math.Max(
                    Math.Max(prevMb, 0),
                    Math.Max(ActualMarginTop, 0));
                double minNeg = Math.Min(
                    Math.Min(prevMb, 0),
                    Math.Min(ActualMarginTop, 0));

                value = maxPos + minNeg;
            }

            CollapsedMarginTop = value;
        }
        else if (_parentBox != null && _parentBox.ActualPaddingTop < 0.1 && _parentBox.ActualPaddingBottom < 0.1 && _parentBox.ActualBorderTopWidth < 0.1 && _parentBox.ActualBorderBottomWidth < 0.1
            // CSS Box Alignment §5.4: align-content != normal establishes
            // a BFC, which prevents parent–child margin collapsing.
            && (_parentBox.AlignContent == null || _parentBox.AlignContent == "normal"))
        {
            double parentEffective = Math.Max(_parentBox.ActualMarginTop, _parentBox.CollapsedMarginTop);

            // CSS2.1 §8.3.1: First in-flow child's top margin collapses
            // with the parent's top margin when the parent has no top
            // border and no top padding.  When the child's margin
            // exceeds the parent's, propagate the excess upward by
            // shifting the parent's position down.  Only do this for
            // non-root containers (not html/body) to avoid disturbing
            // the root element's established position.
            if (ActualMarginTop > parentEffective + 0.1
                && _parentBox.ParentBox != null
                && _parentBox.ParentBox.ParentBox != null)
            {
                double propagation = ActualMarginTop - parentEffective;

                _parentBox.Location = new PointF(
                    _parentBox.Location.X,
                    _parentBox.Location.Y + (float)propagation);
                _parentBox.CollapsedMarginTop = ActualMarginTop;

                value = 0;
            }
            else
            {
                value = Math.Max(0, ActualMarginTop - parentEffective);
            }
        }
        else
        {
            value = ActualMarginTop;

            // When the parent establishes a BFC (e.g. via align-content),
            // the first child's margin is fully consumed for positioning.
            // Record it so that an empty-collapsible sibling can subtract
            // the already-consumed portion during its own collapse.
            if (_parentBox != null
                && _parentBox.AlignContent != null
                && _parentBox.AlignContent != "normal")
            {
                CollapsedMarginTop = value;
            }
        }

        // fix for hr tag
        if (value < 0.1 && HtmlTag != null && HtmlTag.Name == "hr")
            value = GetEmHeight() * 1.1f;

        return value;
    }

    public bool BreakPage()
    {
        var container = LayoutEnvironment;

        if (Size.Height >= container.PageSize.Height)
            return false;

        var remTop = (Location.Y - container.MarginTop) % container.PageSize.Height;
        var remBottom = (ActualBottom - container.MarginTop) % container.PageSize.Height;

        if (remTop > remBottom)
        {
            var diff = container.PageSize.Height - remTop;
            Location = new PointF(Location.X, (float)(Location.Y + diff + 1));
            
            return true;
        }

        return false;
    }

    private double CalculateActualRight()
    {
        if (ActualRight <= 90999)
            return ActualRight;

        var maxRight = 0d;

        foreach (var box in Boxes)
            maxRight = Math.Max(maxRight, box.ActualRight + box.ActualMarginRight);

        return maxRight + ActualPaddingRight + ActualMarginRight + ActualBorderRightWidth;
    }

    private double MarginBottomCollapse()
    {
        double margin = 0;

        // NOTE: When the last in-flow child's bottom margin collapses through
        // this box (computed below, once the last child is known) the collapsed
        // margin is NOT included in this box's height — it is external spacing
        // propagated to the parent via GetPropagatedMarginBottom().  The
        // `margin` variable stays 0.

        // CSS2.1 §10.6.3 / §10.6.7: Floated children contribute to the
        // height of their parent only when the parent establishes a new
        // block formatting context (BFC).  Non-BFC blocks (e.g. a plain
        // <ul> inside a floated <dd>) must not include descendant floats
        // in their height calculation.
        bool isBfc = Float != CssConstants.None
            || Display == CssConstants.InlineBlock
            || Display == CssConstants.TableCell
            || (Overflow != null && Overflow != CssConstants.Visible)
            || Position == CssConstants.Absolute
            || Position == CssConstants.Fixed
            || (AlignContent != null && AlignContent != "normal");

        // Use the maximum ActualBottom across all children to handle
        // floated children that may not be the last in source order.
        // Initialize to the content-area top so that padding is preserved
        // even when all children are floated (CSS2.1 §10.6.3: content
        // height is zero but padding is additive).
        double maxChildBottom = Location.Y + ActualBorderTopWidth + ActualPaddingTop;
        CssBox lastInFlowChild = null;
        
        foreach (var child in Boxes)
        {
            // CSS2.1 §10.6.3: Only children in the normal flow are taken
            // into account.  Absolutely positioned and fixed-position boxes
            // are out of flow and must not influence the parent's auto height.
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;

            if (!isBfc && child.Float != CssConstants.None)
                continue;

            // CSS2.1 §9.4.3: Relative positioning is visual-only and
            // does not affect the flow position used for auto-height
            // calculation.  Undo the relative offset so the parent
            // measures the child's normal-flow bottom.
            double childBottom = child.ActualBottom;

            if (child.Position == CssConstants.Relative)
                childBottom -= CssBoxHelper.GetRelativeOffsetY(child);

            maxChildBottom = Math.Max(maxChildBottom, childBottom);
            lastInFlowChild = child;
        }

        // CSS2.1 §10.6.7: When a BFC root auto-sizes its height it must
        // extend to contain all descendant floats — not only direct-child
        // floats.  Walk the subtree (stopping at nested BFC boundaries)
        // to find the maximum float bottom.
        if (isBfc)
        {
            double maxFloatDesc = maxChildBottom;

            FindMaxDescendantFloatBottom(this, ref maxFloatDesc);
            maxChildBottom = Math.Max(maxChildBottom, maxFloatDesc);
        }

        // CSS2.1 §8.3.1 / §10.6.3: The auto height extends to the bottom
        // margin-edge of the last in-flow child unless that child's bottom
        // margin collapses through this box.  Collapse-through happens when
        // this box has no bottom border or padding, an auto (or
        // auto-resolved) height, and a block-level last in-flow child.  This
        // must match the condition used by GetPropagatedMarginBottom() (which
        // propagates the same margin to the parent): otherwise the child's
        // margin is double-counted — once inside this box's height and once as
        // external spacing.  Note this does NOT depend on whether this box is
        // its own parent's last child, nor on this box's own bottom margin.
        bool autoHeight = Height == CssConstants.Auto || string.IsNullOrEmpty(Height)
            || (Height.Contains('%')
                && (ContainingBlock == null || ContainingBlock.Height == CssConstants.Auto
                    || string.IsNullOrEmpty(ContainingBlock.Height)));

        bool collapseThrough = lastInFlowChild != null
            && ActualPaddingBottom < 0.1 && ActualBorderBottomWidth < 0.1
            && autoHeight
            && lastInFlowChild.Float == CssConstants.None
            && lastInFlowChild.Display != CssConstants.Inline
            && lastInFlowChild.Display != CssConstants.InlineBlock;

        if (!collapseThrough && lastInFlowChild != null)
            maxChildBottom += lastInFlowChild.ActualMarginBottom;

        return Math.Max(ActualBottom, maxChildBottom + margin + ActualPaddingBottom + ActualBorderBottomWidth);
    }
}
