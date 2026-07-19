using Broiler.CSS;
using System.Drawing;
using System.Globalization;


namespace Broiler.Layout.Engine;

internal partial class CssBox : CssBoxProperties, IDisposable
{
    /// <summary>
    /// CSS Multi-column §6.2: resolves <c>column-gap</c>.  The initial value
    /// 'normal' is ≈ 1em; an explicit length (including 0) overrides it.
    /// </summary>
    internal double ResolveColumnGap()
    {
        if (!string.IsNullOrEmpty(ColumnGap) && ColumnGap != "normal")
            // column-gap resolves against the multicol container's own (already zoom-scaled) content
            // width, so a percentage carries the full effective factor; an absolute gap scales by zoom.
            return ParseUsedLength(ColumnGap, Size.Width, percentAgainstContainingBlock: false);

        return GetEmHeight(); // 'normal' ≈ 1em, already zoomed via GetEmHeight (increment 2)
    }

    private sealed class FlexItemLayout
    {
        public CssBox Box { get; init; }
        public double Grow { get; init; }
        public double Shrink { get; init; }
        public double BaseOuterWidth { get; init; }
        public double TargetOuterWidth { get; set; }
    }

    private sealed class FlexLineLayout
    {
        public List<FlexItemLayout> Items { get; } = [];
        public double BaseOuterWidth { get; set; }
        public double CrossSize { get; set; }
    }

    private bool IsFlexContainer() => Display is "flex" or "inline-flex";

    internal bool IsRowFlexContainer()
    {
        if (!IsFlexContainer())
            return false;

        string direction = FlexDirection?.Trim().ToLowerInvariant() ?? "row";
        return direction is not ("column" or "column-reverse");
    }

    internal void PerformFlexRowLayout(ILayoutEnvironment g)
    {
        EnsureDescendantWordsMeasured(g);

        double contentLeft = ClientLeft;
        double contentTop = ClientTop;
        double contentWidth = Math.Max(0, Size.Width
            - ActualBorderLeftWidth - ActualBorderRightWidth
            - ActualPaddingLeft - ActualPaddingRight);

        double columnGap = ResolveFlexGap(ColumnGap, contentWidth);
        double rowGap = ResolveFlexGap(RowGap, Size.Height);
        bool wrap = FlexWrap is "wrap" or "wrap-reverse";

        var lines = new List<FlexLineLayout>();
        var currentLine = new FlexLineLayout();

        foreach (var child in Boxes)
        {
            if (!IsInFlowFlexItem(child))
                continue;

            var item = new FlexItemLayout
            {
                Box = child,
                Grow = ParseFlexFactor(child.FlexGrow, 0),
                Shrink = ParseFlexFactor(child.FlexShrink, 1),
                BaseOuterWidth = ResolveFlexItemBaseOuterWidth(child, contentWidth)
            };

            item.TargetOuterWidth = item.BaseOuterWidth;

            double candidateWidth = currentLine.BaseOuterWidth + item.BaseOuterWidth
                + (currentLine.Items.Count > 0 ? columnGap : 0);

            if (wrap && currentLine.Items.Count > 0 && candidateWidth > contentWidth + 0.5)
            {
                lines.Add(currentLine);
                currentLine = new FlexLineLayout();
            }

            currentLine.Items.Add(item);
            currentLine.BaseOuterWidth += item.BaseOuterWidth;
        }

        if (currentLine.Items.Count > 0 || lines.Count == 0)
            lines.Add(currentLine);

        double cursorY = contentTop;
        bool reverse = FlexDirection?.Trim().Equals("row-reverse", StringComparison.OrdinalIgnoreCase) == true;

        foreach (var line in lines)
        {
            ResolveFlexLineWidths(line, contentWidth, columnGap);

            foreach (var item in line.Items)
            {
                LayoutFlexItemAtTargetWidth(g, item.Box, item.TargetOuterWidth);

                double itemHeight = GetFlexItemOuterHeight(item.Box);

                if (itemHeight > line.CrossSize)
                    line.CrossSize = itemHeight;
            }

            double usedWidth = 0;

            foreach (var item in line.Items)
                usedWidth += item.TargetOuterWidth;

            int itemCount = line.Items.Count;
            double freeSpace = contentWidth - usedWidth - Math.Max(0, itemCount - 1) * columnGap;

            if (freeSpace < 0)
                freeSpace = 0;

            ResolveJustifyContent(itemCount, freeSpace, columnGap, out double lineOffset, out double itemGap);

            double cursorX = contentLeft + lineOffset;

            if (reverse)
                cursorX = contentLeft + contentWidth - lineOffset;

            for (int i = 0; i < itemCount; i++)
            {
                int itemIndex = reverse ? itemCount - i - 1 : i;
                var item = line.Items[itemIndex];
                var child = item.Box;

                double marginBoxWidth = item.TargetOuterWidth;
                double borderBoxWidth = Math.Max(0, marginBoxWidth
                    - child.ActualMarginLeft - child.ActualMarginRight);
                double itemLeft = reverse
                    ? cursorX - marginBoxWidth + child.ActualMarginLeft
                    : cursorX + child.ActualMarginLeft;

                double crossOffset = ResolveFlexCrossOffset(child, line.CrossSize);
                double itemTop = cursorY + crossOffset + child.ActualMarginTop;

                double dx = itemLeft - child.Location.X;
                double dy = itemTop - child.Location.Y;

                if (Math.Abs(dx) > 0.1)
                    child.OffsetLeft(dx);

                if (Math.Abs(dy) > 0.1)
                    child.OffsetTop(dy);

                if (reverse)
                    cursorX -= marginBoxWidth + itemGap;
                else
                    cursorX += marginBoxWidth + itemGap;

                // The temporary width is restored after child layout; keep
                // the used border-box width in Size for painting/layout dumps.
                if (borderBoxWidth > 0 && Math.Abs(child.Size.Width - borderBoxWidth) > 0.5)
                    child.Size = new SizeF((float)borderBoxWidth, child.Size.Height);
            }

            cursorY += line.CrossSize + rowGap;
        }

        if (lines.Count > 0)
            cursorY -= rowGap;

        ActualBottom = cursorY + ActualPaddingBottom + ActualBorderBottomWidth;
        ActualRight = Location.X + Size.Width;
    }

    private static bool IsInFlowFlexItem(CssBox child) =>
        child.Display != CssConstants.None
        && child.Position is not (CssConstants.Absolute or CssConstants.Fixed);

    private static double ResolveFlexItemBaseOuterWidth(CssBox child, double containerContentWidth)
    {
        double borderBoxWidth;
        string basis = child.FlexBasis?.Trim();

        if (!string.IsNullOrEmpty(basis)
            && !basis.Equals("auto", StringComparison.OrdinalIgnoreCase)
            && !basis.Equals("content", StringComparison.OrdinalIgnoreCase))
        {
            borderBoxWidth = child.ResolveSpecifiedWidthToBorderBox(
                ParseFlexLengthOrZero(child, basis, containerContentWidth));
        }
        else if (child.Width != CssConstants.Auto && !string.IsNullOrEmpty(child.Width)
            && !IsIntrinsicWidthKeyword(child.Width))
        {
            borderBoxWidth = child.ResolveSpecifiedWidthToBorderBox(
                ParseFlexLengthOrZero(child, child.Width, containerContentWidth));
        }
        else
        {
            child.GetMinMaxWidth(out _, out double preferred);

            if (double.IsNaN(preferred) || preferred < 0)
                preferred = 0;

            borderBoxWidth = preferred + child.ActualBorderLeftWidth + child.ActualBorderRightWidth;
        }

        borderBoxWidth = ClampFlexItemBorderBoxWidth(child, borderBoxWidth, containerContentWidth);
        return borderBoxWidth + child.ActualMarginLeft + child.ActualMarginRight;
    }

    private static double ParseFlexLengthOrZero(CssBox box, string value, double percentBase)
    {
        try
        {
            return box.ParseLengthWithLineHeight(value, percentBase);
        }
        catch
        {
            return 0;
        }
    }

    private static double ClampFlexItemBorderBoxWidth(CssBox child, double borderBoxWidth, double containerContentWidth)
    {
        if (!string.IsNullOrEmpty(child.MaxWidth) && !child.MaxWidth.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            double length = ParseFlexLengthOrZero(child, child.MaxWidth, containerContentWidth);
            double maxWidth = child.ResolveSpecifiedWidthToBorderBox(length);

            if (borderBoxWidth > maxWidth)
                borderBoxWidth = maxWidth;
        }

        bool useAutomaticMinWidth = !child.IsMinWidthSpecified || child.MinWidth.Equals("auto", StringComparison.OrdinalIgnoreCase);

        if (useAutomaticMinWidth)
        {
            child.GetMinMaxWidth(out double minContentWidth, out _);

            if (!double.IsNaN(minContentWidth) && minContentWidth > 0)
            {
                double minBorderBoxWidth = minContentWidth + child.ActualBorderLeftWidth + child.ActualBorderRightWidth;

                if (borderBoxWidth < minBorderBoxWidth)
                    borderBoxWidth = minBorderBoxWidth;
            }
        }
        else if (!string.IsNullOrEmpty(child.MinWidth) && !child.MinWidth.Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            double length = ParseFlexLengthOrZero(child, child.MinWidth, containerContentWidth);
            double minWidth = child.ResolveSpecifiedWidthToBorderBox(length);

            if (borderBoxWidth < minWidth)
                borderBoxWidth = minWidth;
        }

        return Math.Max(0, borderBoxWidth);
    }

    private static void ResolveFlexLineWidths(FlexLineLayout line, double contentWidth, double columnGap)
    {
        int itemCount = line.Items.Count;

        if (itemCount == 0)
            return;

        double gapTotal = Math.Max(0, itemCount - 1) * columnGap;
        double freeSpace = contentWidth - line.BaseOuterWidth - gapTotal;

        if (freeSpace > 0.5)
        {
            double growTotal = 0;

            foreach (var item in line.Items)
                growTotal += item.Grow;

            if (growTotal > 0)
            {
                foreach (var item in line.Items)
                    item.TargetOuterWidth = item.BaseOuterWidth + freeSpace * (item.Grow / growTotal);
            }
        }
        else if (freeSpace < -0.5)
        {
            double shrinkTotal = 0;

            foreach (var item in line.Items)
                shrinkTotal += item.Shrink * Math.Max(0, item.BaseOuterWidth);

            if (shrinkTotal > 0)
            {
                foreach (var item in line.Items)
                {
                    double shrinkShare = (item.Shrink * Math.Max(0, item.BaseOuterWidth)) / shrinkTotal;
                    double target = item.BaseOuterWidth + freeSpace * shrinkShare;
                    double marginWidth = item.Box.ActualMarginLeft + item.Box.ActualMarginRight;
                    double borderBoxWidth = ClampFlexItemBorderBoxWidth(
                        item.Box,
                        Math.Max(0, target - marginWidth),
                        contentWidth);

                    item.TargetOuterWidth = borderBoxWidth + marginWidth;
                }
            }
        }
    }

    private static void LayoutFlexItemAtTargetWidth(ILayoutEnvironment g, CssBox child, double targetOuterWidth)
    {
        double targetBorderBoxWidth = Math.Max(0, targetOuterWidth - child.ActualMarginLeft - child.ActualMarginRight);
        double cssWidth = child.UsesBorderBoxSizing
            ? targetBorderBoxWidth
            : targetBorderBoxWidth
              - child.ActualPaddingLeft - child.ActualPaddingRight
              - child.ActualBorderLeftWidth - child.ActualBorderRightWidth;

        string savedWidth = child.Width;

        child.Width = FormatCssPx(Math.Max(0, cssWidth));
        child.PerformLayout(g);
        child.Width = savedWidth;
    }

    private static string FormatCssPx(double value) => value.ToString("0.####", CultureInfo.InvariantCulture) + "px";

    private static double ParseFlexFactor(string value, double fallback)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && parsed >= 0)
            return parsed;

        return fallback;
    }

    private double ResolveFlexGap(string value, double percentBase)
    {
        if (string.IsNullOrEmpty(value) || value.Equals("normal", StringComparison.OrdinalIgnoreCase))
            return 0;

        try
        {
            return CssLengthParser.ParseLength(value, percentBase, GetEmHeight());
        }
        catch
        {
            return 0;
        }
    }

    private void ResolveJustifyContent(int itemCount, double freeSpace, double baseGap, out double lineOffset, out double itemGap)
    {
        lineOffset = 0;
        itemGap = baseGap;

        string justify = NormalizeBoxAlignment(JustifyContent);
        switch (justify)
        {
            case "center":
                lineOffset = freeSpace / 2;
                break;

            case "end":
            case "flex-end":
            case "right":
                lineOffset = freeSpace;
                break;

            case "space-between":
                if (itemCount > 1)
                    itemGap = baseGap + freeSpace / (itemCount - 1);
                break;

            case "space-around":
                if (itemCount > 0)
                {
                    itemGap = baseGap + freeSpace / itemCount;
                    lineOffset = (itemGap - baseGap) / 2;
                }
                break;

            case "space-evenly":
                if (itemCount > 0)
                {
                    double extraGap = freeSpace / (itemCount + 1);
                    itemGap = baseGap + extraGap;
                    lineOffset = extraGap;
                }
                break;
        }
    }

    private double ResolveFlexCrossOffset(CssBox child, double lineCrossSize)
    {
        string align = NormalizeBoxAlignment(child.AlignSelf);
        if (align is "" or "auto" or "normal")
            align = NormalizeBoxAlignment(AlignItems);

        double itemOuterHeight = GetFlexItemOuterHeight(child);
        double freeSpace = lineCrossSize - itemOuterHeight;
        if (freeSpace <= 0)
            return 0;

        return align switch
        {
            "center" => freeSpace / 2,
            "end" or "flex-end" or "self-end" => freeSpace,
            _ => 0
        };
    }

    private static double GetFlexItemOuterHeight(CssBox child) =>
        Math.Max(0, child.ActualBottom - child.Location.Y)
        + child.ActualMarginTop + child.ActualMarginBottom;

    private void ApplyFlexColumnInlineAxisAlignment()
    {
        if (!IsFlexContainer())
            return;

        string direction = FlexDirection?.Trim().ToLowerInvariant() ?? "row";
        if (direction is not ("column" or "column-reverse"))
            return;

        double contentWidth = Math.Max(0, Size.Width
            - ActualBorderLeftWidth - ActualBorderRightWidth
            - ActualPaddingLeft - ActualPaddingRight);

        if (contentWidth <= 0)
            return;

        string containerAlign = NormalizeBoxAlignment(AlignItems);
        foreach (var child in Boxes)
        {
            if (!IsInFlowFlexItem(child))
                continue;

            string align = NormalizeBoxAlignment(child.AlignSelf);
            if (align is "" or "auto" or "normal")
                align = containerAlign;

            double marginBoxWidth = child.Size.Width + child.ActualMarginLeft + child.ActualMarginRight;
            double freeSpace = contentWidth - marginBoxWidth;

            if (freeSpace <= 0.5)
                continue;

            double offset = align switch
            {
                "center" => freeSpace / 2,
                "end" or "flex-end" or "self-end" or "right" => freeSpace,
                _ => 0
            };

            if (offset <= 0.5)
                continue;

            double targetLeft = ClientLeft + offset + child.ActualMarginLeft;
            double dx = targetLeft - child.Location.X;

            if (Math.Abs(dx) > 0.5)
                child.OffsetLeft(dx);
        }
    }

    /// <summary>
    /// CSS Box Alignment Level 3 §6.2 / CSS Flexbox §8.3: position flex/grid
    /// items along the block (cross) axis according to <c>align-items</c>
    /// (overridable per item by <c>align-self</c>).  Broiler approximates
    /// flex/grid with an inline formatting context (FlowInlineBlock), which
    /// leaves every item at the content-box block-start; this pass shifts
    /// items to center/end when the container has a definite block size with
    /// free space.  Only the common horizontal-writing-mode case is handled
    /// (grid and row/row-reverse flex); column flex (cross axis = inline)
    /// and overflowing content fall back to start alignment.
    /// </summary>
    private void ApplyFlexGridCrossAxisAlignment()
    {
        if (Display is not ("flex" or "inline-flex" or "grid" or "inline-grid"))
            return;

        // The real definite-track grid pass already placed items in their areas
        // (including block-axis alignment); skip the approximate re-alignment.
        if (_gridTrackLayoutApplied)
            return;

        // The cross axis must be the block (vertical) axis: true for grid and
        // for row-direction flex.  Column flex aligns items on the inline
        // axis, which this approximation does not handle.
        if (Display is "flex" or "inline-flex" && FlexDirection is "column" or "column-reverse")
            return;

        // A definite block size is required to have free space to distribute.
        // (Size.Height is content-derived at this point — the specified height
        // is only pre-resolved into Size for percentage values — so resolve
        // the definite block size directly from the 'height' declaration.)
        if (string.IsNullOrEmpty(Height) || Height == CssConstants.Auto || HeightPercentageResolvesToAuto())
            return;

        double cbHeight;
        
        if (Position == CssConstants.Fixed && LayoutEnvironment != null)
            cbHeight = LayoutEnvironment.ViewportSize.Height;
        else if (ContainingBlock?.ParentBox == null && LayoutEnvironment != null)
            cbHeight = LayoutEnvironment.ViewportSize.Height;
        else
            cbHeight = ContainingBlock?.Size.Height ?? 0;

        double contentTop = ClientTop;
        double length = CssLengthParser.ParseLength(Height, cbHeight, GetEmHeight());
        double contentHeight = ResolveSpecifiedHeightToBorderBox(length)
            - ActualPaddingTop - ActualPaddingBottom
            - ActualBorderTopWidth - ActualBorderBottomWidth;
        
        if (contentHeight <= 0)
            return;

        string containerAlign = NormalizeBoxAlignment(AlignItems);

        foreach (CssBox item in Boxes)
        {
            if (item.Display == CssConstants.None)
                continue;

            // CSS2.1 §9.6.1 / §9.5: out-of-flow items are positioned separately.
            if (item.Position is CssConstants.Absolute or CssConstants.Fixed)
                continue;

            if (item.Float != CssConstants.None)
                continue;

            // 'align-self: auto' (and 'normal') resolve to the container's
            // 'align-items' (CSS Box Alignment §6.2).
            string self = NormalizeBoxAlignment(item.AlignSelf);
            string align = self is "" or "auto" or "normal" ? containerAlign : self;

            bool toCenter = align == "center";
            bool toEnd = align is "end" or "flex-end" or "self-end";

            if (!toCenter && !toEnd)
                continue; // start/flex-start/baseline/stretch → block-start

            double marginBoxHeight = (item.ActualBottom - item.Location.Y)
                + item.ActualMarginTop + item.ActualMarginBottom;
            double free = contentHeight - marginBoxHeight;

            if (free <= 0.5)
                continue; // safe alignment: no room → keep at start

            double marginBoxOffset = toCenter ? free / 2 : free;
            double targetTop = contentTop + marginBoxOffset + item.ActualMarginTop;
            double dy = targetTop - item.Location.Y;

            if (Math.Abs(dy) > 0.5)
            {
                item.OffsetTop(dy);
                item.ActualBottom += dy;
            }
        }
    }

    /// <summary>
    /// Normalises a CSS Box Alignment keyword: trims, strips a leading
    /// <c>safe</c>/<c>unsafe</c> overflow-alignment qualifier, and lower-cases.
    /// </summary>
    private static string NormalizeBoxAlignment(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        string v = value.Trim();

        if (v.StartsWith("safe ", StringComparison.OrdinalIgnoreCase))
            v = v[5..].Trim();
        else if (v.StartsWith("unsafe ", StringComparison.OrdinalIgnoreCase))
            v = v[7..].Trim();

        return v.ToLowerInvariant();
    }
}
