using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.Dom;
using TheArtOfDev.HtmlRenderer.Image;
using Xunit;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Regression tests for CSS 2.1 §5.3 universal selector '*' support
/// in ancestor/descendant selector matching.  Verifies that rules like
/// <c>* div.target { ... }</c> correctly match elements with the
/// universal selector as an ancestor requirement.
/// </summary>
public class UniversalSelectorTests : IDisposable
{
    /// <summary>
    /// CSS 2.1 §5.3: The universal selector '*' in a descendant combinator
    /// (e.g. <c>* div.target</c>) must match any ancestor element.
    /// Verifies that border-width specified via such a selector is applied.
    /// </summary>
    [Fact]
    public void UniversalAncestor_BorderWidth_IsApplied()
    {
        string html = @"<html><head><style>
html, body { margin: 0; padding: 0; font: 12px sans-serif; }
.container div { border: solid; }
* div.target { border-width: 0 24px; }
</style></head><body>
<div class='container'><div class='target'></div></div>
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

        var root = container.HtmlContainerInt.Root;
        var target = FindBoxByClass(root, "target");
        Assert.NotNull(target);

        // border-width: 0 24px → top=0, right=24, bottom=0, left=24
        Assert.True(target.ActualBorderTopWidth < 0.5,
            $"Expected border-top-width ≈ 0, got {target.ActualBorderTopWidth}");
        Assert.True(Math.Abs(target.ActualBorderRightWidth - 24) < 0.5,
            $"Expected border-right-width ≈ 24, got {target.ActualBorderRightWidth}");
        Assert.True(target.ActualBorderBottomWidth < 0.5,
            $"Expected border-bottom-width ≈ 0, got {target.ActualBorderBottomWidth}");
        Assert.True(Math.Abs(target.ActualBorderLeftWidth - 24) < 0.5,
            $"Expected border-left-width ≈ 24, got {target.ActualBorderLeftWidth}");
    }

    /// <summary>
    /// Verifies that the universal selector in a descendant combinator
    /// takes precedence over a less-specific tag selector rule when
    /// applied in correct source order per CSS 2.1 §6.4.1.
    /// </summary>
    [Fact]
    public void UniversalAncestor_Overrides_TagSelector_BorderWidth()
    {
        // .wrapper div sets border to solid (medium = 2px all around).
        // * div.inner overrides border-width to 0 10px (top/bottom=0, left/right=10).
        // Since * div.inner has a class selector (higher specificity than tag),
        // its border-width should override.
        string html = @"<html><head><style>
html, body { margin: 0; padding: 0; font: 12px sans-serif; }
.wrapper div { border: solid; }
* div.inner { border-width: 0 10px; }
</style></head><body>
<div class='wrapper'><div class='inner'>text</div></div>
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

        var root = container.HtmlContainerInt.Root;
        var inner = FindBoxByClass(root, "inner");
        Assert.NotNull(inner);

        Assert.True(inner.ActualBorderTopWidth < 0.5,
            $"Expected border-top-width ≈ 0, got {inner.ActualBorderTopWidth}");
        Assert.True(Math.Abs(inner.ActualBorderLeftWidth - 10) < 0.5,
            $"Expected border-left-width ≈ 10, got {inner.ActualBorderLeftWidth}");
    }

    /// <summary>
    /// CSS 2.1 §4.1.7: Declarations with invalid <c>!</c> modifiers
    /// (e.g. <c>border: 5em solid red ! error</c>) must be discarded
    /// entirely and must not reset previously applied border properties.
    /// </summary>
    [Fact]
    public void InvalidBangModifier_Discards_Declaration()
    {
        // First rule sets border: solid 4px black.
        // Second rule has "! error" which should cause the declaration to be discarded.
        string html = @"<html><head><style>
html, body { margin: 0; padding: 0; font: 12px sans-serif; }
.box { border: solid 4px black; }
.box { border: 5em solid red ! error; }
</style></head><body>
<div class='box'>text</div>
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

        var root = container.HtmlContainerInt.Root;
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);

        // The "! error" declaration should be discarded, so border stays at 4px
        Assert.True(Math.Abs(box.ActualBorderTopWidth - 4) < 0.5,
            $"Expected border-top-width ≈ 4 (invalid ! error should be discarded), got {box.ActualBorderTopWidth}");
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

    public void Dispose()
    {
        // No persistent resources to clean up
    }
}
