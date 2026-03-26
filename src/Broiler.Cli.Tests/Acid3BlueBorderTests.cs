using SkiaSharp;
using Broiler.HTML.Image;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class Acid3BlueBorderTests
{
    private readonly ITestOutputHelper _output;
    public Acid3BlueBorderTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Bucket_Borders_Not_Blue_After_Important_Override()
    {
        // Reproduce the exact Acid3 CSS pattern for bucket elements
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; border: 1px blue; padding: 0; border-spacing: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }
html { font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; }
:root { background: silver; color: black; border-width: 0 0.2em 0.2em 0; }
body { padding: 2em 2em 0; background: white; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }
.buckets { font: 0/0 Arial, sans-serif; padding: 0 0 150px 3px; }
:first-child + * .buckets p { display: inline-block; vertical-align: 2em; border: 2em dotted red; padding: 1.0em 0 1.0em 2em; }
* + * > * > p { margin: 0; border: 1px solid ! important; }
.z { visibility: hidden; }
#bucket1 { font-size: 20px; margin-left: 0.2em; padding-left: 1.3em; padding-right: 1.3em; }
#bucket1.zPPPPPPPPPPPPPPPP { background: red; }
</style></head><body>
<h1>Acid3</h1>
<div class=""buckets"">
<p id=""bucket1"" class=""zPPPPPPPPPPPPPPPP""></p>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 800, 600);

        int bluePixels = 0;
        int redPixels = 0;
        int blackPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Blue > 200 && p.Red < 80 && p.Green < 80) bluePixels++;
                if (p.Red > 200 && p.Green < 80 && p.Blue < 80) redPixels++;
                if (p.Red < 30 && p.Green < 30 && p.Blue < 30 && p.Alpha > 200) blackPixels++;
            }

        _output.WriteLine($"Blue pixels: {bluePixels}");
        _output.WriteLine($"Red pixels: {redPixels}");
        _output.WriteLine($"Black pixels: {blackPixels}");

        // The bucket borders should be 1px solid black (from !important),
        // not blue (from universal rule). Red background is expected from bucket1.
        Assert.True(bluePixels < 10, $"Expected minimal blue pixels from !important override, got {bluePixels}");
    }

    [Fact]
    public void Bucket_Div_Border_Should_Not_Be_Blue()
    {
        // The .buckets div matches the universal rule only.
        // border: 1px blue without style should NOT render.
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; border: 1px blue; padding: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }
html { font: 20px Arial, sans-serif; width: 32em; }
:root { background: silver; color: black; }
body { background: white; }
.buckets { font: 0/0 Arial, sans-serif; padding: 0 0 150px 3px; }
</style></head><body>
<div class=""buckets""><p style=""display:inline-block;width:60px;height:40px;background:lime""></p></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 300);

        int bluePixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Blue > 200 && p.Red < 80 && p.Green < 80) bluePixels++;
            }

        _output.WriteLine($"Blue pixels: {bluePixels}");
        // No element has border-style:solid/dotted/etc, so no borders should render
        Assert.Equal(0, bluePixels);
    }

    [Fact]
    public void Important_Border_Overrides_Higher_Specificity_Non_Important()
    {
        // Test the exact Acid3 pattern: !important on lower specificity must
        // override higher specificity non-important.
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; border: 1px blue; padding: 0; }
:root { color: black; }
:first-child + * .container p { border: 2em dotted red; display: inline-block; width: 60px; height: 40px; }
* + * > * > p { margin: 0; border: 1px solid ! important; }
</style></head><body>
<div class=""container""><p>X</p></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 200);

        int bluePixels = 0;
        int redPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Blue > 200 && p.Red < 80 && p.Green < 80) bluePixels++;
                if (p.Red > 200 && p.Green < 80 && p.Blue < 80) redPixels++;
            }

        _output.WriteLine($"Blue pixels: {bluePixels}, Red pixels: {redPixels}");
        Assert.Equal(0, bluePixels);
        Assert.Equal(0, redPixels);
    }
}
