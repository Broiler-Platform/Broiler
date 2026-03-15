using Broiler.HTML.Adapters.Adapters;
using Broiler.HTML.Core.Core;
using Broiler.HTML.Core.Core.Entities;
using Broiler.HTML.CSS.Core.Parse;
using Broiler.HTML.Dom.Core.Utils;
using Broiler.HTML.Utils.Core.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace Broiler.HTML.Dom.Core.Dom;

internal class CssBox : CssBoxProperties, IDisposable
{
    private CssBox _parentBox;
    protected IHtmlContainerInt _htmlContainer;
    private ReadOnlyMemory<char> _text;

    internal bool _tableFixed;

    protected bool _wordsSizeMeasured;
    private CssBox _listItemBox;
    private IImageLoadHandler _imageLoadHandler;

    /// <summary>
    /// Returns the loaded background image handle, or null if no background image is loaded.
    /// Used by <c>FragmentTreeBuilder</c> to capture background images for the new paint path.
    /// </summary>
    internal object LoadedBackgroundImage => _imageLoadHandler?.Image;

    public CssBox(CssBox parentBox, HtmlTag tag)
    {
        if (parentBox != null)
        {
            _parentBox = parentBox;
            _parentBox.Boxes.Add(this);
        }

        HtmlTag = tag;
    }

    /// <summary>
    /// The container abstracted through <see cref="IHtmlContainerInt"/>. Used by
    /// CssBox and subclass code for decoupled access.
    /// </summary>
    internal IHtmlContainerInt ContainerInt
    {
        get { return _htmlContainer ??= _parentBox?.ContainerInt; }
        set { _htmlContainer = value; }
    }

    public CssBox ParentBox
    {
        get { return _parentBox; }
        set
        {
            _parentBox?.Boxes.Remove(this);
            _parentBox = value;

            if (value != null)
                _parentBox.Boxes.Add(this);
        }
    }

    public List<CssBox> Boxes { get; } = [];

    public override bool AvoidGeometryAntialias => ContainerInt?.AvoidGeometryAntialias ?? false;

    public bool IsBrElement => HtmlTag != null && HtmlTag.Name.Equals("br", StringComparison.InvariantCultureIgnoreCase);
    public bool IsInline => (Display == CssConstants.Inline || Display == CssConstants.InlineBlock) && !IsBrElement;
    public bool IsBlock => Display == CssConstants.Block;
    public virtual bool IsClickable => HtmlTag != null && HtmlTag.Name == HtmlConstants.A && !HtmlTag.HasAttribute("id");

    public virtual bool IsFixed
    {
        get
        {
            if (Position == CssConstants.Fixed)
                return true;

            if (ParentBox == null)
                return false;

            CssBox parent = this;

            while (!(parent.ParentBox == null || parent == parent.ParentBox))
            {
                parent = parent.ParentBox;

                if (parent.Position == CssConstants.Fixed)
                    return true;
            }

            return false;
        }
    }

    public virtual string HrefLink => GetAttribute(HtmlConstants.Href);

    public CssBox ContainingBlock
    {
        get
        {
            if (ParentBox == null)
                return this; //This is the initial containing block.

            var box = ParentBox;

            while (!box.IsBlock && box.Display != CssConstants.ListItem && box.Display != CssConstants.Table &&
                   box.Display != CssConstants.TableCell && box.ParentBox != null)
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
    /// </summary>
    private CssBox FindPositionedContainingBlock()
    {
        var box = ParentBox;
        while (box != null)
        {
            if (box.Position is CssConstants.Relative or CssConstants.Absolute or CssConstants.Fixed
                || box.ParentBox == null)
            {
                return box;
            }

            box = box.ParentBox;
        }

        return ContainingBlock;
    }

    /// <summary>
    /// Returns true when <see cref="Height"/> is a percentage that resolves
    /// to auto because the containing block's height is not explicitly
    /// specified (CSS 2.1 §10.5).  Callers must still verify that Height is
    /// not auto/empty before using this — the check only tests whether a
    /// non-auto percentage value should be treated as auto.
    /// </summary>
    internal bool HeightPercentageResolvesToAuto()
    {
        return Height.Contains('%')
            && (ContainingBlock.Height == CssConstants.Auto
                || string.IsNullOrEmpty(ContainingBlock.Height));
    }

    public HtmlTag HtmlTag { get; }

    public bool IsImage => Words.Count == 1 && Words[0].IsImage;

    public bool IsSpaceOrEmpty
    {
        get
        {
            if ((Words.Count != 0 || Boxes.Count != 0) && (Words.Count != 1 || !Words[0].IsSpaces))
            {
                foreach (CssRect word in Words)
                {
                    if (!word.IsSpaces)
                        return false;
                }
            }

            return true;
        }
    }

    public ReadOnlyMemory<char> Text
    {
        get { return _text; }
        set
        {
            _text = value;
            Words.Clear();
        }
    }

    internal List<CssLineBox> LineBoxes { get; } = [];
    internal List<CssLineBox> ParentLineBoxes { get; } = [];
    internal Dictionary<CssLineBox, RectangleF> Rectangles { get; } = [];
    internal List<CssRect> Words { get; } = [];
    internal CssRect FirstWord => Words[0];

    internal CssLineBox FirstHostingLineBox { get; set; }

    internal CssLineBox LastHostingLineBox { get; set; }

    public void PerformLayout(RGraphics g)
    {
        try
        {
            PerformLayoutImp(g);
        }
        catch (Exception ex)
        {
            ContainerInt.ReportError(HtmlRenderErrorType.Layout, "Exception in box layout", ex);
        }
    }

    public void SetBeforeBox(CssBox before)
    {
        int index = _parentBox.Boxes.IndexOf(before);

        if (index < 0)
            throw new Exception("before box doesn't exist on parent");

        _parentBox.Boxes.Remove(this);
        _parentBox.Boxes.Insert(index, this);
    }

    public void SetAllBoxes(CssBox fromBox)
    {
        foreach (var childBox in fromBox.Boxes)
            childBox._parentBox = this;

        Boxes.AddRange(fromBox.Boxes);
        fromBox.Boxes.Clear();
    }

    public void ParseToWords()
    {
        Words.Clear();

        int startIdx = 0;
        bool preserveSpaces = WhiteSpace == CssConstants.Pre || WhiteSpace == CssConstants.PreWrap;
        bool respoctNewline = preserveSpaces || WhiteSpace == CssConstants.PreLine;

        var textSpan = _text.Span;
        while (startIdx < textSpan.Length)
        {
            while (startIdx < textSpan.Length && textSpan[startIdx] == '\r')
                startIdx++;

            if (startIdx < textSpan.Length)
            {
                var endIdx = startIdx;

                while (endIdx < textSpan.Length && char.IsWhiteSpace(textSpan[endIdx]) && textSpan[endIdx] != '\n')
                    endIdx++;

                if (endIdx > startIdx)
                {
                    if (preserveSpaces)
                    {
                        // CSS2.1 §16.6: For pre-wrap, emit each space as a
                        // separate word so the layout engine can break lines
                        // at any space position.  For pre, emit the entire
                        // whitespace run as one word (no wrapping allowed).
                        if (WhiteSpace == CssConstants.PreWrap)
                        {
                            // Cache " " string to avoid per-char allocation
                            const string singleSpace = " ";
                            for (int i = startIdx; i < endIdx; i++)
                            {
                                var ch = _text.Slice(i, 1).ToString();
                                Words.Add(new CssRectWord(this, ch == " " ? singleSpace : ch, false, false));
                            }
                        }
                        else
                        {
                            Words.Add(new CssRectWord(this, HtmlUtils.DecodeHtml(_text.Slice(startIdx, endIdx - startIdx).ToString()), false, false));
                        }
                    }
                }
                else
                {
                    endIdx = startIdx;

                    while (endIdx < textSpan.Length && !char.IsWhiteSpace(textSpan[endIdx]) && textSpan[endIdx] != '-' && WordBreak != CssConstants.BreakAll && !CommonUtils.IsAsianCharecter(textSpan[endIdx]))
                        endIdx++;

                    if (endIdx < textSpan.Length && (textSpan[endIdx] == '-' || WordBreak == CssConstants.BreakAll || CommonUtils.IsAsianCharecter(textSpan[endIdx])))
                        endIdx++;

                    if (endIdx > startIdx)
                    {
                        var hasSpaceBefore = !preserveSpaces && startIdx > 0 && Words.Count == 0 && char.IsWhiteSpace(textSpan[startIdx - 1]);
                        var hasSpaceAfter = !preserveSpaces && endIdx < textSpan.Length && char.IsWhiteSpace(textSpan[endIdx]);

                        Words.Add(new CssRectWord(this, HtmlUtils.DecodeHtml(_text.Slice(startIdx, endIdx - startIdx).ToString()), hasSpaceBefore, hasSpaceAfter));
                    }
                }

                // create new-line word so it will effect the layout
                if (endIdx < textSpan.Length && textSpan[endIdx] == '\n')
                {
                    endIdx++;

                    if (respoctNewline)
                        Words.Add(new CssRectWord(this, "\n", false, false));
                }

                startIdx = endIdx;
            }
        }
    }

    public virtual void Dispose()
    {
        _imageLoadHandler?.Dispose();

        foreach (var childBox in Boxes)
            childBox.Dispose();
    }

    protected virtual void PerformLayoutImp(RGraphics g)
    {
        if (Display != CssConstants.None)
        {
            RectanglesReset();
            MeasureWordsSize(g);
        }

        if (IsBlock || Display == CssConstants.ListItem || Display == CssConstants.Table || Display == CssConstants.InlineTable || Display == CssConstants.TableCell)
        {
            // Because their width and height are set by CssTable
            if (Display != CssConstants.TableCell && Display != CssConstants.Table)
            {
                double width = ContainingBlock.Size.Width
                               - ContainingBlock.ActualPaddingLeft - ContainingBlock.ActualPaddingRight
                               - ContainingBlock.ActualBorderLeftWidth - ContainingBlock.ActualBorderRightWidth;

                if (Width != CssConstants.Auto && !string.IsNullOrEmpty(Width))
                {
                    double containingWidth = width;
                    width = CssValueParser.ParseLength(Width, containingWidth, GetEmHeight());

                    // CSS2.1 §10.4: Apply max-width constraint
                    if (MaxWidth != "none" && !string.IsNullOrEmpty(MaxWidth))
                    {
                        double maxW = CssValueParser.ParseLength(MaxWidth, containingWidth, GetEmHeight());
                        if (width > maxW) width = maxW;
                    }

                    // CSS2.1 §10.4: Apply min-width constraint (min wins over max per §10.4)
                    if (MinWidth != "0" && !string.IsNullOrEmpty(MinWidth))
                    {
                        double minW = CssValueParser.ParseLength(MinWidth, containingWidth, GetEmHeight());
                        if (width < minW) width = minW;
                    }

                    width += ActualPaddingLeft + ActualPaddingRight + ActualBorderLeftWidth + ActualBorderRightWidth;
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
                            string halfMargin = (remainingSpace / 2).ToString("F4",
                                System.Globalization.CultureInfo.InvariantCulture) + "px";
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
                        MarginLeft = leftMargin.ToString("F4",
                            System.Globalization.CultureInfo.InvariantCulture) + "px";
                    }
                    else if (MarginRight == CssConstants.Auto)
                    {
                        double leftMargin = ActualMarginLeft;
                        double rightMargin = Math.Max(0, remainingSpace - leftMargin);
                        MarginRight = rightMargin.ToString("F4",
                            System.Globalization.CultureInfo.InvariantCulture) + "px";
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
                    if (double.IsNaN(prefMin)) prefMin = 0;
                    if (double.IsNaN(preferred)) preferred = 0;
                    double stfWidth = Math.Min(Math.Max(prefMin, available), preferred);

                    if (MaxWidth != "none" && !string.IsNullOrEmpty(MaxWidth))
                    {
                        double maxW = CssValueParser.ParseLength(MaxWidth, width, GetEmHeight());
                        if (stfWidth > maxW) stfWidth = maxW;
                    }
                    if (MinWidth != "0" && !string.IsNullOrEmpty(MinWidth))
                    {
                        double minW = CssValueParser.ParseLength(MinWidth, width, GetEmHeight());
                        if (stfWidth < minW) stfWidth = minW;
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
                    if (double.IsNaN(prefMin)) prefMin = 0;
                    if (double.IsNaN(preferred)) preferred = 0;
                    double stfWidth = Math.Min(Math.Max(prefMin, available), preferred);

                    if (MaxWidth != "none" && !string.IsNullOrEmpty(MaxWidth))
                    {
                        double maxW = CssValueParser.ParseLength(MaxWidth, width, GetEmHeight());
                        if (stfWidth > maxW) stfWidth = maxW;
                    }
                    if (MinWidth != "0" && !string.IsNullOrEmpty(MinWidth))
                    {
                        double minW = CssValueParser.ParseLength(MinWidth, width, GetEmHeight());
                        if (stfWidth < minW) stfWidth = minW;
                    }

                    stfWidth += ActualBorderLeftWidth + ActualBorderRightWidth
                              + ActualPaddingLeft + ActualPaddingRight;

                    Size = new SizeF((float)stfWidth, Size.Height);
                }
                else if (Width == CssConstants.Auto || string.IsNullOrEmpty(Width))
                {
                    // Margins reduce the box width only for auto-width elements.
                    // For explicit widths, margins affect position only (CSS1 box model).
                    Size = new SizeF((float)(width - ActualMarginLeft - ActualMarginRight), Size.Height);
                }
            }

            if (Display != CssConstants.TableCell)
            {
                var prevSibling = DomUtils.GetPreviousSibling(this);

                if (Position != CssConstants.Fixed)
                {
                    double left = ContainingBlock.Location.X + ContainingBlock.ActualPaddingLeft + ActualMarginLeft + ContainingBlock.ActualBorderLeftWidth;

                    // CSS2.1 §9.5: floats are out of normal flow. Non-floated
                    // blocks must be positioned as if preceding floats do not
                    // exist.  For cleared elements this also prevents margin
                    // collapsing with the float (CSS2.1 §8.3.1).
                    var flowPrev = prevSibling;
                    if (Float == CssConstants.None
                        && flowPrev != null && flowPrev.Float != CssConstants.None)
                    {
                        flowPrev = DomUtils.GetPreviousInFlowSibling(flowPrev);
                    }

                    // CSS2.1 §9.4.3: Relative positioning is visual-only.
                    // Use the flow-position bottom (before relative offset)
                    // when computing the next sibling's position.
                    double flowPrevBottom = flowPrev?.ActualBottom ?? 0;
                    if (flowPrev is CssBox flowPrevBox && flowPrevBox.Position == CssConstants.Relative)
                        flowPrevBottom -= GetRelativeOffsetY(flowPrevBox);

                    double top = (flowPrev == null && ParentBox != null ? ParentBox.ClientTop : ParentBox == null ? Location.Y : 0) + MarginTopCollapse(flowPrev) + flowPrevBottom;

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
                        var precedingFloats = CollectPrecedingFloatsInBfc(this);

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

                                if (maxBottom <= top) break;
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

                                if (maxBottom <= top) break;
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
                                    ? GetEffectiveMarginBottom(fpb)
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

                            // clearance = max(amount to clear float, amount to
                            // reach hypothetical position).  This can be negative.
                            double clearance = Math.Max(
                                maxFloatBottom - uncollapsedTop,
                                hypotheticalTop - uncollapsedTop);

                            top = uncollapsedTop + clearance;
                        }
                    }

                    Location = new PointF((float)left, (float)top);
                    ActualBottom = top;

                    // CSS2.1 §10.3.7 / §10.6.4: For absolutely positioned
                    // elements with explicit 'top'/'left', override the static
                    // position with the CSS-specified offset from the containing
                    // block's padding edge.
                    if (Position == CssConstants.Absolute)
                    {
                        var cb = FindPositionedContainingBlock();
                        // CSS2.1 §10.1: The containing block for an absolutely
                        // positioned element is the padding-box of the nearest
                        // positioned ancestor.
                        double cbPadLeft = cb.Location.X + cb.ActualBorderLeftWidth;
                        double cbPadTop = cb.Location.Y + cb.ActualBorderTopWidth;
                        double cbPadWidth = cb.Size.Width - cb.ActualBorderLeftWidth - cb.ActualBorderRightWidth;
                        double cbPadHeight = (cb.ActualBottom - cb.Location.Y) - cb.ActualBorderTopWidth - cb.ActualBorderBottomWidth;

                        float newX = Location.X, newY = Location.Y;

                        if (Left != null && Left != CssConstants.Auto)
                        {
                            double cssLeft = CssValueParser.ParseLength(Left, cbPadWidth, GetEmHeight());
                            newX = (float)(cbPadLeft + cssLeft + ActualMarginLeft);
                        }
                        else if (Right != null && Right != CssConstants.Auto)
                        {
                            // CSS2.1 §10.3.7: When left is auto and right is
                            // specified, position from the right padding edge.
                            double cssRight = CssValueParser.ParseLength(Right, cbPadWidth, GetEmHeight());
                            newX = (float)(cbPadLeft + cbPadWidth - cssRight - ActualMarginRight - Size.Width);
                        }

                        if (Top != null && Top != CssConstants.Auto)
                        {
                            double cssTop = CssValueParser.ParseLength(Top, cbPadHeight, GetEmHeight());
                            newY = (float)(cbPadTop + cssTop + ActualMarginTop);
                        }
                        else if (Bottom != null && Bottom != CssConstants.Auto)
                        {
                            // CSS2.1 §10.6.4: When top is auto and bottom is
                            // specified, position from the bottom padding edge.
                            double cssBottom = CssValueParser.ParseLength(Bottom, cbPadHeight, GetEmHeight());
                            double boxHeight = ActualBottom - Location.Y;
                            // boxHeight may be zero when the box position was
                            // just initialised and children have not yet been
                            // laid out.  Fall back to Size.Height which reflects
                            // any explicit CSS height already applied.
                            if (boxHeight <= 0) boxHeight = Size.Height;
                            newY = (float)(cbPadTop + cbPadHeight - cssBottom - ActualMarginBottom - boxHeight);
                        }

                        Location = new PointF(newX, newY);
                        ActualBottom = newY;
                    }
                }
                else
                {
                    // CSS2.1 §10.6.4: For fixed-position elements, 'top'/'left'
                    // specify the offset of the top/left margin edge from the
                    // viewport.  Location represents the border edge, so add
                    // the final computed margins (which may have been updated by
                    // later CSS rules such as the Acid2 'p + table + p' rule).
                    var basePos = GetActualLocation(Left, Top);
                    Location = new PointF(
                        basePos.X + (float)ActualMarginLeft,
                        basePos.Y + (float)ActualMarginTop);
                    ActualBottom = Location.Y;
                }
            }

            //If we're talking about a table here..
            if (Display == CssConstants.Table || Display == CssConstants.InlineTable)
            {
                CssLayoutEngineTable.PerformLayout(g, this);
            }
            else
            {
                //If there's just inline boxes, create LineBoxes
                if (DomUtils.ContainsInlinesOnly(this))
                {
                    ActualBottom = Location.Y;
                    CssLayoutEngine.CreateLineBoxes(g, this); //This will automatically set the bottom of this block
                }
                else if (Boxes.Count > 0)
                {
                    foreach (var childBox in Boxes)
                        childBox.PerformLayout(g);

                    ActualRight = CalculateActualRight();
                    ActualBottom = MarginBottomCollapse();
                }
            }
        }
        else
        {
            var prevSibling = DomUtils.GetPreviousSibling(this);
            if (prevSibling != null)
            {
                if (Location == PointF.Empty)
                    Location = prevSibling.Location;

                ActualBottom = prevSibling.ActualBottom;
            }
        }

        // CSS content-box model: 'height' specifies the content height only;
        // padding and border are additive (CSS2.1 §10.6.3).
        if (Height != CssConstants.Auto && !string.IsNullOrEmpty(Height))
        {
            // CSS2.1 §10.5: If height is a percentage and the containing
            // block's height is not explicitly specified (auto), the
            // percentage resolves to auto and this constraint is skipped.
            if (!HeightPercentageResolvesToAuto())
            {
                double borderBoxHeight = ActualHeight + ActualPaddingTop + ActualPaddingBottom + ActualBorderTopWidth + ActualBorderBottomWidth;

                // CSS2.1 §10.6.3: An explicit height sets the content box
                // height.  Content that exceeds this height overflows
                // (visible by default) but does not affect sibling
                // positioning.  Use direct assignment so that explicit
                // height (e.g. height:0) can override the height computed
                // by CreateLineBoxes (e.g. from line-height).
                ActualBottom = Location.Y + borderBoxHeight;
            }
        }

        // CSS2.1 §10.7: Apply min-height / max-height constraints.
        // When min-height > max-height, min-height wins.
        {
            double contentHeight = ActualBottom - Location.Y - ActualPaddingTop - ActualPaddingBottom - ActualBorderTopWidth - ActualBorderBottomWidth;
            bool constrained = false;

            if (MaxHeight != "none" && !string.IsNullOrEmpty(MaxHeight))
            {
                double maxH = CssValueParser.ParseLength(MaxHeight, ContainingBlock.Size.Height, GetEmHeight());
                if (contentHeight > maxH)
                {
                    contentHeight = maxH;
                    constrained = true;
                }
            }

            if (MinHeight != "0" && !string.IsNullOrEmpty(MinHeight))
            {
                double minH = CssValueParser.ParseLength(MinHeight, ContainingBlock.Size.Height, GetEmHeight());
                if (contentHeight < minH)
                {
                    contentHeight = minH;
                    constrained = true;
                }
            }

            if (constrained)
            {
                ActualBottom = Location.Y + contentHeight + ActualPaddingTop + ActualPaddingBottom + ActualBorderTopWidth + ActualBorderBottomWidth;
            }
        }

        // Floats with an explicit CSS height establish a new BFC.
        // Their ActualBottom should reflect the stated height, not
        // content overflow from child floats (CSS2.1 §10.6.1).
        // CSS2.1 §10.5: Percentage heights resolve to auto when
        // the containing block's height is not explicitly specified.
        if (Float != CssConstants.None && Height != CssConstants.Auto && !string.IsNullOrEmpty(Height))
        {
            if (!HeightPercentageResolvesToAuto())
            {
                double borderBoxHeight = ActualHeight + ActualPaddingTop + ActualPaddingBottom + ActualBorderTopWidth + ActualBorderBottomWidth;
                ActualBottom = Location.Y + borderBoxHeight;
            }
        }

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
                dx = CssValueParser.ParseLength(Left, Size.Width, GetEmHeight());
            else if (hasRight)
                dx = -CssValueParser.ParseLength(Right, Size.Width, GetEmHeight());

            if (hasTop)
                dy = CssValueParser.ParseLength(Top, Size.Height, GetEmHeight());
            else if (hasBottom)
                dy = -CssValueParser.ParseLength(Bottom, Size.Height, GetEmHeight());

            if (dx != 0)
                OffsetLeft(dx);
            if (dy != 0)
                OffsetTop(dy);
        }

        CreateListItemBox(g);

        if (!IsFixed)
        {
            var actualWidth = Math.Max(GetMinimumWidth() + CssBoxHelper.GetWidthMarginDeep(this), Size.Width < 90999 ? ActualRight - ContainerInt.RootLocation.X : 0);
            ContainerInt.ActualSize = CommonUtils.Max(ContainerInt.ActualSize, new SizeF((float)actualWidth, (float)(ActualBottom - ContainerInt.RootLocation.Y)));
        }
    }

    /// <summary>
    /// Loads the CSS background image if one is specified and not yet loaded.
    /// Called from <see cref="MeasureWordsSize"/> and overridden versions in
    /// subclasses (e.g. <see cref="CssBoxImage"/>) that replace the base
    /// measurement logic.
    /// </summary>
    protected void LoadBackgroundImageIfNeeded()
    {
        if (BackgroundImage != CssConstants.None && _imageLoadHandler == null)
        {
            _imageLoadHandler = ContainerInt.CreateImageLoadHandler(OnImageLoadComplete);

            // CSS background-image stores the value with a url() wrapper
            // (e.g. "url(data:image/png;base64,...)").  Strip the wrapper
            // so ImageLoadHandler.LoadImage can detect data: URIs via its
            // src.StartsWith("data:image") check (§14.2.1).
            var src = BackgroundImage;
            if (src.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && src.EndsWith(")"))
            {
                src = src.Substring(4, src.Length - 5).Trim();
                // Remove optional quotes around the URL
                if (src.Length >= 2 &&
                    ((src[0] == '\'' && src[^1] == '\'') ||
                     (src[0] == '"' && src[^1] == '"')))
                {
                    src = src[1..^1];
                }
            }

            _imageLoadHandler.LoadImage(src, HtmlTag != null ? HtmlTag.Attributes : null);
        }
    }

    internal virtual void MeasureWordsSize(RGraphics g)
    {
        if (_wordsSizeMeasured)
            return;

        LoadBackgroundImageIfNeeded();

        MeasureWordSpacing(g);

        if (Words.Count > 0)
        {
            foreach (var boxWord in Words)
            {
                boxWord.Width = boxWord.Text != "\n" ? g.MeasureString(boxWord.Text, ActualFont).Width : 0;
                boxWord.Height = ActualFont.Height;
            }
        }

        _wordsSizeMeasured = true;
    }

    /// <summary>
    /// Recursively calls <see cref="MeasureWordsSize"/> on all descendant
    /// boxes so that <c>ActualWordSpacing</c> and word dimensions are
    /// computed before <see cref="GetMinMaxWidth"/> is invoked for
    /// shrink-to-fit width (CSS2.1 §10.3.7).
    /// Note: the current box (<c>this</c>) is already measured by the
    /// <see cref="MeasureWordsSize"/> call at the start of
    /// <see cref="PerformLayoutImp"/>; only descendants need measuring.
    /// </summary>
    private void EnsureDescendantWordsMeasured(RGraphics g)
    {
        var stack = new Stack<CssBox>();
        foreach (var child in Boxes)
            stack.Push(child);

        while (stack.Count > 0)
        {
            var box = stack.Pop();
            box.MeasureWordsSize(g);
            foreach (var child in box.Boxes)
                stack.Push(child);
        }
    }

    protected override sealed CssBoxProperties GetParent() => _parentBox;

    private int GetIndexForList()
    {
        // Phase 2: Read list attributes from CssBoxProperties instead of GetAttribute().
        bool reversed = ParentBox.ListReversed;

        int index;
        if (ParentBox.ListStart.HasValue)
        {
            index = ParentBox.ListStart.Value;
        }
        else if (reversed)
        {
            index = 0;
            foreach (CssBox b in ParentBox.Boxes)
            {
                if (b.Display == CssConstants.ListItem)
                    index++;
            }
        }
        else
        {
            index = 1;
        }

        foreach (CssBox b in ParentBox.Boxes)
        {
            if (b.Equals(this))
                return index;

            if (b.Display == CssConstants.ListItem)
                index += reversed ? -1 : 1;
        }

        return index;
    }

    private void CreateListItemBox(RGraphics g)
    {
        if (Display != CssConstants.ListItem || ListStyleType == CssConstants.None)
            return;

        if (_listItemBox == null)
        {
            _listItemBox = new CssBox(null, null);
            _listItemBox.InheritStyle(this);
            _listItemBox.Display = CssConstants.Inline;
            _listItemBox._htmlContainer = ContainerInt;

            if (ListStyleType.Equals(CssConstants.Disc, StringComparison.InvariantCultureIgnoreCase))
            {
                _listItemBox.Text = "•".AsMemory();
            }
            else if (ListStyleType.Equals(CssConstants.Circle, StringComparison.InvariantCultureIgnoreCase))
            {
                _listItemBox.Text = "o".AsMemory();
            }
            else if (ListStyleType.Equals(CssConstants.Square, StringComparison.InvariantCultureIgnoreCase))
            {
                _listItemBox.Text = "♠".AsMemory();
            }
            else if (ListStyleType.Equals(CssConstants.Decimal, StringComparison.InvariantCultureIgnoreCase))
            {
                _listItemBox.Text = (GetIndexForList().ToString(CultureInfo.InvariantCulture) + ".").AsMemory();
            }
            else if (ListStyleType.Equals(CssConstants.DecimalLeadingZero, StringComparison.InvariantCultureIgnoreCase))
            {
                _listItemBox.Text = (GetIndexForList().ToString("00", CultureInfo.InvariantCulture) + ".").AsMemory();
            }
            else
            {
                _listItemBox.Text = (CommonUtils.ConvertToAlphaNumber(GetIndexForList(), ListStyleType) + ".").AsMemory();
            }

            _listItemBox.ParseToWords();

            _listItemBox.PerformLayoutImp(g);
            _listItemBox.Size = new SizeF((float)_listItemBox.Words[0].Width, (float)_listItemBox.Words[0].Height);
        }

        _listItemBox.Words[0].Left = Location.X - _listItemBox.Size.Width - 5;
        _listItemBox.Words[0].Top = Location.Y + ActualPaddingTop; // +FontAscent;
    }

    internal string GetAttribute(string attribute) => GetAttribute(attribute, string.Empty);
    internal string GetAttribute(string attribute, string defaultValue) => HtmlTag != null ? HtmlTag.TryGetAttribute(attribute, defaultValue) : defaultValue;

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

    internal void GetMinMaxWidth(out double minWidth, out double maxWidth)
    {
        double min = 0f;
        double maxSum = 0f;
        double paddingSum = 0f;
        double marginSum = 0f;

        CssBoxHelper.GetMinMaxSumWords(this, ref min, ref maxSum, ref paddingSum, ref marginSum);

        maxWidth = paddingSum + maxSum;
        minWidth = paddingSum + (min < 90999 ? min : 0);
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
        double maxLineWidth = 0;

        foreach (var child in Boxes)
        {
            double childWidth;

            if (child.Width != CssConstants.Auto && !string.IsNullOrEmpty(child.Width))
            {
                // Explicit width: use declared width + borders/padding
                double containingBlockWidth = Size.Width > 0 && !double.IsNaN(Size.Width) ? Size.Width : 0;
                childWidth = CssValueParser.ParseLength(child.Width, containingBlockWidth, child.GetEmHeight())
                           + child.ActualBorderLeftWidth + child.ActualBorderRightWidth
                           + child.ActualPaddingLeft + child.ActualPaddingRight;
            }
            else
            {
                // Auto-width child: compute its intrinsic preferred width.
                // Guard against NaN from unmeasured words in deeply nested
                // inline elements (e.g. Acid2 .eyes → #eyes-a → <object>).
                child.GetMinMaxWidth(out _, out double childMax);
                childWidth = double.IsNaN(childMax) ? 0 : childMax;
            }

            childWidth += child.ActualMarginLeft + child.ActualMarginRight;
            if (!double.IsNaN(childWidth))
                maxLineWidth = Math.Max(maxLineWidth, childWidth);
        }

        return maxLineWidth;
    }

    internal bool HasJustInlineSiblings() => ParentBox != null && DomUtils.ContainsInlinesOnly(ParentBox);

    internal new void InheritStyle(CssBox box = null, bool everything = false) => base.InheritStyle(box ?? ParentBox, everything);

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
            if (prevSibling is CssBox prevBox && IsEmptyCollapsible(prevBox))
            {
                double maxPos = Math.Max(ActualMarginTop, 0);
                double maxNeg = Math.Min(ActualMarginTop, 0);
                CollectEmptyBoxMargins(prevBox, ref maxPos, ref maxNeg);
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
                    ? GetPropagatedMarginBottom(prevSibBox)
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
        else if (_parentBox != null && ActualPaddingTop < 0.1 && ActualPaddingBottom < 0.1 && _parentBox.ActualPaddingTop < 0.1 && _parentBox.ActualPaddingBottom < 0.1 && _parentBox.ActualBorderTopWidth < 0.1 && _parentBox.ActualBorderBottomWidth < 0.1)
        {
            value = Math.Max(0, ActualMarginTop - Math.Max(_parentBox.ActualMarginTop, _parentBox.CollapsedMarginTop));
        }
        else
        {
            value = ActualMarginTop;
        }

        // fix for hr tag
        if (value < 0.1 && HtmlTag != null && HtmlTag.Name == "hr")
            value = GetEmHeight() * 1.1f;

        return value;
    }

    public bool BreakPage()
    {
        var container = ContainerInt;

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

        if (ParentBox != null && ParentBox.Boxes.IndexOf(this) == ParentBox.Boxes.Count - 1 && _parentBox.ActualMarginBottom < 0.1)
        {
            var lastChildBottomMargin = Boxes[Boxes.Count - 1].ActualMarginBottom;
            margin = Height == "auto" ? Math.Max(ActualMarginBottom, lastChildBottomMargin) : lastChildBottomMargin;
        }

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
            || Position == CssConstants.Fixed;

        // Use the maximum ActualBottom across all children to handle
        // floated children that may not be the last in source order.
        // Initialize to the content-area top so that padding is preserved
        // even when all children are floated (CSS2.1 §10.6.3: content
        // height is zero but padding is additive).
        double maxChildBottom = Location.Y + ActualBorderTopWidth + ActualPaddingTop;
        
        foreach (var child in Boxes)
        {
            if (!isBfc && child.Float != CssConstants.None)
            {
                continue;
            }

            // CSS2.1 §9.4.3: Relative positioning is visual-only and
            // does not affect the flow position used for auto-height
            // calculation.  Undo the relative offset so the parent
            // measures the child's normal-flow bottom.
            double childBottom = child.ActualBottom;
            if (child.Position == CssConstants.Relative)
                childBottom -= GetRelativeOffsetY(child);

            maxChildBottom = Math.Max(maxChildBottom, childBottom);
        }

        return Math.Max(ActualBottom, maxChildBottom + margin + ActualPaddingBottom + ActualBorderBottomWidth);
    }

    /// <summary>
    /// Computes the vertical offset applied by <c>position: relative</c>.
    /// CSS2.1 §9.4.3: <c>top</c> takes precedence over <c>bottom</c>.
    /// Returns 0 if the element is not relatively positioned or has no offset.
    /// </summary>
    private static double GetRelativeOffsetY(CssBoxProperties box)
    {
        bool hasTop = box.Top != null && box.Top != CssConstants.Auto;
        bool hasBottom = box.Bottom != null && box.Bottom != CssConstants.Auto;

        if (hasTop)
            return CssValueParser.ParseLength(box.Top, box.Size.Height, box.GetEmHeight());
        if (hasBottom)
            return -CssValueParser.ParseLength(box.Bottom, box.Size.Height, box.GetEmHeight());
        return 0;
    }

    /// <summary>
    /// Collects all float boxes in the same block formatting context that
    /// precede <paramref name="box"/> in the DOM tree. This includes floats
    /// nested inside non-BFC siblings (e.g., floated <c>li</c> elements
    /// inside a non-floated <c>ul</c>) and floats that are siblings of
    /// ancestor elements when those ancestors do not establish a new BFC
    /// (CSS2.1 §9.4.1).
    /// </summary>
    private static List<CssBox> CollectPrecedingFloatsInBfc(CssBox box)
    {
        var result = new List<CssBox>();
        if (box.ParentBox == null) return result;

        // Collect preceding sibling floats (and their non-BFC subtrees).
        foreach (var sibling in box.ParentBox.Boxes)
        {
            if (sibling == box) break;
            CollectFloatsInSubtree(sibling, result);
        }

        // Walk up ancestor chain: collect floats from each ancestor's
        // preceding siblings while the ancestor does not establish a BFC.
        var current = box.ParentBox;
        while (current != null && current.ParentBox != null)
        {
            if (EstablishesBfc(current))
                break;

            foreach (var sibling in current.ParentBox.Boxes)
            {
                if (sibling == current) break;
                CollectFloatsInSubtree(sibling, result);
            }

            current = current.ParentBox;
        }

        return result;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="box"/> establishes a new
    /// block formatting context (CSS2.1 §9.4.1).
    /// </summary>
    private static bool EstablishesBfc(CssBox box)
    {
        return box.Float != CssConstants.None
            || box.Display == CssConstants.InlineBlock
            || box.Display == CssConstants.TableCell
            || box.Position == CssConstants.Absolute
            || box.Position == CssConstants.Fixed
            || (box.Overflow != null && box.Overflow != CssConstants.Visible);
    }

    private static void CollectFloatsInSubtree(CssBox root, List<CssBox> result)
    {
        if (root.Float != CssConstants.None && root.Display != CssConstants.None)
        {
            result.Add(root);
            // Float establishes a new BFC – don't recurse into descendants.
            return;
        }

        foreach (var child in root.Boxes)
            CollectFloatsInSubtree(child, result);
    }

    internal void OffsetTop(double amount)
    {
        List<CssLineBox> lines = [.. Rectangles.Keys];

        foreach (CssLineBox line in lines)
        {
            RectangleF r = Rectangles[line];
            Rectangles[line] = new RectangleF(r.X, (float)(r.Y + amount), r.Width, r.Height);
        }

        foreach (CssRect word in Words)
            word.Top += amount;

        foreach (CssBox b in Boxes)
        {
            // CSS2.1 §9.6.1: position:fixed elements are positioned relative
            // to the viewport and must not be shifted by ancestor offsets
            // (e.g. a parent's position:relative visual offset).
            if (b.Position != CssConstants.Fixed)
                b.OffsetTop(amount);
        }

        _listItemBox?.OffsetTop(amount);

        Location = new PointF(Location.X, (float)(Location.Y + amount));
    }

    internal void OffsetLeft(double amount)
    {
        List<CssLineBox> lines = [.. Rectangles.Keys];

        foreach (CssLineBox line in lines)
        {
            RectangleF r = Rectangles[line];
            Rectangles[line] = new RectangleF((float)(r.X + amount), r.Y, r.Width, r.Height);
        }

        foreach (CssRect word in Words)
            word.Left += amount;

        foreach (CssBox b in Boxes)
        {
            // CSS2.1 §9.6.1: position:fixed elements are positioned relative
            // to the viewport and must not be shifted by ancestor offsets.
            if (b.Position != CssConstants.Fixed)
                b.OffsetLeft(amount);
        }

        _listItemBox?.OffsetLeft(amount);

        Location = new PointF((float)(Location.X + amount), Location.Y);
    }

    internal void OffsetRectangle(CssLineBox lineBox, double gap)
    {
        if (Rectangles.TryGetValue(lineBox, out RectangleF r))
            Rectangles[lineBox] = new RectangleF(r.X, (float)(r.Y + gap), r.Width, r.Height);
    }

    internal void RectanglesReset() => Rectangles.Clear();

    private void OnImageLoadComplete(RImage image, RectangleF rectangle, bool async)
    {
        if (image != null && async)
            ContainerInt.RequestRefresh(false);
    }

    protected override RFont GetCachedFont(string fontFamily, double fsize, FontStyle st) => ContainerInt.GetFont(fontFamily, fsize, st);

    protected override Color GetActualColor(string colorStr) => ContainerInt.ParseColor(colorStr);

    protected override PointF GetActualLocation(string X, string Y)
    {
        var left = CssValueParser.ParseLength(X, ContainerInt.PageSize.Width, GetEmHeight(), null);
        var top = CssValueParser.ParseLength(Y, ContainerInt.PageSize.Height, GetEmHeight(), null);

        return new PointF((float)left, (float)top);
    }

    /// <summary>
    /// CSS2.1 §8.3.1: Returns <c>true</c> if a box is "empty" — its own
    /// top and bottom margins are adjoining and collapse through.
    /// Conditions: min-height is zero, no top/bottom borders or padding,
    /// height is 0 or auto (or percentage that resolves to auto), no line
    /// boxes, and all in-flow children's margins also collapse.
    /// </summary>
    private static bool IsEmptyCollapsible(CssBox box)
    {
        if (box.ActualBorderTopWidth > 0.1 || box.ActualBorderBottomWidth > 0.1)
            return false;
        if (box.ActualPaddingTop > 0.1 || box.ActualPaddingBottom > 0.1)
            return false;

        // Check if height resolves to zero/auto
        if (box.Height != CssConstants.Auto && !string.IsNullOrEmpty(box.Height))
        {
            bool resolvedToAuto = box.Height.Contains('%')
                && (box.ContainingBlock.Height == CssConstants.Auto
                    || string.IsNullOrEmpty(box.ContainingBlock.Height));
            if (!resolvedToAuto)
            {
                double h = CssValueParser.ParseLength(box.Height, box.Size.Height, box.GetEmHeight());
                if (h > 0.1)
                    return false;
            }
        }

        // Zero content height — ActualBottom should equal Location.Y
        // (tolerance 0.5 accounts for sub-pixel rounding in layout)
        if (Math.Abs(box.ActualBottom - box.Location.Y) > 0.5)
            return false;

        // Must not contain any line boxes (inline content)
        if (box.LineBoxes.Count > 0)
            return false;

        return true;
    }

    /// <summary>
    /// Collects the maximum positive and minimum negative margins from an
    /// empty collapsible box and all its in-flow children (recursively for
    /// children that are also empty and collapsible).
    /// </summary>
    private static void CollectEmptyBoxMargins(CssBox box, ref double maxPos, ref double maxNeg)
    {
        maxPos = Math.Max(maxPos, Math.Max(box.ActualMarginTop, 0));
        maxPos = Math.Max(maxPos, Math.Max(box.ActualMarginBottom, 0));
        maxNeg = Math.Min(maxNeg, Math.Min(box.ActualMarginTop, 0));
        maxNeg = Math.Min(maxNeg, Math.Min(box.ActualMarginBottom, 0));

        foreach (var child in box.Boxes)
        {
            if (child.Float != CssConstants.None
                || child.Position == CssConstants.Absolute
                || child.Position == CssConstants.Fixed)
                continue;

            maxPos = Math.Max(maxPos, Math.Max(child.ActualMarginTop, 0));
            maxPos = Math.Max(maxPos, Math.Max(child.ActualMarginBottom, 0));
            maxNeg = Math.Min(maxNeg, Math.Min(child.ActualMarginTop, 0));
            maxNeg = Math.Min(maxNeg, Math.Min(child.ActualMarginBottom, 0));

            if (IsEmptyCollapsible(child))
                CollectEmptyBoxMargins(child, ref maxPos, ref maxNeg);
        }
    }

    /// <summary>
    /// Returns the effective bottom margin for a box, accounting for margins
    /// that collapse through the box when it is "empty" per CSS2.1 §8.3.1.
    /// For non-empty boxes returns <see cref="CssBoxProperties.ActualMarginBottom"/>.
    /// </summary>
    private static double GetEffectiveMarginBottom(CssBox box)
    {
        if (!IsEmptyCollapsible(box))
            return box.ActualMarginBottom;

        double maxPos = 0, maxNeg = 0;
        CollectEmptyBoxMargins(box, ref maxPos, ref maxNeg);
        double collapsed = maxPos + maxNeg;
        return collapsed - box.CollapsedMarginTop;
    }

    /// <summary>
    /// Returns the effective bottom margin for a box, accounting for
    /// parent-child bottom-margin collapse (CSS 2.1 §8.3.1).
    /// When a box has no bottom border, no bottom padding, and auto height,
    /// the last in-flow block-level child's bottom margin collapses with
    /// the box's own bottom margin.  This is applied recursively.
    /// </summary>
    private static double GetPropagatedMarginBottom(CssBox box)
    {
        double mb = box.ActualMarginBottom;

        if (box.ActualBorderBottomWidth > 0.1 || box.ActualPaddingBottom > 0.1)
            return mb;

        if (box.Height != CssConstants.Auto && !string.IsNullOrEmpty(box.Height))
        {
            bool resolvedToAuto = box.Height.Contains('%')
                && (box.ContainingBlock.Height == CssConstants.Auto
                    || string.IsNullOrEmpty(box.ContainingBlock.Height));
            if (!resolvedToAuto)
                return mb;
        }

        // Find last in-flow block-level child (CSS 2.1 §8.3.1).
        CssBox? lastInFlow = null;
        foreach (var child in box.Boxes)
        {
            if (child.Float != CssConstants.None
                || child.Position == CssConstants.Absolute
                || child.Position == CssConstants.Fixed)
                continue;
            if (child.Display == CssConstants.Inline
                || child.Display == CssConstants.InlineBlock)
                continue;
            lastInFlow = child;
        }

        if (lastInFlow == null)
            return mb;

        double childMb = GetPropagatedMarginBottom(lastInFlow);

        // Collapse: max(positives,0) + min(negatives,0)
        double maxPos = Math.Max(Math.Max(mb, 0), Math.Max(childMb, 0));
        double minNeg = Math.Min(Math.Min(mb, 0), Math.Min(childMb, 0));
        return maxPos + minNeg;
    }

    public override string ToString()
    {
        var tag = HtmlTag != null ? $"<{HtmlTag.Name}>" : "anon";

        if (IsBlock)
        {
            return $"{(ParentBox == null ? "Root: " : string.Empty)}{tag} Block {FontSize}, Children:{Boxes.Count}";
        }
        else if (Display == CssConstants.None)
        {
            return $"{(ParentBox == null ? "Root: " : string.Empty)}{tag} None";
        }
        else
        {
            return $"{(ParentBox == null ? "Root: " : string.Empty)}{tag} {Display}: {Text}";
        }
    }
}