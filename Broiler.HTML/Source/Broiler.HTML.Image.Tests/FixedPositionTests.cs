using System.Drawing;
using Broiler.HTML.Dom.Core.Dom;
using SkiaSharp;
using Xunit;

namespace Broiler.HTML.Image.Tests;

/// <summary>
/// Regression tests for CSS 2.1 §9.6.1 position:fixed viewport anchoring.
/// Validates that fixed-position elements render at viewport-relative
/// coordinates and remain stable across different scroll offsets.
/// Phase 6.5 of the Acid2 compliance effort.
/// </summary>
public class FixedPositionTests : IDisposable
{
    /// <summary>
    /// CSS 2.1 §9.6.1: A position:fixed element's layout position
    /// is determined by its CSS 'top' property relative to the viewport,
    /// not relative to any positioned ancestor.
    /// </summary>
    [Fact]
    public void FixedElement_LayoutPosition_MatchesCssTop()
    {
        const string html = @"<html><head><style>
html, body { margin: 0; padding: 0; font: 12px sans-serif; }
.fixed { position: fixed; top: 30px; left: 20px; width: 50px; height: 10px; background: red; }
</style></head><body>
<div class='fixed'></div>
<div style='height: 2000px;'></div>
</body></html>";

        int w = 200, h = 200;
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(html);

        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        container.PerformLayout(canvas, new RectangleF(0, 0, w, 99999));

        var fixedBox = FindBoxByClass(container.HtmlContainerInt.Root, "fixed");
        Assert.NotNull(fixedBox);

        // Position:fixed with top:30px should be at Y≈30 from viewport top
        Assert.True(fixedBox!.Location.Y >= 29 && fixedBox.Location.Y <= 31,
            $"Fixed element Location.Y={fixedBox.Location.Y}, expected ~30 (CSS top:30px)");

        // Position:fixed with left:20px should be at X≈20 from viewport left
        Assert.True(fixedBox.Location.X >= 19 && fixedBox.Location.X <= 21,
            $"Fixed element Location.X={fixedBox.Location.X}, expected ~20 (CSS left:20px)");
    }

    /// <summary>
    /// CSS 2.1 §9.6.1: A position:fixed element must anchor to the viewport,
    /// not to any positioned ancestor.  Even when wrapped in a position:relative
    /// container, the fixed element should be at viewport-relative coordinates.
    /// </summary>
    [Fact]
    public void FixedElement_IgnoresPositionedAncestor()
    {
        const string html = @"<html><head><style>
html, body { margin: 0; padding: 0; font: 12px sans-serif; }
.wrapper { position: relative; top: 100px; left: 100px; width: 50px; height: 50px; }
.fixed { position: fixed; top: 20px; left: 10px; width: 30px; height: 10px; background: red; }
</style></head><body>
<div class='wrapper'><div class='fixed'></div></div>
</body></html>";

        int w = 300, h = 200;
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(html);

        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        container.PerformLayout(canvas, new RectangleF(0, 0, w, 99999));

        var fixedBox = FindBoxByClass(container.HtmlContainerInt.Root, "fixed");
        Assert.NotNull(fixedBox);

        // Fixed element should be at viewport-relative (10, 20), NOT at
        // ancestor-relative (100+10, 100+20) = (110, 120)
        Assert.True(fixedBox!.Location.Y >= 19 && fixedBox.Location.Y <= 21,
            $"Fixed element Y={fixedBox.Location.Y}, expected ~20 (viewport-relative, not ancestor-relative)");
        Assert.True(fixedBox.Location.X >= 9 && fixedBox.Location.X <= 11,
            $"Fixed element X={fixedBox.Location.X}, expected ~10 (viewport-relative, not ancestor-relative)");
    }

    /// <summary>
    /// CSS 2.1 §9.6.1: When rendering a scrolled region, position:fixed elements
    /// must appear at the same viewport-relative position.  The PaintWalker
    /// offsets fixed elements by viewport coordinates so they remain anchored
    /// after canvas translation.
    /// </summary>
    [Fact]
    public void FixedElement_RendersAtViewportY_WhenScrolled()
    {
        // A fixed element at top:5px with red background, plus tall content
        // with a scroll target partway down.
        const string html = @"<html><head><style>
html, body { margin: 0; padding: 0; font: 12px sans-serif; }
.fixed { position: fixed; top: 5px; left: 0; width: 200px; height: 10px; background: red; }
</style></head><body>
<div class='fixed'></div>
<div style='height: 400px;'></div>
<div id='scroll-target' style='height: 1px;'></div>
<div style='height: 400px;'></div>
</body></html>";

        int w = 200, h = 100;

        // 1. Layout with tall viewport
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(html);

        using var layoutBmp = new SKBitmap(w, 2000, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var layoutCanvas = new SKCanvas(layoutBmp);
        layoutCanvas.Clear(SKColors.White);
        container.PerformLayout(layoutCanvas, new RectangleF(0, 0, w, 99999));

        // 2. Find the scroll target
        var anchorRect = container.GetElementRectangle("scroll-target");
        Assert.NotNull(anchorRect);
        float scrollY = anchorRect!.Value.Y;
        Assert.True(scrollY > 100, $"Scroll target Y={scrollY}, expected >100");

        // 3. Render with scroll to anchor
        container.Location = new PointF(0, scrollY);
        container.MaxSize = new SizeF(w, h);

        using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.Translate(0, -scrollY);
        container.PerformPaint(canvas, new RectangleF(0, scrollY, w, h));
        canvas.Restore();

        // 4. The red fixed element (top:5px, height:10px) should appear at
        //    y=5..14 in the bitmap, NOT at y=(scrollY+5) or absent.
        int redRowCount = 0;
        for (int y = 0; y < 20; y++)
        {
            bool hasRed = false;
            for (int x = 0; x < w; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                {
                    hasRed = true;
                    break;
                }
            }
            if (hasRed) redRowCount++;
        }

        Assert.True(redRowCount >= 5,
            $"Expected ≥5 red rows in y=0..19 (fixed element at top:5px), found {redRowCount}. " +
            "position:fixed may not be viewport-anchored when scrolled.");
    }

    /// <summary>
    /// Rendering the same page at two different scroll offsets must produce
    /// the fixed element at the same bitmap Y position, confirming it does
    /// not move with scrolling.
    /// </summary>
    [Fact]
    public void FixedElement_StableAcrossScrollOffsets()
    {
        const string html = @"<html><head><style>
html, body { margin: 0; padding: 0; font: 12px sans-serif; }
.fixed { position: fixed; top: 5px; left: 0; width: 200px; height: 10px; background: red; }
</style></head><body>
<div class='fixed'></div>
<div style='height: 300px;'></div>
<div id='anchor1' style='height: 1px;'></div>
<div style='height: 200px;'></div>
<div id='anchor2' style='height: 1px;'></div>
<div style='height: 300px;'></div>
</body></html>";

        int w = 200, h = 100;

        int firstRedY1 = RenderAndFindFirstRedRow(html, "anchor1", w, h);
        int firstRedY2 = RenderAndFindFirstRedRow(html, "anchor2", w, h);

        Assert.True(firstRedY1 >= 0, "Fixed element not found when scrolled to anchor1");
        Assert.True(firstRedY2 >= 0, "Fixed element not found when scrolled to anchor2");

        // The fixed element should be at the same Y in both renders
        Assert.True(Math.Abs(firstRedY1 - firstRedY2) <= 1,
            $"Fixed element Y differs between scrolls: anchor1→y={firstRedY1}, anchor2→y={firstRedY2}. " +
            "position:fixed should be stable across scroll offsets.");
    }

    /// <summary>
    /// CSS 2.1 §10.6.7: A position:fixed element establishes a BFC, so
    /// float children contribute to its auto-height.
    /// </summary>
    [Fact]
    public void FixedElement_EstablishesBFC_FloatsContributeToHeight()
    {
        const string html = @"<html><head><style>
html, body { margin: 0; padding: 0; font: 12px sans-serif; }
.fixed { position: fixed; top: 0; left: 0; width: 100px; background: yellow; }
.floated { float: left; width: 40px; height: 30px; background: red; }
</style></head><body>
<div class='fixed'><div class='floated'></div></div>
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

        var fixedBox = FindBoxByClass(container.HtmlContainerInt.Root, "fixed");
        Assert.NotNull(fixedBox);

        double fixedHeight = fixedBox!.ActualBottom - fixedBox.Location.Y;
        // Fixed element's auto-height should include the floated child (30px)
        Assert.True(fixedHeight >= 29,
            $"Fixed element height={fixedHeight}, expected ≥29 (float child 30px should contribute to BFC auto-height)");
    }

    /// <summary>
    /// Helper: renders the given HTML scrolled to the anchor, returns the
    /// first bitmap row that contains red pixels, or -1 if none found.
    /// </summary>
    private static int RenderAndFindFirstRedRow(string html, string anchorId, int w, int h)
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(html);

        using var layoutBmp = new SKBitmap(w, 2000, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var layoutCanvas = new SKCanvas(layoutBmp);
        layoutCanvas.Clear(SKColors.White);
        container.PerformLayout(layoutCanvas, new RectangleF(0, 0, w, 99999));

        var anchorRect = container.GetElementRectangle(anchorId);
        if (anchorRect is null) return -1;
        float scrollY = anchorRect.Value.Y;

        container.Location = new PointF(0, scrollY);
        container.MaxSize = new SizeF(w, h);

        using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.Translate(0, -scrollY);
        container.PerformPaint(canvas, new RectangleF(0, scrollY, w, h));
        canvas.Restore();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                    return y;
            }
        }

        return -1;
    }

    private static CssBox? FindBoxByClass(CssBox root, string className)
    {
        if (root.HtmlTag?.TryGetAttribute("class") == className)
            return root;
        foreach (var child in root.Boxes)
        {
            var result = FindBoxByClass(child, className);
            if (result != null) return result;
        }
        return null;
    }

    public void Dispose() { }
}
