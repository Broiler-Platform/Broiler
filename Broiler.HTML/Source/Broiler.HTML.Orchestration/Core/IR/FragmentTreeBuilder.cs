using Broiler.HTML.Core.Core.IR;
using Broiler.HTML.Dom.Core.Dom;
using Broiler.HTML.Utils.Core.Utils;
using System.Collections.Generic;
using System.Drawing;

namespace Broiler.HTML.Orchestration.Core.IR;

/// <summary>
/// Walks a <see cref="CssBox"/> tree after layout and builds a read-only
/// <see cref="Fragment"/> tree that snapshots the layout geometry.
/// </summary>
internal static class FragmentTreeBuilder
{
    /// <summary>
    /// Builds a <see cref="Fragment"/> tree from the given root <see cref="CssBox"/>.
    /// Should be called after <c>PerformLayout</c> has completed.
    /// </summary>
    public static Fragment Build(CssBox root) => BuildFragment(root);

    private static Fragment BuildFragment(CssBox box)
    {
        var style = ComputedStyleBuilder.FromBox(box);

        var children = new List<Fragment>(box.Boxes.Count);
        foreach (var child in box.Boxes)
            children.Add(BuildFragment(child));

        List<LineFragment>? lines = null;
        if (box.LineBoxes.Count > 0)
        {
            lines = new List<LineFragment>(box.LineBoxes.Count);
            foreach (var lineBox in box.LineBoxes)
                lines.Add(BuildLineFragment(lineBox));
        }

        // Phase 3: Capture background image handle for the new paint path
        object? bgImage = box.LoadedBackgroundImage;

        // Phase 3: Capture replaced image handle (e.g. <img> elements)
        object? imgHandle = null;
        RectangleF imgSourceRect = RectangleF.Empty;
        if (box is CssBoxImage imgBox)
        {
            imgHandle = imgBox.Image;
            // CssBoxImage stores source rect on its internal CssRectImage word
            if (imgBox.Words.Count > 0 && imgBox.Words[0] is CssRectImage rectImage)
                imgSourceRect = rectImage.ImageRectangle;
        }

        // Capture per-line-box rectangles for inline elements (used for backgrounds/borders)
        List<RectangleF>? inlineRects = null;
        if (box.Rectangles.Count > 0)
        {
            inlineRects = new List<RectangleF>(box.Rectangles.Values);
        }

        // CssBox.Size.Height is never set for block-level boxes during layout;
        // the actual rendered height is tracked via ActualBottom instead.
        // Compute the correct border-box height so that PaintWalker can
        // draw backgrounds and borders (which skip Height <= 0 rects).
        var size = box.Size;

        // Sanitise NaN width: auto-width absolutely positioned elements
        // may still have NaN if ComputeShrinkToFitWidth could not resolve
        // a finite value (e.g. deeply nested inline objects).  Fall back
        // to ActualRight - Location.X which is layout-computed.
        if (float.IsNaN(size.Width))
        {
            float layoutWidth = (float)(box.ActualRight - box.Location.X);
            size = new SizeF(layoutWidth > 0 ? layoutWidth : 0, size.Height);
        }

        float layoutHeight = (float)(box.ActualBottom - box.Location.Y);
        if (layoutHeight > size.Height)
            size = new SizeF(size.Width, layoutHeight);

        return new Fragment
        {
            Location = box.Location,
            Size = size,
            Margin = style.Margin,
            Border = style.Border,
            Padding = style.Padding,
            Lines = lines,
            Children = children,
            Style = style,
            CreatesStackingContext = IsStackingContext(box),
            StackLevel = GetStackLevel(box),
            BackgroundImageHandle = bgImage,
            ImageHandle = imgHandle,
            ImageSourceRect = imgSourceRect,
            InlineRects = inlineRects,
        };
    }

    private static LineFragment BuildLineFragment(CssLineBox lineBox)
    {
        var inlines = new List<InlineFragment>();

        foreach (var word in lineBox.Words)
        {
            var ownerStyle = ComputedStyleBuilder.FromBox(word.OwnerBox);
            inlines.Add(new InlineFragment
            {
                X = (float)word.Left,
                Y = (float)word.Top,
                Width = (float)word.Width,
                Height = (float)word.Height,
                Text = word.IsSpaces
                    ? (word.OwnerBox.WhiteSpace is CssConstants.Pre or CssConstants.PreWrap
                        ? word.Text   // CSS2.1 §16.6: preserve space sequences in pre/pre-wrap
                        : " ")
                    : word.Text,
                Style = ownerStyle,
                FontHandle = word.OwnerBox.ActualFont,
                Selected = word.Selected,
                SelectedStartOffset = word.SelectedStartOffset,
                SelectedEndOffset = word.SelectedEndOffset,
            });
        }

        // Compute line bounds from all rectangles in this line box
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxR = float.MinValue, maxB = float.MinValue;

        foreach (var rect in lineBox.Rectangles.Values)
        {
            if (rect.X < minX) minX = rect.X;
            if (rect.Y < minY) minY = rect.Y;
            if (rect.Right > maxR) maxR = rect.Right;
            if (rect.Bottom > maxB) maxB = rect.Bottom;
        }

        if (lineBox.Rectangles.Count == 0)
            minX = minY = maxR = maxB = 0;

        return new LineFragment
        {
            X = minX,
            Y = minY,
            Width = maxR - minX,
            Height = maxB - minY,
            Baseline = 0,
            Inlines = inlines,
        };
    }

    private static bool IsStackingContext(CssBox box)
    {
        // A box creates a stacking context if it is positioned with a z-index,
        // or has opacity < 1, or is a fixed/absolute-positioned element.
        if (box.Position == CssConstants.Absolute || box.Position == CssConstants.Fixed)
            return true;

        // CSS2.1 §9.9.1: A positioned element with a computed z-index
        // other than 'auto' establishes a new stacking context.
        if (box.Position == CssConstants.Relative
            && box.ZIndex != null && box.ZIndex != CssConstants.Auto
            && int.TryParse(box.ZIndex, out _))
            return true;

        if (double.TryParse(box.Opacity, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var opacity) && opacity < 1.0)
            return true;

        return false;
    }

    /// <summary>
    /// Returns the computed stack level (z-index) for a box.
    /// CSS2.1 §9.9.1: 'auto' computes to 0 for painting order.
    /// </summary>
    private static int GetStackLevel(CssBox box)
    {
        if (box.ZIndex != null && box.ZIndex != CssConstants.Auto && int.TryParse(box.ZIndex, out int z))
            return z;

        return 0;
    }
}
