using System;
using System.Drawing;
using Xunit;
using Broiler.HTML.Image;
using SkiaSharp;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for vertical text positioning fix.
/// SetBaseLine must convert from baseline Y to word-top Y for inline text
/// boxes so that text renders at the correct vertical position regardless
/// of whether the parent has a visible background.
/// </summary>
public class VerticalTextPositioningTests
{
    [Fact]
    public void Text_Position_Consistent_With_And_Without_Background()
    {
        var htmlWithBg = @"<!DOCTYPE html>
<html>
<style>
body { margin: 0; padding: 40px; background: white; font: 20px Arial, sans-serif; }
h1 { margin: 0; padding: 0; font-size: 5em; font-weight: bold; line-height: 1.2; background: lightyellow; }
</style>
<body><h1>Acid3</h1></body>
</html>";

        var htmlNoBg = @"<!DOCTYPE html>
<html>
<style>
body { margin: 0; padding: 40px; background: white; font: 20px Arial, sans-serif; }
h1 { margin: 0; padding: 0; font-size: 5em; font-weight: bold; line-height: 1.2; }
</style>
<body><h1>Acid3</h1></body>
</html>";

        using var bmpBg = HtmlRender.RenderToImage(htmlWithBg, 800, 300);
        using var bmpNoBg = HtmlRender.RenderToImage(htmlNoBg, 800, 300);

        int yBg = FindFirstDarkRow(bmpBg);
        int yNoBg = FindFirstDarkRow(bmpNoBg);

        // Both should render text near the top (body padding 40px + glyph offset)
        Assert.True(yNoBg < 80, $"Without bg: text at y={yNoBg}, expected y<80");
        Assert.True(Math.Abs(yBg - yNoBg) < 20,
            $"Text position differs by {Math.Abs(yBg - yNoBg)}px (bg={yBg}, noBg={yNoBg})");
    }

    [Fact]
    public void Inline_Top_And_Bottom_Padding_Produce_Similar_Line_Rect_Heights()
    {
        var html = @"<!DOCTYPE html>
<html>
<style>
body { margin: 0; padding: 0; font: 20px/20px Arial, sans-serif; }
p { margin: 0; }
#topOnly { padding-top: 20px; background: lightyellow; }
#bottomOnly { padding-bottom: 20px; background: lightyellow; }
</style>
<body>
  <p><span id=""topOnly"">Top</span></p>
  <p><span id=""bottomOnly"">Bottom</span></p>
</body>
</html>";

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html);
        container.MaxSize = new SizeF(300, 0);

        using var bitmap = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(bitmap);
        container.PerformLayout(canvas, new RectangleF(0, 0, 300, 300));

        var topRect = container.GetElementRectangle("topOnly");
        var bottomRect = container.GetElementRectangle("bottomOnly");

        Assert.True(topRect.HasValue, "Expected to resolve the top-padded inline span.");
        Assert.True(bottomRect.HasValue, "Expected to resolve the bottom-padded inline span.");
        Assert.True(Math.Abs(topRect.Value.Height - bottomRect.Value.Height) < 5,
            $"Expected top-only and bottom-only inline padding to produce similar line-rect heights, got top={topRect.Value.Height}px and bottom={bottomRect.Value.Height}px.");
    }

    private static int FindFirstDarkRow(SKBitmap bmp)
    {
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 50 && px.Green < 50 && px.Blue < 50)
                    return y;
            }
        return -1;
    }
}
