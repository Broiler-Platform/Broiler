using Broiler.CSS;
using System.Globalization;


namespace Broiler.Layout.Engine;

internal partial class CssBox : CssBoxProperties, IDisposable
{
    private bool UsesBorderBoxSizing =>
        BoxSizing != null && BoxSizing.Equals("border-box", StringComparison.OrdinalIgnoreCase);

    private double ResolveSpecifiedWidthToBorderBox(double cssWidth)
    {
        if (!UsesBorderBoxSizing)
            cssWidth += ActualPaddingLeft + ActualPaddingRight + ActualBorderLeftWidth + ActualBorderRightWidth;

        return Math.Max(0, cssWidth);
    }

    /// <summary>CSS Sizing 3: <c>true</c> for a content-based intrinsic width
    /// keyword (<c>min-content</c>, <c>max-content</c>, <c>fit-content</c> /
    /// <c>fit-content()</c>) that resolves to the box's content size rather than a
    /// length against the containing block.</summary>
    private static bool IsIntrinsicSizingWidthKeyword(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        string v = value.Trim();
        return v.Equals("min-content", StringComparison.OrdinalIgnoreCase)
            || v.Equals("max-content", StringComparison.OrdinalIgnoreCase)
            || v.Equals("fit-content", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("fit-content(", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>CSS Sizing 3: <c>true</c> for a content-based intrinsic <c>height</c>
    /// keyword. A block box's min-/max-/fit-content block size is its content
    /// height, so such a height must not be treated as a specified length (which,
    /// under <c>box-sizing:border-box</c>, would wrongly reinterpret the already
    /// content-derived height as a border-box value and drop the border/padding);
    /// leave the content-computed <c>ActualBottom</c> in place and let the §10.7
    /// min-/max-height clamp apply.</summary>
    private static bool IsIntrinsicSizingHeightKeyword(string value) =>
        IsIntrinsicSizingWidthKeyword(value);

    private double ResolveSpecifiedHeightToBorderBox(double cssHeight)
    {
        if (!UsesBorderBoxSizing)
            cssHeight += ActualPaddingTop + ActualPaddingBottom + ActualBorderTopWidth + ActualBorderBottomWidth;

        return Math.Max(0, cssHeight);
    }

    /// <summary>
    /// CSS2.1 §10.7: clamp a specified (author-declared) height to
    /// <c>min-height</c>/<c>max-height</c> in the same box-sizing frame (both share
    /// it), returning the clamped specified value — the caller normalizes to the
    /// border box via <see cref="ResolveSpecifiedHeightToBorderBox"/>. A percentage
    /// min-/max-height against an indefinite (auto-height) flow containing block is
    /// treated as its initial value (<c>0</c>/<c>none</c>), per §10.7.
    /// </summary>
    private double ClampSpecifiedHeightToMinMax(double specifiedHeight)
    {
        double cbHeight = (Position == CssConstants.Fixed && LayoutEnvironment != null)
            ? LayoutEnvironment.ViewportSize.Height
            : ContainingBlock?.Size.Height ?? 0;
        bool cbIndefinite = Position is not (CssConstants.Absolute or CssConstants.Fixed)
            && ContainingBlock?.ParentBox != null
            && (ContainingBlock.Height == CssConstants.Auto || string.IsNullOrEmpty(ContainingBlock.Height));

        if (MaxHeight != "none" && !string.IsNullOrEmpty(MaxHeight)
            && !(MaxHeight.Contains('%') && cbIndefinite))
        {
            double maxH = CssLengthParser.ParseLength(MaxHeight, cbHeight, GetEmHeight());
            if (specifiedHeight > maxH) specifiedHeight = maxH;
        }
        if (MinHeight != "0" && !string.IsNullOrEmpty(MinHeight)
            && !(MinHeight.Contains('%') && cbIndefinite))
        {
            double minH = CssLengthParser.ParseLength(MinHeight, cbHeight, GetEmHeight());
            if (specifiedHeight < minH) specifiedHeight = minH;
        }
        return specifiedHeight;
    }

    internal double GetMinimumWidth()
    {
        double maxWidth = 0;
        CssRect maxWidthWord = null;
        CssBoxHelper.GetMinimumWidth_LongestWord(this, ref maxWidth, ref maxWidthWord);

        double padding = 0f;
        if (maxWidthWord != null)
        {
            var box = maxWidthWord.OwnerBox;
            while (box != null)
            {
                padding += box.ActualBorderRightWidth + box.ActualPaddingRight + box.ActualBorderLeftWidth + box.ActualPaddingLeft;
                box = box != this ? box.ParentBox : null;
            }
        }

        return maxWidth + padding;
    }

    /// <summary>
    /// Min/max-content width measured from this box's <em>content</em>, ignoring
    /// its own explicit width (a percentage width is resolved against the caller's
    /// context, so for grid track sizing it is treated as auto). Descendants'
    /// explicit widths are honoured. Used by the grid track-sizing algorithm.
    /// </summary>
    internal void GetContentMinMaxWidth(out double minWidth, out double maxWidth)
    {
        double min = 0f;
        double maxSum = 0f;
        double paddingSum = 0f;
        double marginSum = 0f;

        CssBoxHelper.GetMinMaxSumWords(this, ref min, ref maxSum, ref paddingSum, ref marginSum, suppressExplicitWidthFor: this);

        maxWidth = paddingSum + maxSum;
        minWidth = paddingSum + (min < 90999 ? min : 0);
        maxWidth -= CssBoxHelper.EdgeWhitespaceSpacing(this);
        if (maxWidth < minWidth)
            maxWidth = minWidth;
    }

    internal void GetMinMaxWidth(out double minWidth, out double maxWidth)
    {
        // A grid with a fixed track template contributes its physical-width track
        // sum (+ gaps + own border/padding) as both min- and max-content, rather
        // than the intrinsic width of its inline content — so a shrink-to-fit grid
        // (or a nested grid item) sizes to its tracks, not its (often empty) text.
        if (TryComputeGridIntrinsicContentWidth(useMax: false, out double gridMin)
            && TryComputeGridIntrinsicContentWidth(useMax: true, out double gridMax))
        {
            double pb = ActualBorderLeftWidth + ActualBorderRightWidth
                      + ActualPaddingLeft + ActualPaddingRight;
            minWidth = gridMin + pb;
            maxWidth = gridMax + pb;
            return;
        }

        double min = 0f;
        double maxSum = 0f;
        double paddingSum = 0f;
        double marginSum = 0f;

        CssBoxHelper.GetMinMaxSumWords(this, ref min, ref maxSum, ref paddingSum, ref marginSum);

        maxWidth = paddingSum + maxSum;
        minWidth = paddingSum + (min < 90999 ? min : 0);

        // CSS Text 3 §4.1.1 (phase II): a collapsible space sequence at the
        // start of the first line / end of the last line of a formatting
        // context is removed and contributes no width.  Broiler models a
        // collapsed space as word-spacing carried on the neighbouring word
        // (HasSpaceBefore / HasSpaceAfter); GetMinMaxSumWords counts that
        // spacing for every word, so the leading space-before of the box's
        // first content word and the trailing space-after of its last word
        // inflate the preferred width by one space each.  This is the box's
        // own formatting-context edge (GetMinMaxWidth is only queried for
        // shrink-to-fit roots — table cells, floats, inline-blocks,
        // abspos), and the paint path already drops those edge spaces, so
        // the width must match.  Subtracting them makes a whitespace-padded
        // table cell (e.g. <td> Cell </td>) shrink to the same width as the
        // tight cell, so adjacent cells abut as in a real <table>
        // (CSS2 tables/table-anonymous-objects-*).
        maxWidth -= CssBoxHelper.EdgeWhitespaceSpacing(this);
        if (maxWidth < minWidth)
            maxWidth = minWidth;
    }

    /// <summary>
    /// CSS2.1 §10.3.7: Computes the shrink-to-fit width for an auto-width
    /// absolutely positioned element by independently measuring each direct
    /// child's total width and returning the maximum.
    /// Each block or float child is its own "line"; the preferred width is
    /// the widest line.  This avoids the incorrect accumulation that occurs
    /// when <see cref="CssBoxHelper.GetMinMaxSumWords"/> sums float widths
    /// with preceding block widths.
    /// </summary>
    private double ComputeShrinkToFitWidth()
    {
        // A grid with a fixed track template shrink-to-fits to its physical-width
        // track sum (+ gaps), not the max-content of its inline content — an empty
        // or small-item grid would otherwise collapse (fit-content / float /
        // inline-grid grids). Content-box width; the caller adds border/padding.
        if (TryComputeGridIntrinsicContentWidth(useMax: true, out double gridMaxContent))
            return gridMaxContent;

        double maxLineWidth = 0;
        // Running width of a horizontal run of adjacent floated children. At
        // max-content the container is under no width constraint, so a run of
        // float:left/right children lays out side by side and their widths ADD;
        // a non-floated (block) child ends the run and starts its own line.
        double floatRunWidth = 0;

        foreach (var child in Boxes)
        {
            double childWidth;

            if (child.Width != CssConstants.Auto && !string.IsNullOrEmpty(child.Width)
                && !IsPercentageWidth(child.Width))
            {
                // Explicit (definite) width: use declared width + borders/padding
                double containingBlockWidth = Size.Width > 0 && !double.IsNaN(Size.Width) ? Size.Width : 0;
                childWidth = child.ParseLengthWithLineHeight(child.Width, containingBlockWidth)
                           + child.ActualBorderLeftWidth + child.ActualBorderRightWidth
                           + child.ActualPaddingLeft + child.ActualPaddingRight;
            }
            else
            {
                // Auto- or percentage-width child: compute its intrinsic
                // preferred width. CSS Sizing 3 §5.1: a child's percentage width
                // resolves against the size we are *computing*, so it is treated
                // as auto for the container's max-content — otherwise a
                // width:100% child resolves against the container's current
                // (available) width and balloons the shrink-to-fit result to the
                // full container (e.g. a float or auto-fill grid item sized 100%
                // pins the float to the viewport instead of its content).
                // Guard against NaN from unmeasured words in deeply nested
                // inline elements (e.g. Acid2 .eyes → #eyes-a → <object>).
                child.GetMinMaxWidth(out _, out double childMax);
                childWidth = double.IsNaN(childMax) ? 0 : childMax;
            }

            childWidth += child.ActualMarginLeft + child.ActualMarginRight;
            if (double.IsNaN(childWidth))
                continue;

            // CSS Sizing 3 §5: the max-content width of a container is the widest
            // of its lines with no wrapping. Inline-level content stays on the line
            // and accumulates — adjacent floats (WPT floats-143: a <ul> of two
            // float:left <li> would otherwise shrink to one child's width and wrap
            // the second below the first) and **atomic inline-level boxes**
            // (inline-block / inline-table / inline-flex / inline-grid), which sit
            // side by side, so two 40px inline-blocks contribute 80, not 40. Only a
            // block-level child ends the run and starts its own line.
            if (child.Float != CssConstants.None
                || CssBoxHelper.IsAtomicInlineLevel(child.Display))
            {
                floatRunWidth += childWidth;
                maxLineWidth = Math.Max(maxLineWidth, floatRunWidth);
            }
            else
            {
                floatRunWidth = 0;
                maxLineWidth = Math.Max(maxLineWidth, childWidth);
            }
        }

        return maxLineWidth;
    }

    // ─────────────────────── CSS Sizing 4: aspect-ratio ───────────────────────

    /// <summary>
    /// CSS Sizing 4 §4: resolve the used border-box block (height) size of a box
    /// whose height is <c>auto</c> from its already-resolved used inline (width)
    /// size and its preferred <c>aspect-ratio</c>. The caller applies this only to
    /// in-flow block-level boxes, whose used width fills the containing block and
    /// so does not itself depend on the aspect ratio, making the transfer
    /// unambiguous.
    /// <para>The reference browser drops the experimental <c>display: grid-lanes</c>
    /// keyword to the element's default display (block; issue #1218) but still
    /// honours <c>aspect-ratio</c>, so a dropped grid-lanes container with an auto
    /// height is sized to a square — the <c>css-grid/grid-lanes/track-sizing/
    /// auto-repeat</c> cluster expects exactly this. Broiler previously ignored
    /// <c>aspect-ratio</c> on ordinary boxes and rendered a viewport-wide,
    /// min-height-tall bar, matching those references by only ~8%.</para>
    /// <para>Returns the transferred border-box height; the caller then applies the
    /// CSS2.1 §10.7 min-/max-height clamp (so a <c>min-height</c> floors the
    /// square). Returns <c>false</c> when there is no preferred aspect ratio,
    /// leaving every aspect-ratio-less box (the overwhelming majority) untouched.</para>
    /// </summary>
    private bool TryResolveAspectRatioBlockHeight(out double borderBoxHeight)
    {
        borderBoxHeight = 0;
        if (!TryParseAspectRatio(AspectRatio, out double ratio) || !(ratio > 0))
            return false;

        double borderBoxWidth = Size.Width;
        if (!(borderBoxWidth > 0))
            return false;

        // aspect-ratio relates the two sizes of the box named by box-sizing
        // (CSS Sizing 4 §4): the border box under `box-sizing: border-box`,
        // otherwise the content box. Transfer width→height in that box (ratio is
        // width/height), then map back to a border-box height for ActualBottom.
        double specifiedHeight;
        if (UsesBorderBoxSizing)
        {
            specifiedHeight = borderBoxWidth / ratio;
        }
        else
        {
            double contentWidth = borderBoxWidth
                - ActualPaddingLeft - ActualPaddingRight
                - ActualBorderLeftWidth - ActualBorderRightWidth;
            if (!(contentWidth > 0))
                return false;
            specifiedHeight = contentWidth / ratio;
        }

        borderBoxHeight = ResolveSpecifiedHeightToBorderBox(specifiedHeight);
        return borderBoxHeight > 0
            && !double.IsNaN(borderBoxHeight) && !double.IsInfinity(borderBoxHeight);
    }

    /// <summary>Parses an <c>aspect-ratio</c> value (<c>&lt;number&gt; [ /
    /// &lt;number&gt; ]?</c>, ignoring a leading/trailing <c>auto</c> keyword)
    /// into a width/height ratio. Returns <c>false</c> for <c>auto</c>/<c>none</c>
    /// or a non-positive ratio.</summary>
    internal static bool TryParseAspectRatio(string value, out double ratio)
    {
        ratio = 0;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        double w = double.NaN, h = 1;

        foreach (var token in value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("auto", StringComparison.OrdinalIgnoreCase)
                || token.Equals("none", StringComparison.OrdinalIgnoreCase))
                continue;

            int slash = token.IndexOf('/');
            if (slash >= 0)
            {
                if (!double.TryParse(token[..slash].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out w))
                    return false;

                string rest = token[(slash + 1)..].Trim();
                if (rest.Length > 0 && !double.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out h))
                    return false;
            }
            else if (double.IsNaN(w))
            {
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out w))
                    return false;
            }
            else
            {
                // A second bare number is the denominator (e.g. `1 / 1` split on space).
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out h))
                    return false;
            }
        }
        if (double.IsNaN(w) || !(w > 0) || !(h > 0))
            return false;

        ratio = w / h;
        return true;
    }

    /// <summary>
    /// CSS Sizing 3 §5.1: <c>true</c> when <paramref name="width"/> is one of
    /// the intrinsic sizing keywords (<c>min-content</c>, <c>max-content</c>,
    /// <c>fit-content</c>).
    /// </summary>
    private static bool IsIntrinsicWidthKeyword(string width) =>
        string.Equals(width, "min-content", StringComparison.OrdinalIgnoreCase)
        || string.Equals(width, "max-content", StringComparison.OrdinalIgnoreCase)
        || string.Equals(width, "fit-content", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// CSS Sizing 3 §5.1: <c>true</c> when <paramref name="width"/> is a plain
    /// percentage (e.g. <c>100%</c>). Such a width resolves against the size being
    /// computed during a container's intrinsic (shrink-to-fit / max-content) pass,
    /// so it must be treated as <c>auto</c> there rather than resolved against the
    /// container's tentative width.
    /// </summary>
    private static bool IsPercentageWidth(string width) =>
        !string.IsNullOrEmpty(width)
        && width.EndsWith('%')
        && !width.Contains('(');

    /// <summary>
    /// CSS Sizing 3 §5: Resolves an intrinsic-keyword width to a used
    /// border-box width.  <c>min-content</c> uses the largest child
    /// min-content contribution, <c>max-content</c> the largest max-content
    /// contribution, and <c>fit-content</c> clamps the max-content size into
    /// the available space (but never below min-content).
    /// </summary>
    private double ResolveIntrinsicWidth(ILayoutEnvironment g, string keyword, double availableContentWidth)
    {
        EnsureDescendantWordsMeasured(g);

        double available = availableContentWidth - ActualMarginLeft - ActualMarginRight;
        double content;
        
        if (string.Equals(keyword, "min-content", StringComparison.OrdinalIgnoreCase))
        {
            content = ComputeIntrinsicInlineSize(useMin: true);
        }
        else if (string.Equals(keyword, "max-content", StringComparison.OrdinalIgnoreCase))
        {
            content = ComputeIntrinsicInlineSize(useMin: false);
        }
        else // fit-content
        {
            double max = ComputeIntrinsicInlineSize(useMin: false);
            double min = ComputeIntrinsicInlineSize(useMin: true);

            content = Math.Min(Math.Max(min, available), max);
        }

        if (double.IsNaN(content) || content < 0)
            content = 0;

        return ResolveSpecifiedWidthToBorderBox(content);
    }

    /// <summary>
    /// Computes the intrinsic inline size (content width) as the widest direct
    /// child contribution.  Each block/float child forms its own line, so the
    /// container's intrinsic size is the maximum child width rather than the
    /// sum.  When <paramref name="useMin"/> is set, auto-width children
    /// contribute their min-content width; otherwise their max-content width.
    /// </summary>
    private double ComputeIntrinsicInlineSize(bool useMin)
    {
        double maxLineWidth = 0;

        foreach (var child in Boxes)
        {
            double childWidth;

            if (child.Width != CssConstants.Auto && !string.IsNullOrEmpty(child.Width)
                && !IsIntrinsicWidthKeyword(child.Width))
            {
                double containingBlockWidth = Size.Width > 0 && !double.IsNaN(Size.Width) ? Size.Width : 0;
                childWidth = child.ParseLengthWithLineHeight(child.Width, containingBlockWidth)
                           + child.ActualBorderLeftWidth + child.ActualBorderRightWidth
                           + child.ActualPaddingLeft + child.ActualPaddingRight;
            }
            else
            {
                child.GetMinMaxWidth(out double childMin, out double childMax);
                double intrinsic = useMin ? childMin : childMax;
                childWidth = double.IsNaN(intrinsic) ? 0 : intrinsic;
            }

            childWidth += child.ActualMarginLeft + child.ActualMarginRight;

            if (!double.IsNaN(childWidth))
                maxLineWidth = Math.Max(maxLineWidth, childWidth);
        }

        return maxLineWidth;
    }

    /// <summary>
    /// Computes the shrink-to-fit content width of this box: the maximum
    /// right edge of all child boxes (relative to this box) plus padding
    /// and border.  Used for abspos self-alignment where the box size is
    /// content-driven rather than stretched.
    /// </summary>
    private double GetShrinkToFitWidth()
    {
        // If there's an explicit CSS width, use it (plus border/padding).
        if (Width != CssConstants.Auto && !string.IsNullOrEmpty(Width))
            return Size.Width;

        double maxRight = 0;

        foreach (var child in Boxes)
        {
            if (child.Display == CssConstants.None) 
                continue;
            
            double childRight = (child.Location.X - Location.X)
                                + child.Size.Width
                                + child.ActualMarginRight;
            maxRight = Math.Max(maxRight, childRight);
        }

        if (maxRight <= 0) 
            return Size.Width;

        return maxRight + ActualPaddingRight + ActualBorderRightWidth;
    }

    /// <summary>
    /// Computes the shrink-to-fit content height of this box: the maximum
    /// bottom edge of all child boxes (relative to this box) plus padding
    /// and border.  Used for abspos self-alignment where the box size is
    /// content-driven rather than stretched.
    /// </summary>
    private double GetShrinkToFitHeight()
    {
        // If there's an explicit CSS height, use it (plus border/padding).
        if (Height != CssConstants.Auto && !string.IsNullOrEmpty(Height))
        {
            double h = ActualBottom - Location.Y;
            return h > 0 ? h : Size.Height;
        }

        double maxBottom = 0;
        
        foreach (var child in Boxes)
        {
            if (child.Display == CssConstants.None) 
                continue;
            
            double childBottom = (child.Location.Y - Location.Y)
                                 + (child.ActualBottom - child.Location.Y)
                                 + child.ActualMarginBottom;
            
            maxBottom = Math.Max(maxBottom, childBottom);
        }

        if (maxBottom <= 0)
        {
            double h = ActualBottom - Location.Y;
            return h > 0 ? h : Size.Height;
        }

        return maxBottom + ActualPaddingBottom + ActualBorderBottomWidth;
    }

    /// <summary>
    /// Recursively finds the maximum bottom edge of any float in the
    /// subtree, stopping at nested BFC boundaries.  Used by the BFC
    /// root height calculation so that grandchild (and deeper) floats
    /// are properly contained.
    /// </summary>
    private static void FindMaxDescendantFloatBottom(CssBox box, ref double maxBottom)
    {
        foreach (var child in box.Boxes)
        {
            if (child.Float != CssConstants.None && child.Display != CssConstants.None)
            {
                maxBottom = Math.Max(maxBottom, child.ActualBottom + child.ActualMarginBottom);
            }

            // Don't recurse into nested BFC roots — their floats are
            // contained by them, not by the outer BFC.
            bool childIsBfc = child.Float != CssConstants.None
                || child.Display == CssConstants.InlineBlock
                || child.Display == CssConstants.TableCell
                || child.Display is "flex" or "inline-flex" or "grid" or "inline-grid"
                || child.Position == CssConstants.Absolute
                || child.Position == CssConstants.Fixed
                || (child.Overflow != null && child.Overflow != CssConstants.Visible)
                || (child.AlignContent != null && child.AlignContent != "normal");

            if (!childIsBfc)
                FindMaxDescendantFloatBottom(child, ref maxBottom);
        }
    }
}
