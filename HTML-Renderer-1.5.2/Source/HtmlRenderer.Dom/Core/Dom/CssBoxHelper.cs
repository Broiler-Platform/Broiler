using System;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using TheArtOfDev.HtmlRenderer.Core.Utils;
using System.Drawing;

namespace TheArtOfDev.HtmlRenderer.Core.Dom;

internal static class CssBoxHelper
{
    public static CssBox CreateBox(HtmlTag tag, CssBox parent = null)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (tag.Name == HtmlConstants.Img)
        {
            return new CssBoxImage(parent, tag);
        }
        else if (tag.Name.Equals("object", StringComparison.OrdinalIgnoreCase) &&
                 tag.TryGetAttribute("data") is { } data &&
                 data.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            // <object data="data:image/..."> — treat as a replaced image element.
            // Any nested fallback content will be removed by CorrectObjectBoxes.
            return new CssBoxImage(parent, tag);
        }
        else if (tag.Name == HtmlConstants.Iframe)
        {
            return new CssBox(parent, tag);
        }
        else if (tag.Name == HtmlConstants.Hr)
        {
            return new CssBoxHr(parent, tag);
        }
        else
        {
            return new CssBox(parent, tag);
        }
    }

    public static CssBox CreateBox(CssBox parent, HtmlTag tag = null, CssBox before = null)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var newBox = new CssBox(parent, tag);
        newBox.InheritStyle();

        if (before != null)
            newBox.SetBeforeBox(before);

        return newBox;
    }

    public static CssBox CreateBlock() => new(null, null) { Display = CssConstants.Block };

    public static CssBox CreateBlock(CssBox parent, HtmlTag tag = null, CssBox before = null)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var newBox = CreateBox(parent, tag, before);
        newBox.Display = CssConstants.Block;

        return newBox;
    }

    internal static CssRect FirstWordOccourence(CssBox b, CssLineBox line)
    {
        if (b.Words.Count == 0 && b.Boxes.Count == 0)
            return null;

        if (b.Words.Count > 0)
        {
            foreach (CssRect word in b.Words)
            {
                if (line.Words.Contains(word))
                    return word;
            }

            return null;
        }
        else
        {
            foreach (CssBox bb in b.Boxes)
            {
                CssRect w = FirstWordOccourence(bb, line);

                if (w != null)
                    return w;
            }

            return null;
        }
    }

    public static void GetMinimumWidth_LongestWord(CssBox box, ref double maxWidth, ref CssRect maxWidthWord)
    {
        if (box.Words.Count > 0)
        {
            foreach (CssRect cssRect in box.Words)
            {
                if (cssRect.Width > maxWidth)
                {
                    maxWidth = cssRect.Width;
                    maxWidthWord = cssRect;
                }
            }
        }
        else
        {
            foreach (CssBox childBox in box.Boxes)
                GetMinimumWidth_LongestWord(childBox, ref maxWidth, ref maxWidthWord);
        }
    }

    public static double GetWidthMarginDeep(CssBox box)
    {
        double sum = 0f;

        if (box.Size.Width > 90999 || (box.ParentBox != null && box.ParentBox.Size.Width > 90999))
        {
            while (box != null)
            {
                sum += box.ActualMarginLeft + box.ActualMarginRight;
                box = box.ParentBox;
            }
        }

        return sum;
    }

    internal static double GetMaximumBottom(CssBox startBox, double currentMaxBottom)
    {
        foreach (var line in startBox.Rectangles.Keys)
            currentMaxBottom = Math.Max(currentMaxBottom, startBox.Rectangles[line].Bottom);

        foreach (var b in startBox.Boxes)
            currentMaxBottom = Math.Max(currentMaxBottom, GetMaximumBottom(b, currentMaxBottom));

        return currentMaxBottom;
    }

    public static void GetMinMaxSumWords(CssBox box, ref double min, ref double maxSum, ref double paddingSum, ref double marginSum)
    {
        double? oldSum = null;

        // not inline (block) boxes start a new line so we need to reset the max sum
        if (box.Display != CssConstants.Inline && box.Display != CssConstants.TableCell && box.WhiteSpace != CssConstants.NoWrap)
        {
            oldSum = maxSum;
            maxSum = marginSum;
        }

        // add the padding 
        paddingSum += box.ActualBorderLeftWidth + box.ActualBorderRightWidth + box.ActualPaddingRight + box.ActualPaddingLeft;


        // for tables the padding also contains the spacing between cells
        if (box.Display == CssConstants.Table)
            paddingSum += CssLayoutEngineTable.GetTableSpacing(box);

        if (box.Words.Count > 0)
        {
            // calculate the min and max sum for all the words in the box
            foreach (CssRect word in box.Words)
            {
                maxSum += word.FullWidth + (word.HasSpaceBefore ? word.OwnerBox.ActualWordSpacing : 0);
                min = Math.Max(min, word.Width);
            }

            // remove the last word padding
            if (box.Words.Count > 0 && !box.Words[box.Words.Count - 1].HasSpaceAfter)
                maxSum -= box.Words[box.Words.Count - 1].ActualWordSpacing;
        }
        else
        {
            // recursively on all the child boxes
            for (int i = 0; i < box.Boxes.Count; i++)
            {
                CssBox childBox = box.Boxes[i];
                marginSum += childBox.ActualMarginLeft + childBox.ActualMarginRight;

                //maxSum += childBox.ActualMarginLeft + childBox.ActualMarginRight;
                GetMinMaxSumWords(childBox, ref min, ref maxSum, ref paddingSum, ref marginSum);

                marginSum -= childBox.ActualMarginLeft + childBox.ActualMarginRight;
            }
        }

        // max sum is max of all the lines in the box
        if (oldSum.HasValue)
            maxSum = Math.Max(maxSum, oldSum.Value);
    }

    public static double GetMaxFloatBottom(CssBox box)
    {
        double maxBottom = 0;
        List<(string tag, double bottom)> considered = null;

        if (box.ParentBox == null)
            return maxBottom;

        foreach (var sibling in box.ParentBox.Boxes)
        {
            if (sibling == box) break;
            CollectMaxFloatBottom(sibling, ref maxBottom, ref considered);
        }

        if (considered != null && considered.Count > 0)
        {
            Debug.WriteLine($"[ClearFloat] Clearance for <{box.HtmlTag?.Name ?? "?"}> clear={box.Clear}: " +
                $"considered {considered.Count} float(s), maxBottom={maxBottom:F1}");
            foreach (var (tag, bottom) in considered)
                Debug.WriteLine($"  - <{tag}> bottom={bottom:F1}");
        }

        return maxBottom;
    }

    /// <summary>
    /// Collects the maximum bottom coordinate of floats in the same
    /// block formatting context (BFC). Floated elements establish a
    /// new BFC, so their descendant floats are excluded from clearance
    /// calculations outside.
    /// </summary>
    private static void CollectMaxFloatBottom(CssBox box, ref double maxBottom,
        ref List<(string tag, double bottom)> considered)
    {
        if (box.Float != CssConstants.None)
        {
            // Compute the float's margin-box bottom ("bottom outer edge"
            // per CSS2.1 §9.5.2) so that clearance positions the cleared
            // element below the float's full margin box.
            double bottom;
            if (box.Height != CssConstants.Auto && !string.IsNullOrEmpty(box.Height))
                bottom = box.Location.Y + box.ActualHeight
                    + box.ActualPaddingTop + box.ActualPaddingBottom
                    + box.ActualBorderTopWidth + box.ActualBorderBottomWidth
                    + box.ActualMarginBottom;
            else
                bottom = box.ActualBottom + box.ActualBorderBottomWidth
                    + box.ActualMarginBottom;
            maxBottom = Math.Max(maxBottom, bottom);
            considered ??= new List<(string, double)>();
            considered.Add((box.HtmlTag?.Name ?? box.Display, bottom));
            // Float establishes a new BFC – don't recurse into descendants.
            return;
        }

        foreach (var child in box.Boxes)
            CollectMaxFloatBottom(child, ref maxBottom, ref considered);
    }

    public static bool IsRectVisible(RectangleF rect, RectangleF clip)
    {
        rect.X -= 2;
        rect.Width += 2;

        clip.Intersect(rect);

        if (clip != RectangleF.Empty)
            return true;

        return false;
    }
}
