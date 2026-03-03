using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;
using System.Drawing;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Tests for the HtmlContainer class.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Rendering")]
[Trait("Engine", "HtmlRenderer")]
public class HtmlContainerTests(RenderingFixture fixture)
{
    [Fact]
    public void HtmlContainer_SetHtmlAndProperties_WorkCorrectly()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;

        Assert.True(container.AvoidAsyncImagesLoading);
        Assert.True(container.AvoidImagesLateLoading);

        container.SetHtml("<div>Test</div>");
    }

    [Fact]
    public void HtmlContainer_LayoutAndPaint_WorkCorrectly()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='width:200px;'>Hello World</div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        Assert.True(container.ActualSize.Width > 0);
        Assert.True(container.ActualSize.Height > 0);

        container.PerformPaint(canvas, clip);
    }
}
