using SkiaSharp;
using Broiler.HTML.Image;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class Acid3DiagnosticTests
{
    private readonly ITestOutputHelper _output;
    public Acid3DiagnosticTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Diag_Border_Shorthand_No_Style_Means_None()
    {
        // The * { border: 1px blue } rule from Acid3 should set
        // border-style: none (omitted from shorthand => initial value).
        // With style:none, the border should NOT render.
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; border: 1px blue; padding: 0; }
</style></head><body>
<div style=""width:100px;height:50px;background:white"">Test</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);

        int bluePixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Blue > 200 && p.Red < 50 && p.Green < 50)
                    bluePixels++;
            }

        _output.WriteLine($"Blue pixels: {bluePixels}");
        // With border-style:none, there should be ZERO visible blue border pixels
        Assert.Equal(0, bluePixels);
    }

    [Fact]
    public void Diag_Border_Shorthand_With_Style_Renders()
    {
        // border: 1px solid blue should render visible blue borders
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; border: 1px solid blue; padding: 0; }
</style></head><body>
<div style=""width:100px;height:50px;background:white"">Test</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 100);

        int bluePixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Blue > 200 && p.Red < 50 && p.Green < 50)
                    bluePixels++;
            }

        _output.WriteLine($"Blue pixels with solid style: {bluePixels}");
        Assert.True(bluePixels > 0, "With border-style:solid, blue borders should render");
    }

    [Fact]
    public void Diag_Bottom_Border_Position()
    {
        // Acid3 pattern: html with border and width, content should determine height
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; border: 1px blue; padding: 0; font: inherit; }
html { font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; }
:root { background: silver; color: black; border-width: 0 0.2em 0.2em 0; }
body { padding: 2em 2em 0; background: white; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }
</style></head><body>
<h1 style=""font-size:5em;font-weight:bolder"">Acid3</h1>
<p style=""height:100px"">Content paragraph</p>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 1024, 768);

        // Find the bottommost gray pixel (the bottom border of the html element)
        int bottomGrayRow = -1;
        for (int y = bitmap.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                // Gray is R~128, G~128, B~128
                if (p.Red > 100 && p.Red < 180 && p.Green > 100 && p.Green < 180 && p.Blue > 100 && p.Blue < 180
                    && Math.Abs(p.Red - p.Green) < 20 && Math.Abs(p.Red - p.Blue) < 20)
                {
                    bottomGrayRow = y;
                    goto found;
                }
            }
        }
        found:
        _output.WriteLine($"Bottom gray border row: {bottomGrayRow}");
        // In the reference, the bottom border should be around row ~450
        // If it's near 745, the layout engine is filling the viewport
        _output.WriteLine($"Expected: ~450, Got: {bottomGrayRow}");
    }
}
