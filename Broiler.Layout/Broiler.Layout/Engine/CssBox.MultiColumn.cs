using Broiler.CSS;
using System.Drawing;


namespace Broiler.Layout.Engine;

internal partial class CssBox : CssBoxProperties, IDisposable
{
    /// <summary>
    /// CSS Multi-column Layout: Redistributes in-flow child boxes into
    /// multiple columns after single-column layout.  Walks down through
    /// single-child containers (e.g. html to body) to find the actual
    /// fragmentable children.
    /// </summary>
    private void ApplyMultiColumnLayout(int colCount)
    {
        // PROTOTYPE (BROILER_VERTICAL_FLOW), Stage 4: in a vertical writing
        // mode the whole subtree is laid out in a logical horizontal frame and
        // rotated into physical space after layout (ApplyVerticalWritingModeFlow).
        // Multi-column fragmentation runs here, in that logical frame, exactly as
        // it does for horizontal-tb: columns advance along the logical inline
        // axis (X). The post-layout rotation then maps the logical inline axis
        // onto the physical inline axis, so the columns stack along the writing
        // mode's inline direction — logical left→right becomes physical top→bottom.
        // Verified against Chromium for css-position/multicol/static-position/
        // vlr-in-multicol-ref.html: an 80×600 logical run fragmented across
        // 100px-tall columns rotates to a 100px-wide × 480px-tall vertical strip
        // (diff 17.3% → ~1.9% vs the legacy single-block run). Right→left modes
        // (vertical-rl / sideways-rl) fragment identically here and are then
        // block-start (right) aligned by ApplyVerticalWritingModeFlow.
        double columnGap = ResolveColumnGap();
        double contentWidth = Size.Width - ActualPaddingLeft - ActualPaddingRight
            - ActualBorderLeftWidth - ActualBorderRightWidth;
        double columnWidth = (contentWidth - (colCount - 1) * columnGap) / colCount;
        if (columnWidth <= 0) return;

        double containerTop = Location.Y + ActualPaddingTop + ActualBorderTopWidth;
        double columnLeft = Location.X + ActualPaddingLeft + ActualBorderLeftWidth;

        // Walk down through single-child containers (html -> body) to find
        // the level with multiple block children to distribute.
        var fragmentParent = FindMultiColumnFragmentParent();
        if (fragmentParent == null)
            return;

        var fragments = new List<CssBox>();

        foreach (var child in fragmentParent.Boxes)
        {
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;

            if (child.Display == CssConstants.None)
                continue;

            fragments.Add(child);
        }

        if (fragments.Count == 0)
            return;

        double firstTop = fragments[0].Location.Y;
        double lastBottom = GetVisualBottom(fragments[^1]);

        foreach (var frag in fragments)
        {
            double vb = GetVisualBottom(frag);

            if (vb > lastBottom)
                lastBottom = vb;
        }

        double totalContentHeight = lastBottom - firstTop;

        if (totalContentHeight <= 0)
            return;

        // Determine column height: balanced columns for auto/max-height,
        // or explicit height.
        bool hasMaxHeight = MaxHeight != "none" && !string.IsNullOrEmpty(MaxHeight);
        bool hasExplicitHeight = Height != CssConstants.Auto && !string.IsNullOrEmpty(Height);
        double maxAllowedHeight = double.MaxValue;

        if (hasMaxHeight)
        {
            double maxH = ParseUsedLength(MaxHeight, ContainingBlock?.Size.Height ?? Size.Height);
            maxAllowedHeight = maxH - ActualPaddingTop - ActualPaddingBottom - ActualBorderTopWidth - ActualBorderBottomWidth;
        }

        double columnHeight;
        if (hasExplicitHeight)
        {
            double h = ParseUsedLength(Height, ContainingBlock?.Size.Height ?? Size.Height);
            columnHeight = h;
        }
        else if (ColumnFill == "auto" && hasMaxHeight)
        {
            // column-fill: auto — fill columns sequentially up to max-height.
            columnHeight = maxAllowedHeight;
        }
        else
        {
            // Balanced column layout: find the minimum column height that
            // distributes all fragments across colCount columns.  Use a
            // binary search between (tallest fragment) and (total height).
            double lo = 0;

            foreach (var frag in fragments)
            {
                double fh = GetVisualBottom(frag) - frag.Location.Y;

                if (fh > lo)
                    lo = fh;
            }

            double hi = totalContentHeight;

            for (int iter = 0; iter < 20; iter++)
            {
                double mid = (lo + hi) / 2;
                int cols = CountColumnsNeededVisual(fragments, mid);

                if (cols <= colCount)
                    hi = mid;
                else
                    lo = mid + 0.5;
            }

            columnHeight = Math.Ceiling(hi);

            if (columnHeight > maxAllowedHeight)
                columnHeight = maxAllowedHeight;
        }

        if (columnHeight <= 0)
            return;

        // CSS Fragmentation §3: When fragments contain boxes with visible
        // overflow that exceeds the column height (e.g. height: 0 parents
        // with overflowing children), flatten the hierarchy by collecting
        // the deepest fragmentable blocks from inside those containers.
        bool needsDeepFragment = false;

        foreach (var frag in fragments)
        {
            double visualH = GetVisualBottom(frag) - frag.Location.Y;

            if (visualH > columnHeight + 0.5 && frag.Boxes.Count > 0)
            {
                needsDeepFragment = true;
                break;
            }
        }

        if (needsDeepFragment)
        {
            var deepFragments = new List<CssBox>();

            foreach (var frag in fragments)
            {
                double visualH = GetVisualBottom(frag) - frag.Location.Y;

                if (visualH > columnHeight + 0.5 && frag.Boxes.Count > 0)
                {
                    CollectFragmentableBlocksCore(frag, columnHeight, deepFragments, 0);
                }
                else
                {
                    deepFragments.Add(frag);
                }
            }

            if (deepFragments.Count > fragments.Count)
            {
                fragments = deepFragments;
                firstTop = fragments[0].Location.Y;
                lastBottom = firstTop;

                foreach (var frag in fragments)
                {
                    double vb = GetVisualBottom(frag);
                    if (vb > lastBottom)
                        lastBottom = vb;
                }

                totalContentHeight = lastBottom - firstTop;

                // Re-compute balanced column height for the new fragment set.
                if (!hasExplicitHeight && !(ColumnFill == "auto" && hasMaxHeight))
                {
                    double lo = 0;

                    foreach (var frag in fragments)
                    {
                        double fh = GetVisualBottom(frag) - frag.Location.Y;
                        if (fh > lo)
                            lo = fh;
                    }

                    double hi = totalContentHeight;

                    for (int iter = 0; iter < 20; iter++)
                    {
                        double mid = (lo + hi) / 2;
                        int cols = CountColumnsNeededVisual(fragments, mid);

                        if (cols <= colCount) hi = mid;
                        else lo = mid + 0.5;
                    }

                    columnHeight = Math.Ceiling(hi);

                    if (columnHeight > maxAllowedHeight)
                        columnHeight = maxAllowedHeight;
                }
            }
        }

        // Distribute fragments across columns.
        int currentCol = 0;
        double currentY = containerTop;

        foreach (var frag in fragments)
        {
            double fragHeight = GetVisualBottom(frag) - frag.Location.Y;

            bool wouldOverflow = (currentY - containerTop) + fragHeight > columnHeight;
            // CSS Multi-column §3.3: column-count sets the column *width* but is
            // not a hard cap on the number of columns.  When content does not fit
            // in the determined number of columns (e.g. column-fill: auto with a
            // constrained block-size), additional "overflow columns" are created
            // in the inline direction rather than piling the remainder into the
            // last column.  Balance mode keeps content within colCount via the
            // height search above, so this only takes effect when genuinely
            // overflowing.
            if (wouldOverflow && currentY > containerTop + 0.5)
            {
                currentCol++;
                currentY = containerTop;
            }

            double targetX = columnLeft + currentCol * (columnWidth + columnGap)
                + (frag.Location.X - fragmentParent.Location.X);
            double targetY = currentY;

            double dx = targetX - frag.Location.X;
            double dy = targetY - frag.Location.Y;

            if (Math.Abs(dx) > 0.1 || Math.Abs(dy) > 0.1)
            {
                frag.OffsetLeft(dx);
                frag.OffsetTop(dy);
            }

            if (frag.Size.Width > columnWidth + 1)
            {
                frag.Size = new SizeF((float)columnWidth, frag.Size.Height);
            }

            currentY += fragHeight;
        }

        // Update container dimensions.
        double maxBottom = containerTop;
        foreach (var frag in fragments)
        {
            double vb = GetVisualBottom(frag);
            if (vb > maxBottom)
                maxBottom = vb;
        }

        double newBottom = maxBottom + ActualPaddingBottom + ActualBorderBottomWidth;
        if (newBottom < ActualBottom)
            ActualBottom = newBottom;

        if (fragmentParent != this)
        {
            double fpBottom = maxBottom + fragmentParent.ActualPaddingBottom + fragmentParent.ActualBorderBottomWidth;
            if (fpBottom < fragmentParent.ActualBottom)
                fragmentParent.ActualBottom = fpBottom;
        }

        // Overflow columns extend the inline (right) edge beyond the declared
        // column-count, so size the scrollable/overflow extent to the columns
        // actually used rather than the specified count.
        int usedCols = Math.Max(colCount, currentCol + 1);
        double rightEdge = columnLeft + usedCols * columnWidth + (usedCols - 1) * columnGap
            + ActualPaddingRight + ActualBorderRightWidth;

        if (rightEdge > ActualRight)
            ActualRight = rightEdge;
    }

    /// <summary>
    /// Walks down through single-child containers to find the nearest
    /// descendant with multiple in-flow block children for multi-column
    /// fragmentation.
    /// </summary>
    private CssBox FindMultiColumnFragmentParent()
    {
        CssBox current = this;

        for (int depth = 0; depth < 10; depth++)
        {
            int inFlowCount = 0;
            CssBox onlyChild = null;

            foreach (var child in current.Boxes)
            {
                if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                    continue;

                if (child.Display == CssConstants.None)
                    continue;

                inFlowCount++;
                onlyChild = child;
            }

            if (inFlowCount > 1)
                return current;

            if (inFlowCount == 1 && onlyChild != null && onlyChild.Boxes.Count > 0)
            {
                current = onlyChild;
                continue;
            }

            break;
        }

        return current.Boxes.Count > 1 ? current : null;
    }


    /// <summary>
    /// Counts columns needed using visual (overflow-aware) heights.
    /// </summary>
    private static int CountColumnsNeededVisual(List<CssBox> fragments, double columnHeight)
    {
        int cols = 1;
        double currentH = 0;

        foreach (var frag in fragments)
        {
            double fh = GetVisualBottom(frag) - frag.Location.Y;

            if (currentH + fh > columnHeight && currentH > 0.5)
            {
                cols++;
                currentH = fh;
            }
            else
            {
                currentH += fh;
            }
        }

        return cols;
    }

    /// <summary>
    /// Returns the visual bottom of a box, accounting for children that
    /// overflow a constrained height (e.g. height: 0 with visible overflow).
    /// </summary>
    private static double GetVisualBottom(CssBox box)
    {
        double bottom = box.ActualBottom;
        foreach (var child in box.Boxes)
        {
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;

            if (child.Display == CssConstants.None)
                continue;

            double cb = GetVisualBottom(child);

            if (cb > bottom)
                bottom = cb;
        }

        return bottom;
    }


    private static void CollectFragmentableBlocksCore(CssBox parent, double columnHeight, List<CssBox> result, int depth)
    {
        if (depth > 15)
            return; // safety limit

        foreach (var child in parent.Boxes)
        {
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;

            if (child.Display == CssConstants.None)
                continue;

            double childHeight = GetVisualBottom(child) - child.Location.Y;

            // If child fits in a column, or has break-inside: avoid, or
            // has no block children to further fragment, keep it as-is.
            bool avoidBreak = child.BreakInside == "avoid" || child.BreakInside == "avoid-column";
            bool hasBlockChildren = false;
            
            foreach (var gc in child.Boxes)
            {
                if (gc.Position != CssConstants.Absolute && gc.Position != CssConstants.Fixed
                    && gc.Display != CssConstants.None)
                {
                    hasBlockChildren = true;
                    break;
                }
            }

            if (childHeight <= columnHeight + 0.5 || avoidBreak || !hasBlockChildren)
            {
                result.Add(child);
            }
            else
            {
                // Recurse: this child is too tall and can be fragmented.
                CollectFragmentableBlocksCore(child, columnHeight, result, depth + 1);
            }
        }
    }
}
