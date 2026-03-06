using Xunit;
using TheArtOfDev.HtmlRenderer.Image;
using System.Drawing;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Validates that the CSS 'transparent' keyword is handled correctly
/// for border colors and background colors (CSS2.1 §4.3.6).
/// </summary>
public class TransparentColorTests
{
    [Fact]
    public void TransparentBorder_DoesNotRenderAsBlack()
    {
        // CSS2.1 §4.3.6: 'transparent' is a valid color keyword.
        // A transparent border should be invisible (background shows through).
        string html = @"<div style='border: 10px solid transparent; width: 100px; height: 100px; background: yellow;'>test</div>";

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(200, 200);
        container.SetHtml(html);

        using var bitmap = new SkiaSharp.SKBitmap(200, 200, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.White);
        container.PerformLayout(canvas, new RectangleF(0, 0, 200, 200));
        container.PerformPaint(canvas, new RectangleF(0, 0, 200, 200));

        // The pixel at (5,50) is in the border area. With a transparent border,
        // the div's yellow background shows through (CSS backgrounds extend to
        // the border edge per §14.2).
        var pixel = bitmap.GetPixel(5, 50);

        Assert.True(pixel.Red > 200 && pixel.Green > 200 && pixel.Blue < 50,
            $"Expected transparent border (yellow background shows through), but got R={pixel.Red} G={pixel.Green} B={pixel.Blue}. " +
            "The 'transparent' CSS color keyword is not being handled correctly.");
    }

    [Fact]
    public void DefaultBackgroundColor_IsTransparent()
    {
        // The default background-color is 'transparent'. Ensure it does not
        // resolve to opaque black when no explicit background is set.
        string html = @"<html><body><div style='width: 50px; height: 50px; background: yellow;'>x</div></body></html>";

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(200, 200);
        container.SetHtml(html);

        using var bitmap = new SkiaSharp.SKBitmap(200, 200, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.White);
        container.PerformLayout(canvas, new RectangleF(0, 0, 200, 200));
        container.PerformPaint(canvas, new RectangleF(0, 0, 200, 200));

        // A pixel well outside the yellow div should be white (from the canvas
        // clear), not black (which would indicate 'transparent' resolved to black).
        var pixel = bitmap.GetPixel(150, 150);

        Assert.True(pixel.Red > 200 && pixel.Green > 200 && pixel.Blue > 200,
            $"Expected white background (transparent body), but got R={pixel.Red} G={pixel.Green} B={pixel.Blue}. " +
            "Default 'transparent' background-color may be resolving to black instead.");
    }
}
