using Broiler.CSS;
using System.Drawing;
using System.Globalization;


namespace Broiler.Layout.Engine;

internal partial class CssBox : CssBoxProperties, IDisposable
{
    /// <summary>
    /// Whether a concrete <c>justify-self</c> alignment (one that actually shifts
    /// the box) is in effect, after resolving <c>auto</c> to the parent's
    /// <c>justify-items</c> and the legacy <c>text-align:-webkit-*</c> fallback.
    /// Mirrors the resolution in <see cref="PerformLayoutImp"/>'s block
    /// justify-self step; used to avoid double-applying alignment with the
    /// CSS2.1 §10.3.3 over-constrained-margin positioning.
    /// </summary>
    private bool HasConcreteJustifySelf()
    {
        string js = JustifySelf?.Trim().ToLowerInvariant() ?? "auto";
        if (js == "auto")
            js = ParentBox?.JustifyItems?.Trim().ToLowerInvariant() ?? "normal";
        if (js is "normal" or "stretch" or "auto" or "legacy")
        {
            js = (ParentBox?.TextAlign?.Trim().ToLowerInvariant()) switch
            {
                "-webkit-right" => "right",
                "-webkit-center" => "center",
                "-webkit-left" => "left",
                _ => js,
            };
        }
        return js is "center" or "end" or "flex-end" or "self-end" or "right"
            or "start" or "flex-start" or "self-start" or "left";
    }

    protected virtual void PerformLayoutImp(ILayoutEnvironment g)
    {
        if (Display != CssConstants.None)
        {
            RectanglesReset();
            MeasureWordsSize(g);
        }

        // CI fallback for the Broiler.HTML submodule <br> patch
        // (patches/0002-broiler-html-br-after-inline-block.patch): DomParser
        // gives a <br> a ".95em" empty-line height when it "follows a block".
        // An atomic inline-block carries no text words, so it is misclassified
        // as block-level and a <br> after it spuriously inserts a full empty
        // line, pushing every following block sibling ~1em down.  Such a <br>
        // merely ends the inline-block's line, so drop its empty-line height.
        // The previous in-flow sibling (an anonymous block wrapping the
        // inline-block, or the inline-block itself) is already laid out by the
        // time this block runs.  Harmless once the submodule patch lands (the
        // <br> then carries no .95em height to drop).
        if (IsBrElement && !string.IsNullOrEmpty(Height) && Height != CssConstants.Auto
            && CssLayoutEngine.EndsWithAtomicInlineBlock(LayoutBoxUtils.GetPreviousSibling(this)))
        {
            Height = CssConstants.Auto;
        }

        // CSS Box Model 4 §6.2: margin-trim zeroes the block-axis margins of
        // this container's first/last in-flow block-level children before they
        // are laid out, so the trimmed margins collapse to nothing.
        ApplyMarginTrim();

        // CSS2.1 §9.7: an out-of-flow (absolutely/fixed positioned) box has its
        // computed 'display' blockified — an inline-level abspos element (e.g. a
        // positioned <a> or <span>) is laid out as a block. Route it through the
        // block path so it resolves its own used width (shrink-to-fit per §10.3.7)
        // and its inset position (ComputeStaticAndFloatPosition), rather than the
        // inline else-branch which would leave its Size/Location uncomputed and let
        // it report the static line-box rectangle instead of its inset box.
        bool isOutOfFlow = Position == CssConstants.Absolute || Position == CssConstants.Fixed;

        if (IsBlock || isOutOfFlow || Display == CssConstants.ListItem || Display == CssConstants.Table || Display == CssConstants.InlineTable || Display == CssConstants.TableCell || Display == CssConstants.TableCaption)
        {
            // Because their width and height are set by CssTable
            if (Display != CssConstants.TableCell && Display != CssConstants.Table)
            {
                ResolveBlockUsedWidth(g);
            }

            if (Display != CssConstants.TableCell)
            {
                ComputeStaticAndFloatPosition();
            }

            PreResolveDefiniteHeightForDescendants();
            LayoutBlockChildren(g);
        }
        else
        {
            var prevSibling = LayoutBoxUtils.GetPreviousSibling(this);
            if (prevSibling != null)
            {
                if (Location == PointF.Empty)
                    Location = prevSibling.Location;

                ActualBottom = prevSibling.ActualBottom;
            }
        }

        ApplyMultiColumnPostLayout();
        ResolveUsedBlockHeight();
        ApplyMinMaxHeightConstraints();
        ApplyFloatExplicitHeight();
        PositionAbsoluteBox();
        ApplyBlockAlignContent();
        ApplyBlockJustifySelf();
        ApplyRelativePositionOffset();
        CreateListItemBox(g);

        if (!IsFixed)
        {
            var actualWidth = Math.Max(GetMinimumWidth() + CssBoxHelper.GetWidthMarginDeep(this), Size.Width < 90999 ? ActualRight - LayoutEnvironment.RootLocation.X : 0);
            LayoutEnvironment.ActualSize = CommonUtils.Max(LayoutEnvironment.ActualSize, new SizeF((float)actualWidth, (float)(ActualBottom - LayoutEnvironment.RootLocation.Y)));
        }
    }


    /// <summary>
    /// Resolve the used inline size (width) of this block-level box: explicit /
    /// intrinsic-keyword/ shrink-to-fit (abspos, float, orthogonal) widths, the
    /// min/max-widthclamps, and auto-margin centering. Sets Size.Width.
    /// </summary>
    private void ResolveBlockUsedWidth(ILayoutEnvironment g)
    {
        // CSS2.1 §9.6.1: The containing block for a fixed-position
        // element is the viewport (initial containing block).
        // CSS2.1 §10.1: For absolutely positioned elements, the
        // containing block is the padding-box of the nearest
        // positioned ancestor.
        // Use the viewport width for percentage/auto resolution.
        double width;

        if (Position == CssConstants.Fixed && LayoutEnvironment != null)
        {
            width = LayoutEnvironment.ViewportSize.Width;
        }
        else if (Position == CssConstants.Absolute)
        {
            var cb = FindPositionedContainingBlock();
            GetAbsoluteContainingBlockPaddingBox(cb, out _, out _, out width, out _);
        }
        else
        {
            width = ContainingBlock.Size.Width
                    - ContainingBlock.ActualPaddingLeft - ContainingBlock.ActualPaddingRight
                    - ContainingBlock.ActualBorderLeftWidth - ContainingBlock.ActualBorderRightWidth;
        }

        if (IsIntrinsicWidthKeyword(Width) && Float == CssConstants.None && Position != CssConstants.Absolute && Position != CssConstants.Fixed)
        {
            // CSS Sizing 3 §5: width resolves to an intrinsic size
            // (min-content / max-content / fit-content).
            width = ResolveIntrinsicWidth(g, Width, width);
        }
        else if (Width != CssConstants.Auto && !string.IsNullOrEmpty(Width) && !IsIntrinsicWidthKeyword(Width))
        {
            double containingWidth = width;

            width = string.Equals(Width, "inherit", StringComparison.OrdinalIgnoreCase) && GetParent() != null
                ? GetParent().ActualWidth
                : ParseLengthWithLineHeight(Width, containingWidth);

            // CSS2.1 §10.4: Apply max-width constraint
            if (MaxWidth != "none" && !string.IsNullOrEmpty(MaxWidth))
            {
                double maxW = ResolveMaxWidthLength(containingWidth);

                if (width > maxW)
                    width = maxW;
            }

            // CSS2.1 §10.4: Apply min-width constraint (min wins over max per §10.4)
            if (MinWidth != "0" && !string.IsNullOrEmpty(MinWidth))
            {
                double minW = ResolveMinWidthLength(containingWidth);
                if (width < minW) width = minW;
            }

            width = ResolveSpecifiedWidthToBorderBox(width);
        }
        else if ((Position == CssConstants.Absolute || Position == CssConstants.Fixed)
            && Left != null && Left != CssConstants.Auto
            && Right != null && Right != CssConstants.Auto)
        {
            // CSS2.1 §10.3.7: For absolutely positioned, non-replaced
            // elements when width is auto and both left and right are
            // specified, compute width from the constraint equation:
            // left + margin-left + width + margin-right + right = CB width
            double cbContentWidth = width;

            if (Position == CssConstants.Fixed && LayoutEnvironment != null)
                cbContentWidth = LayoutEnvironment.ViewportSize.Width;

            double cssLeft = CssLengthParser.ParseLength(Left, cbContentWidth, GetEmHeight());
            double cssRight = CssLengthParser.ParseLength(Right, cbContentWidth, GetEmHeight());

            width = cbContentWidth - cssLeft - cssRight - ActualMarginLeft - ActualMarginRight;

            if (width < 0)
                width = 0;

            width = ResolveSpecifiedWidthToBorderBox(width);
        }

        // CSS2.1 §10.4: Apply max-width constraint even when
        // Width is auto — the tentative used width must not exceed
        // max-width.

        if (MaxWidth != "none" && !string.IsNullOrEmpty(MaxWidth))
        {
            double maxW = ResolveMaxWidthLength(width);
            maxW = ResolveSpecifiedWidthToBorderBox(maxW);
            if (width > maxW) width = maxW;
        }

        // CSS2.1 §10.4: Apply min-width constraint (min wins over
        // max per §10.4) — also when Width is auto.
        if (MinWidth != "0" && !string.IsNullOrEmpty(MinWidth))
        {
            double minW = ResolveMinWidthLength(width);

            minW = ResolveSpecifiedWidthToBorderBox(minW);

            if (width < minW)
                width = minW;
        }

        Size = new SizeF((float)width, Size.Height);

        // CSS2.1 §10.3.3: For block-level, non-replaced elements in
        // normal flow with an explicit width and auto margins, resolve
        // the auto margins so the element is centered horizontally.
        if (Width != CssConstants.Auto && !string.IsNullOrEmpty(Width)
            && Float == CssConstants.None
            && Position != CssConstants.Absolute && Position != CssConstants.Fixed)
        {
            double containingContentWidth = ContainingBlock.Size.Width
                - ContainingBlock.ActualPaddingLeft - ContainingBlock.ActualPaddingRight
                - ContainingBlock.ActualBorderLeftWidth - ContainingBlock.ActualBorderRightWidth;
            double remainingSpace = containingContentWidth - Size.Width;

            if (MarginLeft == CssConstants.Auto && MarginRight == CssConstants.Auto)
            {
                if (remainingSpace >= 0)
                {
                    string halfMargin = (remainingSpace / 2).ToString("F4", CultureInfo.InvariantCulture) + "px";

                    MarginLeft = halfMargin;
                    MarginRight = halfMargin;
                }
                else
                {
                    MarginLeft = "0";
                    MarginRight = "0";
                }
            }
            else if (MarginLeft == CssConstants.Auto)
            {
                double rightMargin = ActualMarginRight;
                double leftMargin = Math.Max(0, remainingSpace - rightMargin);

                MarginLeft = leftMargin.ToString("F4", CultureInfo.InvariantCulture) + "px";

            }

            else if (MarginRight == CssConstants.Auto)
            {
                double leftMargin = ActualMarginLeft;
                double rightMargin = Math.Max(0, remainingSpace - leftMargin);

                MarginRight = rightMargin.ToString("F4", CultureInfo.InvariantCulture) + "px";
            }
            else if ((IsBlock || Display == CssConstants.ListItem) && remainingSpace >= 0
                     && ContainingBlock?.Position != CssConstants.Absolute
                     && ContainingBlock?.Position != CssConstants.Fixed
                     && !IsVerticalWritingMode(ContainingBlock?.WritingMode ?? WritingMode)
                     && (ContainingBlock?.Direction ?? Direction) == "rtl"
                     && !HasConcreteJustifySelf())
            {
                // CSS2.1 §10.3.3: when width and both margins are
                // specified the box is over-constrained, so one used
                // margin is ignored and solved for. In a left-to-right
                // containing block that is margin-right (and the box
                // stays at its margin-left, which the X computation
                // already honours, so no adjustment is needed). In a
                // right-to-left containing block margin-LEFT is the one
                // ignored, so recompute it from the remaining space —
                // this positions the box against the right edge instead
                // of the left (e.g. a fixed-width block in a dir=rtl
                // container; WPT css-anchor-position/anchor-position-borders).
                // Skipped when a concrete justify-self alignment applies,
                // because that is resolved later (see ApplyBlockJustifySelf)
                // and would otherwise be double-applied.
                double leftMargin = remainingSpace - ActualMarginRight;

                MarginLeft = leftMargin.ToString("F4", CultureInfo.InvariantCulture) + "px";
            }
        }

        // CSS2.1 §10.3.7: Absolutely positioned non-replaced elements
        // with auto width use shrink-to-fit when at least one of
        // left/right is auto.  Shrink-to-fit =
        //   min(max(preferred_minimum, available), preferred)
        if ((Width == CssConstants.Auto || string.IsNullOrEmpty(Width))
            && (Position == CssConstants.Absolute || Position == CssConstants.Fixed)
            && (Left == null || Left == CssConstants.Auto
             || Right == null || Right == CssConstants.Auto))
        {
            // Ensure descendant word sizes (and ActualWordSpacing) are
            // measured before computing intrinsic min/max widths.
            // Without this, word.FullWidth may be NaN because
            // ActualWordSpacing defaults to NaN until MeasureWordSpacing
            // runs, causing the entire shrink-to-fit result to be NaN.
            EnsureDescendantWordsMeasured(g);

            // Compute preferred width by independently measuring each
            // direct child and taking the maximum.  This correctly
            // treats each block/float child as its own "line" and avoids
            // the additive accumulation in GetMinMaxSumWords where a
            // float's width would incorrectly sum with a preceding
            // block child's width.
            double preferred = ComputeShrinkToFitWidth();
            double available = width - ActualMarginLeft - ActualMarginRight;

            GetMinMaxWidth(out double prefMin, out _);

            // Guard against NaN from unmeasured descendants
            if (double.IsNaN(prefMin))
                prefMin = 0;

            if (double.IsNaN(preferred))
                preferred = 0;

            double stfWidth = Math.Min(Math.Max(prefMin, available), preferred);

            if (MaxWidth != "none" && !string.IsNullOrEmpty(MaxWidth))
            {
                double maxW = ResolveMaxWidthLength(width);

                if (stfWidth > maxW)
                    stfWidth = maxW;
            }

            if (MinWidth != "0" && !string.IsNullOrEmpty(MinWidth))
            {
                double minW = ResolveMinWidthLength(width);

                if (stfWidth < minW)
                    stfWidth = minW;
            }

            // CSS2.1 §10.3.7: Shrink-to-fit gives the content
            // width; add own borders and padding for the border-box
            // width that Size.Width represents.
            stfWidth += ActualBorderLeftWidth + ActualBorderRightWidth
                      + ActualPaddingLeft + ActualPaddingRight;

            Size = new SizeF((float)stfWidth, Size.Height);
        }
        else if ((Width == CssConstants.Auto || string.IsNullOrEmpty(Width))
            && Float != CssConstants.None)
        {
            // CSS2.1 §10.3.5: Floating non-replaced elements with
            // 'width: auto' use shrink-to-fit width.
            EnsureDescendantWordsMeasured(g);

            double preferred = ComputeShrinkToFitWidth();
            double available = width - ActualMarginLeft - ActualMarginRight;

            GetMinMaxWidth(out double prefMin, out _);

            if (double.IsNaN(prefMin))
                prefMin = 0;

            if (double.IsNaN(preferred))
                preferred = 0;

            double stfWidth = Math.Min(Math.Max(prefMin, available), preferred);

            if (MaxWidth != "none" && !string.IsNullOrEmpty(MaxWidth))
            {
                double maxW = ResolveMaxWidthLength(width);

                if (stfWidth > maxW)
                    stfWidth = maxW;
            }

            if (MinWidth != "0" && !string.IsNullOrEmpty(MinWidth))
            {
                double minW = ResolveMinWidthLength(width);

                if (stfWidth < minW)
                    stfWidth = minW;
            }

            stfWidth += ActualBorderLeftWidth + ActualBorderRightWidth
                      + ActualPaddingLeft + ActualPaddingRight;

            Size = new SizeF((float)stfWidth, Size.Height);
        }
        else if ((Width == CssConstants.Auto || string.IsNullOrEmpty(Width))
            && Float == CssConstants.None
            && Position != CssConstants.Absolute && Position != CssConstants.Fixed
            && VerticalFlowPrototype.Enabled
            && IsVerticalWritingMode(WritingMode)
            && (ParentBox == null || !IsVerticalWritingMode(ParentBox.WritingMode))
            && ContainingBlock is { ParentBox: not null } orthoCb
            && (string.IsNullOrEmpty(orthoCb.Height) || orthoCb.Height == CssConstants.Auto))
        {
            // CSS Writing Modes 4 §7.3 (auto-sizing in orthogonal flows):
            // a box establishing an orthogonal flow — here a vertical
            // writing-mode box inside a non-vertical containing block — with
            // an auto inline size is sized to fit-content, NOT stretched to
            // the containing block's (perpendicular) inline size. In the
            // vertical-flow prototype this box is laid out in a logical
            // horizontal frame where its logical width IS its inline size, so
            // compute that width as shrink-to-fit; the post-layout rotation
            // (ApplyVerticalWritingModeFlow) then maps it onto physical height.
            // Gated on an indefinite containing-block block size (an auto-height
            // in-flow ancestor) so a definite orthogonal size — a root box
            // filling the viewport, or an explicit-height container — keeps the
            // existing fill behaviour. Without this, an empty (or short)
            // vertical box fills the container width and rotates into a
            // viewport-tall strip instead of collapsing to its content
            // (WPT css-grid/grid-lanes row-subgrid-auto-fill-007).
            EnsureDescendantWordsMeasured(g);

            double preferred = ComputeShrinkToFitWidth();

            // Indefinite orthogonal available inline size falls back to the
            // initial containing block (viewport) block size — here the
            // viewport height, the vertical inline axis's extent.
            double available = LayoutEnvironment?.ViewportSize.Height ?? width;

            GetMinMaxWidth(out double prefMin, out _);

            if (double.IsNaN(prefMin))
                prefMin = 0;

            if (double.IsNaN(preferred))
                preferred = 0;

            double stfWidth = Math.Min(Math.Max(prefMin, available), preferred);

            // Border/padding added for the border-box width Size.Width holds
            // (shrink-to-fit yields a content width). max-width/min-width are
            // physical-width (block-size) constraints and do not clamp the
            // inline size resolved here.
            stfWidth += ActualBorderLeftWidth + ActualBorderRightWidth
                      + ActualPaddingLeft + ActualPaddingRight;

            Size = new SizeF((float)stfWidth, Size.Height);
        }
        else if (IsIntrinsicSizingWidthKeyword(Width))
        {
            // CSS Sizing 3 §5.1: an intrinsic-sizing keyword width resolves
            // to the box's content-based size, not the containing block.
            //   min-content → the min-content (preferred-minimum) width,
            //   max-content → the max-content (preferred) width,
            //   fit-content → min(max(min-content, available), max-content).
            // Without this these keywords fell through to the stretched
            // container width (e.g. a shrink-to-fit grid stayed 1024 instead
            // of its min-width — WPT css-grid grid-auto-repeat-min-size-001).
            // Mirrors the float shrink-to-fit path (content widths + own
            // border/padding for the border-box Size.Width, then min/max-width).
            EnsureDescendantWordsMeasured(g);

            double ownPadBorder = ActualBorderLeftWidth + ActualBorderRightWidth
                                + ActualPaddingLeft + ActualPaddingRight;

            // Both contributions must be in the same frame: ComputeShrinkToFitWidth
            // returns a content-box width, but GetMinMaxWidth returns a border-box
            // one, so strip this box's own padding/border off the min side before
            // combining — otherwise fit-content double-counts it.

            double maxContent = ComputeShrinkToFitWidth();

            GetMinMaxWidth(out double minContentBorderBox, out _);

            if (double.IsNaN(minContentBorderBox))
                minContentBorderBox = 0;

            if (double.IsNaN(maxContent))
                maxContent = 0;

            double minContent = Math.Max(0, minContentBorderBox - ownPadBorder);
            double available = width - ActualMarginLeft - ActualMarginRight;

            double resolved = Width.StartsWith("min-content", StringComparison.OrdinalIgnoreCase)
                ? minContent
                : Width.StartsWith("max-content", StringComparison.OrdinalIgnoreCase)
                    ? maxContent
                    : Math.Min(Math.Max(minContent, available), maxContent); // fit-content

            if (MaxWidth != "none" && !string.IsNullOrEmpty(MaxWidth))
            {
                double maxW = ResolveMaxWidthLength(width);

                if (resolved > maxW)
                    resolved = maxW;
            }

            if (MinWidth != "0" && !string.IsNullOrEmpty(MinWidth))
            {
                double minW = ResolveMinWidthLength(width);

                if (resolved < minW)
                    resolved = minW;
            }

            resolved += ownPadBorder;
            Size = new SizeF((float)resolved, Size.Height);
        }
        else if (Width == CssConstants.Auto || string.IsNullOrEmpty(Width))
        {
            // Margins reduce the box width only for auto-width elements.
            // For explicit widths, margins affect position only (CSS1 box model).
            Size = new SizeF((float)(width - ActualMarginLeft - ActualMarginRight), Size.Height);
        }
    }

    /// <summary>
    /// Compute this box's in-flow static position, then apply float placement
    /// (CSS2.1§9.5 collision resolution), clearance, BFC float avoidance, and
    /// theabsolute/fixed offset overrides. Sets Location / ActualBottom.
    /// </summary>
    private void ComputeStaticAndFloatPosition()
    {
        var prevSibling = LayoutBoxUtils.GetPreviousSibling(this);

        // Compute the static position for all elements (including
        // position:fixed).  Fixed elements need the static position
        // as fallback when offset properties are auto (CSS2.1 §10.6.4).
        {
            double left = ContainingBlock.Location.X + ContainingBlock.ActualPaddingLeft + ActualMarginLeft + ContainingBlock.ActualBorderLeftWidth;

            // CSS2.1 §9.5: floats are out of normal flow. Non-floated
            // blocks must be positioned as if preceding floats do not
            // exist.  For cleared elements this also prevents margin
            // collapsing with the float (CSS2.1 §8.3.1).

            var flowPrev = prevSibling;

            if (Float == CssConstants.None && flowPrev != null && flowPrev.Float != CssConstants.None)
            {
                flowPrev = LayoutBoxUtils.GetPreviousInFlowSibling(flowPrev);
            }

            // CSS2.1 §9.4.3: Relative positioning is visual-only.
            // Use the flow-position bottom (before relative offset)
            // when computing the next sibling's position.
            double flowPrevBottom = flowPrev?.ActualBottom ?? 0;

            if (flowPrev is CssBox flowPrevBox && flowPrevBox.Position == CssConstants.Relative)
                flowPrevBottom -= CssBoxHelper.GetRelativeOffsetY(flowPrevBox);

            // CSS2.1 §8.3.1: MarginTopCollapse may propagate margins
            // and update the parent's Location, so compute it before
            // reading ParentBox.ClientTop.
            double marginCollapse = MarginTopCollapse(flowPrev);
            double top = (flowPrev == null && ParentBox != null ? ParentBox.ClientTop : ParentBox == null ? Location.Y : 0) + marginCollapse + flowPrevBottom;

            // CSS2.1 §10.3.7 / §10.6.4: an out-of-flow box that was flowed
            // through an inline formatting context takes its *static* position
            // from the inline cursor recorded at flow time, not from the block
            // stacking computed above (which does not model inline placement).
            // An axis with auto insets keeps this static position; an explicit
            // inset overrides it below. Without this an abspos with auto insets
            // inside inline content would re-flow its own content from the top
            // of its containing block instead of its in-flow line position.
            if ((Position == CssConstants.Absolute || Position == CssConstants.Fixed)
                && InlineStaticPosition is { } inlineStatic)
            {
                left = inlineStatic.X + ActualMarginLeft;
                top = inlineStatic.Y + ActualMarginTop;
            }

            // --- Float positioning ---
            if (Float != CssConstants.None)
            {
                // Align Y with previous float sibling if consecutive
                if (prevSibling != null && prevSibling.Float != CssConstants.None)
                    top = prevSibling.Location.Y;

                double containerLeft = ContainingBlock.Location.X + ContainingBlock.ActualPaddingLeft + ContainingBlock.ActualBorderLeftWidth;
                double containerRight = ContainingBlock.ClientLeft + ContainingBlock.AvailableWidth;

                double floatHeight = Math.Max(ActualHeight + ActualPaddingTop + ActualPaddingBottom + ActualBorderTopWidth + ActualBorderBottomWidth, 1);

                // Collect all preceding floats in the BFC, including
                // those nested inside non-BFC siblings (CSS2.1 §9.5.1).
                var precedingFloats = CssBoxHelper.CollectPrecedingFloatsInBfc(this);

                // CSS2.1 §9.5.1 rule 4: A floating box's outer top
                // (margin edge) may not be higher than the top of its
                // containing block.  `top` already includes the margin
                // contribution (from MarginTopCollapse), so the outer
                // (margin-edge) top = top - ActualMarginTop.  The
                // constraint outer_top >= ClientTop translates to:
                //   top >= ClientTop + ActualMarginTop
                // This allows negative margins to pull the float above
                // the content-area edge while still honoring the rule.
                if (ParentBox != null)
                    top = Math.Max(top, ParentBox.ClientTop + ActualMarginTop);

                // CSS2.1 §9.5.1 rule 6: The outer top of a floating
                // box may not be higher than the outer top of any
                // block or floated box generated by an element earlier
                // in the source document.
                foreach (var pf in precedingFloats)
                    top = Math.Max(top, pf.Location.Y);

                if (Float == CssConstants.Left)
                {
                    // Iteratively resolve collisions with all prior floats (CSS1 §5.5.25)
                    for (int iter = 0; iter < 100; iter++)
                    {
                        left = containerLeft + ActualMarginLeft;

                        foreach (var floatBox in precedingFloats)
                        {
                            if (floatBox.Float == CssConstants.Left)
                            {
                                double fBottom = floatBox.ActualBottom;

                                if (top < fBottom && top + floatHeight > floatBox.Location.Y)
                                    left = Math.Max(left, floatBox.Location.X + floatBox.Size.Width + floatBox.ActualMarginRight + ActualMarginLeft);
                            }
                        }

                        // Also ensure left float doesn't overlap with right floats
                        double effectiveRight = containerRight;

                        foreach (var floatBox in precedingFloats)
                        {
                            if (floatBox.Float == CssConstants.Right)
                            {
                                double fBottom = floatBox.ActualBottom;

                                if (top < fBottom && top + floatHeight > floatBox.Location.Y)
                                    effectiveRight = Math.Min(effectiveRight, floatBox.Location.X - floatBox.ActualMarginLeft);
                            }
                        }

                        if (left + Size.Width <= effectiveRight)
                            break;

                        // Move below the lowest overlapping float
                        double maxBottom = top;

                        foreach (var floatBox in precedingFloats)
                        {
                            double fBottom = floatBox.ActualBottom;

                            if (top < fBottom && top + floatHeight > floatBox.Location.Y)
                                maxBottom = Math.Max(maxBottom, fBottom);
                        }

                        if (maxBottom <= top)
                            break;

                        top = maxBottom;
                    }
                }
                else if (Float == CssConstants.Right)
                {
                    // Iteratively resolve collisions with all prior floats (CSS1 §5.5.26)
                    for (int iter = 0; iter < 100; iter++)
                    {
                        left = containerRight - Size.Width - ActualMarginRight;

                        // Avoid overlapping with preceding right floats
                        foreach (var floatBox in precedingFloats)
                        {
                            if (floatBox.Float == CssConstants.Right)
                            {
                                double fBottom = floatBox.ActualBottom;

                                if (top < fBottom && top + floatHeight > floatBox.Location.Y)
                                    left = Math.Min(left, floatBox.Location.X - floatBox.ActualMarginLeft - Size.Width - ActualMarginRight);
                            }
                        }

                        // Ensure right float doesn't overlap with left floats
                        double leftFloatEdge = containerLeft;

                        foreach (var floatBox in precedingFloats)
                        {
                            if (floatBox.Float == CssConstants.Left)
                            {
                                double fBottom = floatBox.ActualBottom;

                                if (top < fBottom && top + floatHeight > floatBox.Location.Y)
                                    leftFloatEdge = Math.Max(leftFloatEdge, floatBox.Location.X + floatBox.Size.Width + floatBox.ActualMarginRight);
                            }
                        }

                        if (left >= leftFloatEdge)
                            break;

                        // Move below the lowest overlapping float
                        double maxBottom = top;

                        foreach (var floatBox in precedingFloats)
                        {
                            double fBottom = floatBox.ActualBottom;

                            if (top < fBottom && top + floatHeight > floatBox.Location.Y)
                                maxBottom = Math.Max(maxBottom, fBottom);
                        }

                        if (maxBottom <= top)
                            break;

                        top = maxBottom;
                    }
                }
            }


            // CSS2.1 §8.3.1/§9.5.2: Handle clear property.  Clearance
            // inhibits margin collapsing and pushes the border edge of the
            // cleared element below the bottom outer edge of the relevant
            // floats.  Clearance can be negative when the uncollapsed
            // position is already past the float.
            if (Clear != CssConstants.None)
            {
                double maxFloatBottom = CssBoxHelper.GetMaxFloatBottom(this);

                if (maxFloatBottom > 0)
                {
                    double hypotheticalTop = top;

                    // Compute uncollapsed position: margins are NOT
                    // collapsed when clearance is present (§8.3.1).
                    // Use the effective margin for empty collapsible
                    // boxes (§8.3.1 margin-through-collapse).
                    double uncollapsedTop;

                    if (flowPrev != null)
                    {
                        double prevMarginBottom = (flowPrev is CssBox fpb)
                            ? CssBoxHelper.GetEffectiveMarginBottom(fpb)
                            : flowPrev.ActualMarginBottom;

                        uncollapsedTop = flowPrevBottom
                            + prevMarginBottom
                            + ActualMarginTop;
                    }
                    else if (ParentBox != null)
                    {
                        uncollapsedTop = ParentBox.ClientTop + ActualMarginTop;
                    }
                    else
                    {
                        uncollapsedTop = hypotheticalTop;
                    }

                    // CSS2.2 §9.5.2: Only introduce clearance when the
                    // hypothetical position (where the top border edge
                    // would be if 'clear' were 'none') is NOT past the
                    // relevant floats.  When the margin alone already
                    // places the element past the float, no clearance is
                    // needed and margin collapsing is preserved.
                    if (hypotheticalTop < maxFloatBottom)
                    {
                        // clearance = max(amount to clear float, amount to
                        // reach hypothetical position).  This can be negative.
                        double clearance = Math.Max(
                            maxFloatBottom - uncollapsedTop,
                            hypotheticalTop - uncollapsedTop);

                        top = uncollapsedTop + clearance;
                    }
                }
            }

            // CSS2.1 §9.5: The border box of an element in normal
            // flow that establishes a new BFC must not overlap the
            // margin box of any floats in the same BFC.  Shift the
            // block right past left floats and narrow it to avoid
            // right floats.  If it cannot fit beside the floats,
            // clear below them.
            if (Float == CssConstants.None && Position != CssConstants.Absolute && Position != CssConstants.Fixed)
            {
                bool isBfcRoot = Display == CssConstants.InlineBlock
                    || Display == CssConstants.TableCell
                    || Display is "flex" or "inline-flex" or "grid" or "inline-grid"
                    || (Overflow != null && Overflow != CssConstants.Visible)
                    || (AlignContent != null && AlignContent != "normal");

                if (isBfcRoot)
                {
                    var precedingFloats = CssBoxHelper.CollectPrecedingFloatsInBfc(this);

                    if (precedingFloats.Count > 0)
                    {
                        double containerLeft = ContainingBlock.Location.X + ContainingBlock.ActualPaddingLeft + ContainingBlock.ActualBorderLeftWidth;
                        double containerRight = ContainingBlock.ClientLeft + ContainingBlock.AvailableWidth;
                        double boxHeight = Math.Max(Size.Height, GetEmHeight());

                        // Try to fit beside floats; if not possible, clear
                        // below them.  100 iterations is a safe upper bound
                        // since each iteration advances past at least one
                        // float's bottom edge.
                        for (int bfcIter = 0; bfcIter < 100; bfcIter++)
                        {
                            double leftEdge = containerLeft + ActualMarginLeft;
                            double rightEdge = containerRight - ActualMarginRight;

                            foreach (var fb in precedingFloats)
                            {
                                double fbBottom = fb.ActualBottom + fb.ActualMarginBottom;

                                if (top < fbBottom && top + boxHeight > fb.Location.Y - fb.ActualMarginTop)
                                {
                                    if (fb.Float == CssConstants.Left)
                                        leftEdge = Math.Max(leftEdge, fb.Location.X + fb.Size.Width + fb.ActualMarginRight + ActualMarginLeft);
                                    else if (fb.Float == CssConstants.Right)
                                        rightEdge = Math.Min(rightEdge, fb.Location.X - fb.ActualMarginLeft - ActualMarginRight);
                                }
                            }

                            double availableWidth = rightEdge - leftEdge;

                            if (availableWidth >= Size.Width || availableWidth >= 0)
                            {
                                left = leftEdge;

                                if (availableWidth < Size.Width && (Width == CssConstants.Auto || string.IsNullOrEmpty(Width)))
                                    Size = new SizeF((float)availableWidth, Size.Height);

                                break;
                            }

                            // Cannot fit beside floats — clear below them.
                            double maxFb = top;

                            foreach (var fb in precedingFloats)
                            {
                                double fbBottom = fb.ActualBottom + fb.ActualMarginBottom;

                                if (top < fbBottom && top + boxHeight > fb.Location.Y - fb.ActualMarginTop)
                                    maxFb = Math.Max(maxFb, fbBottom);
                            }

                            if (maxFb <= top)
                                break;

                            top = maxFb;
                        }
                    }
                }
            }

            Location = new PointF((float)left, (float)top);
            ActualBottom = top;
            AbsposLocationFinalized = false;

            // CSS2.1 §10.3.7 / §10.6.4: For absolutely positioned
            // elements with explicit 'top'/'left', override the static
            // position with the CSS-specified offset from the containing
            // block's padding edge.
            if (Position == CssConstants.Absolute)
            {
                var cb = FindPositionedContainingBlock();

                GetAbsoluteContainingBlockPaddingBox(cb, out double cbPadLeft, out double cbPadTop, out double cbPadWidth, out double cbPadHeight);

                float newX = Location.X, newY = Location.Y;

                if (Left != null && Left != CssConstants.Auto)
                {
                    double cssLeft = CssLengthParser.ParseLength(Left, cbPadWidth, GetEmHeight());
                    newX = (float)(cbPadLeft + cssLeft + ActualMarginLeft);
                }
                else if (Right != null && Right != CssConstants.Auto)
                {
                    // CSS2.1 §10.3.7: When left is auto and right is
                    // specified, position from the right padding edge.
                    double cssRight = CssLengthParser.ParseLength(Right, cbPadWidth, GetEmHeight());
                    newX = (float)(cbPadLeft + cbPadWidth - cssRight - ActualMarginRight - Size.Width);
                }

                if (Top != null && Top != CssConstants.Auto)
                {
                    double cssTop = CssLengthParser.ParseLength(Top, cbPadHeight, GetEmHeight());
                    newY = (float)(cbPadTop + cssTop + ActualMarginTop);
                }
                else if (Bottom != null && Bottom != CssConstants.Auto)
                {
                    // CSS2.1 §10.6.4: When top is auto and bottom is
                    // specified, position from the bottom padding edge.
                    double cssBottom = CssLengthParser.ParseLength(Bottom, cbPadHeight, GetEmHeight());
                    double boxHeight = ActualBottom - Location.Y;

                    // boxHeight may be zero when the box position was
                    // just initialised and children have not yet been
                    // laid out.  Fall back to Size.Height which reflects
                    // any explicit CSS height already applied.
                    if (boxHeight <= 0)
                        boxHeight = Size.Height;

                    newY = (float)(cbPadTop + cbPadHeight - cssBottom - ActualMarginBottom - boxHeight);
                }

                Location = new PointF(newX, newY);
                ActualBottom = newY;

                // Location now holds the final left/top offset; the content
                // flow below starts here, so AdjustAbsolutePosition must not
                // add the offset again (WPT css-anchor-position anchor-scroll).
                if (Left != null && Left != CssConstants.Auto || Top != null && Top != CssConstants.Auto)
                    AbsposLocationFinalized = true;
            }

            // CSS2.1 §10.6.4 / §9.6.1: For fixed-position elements,
            // the containing block is the viewport.  When top/left/
            // bottom/right are explicitly set, use those offsets from
            // the viewport edge.  When they are auto, the static
            // position (computed above) is kept.
            if (Position == CssConstants.Fixed && LayoutEnvironment != null)
            {
                bool hasLeft = Left != null && Left != CssConstants.Auto;
                bool hasRight = Right != null && Right != CssConstants.Auto;
                bool hasTop = Top != null && Top != CssConstants.Auto;
                bool hasBottom = Bottom != null && Bottom != CssConstants.Auto;

                if (hasLeft || hasRight || hasTop || hasBottom)
                {
                    var vpSize = LayoutEnvironment.ViewportSize;
                    float newX = Location.X, newY = Location.Y;

                    if (hasLeft)
                    {
                        double cssLeft = CssLengthParser.ParseLength(Left, vpSize.Width, GetEmHeight());
                        newX = (float)(cssLeft + ActualMarginLeft);
                    }
                    else if (hasRight)
                    {
                        double cssRight = CssLengthParser.ParseLength(Right, vpSize.Width, GetEmHeight());
                        newX = (float)(vpSize.Width - cssRight - ActualMarginRight - Size.Width);
                    }

                    if (hasTop)
                    {
                        double cssTop = CssLengthParser.ParseLength(Top, vpSize.Height, GetEmHeight());
                        newY = (float)(cssTop + ActualMarginTop);
                    }
                    else if (hasBottom)
                    {
                        double cssBottom = CssLengthParser.ParseLength(Bottom, vpSize.Height, GetEmHeight());
                        double boxHeight = ActualBottom - Location.Y;

                        if (boxHeight <= 0)
                            boxHeight = Size.Height;

                        newY = (float)(vpSize.Height - cssBottom - ActualMarginBottom - boxHeight);
                    }

                    Location = new PointF(newX, newY);
                    ActualBottom = newY;

                    if (hasLeft || hasTop)
                        AbsposLocationFinalized = true;
                }

                // When all offsets are auto, keep the static position
                // (Location is already set from normal-flow
                // calculation above).
            }
        }
    }

    /// <summary>
    /// Pre-resolve a percentage or aspect-ratio block size from the used width
    /// BEFOREchild layout, so a percentage-height descendant can resolve against
    /// thiscontainer's definite height (CSS2.1 §10.5 / Sizing 4 §4).
    /// </summary>
    private void PreResolveDefiniteHeightForDescendants()
    {
        // CSS2.1 §10.5: Pre-resolve percentage heights so that children
        // can use ContainingBlock.Size.Height for their own percentage
        // height resolution.  This must run AFTER position assignment
        // (which resets Size.Height to 0 via ActualBottom = top) but
        // BEFORE child layout so descendants see the correct height.
        if (Height != CssConstants.Auto && !string.IsNullOrEmpty(Height) && Height.Contains('%') && !HeightPercentageResolvesToAuto())
        {
            double cbHeight = PercentageHeightContainingBlockHeight();
            double length = CssLengthParser.ParseLength(Height, cbHeight, GetEmHeight());
            double preHeight = ResolveSpecifiedHeightToBorderBox(length);

            Size = new SizeF(Size.Width, (float)preHeight);
        }

        // CSS Sizing 4 §4: likewise pre-resolve an aspect-ratio block size from
        // the used width so a percentage-height child (e.g. a filling
        // background element) can resolve against the container's definite
        // aspect-ratio height. The final ActualBottom is re-established after
        // child layout below; this only makes the height visible to
        // descendants beforehand, mirroring the §10.5 pre-resolution above.
        else if ((Height == CssConstants.Auto || string.IsNullOrEmpty(Height))
            && Display == CssConstants.Block
            && Float == CssConstants.None
            && Position != CssConstants.Absolute && Position != CssConstants.Fixed
            && !IsImage
            && TryResolveAspectRatioBlockHeight(out double aspectRatioPreHeight))
        {
            Size = new SizeF(Size.Width, (float)aspectRatioPreHeight);
        }
    }

    /// <summary>
    /// Lay out this block's contents: table, row-flex,
    /// inline-formatting-context,or block children (with multi-column
    /// pre-constraint),then run the flex/grid alignment passes.
    /// </summary>
    private void LayoutBlockChildren(ILayoutEnvironment g)
    {
        //If we're talking about a table here..
        if (Display == CssConstants.Table || Display == CssConstants.InlineTable)
        {
            CssLayoutEngineTable.PerformLayout(g, this, BaseUrl);
        }
        else
        {
            // CSS Flexbox §8.2/§8.4: Map flex alignment properties to
            // CSS2.1 text-align so that the inline formatting context
            // fallback (FlowInlineBlock) produces visually aligned items.
            // This only applies when the author has not set text-align
            // explicitly (i.e. it still has the default 'left' value).
            if (Display is "flex" or "inline-flex" or "grid" or "inline-grid")
            {
                if (JustifyContent is "center" && TextAlign is CssConstants.Left or "start" or "")
                {
                    TextAlign = CssConstants.Center;
                }
                else if (JustifyContent is "flex-end" or "end" && TextAlign is CssConstants.Left or "start" or "")
                {
                    TextAlign = CssConstants.Right;
                }
            }

            if (IsRowFlexContainer())
            {
                PerformFlexRowLayout(g);
            }

            //If there's just inline boxes, create LineBoxes
            else if (LayoutBoxUtils.ContainsInlinesOnly(this))
            {
                ActualBottom = Location.Y;
                CssLayoutEngine.CreateLineBoxes(g, this); //This will automatically set the bottom of this block

                // CSS2.1 §9.5: Floated children were skipped by
                // CreateLineBoxes (they are out-of-flow).  Lay them out
                // now so they are positioned and painted.
                foreach (var childBox in Boxes)
                {
                    if (childBox.Float != CssConstants.None)
                    {
                        childBox.PerformLayout(g);

                        // CSS2.1 §13.3.1: When page-break-inside:avoid is
                        // set on a float's containing block, move the float
                        // to the next page if it would otherwise cross a
                        // page boundary.
                        if (PageBreakInside == CssConstants.Avoid)
                            childBox.BreakPage();
                    }
                }

                // CSS2.1 §10.6.7: Elements that establish a new block
                // formatting context (BFC) must include descendant floats
                // in their auto-height calculation.  The inline path above
                // does not call MarginBottomCollapse(), so BFC elements
                // with only floated children would otherwise have zero
                // content height.
                bool isBfc = Float != CssConstants.None
                    || Display == CssConstants.InlineBlock
                    || Display == CssConstants.TableCell
                    || Display is "flex" or "inline-flex" or "grid" or "inline-grid"
                    || (Overflow != null && Overflow != CssConstants.Visible)
                    || Position == CssConstants.Absolute
                    || Position == CssConstants.Fixed
                    || (AlignContent != null && AlignContent != "normal");

                if (isBfc)
                {
                    ActualBottom = MarginBottomCollapse();
                }

                // CSS Grid Level 1 §8.5: When all grid items share
                // the same grid-row and grid-column, reposition them
                // to the container's content-area origin so they
                // overlap visually.  (This duplicates the same logic
                // in the block path below; it is needed here because
                // ContainsInlinesOnly() forces grid containers into
                // the inline layout path for shrink-to-fit sizing.)
                if (Display is "grid" or "inline-grid")
                    ApplyGridLayoutAfterInline();

                // CSS Box Alignment §6.2: distribute flex/grid items along
                // the block (cross) axis per align-items / align-self.
                ApplyFlexGridCrossAxisAlignment();
                ApplyFlexColumnInlineAxisAlignment();
            }
            else if (Boxes.Count > 0)
            {
                // CSS Multi-column: Pre-constrain width so children
                // lay out at column width instead of full container width.
                float savedWidth = Size.Width;
                int preColCount = 0;

                bool hasExplicitColCount = ColumnCount != null && ColumnCount != "auto"
                    && int.TryParse(ColumnCount, out preColCount) && preColCount > 1;
                bool hasColWidth = ColumnWidth != null && ColumnWidth != "auto"
                    && !string.IsNullOrEmpty(ColumnWidth);

                bool isMultiColumn = hasExplicitColCount || hasColWidth;

                if (isMultiColumn && !hasExplicitColCount && hasColWidth)
                {
                    // Auto column-count from column-width: compute the
                    // number of columns so we can pre-constrain width.
                    double cwVal = CssLengthParser.ParseLength(ColumnWidth, Size.Width, GetEmHeight());
                    double gap = ResolveColumnGap();
                    double available = Size.Width - ActualPaddingLeft - ActualPaddingRight
                        - ActualBorderLeftWidth - ActualBorderRightWidth;

                    if (cwVal > 0 && available > 0)
                        preColCount = Math.Max(1, (int)Math.Floor((available + gap) / (cwVal + gap)));

                    isMultiColumn = preColCount > 1;
                }

                if (isMultiColumn && preColCount > 1)
                {
                    double columnGap = ResolveColumnGap();
                    double cw = Size.Width - ActualPaddingLeft - ActualPaddingRight
                        - ActualBorderLeftWidth - ActualBorderRightWidth;

                    double colWidth = (cw - (preColCount - 1) * columnGap) / preColCount;

                    if (colWidth > 0)
                        Size = new SizeF((float)colWidth, Size.Height);
                }

                foreach (var childBox in Boxes)
                {
                    childBox.PerformLayout(g);

                    // CSS2.1 §13.3.1: When page-break-inside:avoid is
                    // set, move floated children to the next page if they
                    // would cross a page boundary.
                    if (childBox.Float != CssConstants.None && PageBreakInside == CssConstants.Avoid)
                        childBox.BreakPage();
                }

                // Restore original width after children are laid out.
                if (isMultiColumn)
                    Size = new SizeF(savedWidth, Size.Height);

                ActualRight = CalculateActualRight();
                ActualBottom = MarginBottomCollapse();

                if (Display is "grid" or "inline-grid")
                    ApplyGridLayoutAfterInline();
            }
        }
    }

    /// <summary>
    /// CSS Multi-column §3: post-layout redistribution of in-flow children into
    /// multiplecolumns when column-count > 1 or column-width is specified.
    /// </summary>
    private void ApplyMultiColumnPostLayout()
    {
        // CSS Multi-column Layout §3: When column-count > 1 or column-width
        // is specified, redistribute in-flow children into multiple columns.
        // This is a post-layout transformation that moves children
        // horizontally and vertically to simulate multi-column flow.
        {
            int colCount = 0;

            bool hasExplicitCount = ColumnCount != null && ColumnCount != "auto"
                && int.TryParse(ColumnCount, out colCount) && colCount > 1;

            bool hasColumnWidth = ColumnWidth != null && ColumnWidth != "auto"
                && !string.IsNullOrEmpty(ColumnWidth);

            if (!hasExplicitCount && hasColumnWidth)
            {
                // Auto column-count from column-width: CSS Multi-column §3.4
                double cw = CssLengthParser.ParseLength(ColumnWidth, Size.Width, GetEmHeight());
                double gap = GetEmHeight();
                double available = Size.Width - ActualPaddingLeft - ActualPaddingRight
                    - ActualBorderLeftWidth - ActualBorderRightWidth;

                if (cw > 0 && available > 0)
                    colCount = Math.Max(1, (int)Math.Floor((available + gap) / (cw + gap)));
            }

            if (colCount > 1 && Boxes.Count > 0)
            {
                ApplyMultiColumnLayout(colCount);
            }
        }
    }

    /// <summary>
    /// Resolve the used block size (height): explicit/percentage height, the
    /// abspostop+bottom constraint height, aspect-ratio transfer, and the
    /// quirks-modehtml/body viewport-fill floor.
    /// </summary>
    private void ResolveUsedBlockHeight()
    {
        // CSS content-box model: 'height' specifies the content height only;
        // padding and border are additive (CSS2.1 §10.6.3). An intrinsic-sizing
        // height keyword (min-/max-/fit-content) is not a length — the content
        // height already in ActualBottom is its used value, so leave it be.
        if (Height != CssConstants.Auto && !string.IsNullOrEmpty(Height) && !IsIntrinsicSizingHeightKeyword(Height))
        {
            // CSS2.1 §10.5: If height is a percentage and the containing
            // block's height is not explicitly specified (auto), the
            // percentage resolves to auto and this constraint is skipped.
            if (!HeightPercentageResolvesToAuto())
            {
                // CSS2.1 §10.5: Percentage heights resolve against the
                // containing block's height, not the element's own size.
                // ActualHeight uses Size.Height (the element's own height
                // from child layout), which is wrong for percentage values.
                // Resolve against the containing block's height instead.
                double contentHeight;

                if (Height.Contains('%'))
                {
                    double cbHeight = PercentageHeightContainingBlockHeight();
                    contentHeight = CssLengthParser.ParseLength(Height, cbHeight, GetEmHeight());
                }
                else
                {
                    contentHeight = string.Equals(Height, "inherit", StringComparison.OrdinalIgnoreCase) && GetParent() != null
                        ? GetParent().ActualHeight
                        : ActualHeight;
                }

                double borderBoxHeight = ResolveSpecifiedHeightToBorderBox(contentHeight);

                // CSS2.1 §10.6.3: An explicit height sets the content box
                // height.  Content that exceeds this height overflows
                // (visible by default) but does not affect sibling
                // positioning.  Use direct assignment so that explicit
                // height (e.g. height:0) can override the height computed
                // by CreateLineBoxes (e.g. from line-height).
                ActualBottom = Location.Y + borderBoxHeight;
            }
        }
        else if ((Position == CssConstants.Absolute || Position == CssConstants.Fixed)
            && Top != null && Top != CssConstants.Auto
            && Bottom != null && Bottom != CssConstants.Auto
            && (Height == CssConstants.Auto || string.IsNullOrEmpty(Height)))
        {
            // CSS2.1 §10.6.4: For absolutely positioned, non-replaced
            // elements when height is auto and both top and bottom are
            // specified, compute height from the constraint equation:
            // top + margin-top + height + margin-bottom + bottom = CB height
            double cbHeight;

            if (Position == CssConstants.Fixed && LayoutEnvironment != null)
                cbHeight = LayoutEnvironment.ViewportSize.Height;
            else
            {
                var cb = FindPositionedContainingBlock();
                GetAbsoluteContainingBlockPaddingBox(cb, out _, out _, out _, out cbHeight);
            }

            double cssTop = CssLengthParser.ParseLength(Top, cbHeight, GetEmHeight());
            double cssBottom = CssLengthParser.ParseLength(Bottom, cbHeight, GetEmHeight());
            double resolvedHeight = cbHeight - cssTop - cssBottom - ActualMarginTop - ActualMarginBottom
                - ActualPaddingTop - ActualPaddingBottom - ActualBorderTopWidth - ActualBorderBottomWidth;

            if (resolvedHeight < 0) 
                resolvedHeight = 0;

            double borderBoxH = resolvedHeight + ActualPaddingTop + ActualPaddingBottom + ActualBorderTopWidth + ActualBorderBottomWidth;

            ActualBottom = Location.Y + borderBoxH;
        }

        // CSS Sizing 4 §4: a box with a preferred aspect-ratio and an auto block
        // (height) axis derives its used height from its used inline (width) size.
        // Runs after the explicit-height paths above (so an author height still
        // wins) and before the §10.7 min-/max-height clamp below (so e.g. a
        // min-height floors the transferred square). Scoped to in-flow block-level
        // boxes, whose used width is already resolved and does not itself depend on
        // the aspect ratio; replaced elements keep their intrinsic-ratio sizing.
        if ((Height == CssConstants.Auto || string.IsNullOrEmpty(Height))
            && Display == CssConstants.Block
            && Float == CssConstants.None
            && Position != CssConstants.Absolute && Position != CssConstants.Fixed
            && !IsImage
            && TryResolveAspectRatioBlockHeight(out double aspectRatioBorderBoxHeight))
        {
            ActualBottom = Location.Y + aspectRatioBorderBoxHeight;
        }

        // Quirks-mode "the body element fills the html element" / "the html
        // element fills the viewport" quirks (https://quirks.spec.whatwg.org/):
        // in quirks mode an auto-height root <html> fills the viewport (minus its
        // margins) and an auto-height <body> fills the html element's content box
        // (minus its own margins), instead of shrink-wrapping to content. Acts as
        // a floor (content taller than the fill still overflows/scrolls).
        if ((Height == CssConstants.Auto || string.IsNullOrEmpty(Height))
            && DocumentQuirksMode
            && LayoutEnvironment != null
            && Position is not (CssConstants.Absolute or CssConstants.Fixed)
            && Float == CssConstants.None
            && HtmlTag != null)
        {
            double? fillBorderBoxHeight = null;

            if (HtmlTag.Name.Equals("html", StringComparison.OrdinalIgnoreCase))
            {
                fillBorderBoxHeight = LayoutEnvironment.ViewportSize.Height - ActualMarginTop - ActualMarginBottom;
            }
            else if (HtmlTag.Name.Equals("body", StringComparison.OrdinalIgnoreCase)
                && ParentBox is { HtmlTag: { } parentTag }
                && parentTag.Name.Equals("html", StringComparison.OrdinalIgnoreCase))
            {
                var html = ParentBox;
                double htmlContentHeight = LayoutEnvironment.ViewportSize.Height
                    - html.ActualMarginTop - html.ActualMarginBottom
                    - html.ActualBorderTopWidth - html.ActualBorderBottomWidth
                    - html.ActualPaddingTop - html.ActualPaddingBottom;

                fillBorderBoxHeight = htmlContentHeight - ActualMarginTop - ActualMarginBottom;
            }

            if (fillBorderBoxHeight is { } fillH && fillH > ActualBottom - Location.Y)
                ActualBottom = Location.Y + fillH;
        }
    }

    /// <summary>
    /// CSS2.1 §10.7: clamp the content height to min-height / max-height (min
    /// winswhen min > max).
    /// </summary>
    private void ApplyMinMaxHeightConstraints()
    {
        // CSS2.1 §10.7: Apply min-height / max-height constraints.
        // When min-height > max-height, min-height wins.
        {
            double contentHeight = ActualBottom - Location.Y - ActualPaddingTop - ActualPaddingBottom - ActualBorderTopWidth - ActualBorderBottomWidth;
            bool constrained = false;

            // CSS2.1 §9.6.1: For fixed-position elements, percentage
            // heights resolve against the viewport, not the parent.
            double cbHeight = (Position == CssConstants.Fixed && LayoutEnvironment != null)
                ? LayoutEnvironment.ViewportSize.Height
                : ContainingBlock.Size.Height;

            if (MaxHeight != "none" && !string.IsNullOrEmpty(MaxHeight))
            {
                // CSS2.1 §10.7: If the containing block's height is not
                // specified explicitly and this element is not absolutely
                // positioned, a percentage max-height is treated as 'none'.
                // Exception: the initial containing block always has a
                // definite height (the viewport), per §10.5.
                bool maxIsPercentageAuto = MaxHeight.Contains('%')
                    && Position != CssConstants.Absolute && Position != CssConstants.Fixed
                    && ContainingBlock?.ParentBox != null
                    && (ContainingBlock.Height == CssConstants.Auto || string.IsNullOrEmpty(ContainingBlock.Height));

                if (!maxIsPercentageAuto)
                {
                    double maxH = CssLengthParser.ParseLength(MaxHeight, cbHeight, GetEmHeight());

                    if (contentHeight > maxH)
                    {
                        contentHeight = maxH;
                        constrained = true;
                    }
                }
            }

            if (MinHeight != "0" && !string.IsNullOrEmpty(MinHeight))
            {
                // CSS2.1 §10.7: If the containing block's height is not
                // specified explicitly and this element is not absolutely
                // positioned, a percentage min-height is treated as '0'.
                // Exception: the initial containing block always has a
                // definite height (the viewport), per §10.5.
                bool minIsPercentageAuto = MinHeight.Contains('%')
                    && Position != CssConstants.Absolute && Position != CssConstants.Fixed
                    && ContainingBlock?.ParentBox != null
                    && (ContainingBlock.Height == CssConstants.Auto || string.IsNullOrEmpty(ContainingBlock.Height));

                if (!minIsPercentageAuto)
                {
                    double minH = CssLengthParser.ParseLength(MinHeight, cbHeight, GetEmHeight());
                    if (contentHeight < minH)
                    {
                        contentHeight = minH;
                        constrained = true;
                    }
                }
            }

            if (constrained)
            {
                ActualBottom = Location.Y + ResolveSpecifiedHeightToBorderBox(contentHeight);
            }
        }
    }

    /// <summary>
    /// A float with an explicit (non-auto) height establishes a BFC and takes
    /// itsstated height rather than child-float overflow (CSS2.1 §10.6.1),
    /// re-clampedto min/max-height.
    /// </summary>
    private void ApplyFloatExplicitHeight()
    {
        // Floats with an explicit CSS height establish a new BFC.
        // Their ActualBottom should reflect the stated height, not
        // content overflow from child floats (CSS2.1 §10.6.1).
        // CSS2.1 §10.5: Percentage heights resolve to auto when
        // the containing block's height is not explicitly specified.
        if (Float != CssConstants.None && Height != CssConstants.Auto && !string.IsNullOrEmpty(Height)
            && !IsIntrinsicSizingHeightKeyword(Height))
        {
            if (!HeightPercentageResolvesToAuto())
            {
                // For percentage heights, resolve against the containing
                // block's height directly.  ActualHeight resolves against
                // Size.Height which may have been cached before the
                // percentage height pre-resolution step set the correct
                // Size.Height (CSS2.1 §10.5).
                double contentHeight;

                if (Height.Contains('%'))
                {
                    double cbHeight = PercentageHeightContainingBlockHeight();
                    contentHeight = CssLengthParser.ParseLength(Height, cbHeight, GetEmHeight());
                }
                else
                {
                    contentHeight = ActualHeight;
                }

                // CSS2.1 §10.7: min-height/max-height also constrain a float's
                // explicit height. This override runs after the §10.7 clamp above,
                // so without re-clamping here a float with height:100; min-height:200
                // kept 100 (e.g. a float:left grid whose auto-fill row count already
                // grew to min-height — WPT css-grid grid-auto-repeat-min-size-001).
                // height and min/max-height share the box-sizing frame, so clamp the
                // specified value; ResolveSpecifiedHeightToBorderBox normalizes it.
                contentHeight = ClampSpecifiedHeightToMinMax(contentHeight);

                double borderBoxHeight = ResolveSpecifiedHeightToBorderBox(contentHeight);

                ActualBottom = Location.Y + borderBoxHeight;
            }
        }
    }

    /// <summary>
    /// Absolute-position completion: solve the remaining inset (right/bottom
    /// anchored)offsets, then apply CSS Box Alignment §6.1 justify-self /
    /// align-selfself-alignment within the inset-modified containing block.
    /// </summary>
    private void PositionAbsoluteBox()
    {
        if (Position == CssConstants.Absolute)
        {
            bool hasLeft = Left != null && Left != CssConstants.Auto;
            bool hasRight = Right != null && Right != CssConstants.Auto;
            bool hasTop = Top != null && Top != CssConstants.Auto;
            bool hasBottom = Bottom != null && Bottom != CssConstants.Auto;

            if ((!hasLeft && hasRight) || (!hasTop && hasBottom))
            {
                var cb = FindPositionedContainingBlock();
                GetAbsoluteContainingBlockPaddingBox(cb, out double cbPadLeft, out double cbPadTop, out double cbPadWidth, out double cbPadHeight);

                float newX = Location.X;
                float newY = Location.Y;

                if (!hasLeft && hasRight)
                {
                    double boxWidth = ActualRight - Location.X;
                    if (boxWidth <= 0)
                        boxWidth = Size.Width;

                    double cssRight = CssLengthParser.ParseLength(Right, cbPadWidth, GetEmHeight());
                    newX = (float)(cbPadLeft + cbPadWidth - cssRight - ActualMarginRight - boxWidth);
                }

                if (!hasTop && hasBottom)
                {
                    double boxHeight = ActualBottom - Location.Y;

                    if (boxHeight <= 0)
                        boxHeight = Size.Height;

                    double cssBottom = CssLengthParser.ParseLength(Bottom, cbPadHeight, GetEmHeight());
                    newY = (float)(cbPadTop + cbPadHeight - cssBottom - ActualMarginBottom - boxHeight);
                }

                float deltaX = newX - Location.X;
                float deltaY = newY - Location.Y;

                if (deltaX != 0)
                    OffsetLeft(deltaX);

                if (deltaY != 0)
                {
                    // OffsetTop already shifts Location.Y, and ActualBottom is a
                    // derived value (ActualBottom => Location.Y + Size.Height), so the
                    // box's bottom edge follows the move automatically.  A further
                    // "ActualBottom += deltaY" would double-apply the shift — its
                    // setter writes Size.Height = ActualBottom - Location.Y, growing
                    // (or, as here for a bottom-anchored full-height abspos box,
                    // collapsing) the height by deltaY.  Mirror the horizontal branch
                    // above, which offsets without touching ActualRight.
                    OffsetTop(deltaY);
                }
            }

            // CSS Box Alignment Level 3 §6.1: Post-layout self-alignment for
            // absolutely positioned elements.  After children are laid out,
            // shrink the box to fit-content size and align within the IMCB.
            // This must run after child layout so content dimensions are known.
            string jsPost = JustifySelf?.Trim().ToLowerInvariant() ?? "auto";
            bool jsPostNonDefault = jsPost != "auto" && jsPost != "normal" && jsPost != "stretch";
            string asPost = AlignSelf?.Trim().ToLowerInvariant() ?? "auto";
            bool asPostNonDefault = asPost != "auto" && asPost != "normal" && asPost != "stretch";

            if (jsPostNonDefault || asPostNonDefault)
            {
                var cb = FindPositionedContainingBlock();
                GetAbsoluteContainingBlockPaddingBox(cb, out double cbPadLeft, out double cbPadTop, out double cbPadWidth, out double cbPadHeight);

                bool hasL = Left != null && Left != CssConstants.Auto;
                bool hasR = Right != null && Right != CssConstants.Auto;
                bool hasT = Top != null && Top != CssConstants.Auto;
                bool hasB = Bottom != null && Bottom != CssConstants.Auto;

                // CSS Writing Modes Level 4: the containing block's writing mode
                // determines which physical axis corresponds to justify-self (inline)
                // and align-self (block).
                bool cbVertical = cb.WritingMode == "vertical-rl" || cb.WritingMode == "vertical-lr";

                float newX = Location.X, newY = Location.Y;

                // When align-self resolves the block axis to a non-stretch value,
                // the box uses its content (shrink-to-fit) block size rather than
                // the stretched inset size; record the resolved border-box height
                // so the apply step can shrink it (mirrors how the inline branch
                // sets Size.Width).  Null = leave the block size untouched.
                double? alignBlockBorderBoxHeight = null;

                // justify-self controls the inline axis:
                //   horizontal-tb → horizontal (L/R insets)
                //   vertical-rl/lr → vertical (T/B insets)
                if (jsPostNonDefault)
                {
                    if (!cbVertical && hasL && hasR)
                    {
                        double cssLeft = CssLengthParser.ParseLength(Left, cbPadWidth, GetEmHeight());
                        double cssRight = CssLengthParser.ParseLength(Right, cbPadWidth, GetEmHeight());
                        double imcbLeft = cbPadLeft + cssLeft;
                        double imcbWidth = cbPadWidth - cssLeft - cssRight;

                        double boxWidth = GetShrinkToFitWidth();
                        Size = new SizeF((float)boxWidth, Size.Height);

                        // For a box the vertical-flow rotation will transpose, the
                        // alignment runs on the CB's inline (horizontal) axis but
                        // the item's PHYSICAL width is its logical HEIGHT (the
                        // rotation swaps them). Align with the physical extent so
                        // an overflowing vrl item (laid out with a small logical
                        // width) is centered/clamped by its true width.
                        double alignWidth = WillBeVerticalTransposed()
                            ? GetShrinkToFitHeight() : boxWidth;

                        // Inline-axis start edge follows the CB's direction (start/end);
                        // self-start/self-end follow the ITEM's start in this horizontal
                        // axis — its inline axis when horizontal-tb (right under rtl), or
                        // its block axis when vertical (vertical-rl starts on the right).
                        bool startIsLow = cb.Direction != "rtl";
                        bool itemStartIsHigh = WritingMode switch
                        {
                            "vertical-rl" => true,
                            "vertical-lr" => false,
                            _ => Direction == "rtl",
                        };

                        double dx = ResolveAbsposSelfAlignment(
                            jsPost, imcbLeft, imcbWidth, cbPadLeft, cbPadWidth,
                            alignWidth, isRtl: !startIsLow, startIsLow,
                            selfStartIsHigh: itemStartIsHigh);

                        newX = (float)(imcbLeft + dx + ActualMarginLeft);
                    }
                    else if (cbVertical && hasT && hasB)
                    {
                        double cssTop = CssLengthParser.ParseLength(Top, cbPadHeight, GetEmHeight());
                        double cssBottom = CssLengthParser.ParseLength(Bottom, cbPadHeight, GetEmHeight());
                        double imcbTop = cbPadTop + cssTop;
                        double imcbHeight = cbPadHeight - cssTop - cssBottom;

                        double boxHeight = GetShrinkToFitHeight();

                        // Non-stretch justify-self on the vertical inline axis →
                        // the box uses its content (shrink-to-fit) height, not the
                        // top-to-bottom inset-stretched height. Record it so the
                        // shared apply step restores the height after the offset
                        // (mirrors the width un-stretch in the !cbVertical inline
                        // branch and the height un-stretch in the align-self
                        // block-axis branch). Without this the box stays stretched
                        // to the IMCB height and renders as a tall bar.
                        alignBlockBorderBoxHeight = boxHeight;

                        // Inline axis is vertical here; its start runs top→bottom
                        // unless the CB's direction is rtl. start/end follow the CB's
                        // inline direction (so the flip is !startIsLow, mirroring the
                        // align-self block-axis branch); self-start/self-end follow the
                        // ITEM's inline direction — for a vertical-wm item the vertical
                        // axis is its inline axis (start at the bottom under rtl), while
                        // for a horizontal-tb item it is the block axis (start at top).
                        bool startIsLow = cb.Direction != "rtl";
                        bool itemStartIsHigh =
                            (WritingMode == "vertical-lr" || WritingMode == "vertical-rl")
                            && Direction == "rtl";

                        double dy = ResolveAbsposSelfAlignment(
                            jsPost, imcbTop, imcbHeight, cbPadTop, cbPadHeight,
                            boxHeight, isRtl: !startIsLow, startIsLow,
                            selfStartIsHigh: itemStartIsHigh);

                        newY = (float)(imcbTop + dy + ActualMarginTop);
                    }
                    else if (!cbVertical && !hasL && !hasR && ParentBox != null)
                    {
                        // Inline insets are auto → the box is at its static
                        // position; justify-self aligns it within the
                        // static-position rectangle, whose inline extent is the
                        // in-flow parent's content box (CSS Position 3
                        // §abspos-alignment). The box keeps its own inline size.
                        double rectStart = ParentBox.ClientLeft;
                        double rectWidth = ParentBox.ClientRight - ParentBox.ClientLeft;
                        double marginBoxWidth = Size.Width + ActualMarginLeft + ActualMarginRight;
                        bool isRtl = Direction == "rtl";
                        bool startIsLow = cb.Direction != "rtl";

                        double dx = ResolveAbsposSelfAlignment("unsafe " + StripSafeUnsafe(jsPost),
                            rectStart, rectWidth, rectStart, rectWidth,
                            marginBoxWidth, isRtl, startIsLow);

                        newX = (float)(rectStart + dx + ActualMarginLeft);
                    }
                }
                else if (!cbVertical && !hasL && !hasR && ParentBox != null
                         && cb.Direction == "rtl")
                {
                    // justify-self:auto (default) + auto inline insets → the box
                    // rests at its static position: the inline-START edge of the
                    // static-position rectangle (the in-flow parent's content
                    // box). That start edge follows the containing block's
                    // direction — for ltr it is the left edge (already set by
                    // base layout), for rtl it is the RIGHT edge. Without this,
                    // abspos items in rtl containers render flush-left, shifted
                    // left by the free inline width
                    // (WPT css-align/abspos/*-rtl-*, issue #1131).
                    double rectStart = ParentBox.ClientLeft;
                    double rectWidth = ParentBox.ClientRight - ParentBox.ClientLeft;

                    // Use the physical width for a box the rotation will transpose
                    // (its physical width is the logical height).
                    double boxW = WillBeVerticalTransposed() ? GetShrinkToFitHeight() : Size.Width;
                    double marginBoxWidth = boxW + ActualMarginLeft + ActualMarginRight;
                    double dx = ResolveAbsposSelfAlignment(
                        "unsafe start", rectStart, rectWidth, rectStart, rectWidth,
                        marginBoxWidth, isRtl: true, startIsLow: false);

                    newX = (float)(rectStart + dx + ActualMarginLeft);
                }
                else if (cbVertical && !hasT && !hasB && cb.Direction == "rtl")
                {
                    // vertical-rl/lr container: the inline axis is VERTICAL.
                    // justify-self:auto + auto block insets (top/bottom) → the box
                    // rests at its static position: the inline-START edge of the
                    // static-position rectangle. That start follows the inline
                    // direction — for ltr the top (Broiler's default), for rtl the
                    // inline axis is reversed so the start is the BOTTOM. Use the
                    // CB padding box (cbPadTop/cbPadHeight) for the vertical
                    // extent: block-axis sizes resolve bottom-up, so ParentBox's
                    // ActualBottom is not final here, whereas cbPad* carries the
                    // definite-height patch. Without this, abspos items in
                    // vertical-rl+rtl containers render flush-top
                    // (WPT css-align/abspos/*-vrl-rtl-*, issue #1131).
                    double marginBoxHeight = Size.Height + ActualMarginTop + ActualMarginBottom;
                    double dy = ResolveAbsposSelfAlignment(
                        "unsafe start", cbPadTop, cbPadHeight, cbPadTop, cbPadHeight,
                        marginBoxHeight, isRtl: true, startIsLow: false);

                    newY = (float)(cbPadTop + dy + ActualMarginTop);

                    // Preserve the box's own (shrink-to-fit) block size: the apply
                    // step shifts ActualBottom by the same delta as Location, so
                    // record the height to restore it (mirrors the align-self
                    // block-axis un-stretch).
                    alignBlockBorderBoxHeight = Size.Height;
                }

                // align-self controls the block axis:
                //   horizontal-tb → vertical (T/B insets)
                //   vertical-rl/lr → horizontal (L/R insets)
                if (asPostNonDefault)
                {
                    if (!cbVertical && hasT && hasB)
                    {
                        double cssTop = CssLengthParser.ParseLength(Top, cbPadHeight, GetEmHeight());
                        double cssBottom = CssLengthParser.ParseLength(Bottom, cbPadHeight, GetEmHeight());
                        double imcbTop = cbPadTop + cssTop;
                        double imcbHeight = cbPadHeight - cssTop - cssBottom;

                        double boxHeight = GetShrinkToFitHeight();

                        // Non-stretch align-self → the box is its content height,
                        // not the stretched top-to-bottom inset height.
                        alignBlockBorderBoxHeight = boxHeight;

                        // For a box the vertical-flow rotation will transpose, the
                        // alignment runs on the CB's block (vertical) axis but the
                        // item's PHYSICAL height is its logical WIDTH (the rotation
                        // swaps them); align with the physical extent.
                        double alignHeight = WillBeVerticalTransposed()
                            ? GetShrinkToFitWidth() : boxHeight;

                        // Block-axis start is the top edge for horizontal-tb. self-start/
                        // self-end use the ITEM's start in this vertical axis: its block
                        // axis when horizontal-tb (top), or its inline axis when vertical
                        // (bottom under direction:rtl).
                        bool itemStartIsHigh =
                            (WritingMode == "vertical-lr" || WritingMode == "vertical-rl")
                            && Direction == "rtl";

                        double dy = ResolveAbsposSelfAlignment(
                            asPost, imcbTop, imcbHeight, cbPadTop, cbPadHeight,
                            alignHeight, isRtl: false, startIsLow: true,
                            selfStartIsHigh: itemStartIsHigh);

                        newY = (float)(imcbTop + dy + ActualMarginTop);
                    }
                    else if (cbVertical && hasL && hasR)
                    {
                        double cssLeft = CssLengthParser.ParseLength(Left, cbPadWidth, GetEmHeight());
                        double cssRight = CssLengthParser.ParseLength(Right, cbPadWidth, GetEmHeight());
                        double imcbLeft = cbPadLeft + cssLeft;
                        double imcbWidth = cbPadWidth - cssLeft - cssRight;

                        double boxWidth = GetShrinkToFitWidth();

                        Size = new SizeF((float)boxWidth, Size.Height);

                        // Block-axis start runs L→R for vertical-lr, R→L for
                        // vertical-rl (so the low/left edge is the start only for lr).
                        // align-self acts on the containing block's BLOCK axis, whose
                        // flow is fixed by writing-mode; `direction` (rtl/ltr) is an
                        // inline-axis property and must NOT flip block-axis start/end
                        // (WPT css-align/abspos/align-self-{vlr,vrl}-*). So the start↔end
                        // flip is driven purely by the writing mode: start sits on the
                        // high edge exactly when the block start is not the low edge.
                        bool startIsLow = cb.WritingMode == "vertical-lr";

                        // self-start/self-end use the ITEM's start edge in this
                        // (horizontal) alignment axis: for a vertical-wm item the
                        // horizontal axis is its block axis (vertical-rl starts on the
                        // right/high edge, vertical-lr on the left/low edge); for a
                        // horizontal-tb item it is the inline axis, whose start is the
                        // right/high edge under direction:rtl.
                        bool itemStartIsHigh = WritingMode switch
                        {
                            "vertical-rl" => true,
                            "vertical-lr" => false,
                            _ => Direction == "rtl",
                        };
                        double dx = ResolveAbsposSelfAlignment(
                            asPost, imcbLeft, imcbWidth, cbPadLeft, cbPadWidth,
                            boxWidth, isRtl: !startIsLow, startIsLow,
                            selfStartIsHigh: itemStartIsHigh);

                        newX = (float)(imcbLeft + dx + ActualMarginLeft);
                    }
                    else if (!cbVertical && !hasT && !hasB)
                    {
                        // Block insets are auto → the box is at its static
                        // position; align-self aligns it within the
                        // static-position rectangle, which has ZERO block size
                        // at the static position (free space = −margin-box
                        // height), so start keeps the box put while center/end
                        // pull it up by half / all of its height (CSS Position 3
                        // §abspos-alignment). The box keeps its own block size:
                        // record it so the shared apply step's ActualBottom
                        // bookkeeping restores the height after the offset
                        // (otherwise moving up shrinks the box by the delta).
                        alignBlockBorderBoxHeight = Size.Height;

                        double marginBoxStart = Location.Y - ActualMarginTop;
                        double marginBoxHeight = Size.Height + ActualMarginTop + ActualMarginBottom;
                        double dy = ResolveAbsposSelfAlignment(
                            "unsafe " + StripSafeUnsafe(asPost),
                            marginBoxStart, 0, marginBoxStart, 0,
                            marginBoxHeight, false, startIsLow: true);

                        newY = (float)(marginBoxStart + dy + ActualMarginTop);
                    }
                }
                else if (cbVertical && !hasL && !hasR && ParentBox != null
                         && cb.WritingMode == "vertical-rl")
                {
                    // align-self:auto (default) + auto block insets (left/right):
                    // for a vertical-rl container the BLOCK axis is horizontal and
                    // flows right-to-left, so the block-START edge is the RIGHT.
                    // The box rests at that block static position, but Broiler's
                    // base layout placed it flush-left, so flush it right within
                    // the parent content box. (vertical-lr keeps the left edge —
                    // its block start — which is Broiler's default, so no branch
                    // is needed there.) The inline (vertical) axis is handled by
                    // the justify-self branch above. Mirrors the rtl inline-axis
                    // static-position fix (WPT css-align/abspos/justify-self-*-vrl-*,
                    // issue #1131). Widths resolve top-down, so ParentBox's
                    // horizontal extent is reliable here (unlike its vertical one).
                    double rectStart = ParentBox.ClientLeft;
                    double rectWidth = ParentBox.ClientRight - ParentBox.ClientLeft;
                    double marginBoxWidth = Size.Width + ActualMarginLeft + ActualMarginRight;
                    double dx = ResolveAbsposSelfAlignment(
                        "unsafe start", rectStart, rectWidth, rectStart, rectWidth,
                        marginBoxWidth, isRtl: true, startIsLow: false);
                    newX = (float)(rectStart + dx + ActualMarginLeft);
                }

                if (newX != Location.X || newY != Location.Y)
                {
                    float deltaX = newX - Location.X;
                    float deltaY = newY - Location.Y;

                    if (deltaX != 0)
                        OffsetLeft(deltaX);

                    if (deltaY != 0)
                    {
                        OffsetTop(deltaY);
                        ActualBottom += deltaY;
                    }
                }

                // Un-stretch the block axis to the content height for non-stretch
                // align-self.  Runs even when the offset was zero (align-self:start
                // keeps the box at the start edge but still shrinks it), so it is
                // outside the offset guard above.
                if (alignBlockBorderBoxHeight is double abh)
                    ActualBottom = Location.Y + abh;
            }
        }
    }

    /// <summary>
    /// CSS Box Alignment §5.4: shift in-flow content vertically for
    /// align-contenton a definite-height block container.
    /// </summary>
    private void ApplyBlockAlignContent()
    {
        // CSS Box Alignment Level 3 §5.4: align-content on block containers
        // shifts the in-flow content vertically when the container has a
        // definite height larger than the content.  Values:
        //   normal/start/baseline/flex-start → no shift (top-aligned)
        //   center                           → center vertically
        //   end/flex-end/last baseline       → bottom-aligned
        //   space-between/space-around/space-evenly → distribute space
        // The "unsafe" and "safe" prefixes are stripped; safe alignment
        // falls back to start when content overflows, but for blocks this
        // is handled implicitly (shift is clamped to ≥ 0).
        if (AlignContent != null && AlignContent != "normal"
            // The definite-track grid pass distributes align-content across its
            // row tracks itself; this block-level shift would double it.
            && !_gridTrackLayoutApplied
            && (IsBlock || Display == CssConstants.ListItem || Display == CssConstants.InlineBlock
                || Display == CssConstants.TableCell)
            && Boxes.Count > 0
            && (Height != CssConstants.Auto && !string.IsNullOrEmpty(Height)
                || Display == CssConstants.TableCell))
        {
            double borderBoxHeight = ActualBottom - Location.Y;
            double containerContentHeight = borderBoxHeight
                - ActualPaddingTop - ActualPaddingBottom
                - ActualBorderTopWidth - ActualBorderBottomWidth;

            // Compute the extent of the in-flow content (excluding absolutely
            // positioned and fixed elements).  Per CSS Box Alignment §5.4 the
            // alignment subject is the content's *margin* box, so the leading
            // child's top margin and the trailing child's bottom margin count
            // toward the consumed space — measuring only border boxes would
            // overstate the free space and shift the content too far.
            double contentTop = double.MaxValue;
            double contentBottom = double.MinValue;
            foreach (var child in Boxes)
            {
                if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                    continue;

                if (child.Display == CssConstants.None)
                    continue;

                double childTop = child.Location.Y - child.ActualMarginTop;
                double childBottom = child.ActualBottom + child.ActualMarginBottom;

                if (childTop < contentTop)
                    contentTop = childTop;

                if (childBottom > contentBottom)
                    contentBottom = childBottom;
            }

            if (contentTop < double.MaxValue && contentBottom > double.MinValue)
            {
                double usedContentHeight = contentBottom - contentTop;
                double freeSpace = containerContentHeight - usedContentHeight;

                // Normalise the align-content value: strip safe/unsafe prefix.
                string ac = AlignContent.Trim();
                bool explicitUnsafe = ac.StartsWith("unsafe ", StringComparison.OrdinalIgnoreCase);
                bool explicitSafe = ac.StartsWith("safe ", StringComparison.OrdinalIgnoreCase);

                if (explicitSafe)
                    ac = ac[5..].Trim();

                else if (explicitUnsafe)
                    ac = ac[7..].Trim();

                // CSS Box Alignment §5.3: when no explicit safe/unsafe keyword
                // is present, the default overflow alignment is "safe".
                bool isSafe = !explicitUnsafe;

                // Only compute shift when there's free space, or when unsafe
                // mode allows shifting even into overflow.
                if (freeSpace > 0.5 || (!isSafe && freeSpace < -0.5))
                {
                    double shift = 0;

                    switch (ac.ToLowerInvariant())
                    {
                        case "center":
                            shift = freeSpace / 2;
                            break;

                        case "end":
                        case "flex-end":
                            shift = freeSpace;
                            break;

                        // baseline / last baseline: with no baseline-sharing group
                        // (each container is independent), both fall back to the
                        // start edge — matching the reference rendering.
                        case "space-between":
                            // Single content group → same as start (no shift).
                            break;

                        case "space-around":
                            shift = freeSpace / 2;
                            break;

                        case "space-evenly":
                            shift = freeSpace / 2;
                            break;

                            // start, flex-start, baseline, normal → no shift.
                    }

                    // Safe alignment: clamp shift to 0 to prevent overflow.
                    if (isSafe && shift < 0)
                        shift = 0;

                    if (Math.Abs(shift) > 0.5)
                    {
                        foreach (var child in Boxes)
                        {
                            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                                continue;

                            if (child.Display == CssConstants.None)
                                continue;

                            child.OffsetTop(shift);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// CSS Box Alignment §6.1: justify-self on a block-level box — the explicit
    /// safe/unsafepath (incl. vertical containers) and the legacy justify-items
    /// /text-align resolution.
    /// </summary>
    private void ApplyBlockJustifySelf()
    {
        // CSS Box Alignment Level 3 §6.1: justify-self on block-level boxes.
        // When a non-replaced block has an explicit width narrower than its
        // containing block, 'justify-self' shifts the box horizontally within
        // the containing block's content area.  Values:
        //   auto/normal/stretch → default behaviour (no shift)
        //   start/flex-start/self-start/left → left-aligned (no shift in LTR)
        //   end/flex-end/self-end/right → right-aligned
        //   center → centered
        // Floated and absolutely/fixed positioned boxes are unaffected.
        //
        // 'auto' and 'normal' are not literally no-ops — they resolve:
        //   • justify-self:auto → the containing block's 'justify-items'
        //     (CSS Box Alignment §justify-self).
        //   • a still-unresolved 'normal'/'stretch' on a definite-width block →
        //     the parent's legacy 'text-align:-webkit-{left,right,center}' block
        //     alignment, if any (non-standard but widely supported; WPT
        //     css-align/blocks/justify-self-text-align exercises it).
        if (Float == CssConstants.None
            && Position != CssConstants.Absolute && Position != CssConstants.Fixed
            && (IsBlock || Display == CssConstants.ListItem)
            && ParentBox != null)
        {
            // CSS Box Alignment §5.3 + §6.1: an explicit overflow-alignment
            // keyword (safe/unsafe) on a block-level box. Unlike the legacy
            // path below, this handles the containing block's inline axis when
            // it is VERTICAL (writing-mode: vertical-*) — where justify-self
            // shifts the box along Y — and it honours overflow: `safe` clamps
            // to start when the box is larger than the alignment container,
            // while `unsafe` keeps the requested edge (allowing a negative
            // shift past the start edge). The keyword-less path is left
            // untouched below to avoid perturbing existing block layout.
            string rawJs = JustifySelf?.Trim().ToLowerInvariant() ?? "auto";

            if (rawJs.StartsWith("safe ", StringComparison.Ordinal)
                || rawJs.StartsWith("unsafe ", StringComparison.Ordinal))
            {
                bool explicitSafe = rawJs.StartsWith("safe ", StringComparison.Ordinal);
                string alignKw = StripSafeUnsafe(rawJs);

                if (alignKw is "center" or "end" or "flex-end" or "self-end" or "right"
                    or "start" or "flex-start" or "self-start" or "left")
                {
                    // When the box will be rotated by the vertical-flow transform
                    // (WillBeVerticalTransposed), layout is happening in the logical
                    // (horizontal) frame, so justify-self is applied along the
                    // logical inline axis (X) here and the transform rotates it onto
                    // the physical vertical axis. Only when the transform is NOT in
                    // play (prototype disabled) does a vertical container require a
                    // direct physical-Y shift.
                    bool containerVertical = IsVerticalWritingMode(ParentBox.WritingMode)
                        && !WillBeVerticalTransposed();

                    double boxSize = containerVertical
                        ? ActualBottom - Location.Y
                        : ActualRight - Location.X;

                    double marginStart = containerVertical ? ActualMarginTop : ActualMarginLeft;
                    double marginEnd = containerVertical ? ActualMarginBottom : ActualMarginRight;

                    // The vertical inline-axis extent must come from ActualHeight
                    // (the resolved content height), not ClientRectangle.Height:
                    // block-axis geometry resolves bottom-up, so the container's
                    // ActualBottom — and thus ClientRectangle.Height — is still 0
                    // when its in-flow child is being aligned.

                    double containerSize = containerVertical
                        ? ParentBox.ActualHeight
                        : ParentBox.ClientRectangle.Width;

                    double axisFree = containerSize - boxSize - marginStart - marginEnd;

                    // 'safe' falls back to 'start' when the box overflows.
                    if (explicitSafe && axisFree < 0)
                        alignKw = "start";

                    bool selfRtl = Direction == "rtl";
                    bool cbRtl = ParentBox?.Direction == "rtl";
                    double d = alignKw switch
                    {
                        "center" => axisFree / 2,
                        "end" or "flex-end" => cbRtl ? 0 : axisFree,
                        "self-end" => selfRtl ? 0 : axisFree,
                        "right" => axisFree,
                        "start" or "flex-start" => cbRtl ? axisFree : 0,
                        "self-start" => selfRtl ? axisFree : 0,
                        _ => 0, // left
                    };

                    if (Math.Abs(d) > 0.5)
                    {
                        if (containerVertical)
                            OffsetTop(d);
                        else
                            OffsetLeft(d);
                    }
                }

                // The keyword-less legacy path below is a no-op for an explicit
                // safe/unsafe value ("safe end" etc. is not a concrete keyword,
                // so it resolves to null there); fall through so any
                // position:relative offset later in this method still applies.
            }

            string js = JustifySelf?.Trim().ToLowerInvariant() ?? "auto";

            if (js == "auto")
                js = ParentBox.JustifyItems?.Trim().ToLowerInvariant() ?? "normal";

            if (js is "normal" or "stretch" or "auto" or "legacy")
            {
                js = (ParentBox.TextAlign?.Trim().ToLowerInvariant()) switch
                {
                    "-webkit-right" => "right",
                    "-webkit-center" => "center",
                    "-webkit-left" => "left",
                    _ => js,
                };
            }

            // Only a concrete edge/center alignment actually moves the box;
            // normal/stretch/baseline leave it at its in-flow position.
            if (js is not ("center" or "end" or "flex-end" or "self-end" or "right"
                or "start" or "flex-start" or "self-start" or "left"))
                js = null!;

            double boxWidth = ActualRight - Location.X;
            double containerWidth = ParentBox.ClientRectangle.Width;

            // Free space is what remains AFTER the box's own margins. Auto margins
            // are resolved during block layout (e.g. margin:auto centres the box by
            // splitting the free space), so they leave nothing here — which makes
            // 'justify-self' a no-op, per CSS Box Alignment §justify-abspos ("auto
            // margins make justify-self have no effect"). Accounting for margins
            // also keeps explicit-margin boxes aligned to the correct edge.
            double freeSpace = containerWidth - boxWidth
                - ActualMarginLeft - ActualMarginRight;

            if (js != null && freeSpace > 0.5)
            {
                // CSS Box Alignment §6.1: 'start'/'end' use the containing
                // block's writing direction; 'self-start'/'self-end' use the
                // element's own writing direction.
                bool isElementRtl = Direction == "rtl";
                bool isContainerRtl = ParentBox?.Direction == "rtl";

                double dx = 0;

                switch (js)
                {
                    case "center":
                        dx = freeSpace / 2;
                        break;

                    case "end":
                    case "flex-end":
                        dx = isContainerRtl ? 0 : freeSpace;
                        break;

                    case "self-end":
                        dx = isElementRtl ? 0 : freeSpace;
                        break;

                    case "right":
                        dx = freeSpace;
                        break;

                    case "start":
                    case "flex-start":
                        dx = isContainerRtl ? freeSpace : 0;
                        break;

                    case "self-start":
                        dx = isElementRtl ? freeSpace : 0;
                        break;

                    case "left":
                        dx = 0;
                        break;
                }

                if (dx > 0.5)
                    OffsetLeft(dx);
            }
        }
    }

    /// <summary>
    /// CSS2.1 §9.4.3: apply the visual position:relative offset after layout
    /// (doesnot affect flow).
    /// </summary>
    private void ApplyRelativePositionOffset()
    {
        // Apply position:relative offset after layout (visual only, does not affect flow)
        // CSS2.1 §9.4.3: For relative positioning, 'left'/'right' and
        // 'top'/'bottom' form constraint pairs.  When 'top' is auto and
        // 'bottom' is not, dy = -bottom.  When both are non-auto, 'bottom'
        // is ignored (in LTR).  Same logic applies to left/right.
        if (Position == CssConstants.Relative)
        {
            double dx = 0, dy = 0;

            bool hasLeft = Left != null && Left != CssConstants.Auto;
            bool hasRight = Right != null && Right != CssConstants.Auto;
            bool hasTop = Top != null && Top != CssConstants.Auto;
            bool hasBottom = Bottom != null && Bottom != CssConstants.Auto;

            if (hasLeft)
                dx = CssLengthParser.ParseLength(Left, Size.Width, GetEmHeight());
            else if (hasRight)
                dx = -CssLengthParser.ParseLength(Right, Size.Width, GetEmHeight());

            if (hasTop)
                dy = CssLengthParser.ParseLength(Top, Size.Height, GetEmHeight());
            else if (hasBottom)
                dy = -CssLengthParser.ParseLength(Bottom, Size.Height, GetEmHeight());

            if (dx != 0)
                OffsetLeft(dx);

            if (dy != 0)
                OffsetTop(dy);
        }
    }
}
