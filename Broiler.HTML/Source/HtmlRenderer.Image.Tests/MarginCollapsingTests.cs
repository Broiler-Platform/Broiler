using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.Dom;
using TheArtOfDev.HtmlRenderer.Image;
using Xunit;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Regression tests for CSS 2.1 §8.3.1 margin collapsing and §9.4.3
/// relative positioning interactions with auto-height.
/// </summary>
public class MarginCollapsingTests : IDisposable
{
    /// <summary>
    /// CSS 2.1 §8.3.1: Parent-child margin collapsing must NOT happen
    /// when the parent has a non-zero border-top.  The child's full
    /// margin-top should be preserved inside the parent's content area.
    /// </summary>
    [Fact]
    public void ParentBorder_PreventsMarginCollapse()
    {
        // Parent has border-top: 2px → child's 20px margin must NOT collapse
        string html = @"<html><head><style>
html, body { margin: 0; padding: 0; font: 12px sans-serif; }
.parent { border: 2px solid black; background: yellow; }
.child  { margin-top: 20px; height: 10px; background: red; }
</style></head><body>
<div class='parent'><div class='child'></div></div>
</body></html>";

        int w = 200, h = 100;
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(html);

        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        container.PerformLayout(canvas, new RectangleF(0, 0, w, 99999));

        // Use box model for accurate height measurement
        var bodyBox = FindBoxByTag(container.HtmlContainerInt.Root, "body");
        Assert.NotNull(bodyBox);

        CssBox? parentDiv = null;
        CssBox? childDiv = null;
        foreach (var child in bodyBox!.Boxes)
        {
            if (child.HtmlTag?.TryGetAttribute("class") == "parent")
            {
                parentDiv = child;
                foreach (var gc in child.Boxes)
                {
                    if (gc.HtmlTag?.TryGetAttribute("class") == "child")
                        childDiv = gc;
                }
            }
        }
        Assert.NotNull(parentDiv);
        Assert.NotNull(childDiv);

        double parentHeight = parentDiv!.ActualBottom - parentDiv.Location.Y;
        double childRelY = childDiv!.Location.Y - parentDiv.Location.Y;

        // Child should start at border(2) + margin(20) = 22 from parent top
        Assert.True(childRelY >= 21, $"Child relative Y ({childRelY}) should be ≥21 (border + margin preserved)");
        // Parent height = border(2) + margin(20) + child(10) + border(2) = 34
        Assert.True(parentHeight >= 33, $"Parent height ({parentHeight}) should be ≥33 (child margin inside border)");
    }

    /// <summary>
    /// CSS 2.1 §8.3.1: Parent-child margin collapsing SHOULD happen
    /// when the parent has no border and no padding.
    /// </summary>
    [Fact]
    public void NoBorder_AllowsMarginCollapse()
    {
        // Parent has no border/padding → child's margin collapses with parent's
        string html = @"<html><head><style>
html, body { margin: 0; padding: 0; font: 12px sans-serif; }
.parent { margin-top: 10px; background: yellow; }
.child  { margin-top: 20px; height: 10px; background: red; }
</style></head><body>
<div class='parent'><div class='child'></div></div>
</body></html>";

        var (_, childY) = MeasureLayout(html, ".parent", ".child");

        // Collapsed margin = max(10, 20) = 20px → child at y=20
        Assert.True(childY >= 19 && childY <= 21, $"Child Y ({childY}) should be ~20 (margins collapsed)");
    }

    /// <summary>
    /// CSS 2.1 §9.4.3: Relative positioning must NOT affect the parent's
    /// auto-height calculation.  A child with position:relative and
    /// bottom:-N should not increase the parent's height.
    /// </summary>
    [Fact]
    public void RelativePositioning_DoesNotAffectParentAutoHeight()
    {
        // Child has height:20px and position:relative; bottom:-10px (shifts down 10px visually)
        // Parent auto-height should be 20px, NOT 30px
        string html = @"<html><head><style>
html, body { margin: 0; padding: 0; font: 12px sans-serif; }
.parent { background: yellow; }
.child  { height: 20px; background: red; position: relative; bottom: -10px; }
</style></head><body>
<div class='parent'><div class='child'></div></div>
</body></html>";

        int w = 200, h = 100;
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(html);

        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        container.PerformLayout(canvas, new RectangleF(0, 0, w, 99999));

        // Measure parent height from box model, not visual pixels
        var bodyBox = FindBoxByTag(container.HtmlContainerInt.Root, "body");
        Assert.NotNull(bodyBox);

        CssBox? parentDiv = null;
        foreach (var child in bodyBox!.Boxes)
        {
            if (child.HtmlTag?.TryGetAttribute("class") == "parent")
            {
                parentDiv = child;
                break;
            }
        }
        Assert.NotNull(parentDiv);

        double parentHeight = parentDiv!.ActualBottom - parentDiv.Location.Y;
        // Parent auto-height = child's flow height (20px), not visual (30px)
        Assert.True(parentHeight <= 21,
            $"Parent box height ({parentHeight}) should be ≤21 (relative offset excluded from auto-height)");
    }

    /// <summary>
    /// CSS 2.1 §9.4.3: Relative positioning with top shifts an element
    /// visually but must not affect sibling positioning.
    /// </summary>
    [Fact]
    public void RelativePositioning_DoesNotAffectSiblingPosition()
    {
        // First child has height:20px and position:relative; top:10px
        // Second child should start at y=20 in flow, not y=30
        string html = @"<html><head><style>
html, body { margin: 0; padding: 0; font: 12px sans-serif; }
.first  { height: 20px; background: red; position: relative; top: 10px; }
.second { height: 20px; background: blue; }
</style></head><body>
<div class='first'></div>
<div class='second'></div>
</body></html>";

        int w = 200, h = 100;
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(html);

        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        container.PerformLayout(canvas, new RectangleF(0, 0, w, 99999));

        // Find the second div's Location.Y (layout position)
        var bodyBox = FindBoxByTag(container.HtmlContainerInt.Root, "body");
        Assert.NotNull(bodyBox);

        // Find the blue (second) div
        CssBox? secondDiv = null;
        foreach (var child in bodyBox!.Boxes)
        {
            if (child.HtmlTag?.TryGetAttribute("class") == "second")
            {
                secondDiv = child;
                break;
            }
        }
        Assert.NotNull(secondDiv);

        // Location.Y should be 20 (flow position), not 30 (visual position of red's bottom)
        Assert.True(secondDiv!.Location.Y >= 19 && secondDiv.Location.Y <= 21,
            $"Second div flow position Y={secondDiv.Location.Y}, expected ~20 (relative offset excluded from flow)");
    }

    private static CssBox? FindBoxByTag(CssBox root, string tagName)
    {
        if (root.HtmlTag?.Name == tagName) return root;
        foreach (var child in root.Boxes)
        {
            var result = FindBoxByTag(child, tagName);
            if (result != null) return result;
        }
        return null;
    }

    private static (int parentHeight, int childY) MeasureLayout(string html, string parentSel, string childSel)
    {
        int w = 200, h = 100;
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(html);

        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        container.PerformLayout(canvas, new RectangleF(0, 0, w, 99999));
        container.PerformPaint(canvas, new RectangleF(0, 0, w, h));

        // Measure yellow (parent) and red (child) regions
        int parentFirst = -1, parentLast = -1;
        int childFirst = -1;

        for (int y = 0; y < h; y++)
        {
            bool hasYellow = false, hasRed = false;
            for (int x = 0; x < w; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Green > 200 && px.Blue < 50) hasYellow = true;
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50) hasRed = true;
            }
            // Parent includes both yellow and red (red is child inside parent)
            if (hasYellow || hasRed)
            {
                if (parentFirst < 0) parentFirst = y;
                parentLast = y;
            }
            if (hasRed && childFirst < 0) childFirst = y;
        }

        int parentHeight = parentFirst >= 0 ? parentLast - parentFirst + 1 : 0;
        return (parentHeight, childFirst);
    }

    public void Dispose() { }
}
