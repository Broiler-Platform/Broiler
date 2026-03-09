using System;
using System.Collections.Generic;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Core.Utils;
using System.Drawing;

namespace TheArtOfDev.HtmlRenderer.Core.Dom;

internal static class CssLayoutEngine
{
    public static void MeasureImageSize(CssRectImage imageWord)
    {
        ArgumentNullException.ThrowIfNull(imageWord);
        ArgumentNullException.ThrowIfNull(imageWord.OwnerBox);

        var width = new CssLength(imageWord.OwnerBox.Width);
        var height = new CssLength(imageWord.OwnerBox.Height);

        bool hasImageTagWidth = width.Number > 0 && width.Unit == CssUnit.Pixels;
        bool hasImageTagHeight = height.Number > 0 && height.Unit == CssUnit.Pixels;
        bool scaleImageHeight = false;

        if (hasImageTagWidth)
        {
            imageWord.Width = width.Number;
        }
        else if (width.Number > 0 && width.IsPercentage)
        {
            imageWord.Width = width.Number * imageWord.OwnerBox.ContainingBlock.Size.Width;
            scaleImageHeight = true;
        }
        else if (imageWord.Image != null)
        {
            imageWord.Width = imageWord.ImageRectangle == RectangleF.Empty ? imageWord.Image.Width : imageWord.ImageRectangle.Width;
        }
        else
        {
            imageWord.Width = hasImageTagHeight ? height.Number / 1.14f : 20;
        }

        var maxWidth = new CssLength(imageWord.OwnerBox.MaxWidth);
        if (maxWidth.Number > 0)
        {
            double maxWidthVal = -1;
            if (maxWidth.Unit == CssUnit.Pixels)
            {
                maxWidthVal = maxWidth.Number;
            }
            else if (maxWidth.IsPercentage)
            {
                maxWidthVal = maxWidth.Number * imageWord.OwnerBox.ContainingBlock.Size.Width;
            }

            if (maxWidthVal > -1 && imageWord.Width > maxWidthVal)
            {
                imageWord.Width = maxWidthVal;
                scaleImageHeight = !hasImageTagHeight;
            }
        }

        if (hasImageTagHeight)
        {
            imageWord.Height = height.Number;
        }
        else if (imageWord.Image != null)
        {
            imageWord.Height = imageWord.ImageRectangle == RectangleF.Empty ? imageWord.Image.Height : imageWord.ImageRectangle.Height;
        }
        else
        {
            imageWord.Height = imageWord.Width > 0 ? imageWord.Width * 1.14f : 22.8f;
        }

        if (imageWord.Image != null)
        {
            // If only the width was set in the html tag, ratio the height.
            if ((hasImageTagWidth && !hasImageTagHeight) || scaleImageHeight)
            {
                // Divide the given tag width with the actual image width, to get the ratio.
                double ratio = imageWord.Width / imageWord.Image.Width;
                imageWord.Height = imageWord.Image.Height * ratio;
            }
            // If only the height was set in the html tag, ratio the width.
            else if (hasImageTagHeight && !hasImageTagWidth)
            {
                // Divide the given tag height with the actual image height, to get the ratio.
                double ratio = imageWord.Height / imageWord.Image.Height;
                imageWord.Width = imageWord.Image.Width * ratio;
            }
        }

        imageWord.Height += imageWord.OwnerBox.ActualBorderBottomWidth + imageWord.OwnerBox.ActualBorderTopWidth + imageWord.OwnerBox.ActualPaddingTop + imageWord.OwnerBox.ActualPaddingBottom;
    }

    public static void CreateLineBoxes(RGraphics g, CssBox blockBox)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(blockBox);

        blockBox.LineBoxes.Clear();

        double limitRight = blockBox.ActualRight - blockBox.ActualPaddingRight - blockBox.ActualBorderRightWidth;

        //Get the start x and y of the blockBox
        double startx = blockBox.Location.X + blockBox.ActualPaddingLeft - 0 + blockBox.ActualBorderLeftWidth;
        double starty = blockBox.Location.Y + blockBox.ActualPaddingTop - 0 + blockBox.ActualBorderTopWidth;
        double curx = startx + blockBox.ActualTextIndent;
        double cury = starty;

        //Reminds the maximum bottom reached
        double maxRight = startx;
        double maxBottom = starty;

        //First line box
        CssLineBox line = new(blockBox);

        //Flow words and boxes
        FlowBox(g, blockBox, blockBox, limitRight, 0, startx, ref line, ref curx, ref cury, ref maxRight, ref maxBottom);

        // if width is not restricted we need to lower it to the actual width
        if (blockBox.ActualRight >= 90999)
        {
            blockBox.ActualRight = maxRight + blockBox.ActualPaddingRight + blockBox.ActualBorderRightWidth;
        }

        //Gets the rectangles for each line-box
        foreach (var linebox in blockBox.LineBoxes)
        {
            ApplyHorizontalAlignment(g, linebox);
            ApplyRightToLeft(blockBox, linebox);
            BubbleRectangles(blockBox, linebox);
            ApplyVerticalAlignment(g, linebox);
            linebox.AssignRectanglesToBoxes();
        }

        // CSS2.1 §10.8: The "strut" — each line box starts with an
        // imaginary zero-width inline box with the block container's font
        // and line-height properties.  This establishes the minimum line
        // box height for inline formatting contexts.
        // The strut only affects content height when height is 'auto';
        // an explicit height (CSS2.1 §10.6.3) overrides the content height.
        bool hasExplicitHeight = blockBox.Height != null && blockBox.Height != CssConstants.Auto;
        if (blockBox.ActualLineHeight > 0 && !hasExplicitHeight)
            maxBottom = Math.Max(maxBottom, starty + blockBox.ActualLineHeight);

        blockBox.ActualBottom = maxBottom + blockBox.ActualPaddingBottom + blockBox.ActualBorderBottomWidth;

        // CSS2.1 §10.6.3: When height is not 'auto', the used value is the
        // specified value.  Content may overflow (controlled by 'overflow').
        if (hasExplicitHeight && blockBox.Overflow == CssConstants.Hidden && blockBox.ActualBottom - blockBox.Location.Y > blockBox.ActualHeight)
            blockBox.ActualBottom = blockBox.Location.Y + blockBox.ActualHeight;
    }

    public static void ApplyCellVerticalAlignment(RGraphics g, CssBox cell)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(cell);

        if (cell.VerticalAlign == CssConstants.Top || cell.VerticalAlign == CssConstants.Baseline)
            return;

        double cellbot = cell.ClientBottom;
        double bottom = CssBoxHelper.GetMaximumBottom(cell, 0f);
        double dist = 0f;

        if (cell.VerticalAlign == CssConstants.Bottom)
        {
            dist = cellbot - bottom;
        }
        else if (cell.VerticalAlign == CssConstants.Middle)
        {
            dist = (cellbot - bottom) / 2;
        }

        foreach (CssBox b in cell.Boxes)
        {
            b.OffsetTop(dist);
        }

        //float top = cell.ClientTop;
        //float bottom = cell.ClientBottom;
        //bool middle = cell.VerticalAlign == CssConstants.Middle;

        //foreach (LineBox line in cell.LineBoxes)
        //{
        //    for (int i = 0; i < line.RelatedBoxes.Count; i++)
        //    {

        //        double diff = bottom - line.RelatedBoxes[i].Rectangles[line].Bottom;
        //        if (middle) diff /= 2f;
        //        RectangleF r = line.RelatedBoxes[i].Rectangles[line];
        //        line.RelatedBoxes[i].Rectangles[line] = new RectangleF(r.X, r.Y + diff, r.Width, r.Height);

        //    }

        //    foreach (BoxWord word in line.Words)
        //    {
        //        double gap = word.Top - top;
        //        word.Top = bottom - gap - word.Height;
        //    }
        //}
    }


    private static void FlowBox(RGraphics g, CssBox blockbox, CssBox box, double limitRight, double linespacing, double startx, ref CssLineBox line, ref double curx, ref double cury, ref double maxRight, ref double maxbottom)
    {
        var startX = curx;
        var startY = cury;
        box.FirstHostingLineBox = line;
        var localCurx = curx;
        var localMaxRight = maxRight;
        var localmaxbottom = maxbottom;

        foreach (CssBox b in box.Boxes)
        {
            double leftspacing = (b.Position != CssConstants.Absolute && b.Position != CssConstants.Fixed) ? b.ActualMarginLeft + b.ActualBorderLeftWidth + b.ActualPaddingLeft : 0;
            double rightspacing = (b.Position != CssConstants.Absolute && b.Position != CssConstants.Fixed) ? b.ActualMarginRight + b.ActualBorderRightWidth + b.ActualPaddingRight : 0;

            b.RectanglesReset();
            b.MeasureWordsSize(g);

            curx += leftspacing;

            if (b.Words.Count > 0)
            {
                bool wrapNoWrapBox = false;
                if (b.WhiteSpace == CssConstants.NoWrap && curx > startx)
                {
                    var boxRight = curx;
                    foreach (var word in b.Words)
                        boxRight += word.FullWidth;

                    if (boxRight > limitRight)
                        wrapNoWrapBox = true;
                }

                if (DomUtils.IsBoxHasWhitespace(b))
                    curx += box.ActualWordSpacing;

                foreach (var word in b.Words)
                {
                    if (maxbottom - cury < box.ActualLineHeight)
                        maxbottom += box.ActualLineHeight - (maxbottom - cury);

                    // CSS2.1 §10.8: The "strut" — each line box has a minimum
                    // height from the block container's font and line-height.
                    // For replaced inline elements (images), apply the block
                    // container's strut so that baseline alignment pushes the
                    // image down when the font is larger than the image.
                    double strutHeight = 0;
                    if (word.IsImage)
                    {
                        strutHeight = blockbox.ActualLineHeight;
                        if (strutHeight <= 0)
                            strutHeight = blockbox.ActualFont.Height;

                        if (maxbottom - cury < strutHeight)
                            maxbottom += strutHeight - (maxbottom - cury);
                    }

                    if ((b.WhiteSpace != CssConstants.NoWrap && b.WhiteSpace != CssConstants.Pre && curx + word.Width + rightspacing > limitRight
                         && (b.WhiteSpace != CssConstants.PreWrap || !word.IsSpaces)) || word.IsLineBreak || wrapNoWrapBox)
                    {
                        wrapNoWrapBox = false;
                        curx = startx;

                        // handle if line is wrapped for the first text element where parent has left margin\padding
                        if (b == box.Boxes[0] && !word.IsLineBreak && (word == b.Words[0] || (box.ParentBox != null && box.ParentBox.IsBlock)))
                            curx += box.ActualMarginLeft + box.ActualBorderLeftWidth + box.ActualPaddingLeft;

                        cury = maxbottom + linespacing;

                        line = new CssLineBox(blockbox);

                        if (word.IsImage || word.Equals(b.FirstWord))
                            curx += leftspacing;
                    }

                    line.ReportExistanceOf(word);

                    word.Left = curx;

                    // CSS2.1 §10.8.1: Replaced inline elements (images) are
                    // baseline-aligned by default — the bottom of the replaced
                    // element sits on the baseline.  The baseline position
                    // within the strut is at the font's ascent from the top.
                    if (word.IsImage && strutHeight > word.Height)
                    {
                        double fontHeight = blockbox.ActualFont.Height;
                        // Approximate ascent ≈ 80% of font height (typical for
                        // Latin fonts). The image bottom sits on the baseline.
                        double baseline = fontHeight * 0.8;
                        double imageTop = cury + baseline - word.Height;
                        if (imageTop > cury)
                            word.Top = imageTop;
                        else
                            word.Top = cury;
                    }
                    else
                    {
                        word.Top = cury;
                    }

                    if (!box.IsFixed)
                    {
                        word.BreakPage();
                    }

                    curx = word.Left + word.FullWidth;

                    maxRight = Math.Max(maxRight, word.Right);
                    maxbottom = Math.Max(maxbottom, word.Bottom);

                    if (b.Position == CssConstants.Absolute)
                    {
                        word.Left += box.ActualMarginLeft;
                        word.Top += box.ActualMarginTop;
                    }
                }
            }
            else
            {
                // Block-level child inside inline flow: force a line break
                // before and after the block (CSS2.1 §9.2.1.1 anonymous
                // block boxes).  This ensures elements like <p> inside an
                // inline <form> start on their own line.
                if (b.IsBlock)
                {
                    if (curx > startx || maxbottom > cury)
                    {
                        cury = maxbottom;
                        curx = startx;
                        line = new CssLineBox(blockbox);
                    }
                }

                FlowBox(g, blockbox, b, limitRight, linespacing, startx, ref line, ref curx, ref cury, ref maxRight, ref maxbottom);

                if (b.IsBlock)
                {
                    cury = maxbottom;
                    curx = startx;
                    line = new CssLineBox(blockbox);
                }
            }

            curx += rightspacing;
        }

        // handle height setting
        if (maxbottom - startY < box.ActualHeight)
            maxbottom += box.ActualHeight - (maxbottom - startY);

        // handle width setting
        if (box.IsInline && 0 <= curx - startX && curx - startX < box.ActualWidth)
        {
            // hack for actual width handling
            curx += box.ActualWidth - (curx - startX);
            line.Rectangles.Add(box, new RectangleF((float)startX, (float)startY, (float)box.ActualWidth, (float)box.ActualHeight));
        }

        // handle box that is only a whitespace
        if (box.Text.Length > 0 && box.Text.Span.IsWhiteSpace() && !box.IsImage && box.IsInline && box.Boxes.Count == 0 && box.Words.Count == 0)
            curx += box.ActualWordSpacing;

        // hack to support specific absolute position elements
        if (box.Position == CssConstants.Absolute)
        {
            curx = localCurx;
            maxRight = localMaxRight;
            maxbottom = localmaxbottom;
            AdjustAbsolutePosition(box, 0, 0);
        }

        box.LastHostingLineBox = line;
    }

    private static void AdjustAbsolutePosition(CssBox box, double left, double top)
    {
        left += box.ActualMarginLeft;
        top += box.ActualMarginTop;

        if (box.Words.Count > 0)
        {
            foreach (var word in box.Words)
            {
                word.Left += left;
                word.Top += top;
            }
        }
        else
        {
            foreach (var b in box.Boxes)
                AdjustAbsolutePosition(b, left, top);
        }
    }

    private static void BubbleRectangles(CssBox box, CssLineBox line)
    {
        if (box.Words.Count > 0)
        {
            double x = Single.MaxValue, y = Single.MaxValue, r = Single.MinValue, b = Single.MinValue;
            List<CssRect> words = line.WordsOf(box);

            if (words.Count <= 0)
                return;

            foreach (CssRect word in words)
            {
                // handle if line is wrapped for the first text element where parent has left margin\padding
                var left = word.Left;

                if (box == box.ParentBox.Boxes[0] && word == box.Words[0] && word == line.Words[0] && line != line.OwnerBox.LineBoxes[0] && !word.IsLineBreak)
                    left -= box.ParentBox.ActualMarginLeft + box.ParentBox.ActualBorderLeftWidth + box.ParentBox.ActualPaddingLeft;


                x = Math.Min(x, left);
                r = Math.Max(r, word.Right);
                y = Math.Min(y, word.Top);
                b = Math.Max(b, word.Bottom);
            }

            line.UpdateRectangle(box, x, y, r, b);
        }
        else
        {
            foreach (CssBox b in box.Boxes)
                BubbleRectangles(b, line);
        }
    }

    private static void ApplyHorizontalAlignment(RGraphics g, CssLineBox lineBox)
    {
        switch (lineBox.OwnerBox.TextAlign)
        {
            case CssConstants.Right:
                ApplyRightAlignment(g, lineBox);
                break;
            case CssConstants.Center:
                ApplyCenterAlignment(g, lineBox);
                break;
            case CssConstants.Justify:
                ApplyJustifyAlignment(g, lineBox);
                break;
            default:
                ApplyLeftAlignment(g, lineBox);
                break;
        }
    }

    private static void ApplyRightToLeft(CssBox blockBox, CssLineBox lineBox)
    {
        if (blockBox.Direction == CssConstants.Rtl)
        {
            ApplyRightToLeftOnLine(lineBox);
        }
        else
        {
            foreach (var box in lineBox.RelatedBoxes)
            {
                if (box.Direction == CssConstants.Rtl)
                    ApplyRightToLeftOnSingleBox(lineBox, box);
            }
        }
    }

    private static void ApplyRightToLeftOnLine(CssLineBox line)
    {
        if (line.Words.Count <= 0)
            return;

        double left = line.Words[0].Left;
        double right = line.Words[line.Words.Count - 1].Right;

        foreach (CssRect word in line.Words)
        {
            double diff = word.Left - left;
            double wright = right - diff;

            word.Left = wright - word.Width;
        }
    }

    private static void ApplyRightToLeftOnSingleBox(CssLineBox lineBox, CssBox box)
    {
        int leftWordIdx = -1;
        int rightWordIdx = -1;

        for (int i = 0; i < lineBox.Words.Count; i++)
        {
            if (lineBox.Words[i].OwnerBox != box)
                continue;

            if (leftWordIdx < 0)
                leftWordIdx = i;

            rightWordIdx = i;
        }

        if (leftWordIdx <= -1 || rightWordIdx <= leftWordIdx)
            return;

        double left = lineBox.Words[leftWordIdx].Left;
        double right = lineBox.Words[rightWordIdx].Right;

        for (int i = leftWordIdx; i <= rightWordIdx; i++)
        {
            double diff = lineBox.Words[i].Left - left;
            double wright = right - diff;

            lineBox.Words[i].Left = wright - lineBox.Words[i].Width;
        }
    }

    private static void ApplyVerticalAlignment(RGraphics g, CssLineBox lineBox)
    {
        double baseline = Single.MinValue;
        foreach (var box in lineBox.Rectangles.Keys)
            baseline = Math.Max(baseline, lineBox.Rectangles[box].Top);

        var boxes = new List<CssBox>(lineBox.Rectangles.Keys);
        foreach (CssBox box in boxes)
        {
            //Important notes on http://www.w3.org/TR/CSS21/tables.html#height-layout
            switch (box.VerticalAlign)
            {
                case CssConstants.Sub:
                    lineBox.SetBaseLine(g, box, baseline + lineBox.Rectangles[box].Height * .5f);
                    break;
                case CssConstants.Super:
                    lineBox.SetBaseLine(g, box, baseline - lineBox.Rectangles[box].Height * .2f);
                    break;
                case CssConstants.TextTop:

                    break;
                case CssConstants.TextBottom:

                    break;
                case CssConstants.Top:

                    break;
                case CssConstants.Bottom:

                    break;
                case CssConstants.Middle:

                    break;
                default:
                    //case: baseline
                    lineBox.SetBaseLine(g, box, baseline);
                    break;
            }
        }
    }

    private static void ApplyJustifyAlignment(RGraphics g, CssLineBox lineBox)
    {
        if (lineBox.Equals(lineBox.OwnerBox.LineBoxes[lineBox.OwnerBox.LineBoxes.Count - 1]))
            return;

        double indent = lineBox.Equals(lineBox.OwnerBox.LineBoxes[0]) ? lineBox.OwnerBox.ActualTextIndent : 0f;
        double textSum = 0f;
        double words = 0f;
        double availWidth = lineBox.OwnerBox.ClientRectangle.Width - indent;

        // Gather text sum
        foreach (CssRect w in lineBox.Words)
        {
            textSum += w.Width;
            words += 1f;
        }

        if (words <= 0f)
            return; //Avoid Zero division

        double spacing = (availWidth - textSum) / words; //Spacing that will be used
        double curx = lineBox.OwnerBox.ClientLeft + indent;

        foreach (CssRect word in lineBox.Words)
        {
            word.Left = curx;
            curx = word.Right + spacing;

            if (word == lineBox.Words[lineBox.Words.Count - 1])
                word.Left = lineBox.OwnerBox.ClientRight - word.Width;
        }
    }

    private static void ApplyCenterAlignment(RGraphics g, CssLineBox line)
    {
        if (line.Words.Count == 0)
            return;

        CssRect lastWord = line.Words[line.Words.Count - 1];
        double right = line.OwnerBox.ActualRight - line.OwnerBox.ActualPaddingRight - line.OwnerBox.ActualBorderRightWidth;
        double diff = right - lastWord.Right - lastWord.OwnerBox.ActualBorderRightWidth - lastWord.OwnerBox.ActualPaddingRight;
        diff /= 2;

        if (diff <= 0)
            return;

        foreach (CssRect word in line.Words)
            word.Left += diff;

        if (line.Rectangles.Count <= 0)
            return;

        foreach (CssBox b in ToList(line.Rectangles.Keys))
        {
            RectangleF r = line.Rectangles[b];
            line.Rectangles[b] = new RectangleF((float)(r.X + diff), r.Y, r.Width, r.Height);
        }
    }

    private static void ApplyRightAlignment(RGraphics g, CssLineBox line)
    {
        if (line.Words.Count == 0)
            return;

        CssRect lastWord = line.Words[line.Words.Count - 1];
        double right = line.OwnerBox.ActualRight - line.OwnerBox.ActualPaddingRight - line.OwnerBox.ActualBorderRightWidth;
        double diff = right - lastWord.Right - lastWord.OwnerBox.ActualBorderRightWidth - lastWord.OwnerBox.ActualPaddingRight;

        if (diff <= 0)
            return;

        foreach (CssRect word in line.Words)
            word.Left += diff;

        if (line.Rectangles.Count <= 0)
            return;

        foreach (CssBox b in ToList(line.Rectangles.Keys))
        {
            RectangleF r = line.Rectangles[b];
            line.Rectangles[b] = new RectangleF((float)(r.X + diff), r.Y, r.Width, r.Height);
        }
    }

    private static void ApplyLeftAlignment(RGraphics g, CssLineBox line)
    {
        //No alignment needed.

        //foreach (LineBoxRectangle r in line.Rectangles)
        //{
        //    double curx = r.Left + (r.Index == 0 ? r.OwnerBox.ActualPaddingLeft + r.OwnerBox.ActualBorderLeftWidth / 2 : 0);

        //    if (r.SpaceBefore) curx += r.OwnerBox.ActualWordSpacing;

        //    foreach (BoxWord word in r.Words)
        //    {
        //        word.Left = curx;
        //        word.Top = r.Top;// +r.OwnerBox.ActualPaddingTop + r.OwnerBox.ActualBorderTopWidth / 2;

        //        curx = word.Right + r.OwnerBox.ActualWordSpacing;
        //    }
        //}
    }

    /// <summary>
    /// todo: optimizate, not creating a list each time
    /// </summary>
    private static List<T> ToList<T>(IEnumerable<T> collection)
    {
        List<T> result = [.. collection];
        return result;
    }
}