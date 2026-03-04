using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using TheArtOfDev.HtmlRenderer.Core.Parse;
using TheArtOfDev.HtmlRenderer.Core.Utils;

namespace TheArtOfDev.HtmlRenderer.Core.Dom;

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
                        Words.Add(new CssRectWord(this, HtmlUtils.DecodeHtml(_text.Slice(startIdx, endIdx - startIdx).ToString()), false, false));
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

                // Margins reduce the box width only for auto-width elements.
                // For explicit widths, margins affect position only (CSS1 box model).
                if (Width == CssConstants.Auto || string.IsNullOrEmpty(Width))
                {
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

                    double top = (flowPrev == null && ParentBox != null ? ParentBox.ClientTop : ParentBox == null ? Location.Y : 0) + MarginTopCollapse(flowPrev) + (flowPrev != null ? flowPrev.ActualBottom + flowPrev.ActualBorderBottomWidth : 0);

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
                                        double fBottom = floatBox.ActualBottom + floatBox.ActualBorderBottomWidth;
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
                                        double fBottom = floatBox.ActualBottom + floatBox.ActualBorderBottomWidth;
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
                                    double fBottom = floatBox.ActualBottom + floatBox.ActualBorderBottomWidth;
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
                                        double fBottom = floatBox.ActualBottom + floatBox.ActualBorderBottomWidth;
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
                                        double fBottom = floatBox.ActualBottom + floatBox.ActualBorderBottomWidth;
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
                                    double fBottom = floatBox.ActualBottom + floatBox.ActualBorderBottomWidth;
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
                            double uncollapsedTop;
                            if (flowPrev != null)
                            {
                                uncollapsedTop = flowPrev.ActualBottom
                                    + flowPrev.ActualBorderBottomWidth
                                    + flowPrev.ActualMarginBottom
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
            bool resolveToAuto = Height.Contains('%')
                && (ContainingBlock.Height == CssConstants.Auto
                    || string.IsNullOrEmpty(ContainingBlock.Height));

            if (!resolveToAuto)
            {
                double borderBoxHeight = ActualHeight + ActualPaddingTop + ActualPaddingBottom + ActualBorderTopWidth + ActualBorderBottomWidth;
                ActualBottom = Math.Max(ActualBottom, Location.Y + borderBoxHeight);
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
        if (Float != CssConstants.None && Height != CssConstants.Auto && !string.IsNullOrEmpty(Height))
        {
            double borderBoxHeight = ActualHeight + ActualPaddingTop + ActualPaddingBottom + ActualBorderTopWidth + ActualBorderBottomWidth;
            ActualBottom = Location.Y + borderBoxHeight;
        }

        // Apply position:relative offset after layout (visual only, does not affect flow)
        // CSS2.1 §9.4.3: For relative positioning, 'left'/'right' and
        // 'top'/'bottom' form constraint pairs.  When 'top' is auto and
        // 'bottom' is not, dy = -bottom.  When both are non-auto, 'bottom'
        // is ignored (in LTR).  Same logic applies to left/right.
        if (Position == CssConstants.Relative)
        {
            double dx = 0, dy = 0;

            bool hasLeft  = Left  != null && Left  != CssConstants.Auto;
            bool hasRight = Right != null && Right != CssConstants.Auto;
            bool hasTop   = Top   != null && Top   != CssConstants.Auto;
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

    internal virtual void MeasureWordsSize(RGraphics g)
    {
        if (_wordsSizeMeasured)
            return;

        if (BackgroundImage != CssConstants.None && _imageLoadHandler == null)
        {
            _imageLoadHandler = ContainerInt.CreateImageLoadHandler(OnImageLoadComplete);
            _imageLoadHandler.LoadImage(BackgroundImage, HtmlTag != null ? HtmlTag.Attributes : null);
        }

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

    internal bool HasJustInlineSiblings() => ParentBox != null && DomUtils.ContainsInlinesOnly(ParentBox);

    internal new void InheritStyle(CssBox box = null, bool everything = false) => base.InheritStyle(box ?? ParentBox, everything);

    protected double MarginTopCollapse(CssBoxProperties prevSibling)
    {
        double value;

        if (prevSibling != null)
        {
            value = Math.Max(prevSibling.ActualMarginBottom, ActualMarginTop);
            CollapsedMarginTop = value;
        }
        else if (_parentBox != null && ActualPaddingTop < 0.1 && ActualPaddingBottom < 0.1 && _parentBox.ActualPaddingTop < 0.1 && _parentBox.ActualPaddingBottom < 0.1)
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
            || (Overflow != null && Overflow != CssConstants.Visible);

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

            maxChildBottom = Math.Max(maxChildBottom, child.ActualBottom + child.ActualBorderBottomWidth);
        }

        return Math.Max(ActualBottom, maxChildBottom + margin + ActualPaddingBottom + ActualBorderBottomWidth);
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
            b.OffsetTop(amount);

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
            b.OffsetLeft(amount);

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