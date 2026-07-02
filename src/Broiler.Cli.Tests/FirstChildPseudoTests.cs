using Broiler.HTML.Image;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class FirstChildPseudoTests
{
    private readonly ITestOutputHelper _output;
    public FirstChildPseudoTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void FirstChild_Rule_Sets_Display_InlineBlock()
    {
        // The :first-child rule should set display:inline-block on matching p elements.
        // Previously this rule was ignored entirely.
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; padding: 0; font: inherit; background: transparent; }
html { font: 20px Arial, sans-serif; }
:root { color: black; background: white; }
body { background: white; }
:first-child + * .container p { display: inline-block; background: lime; width: 60px; height: 40px; }
</style></head><body>
<div class=""container""><p>X</p></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 300, 200);

        // Count lime pixels — with display:inline-block the p should render with lime bg
        int limePixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.G > 200 && p.R < 150 && p.B < 50) limePixels++;
            }

        _output.WriteLine($"Lime pixels: {limePixels}");
        // With the fix, the :first-child rule should apply and make the p lime
        Assert.True(limePixels > 100, $"Expected lime pixels from :first-child rule, got {limePixels}");
    }

    [Fact]
    public void LastChild_Rule_Applied_To_Terminal()
    {
        // h1:last-child or #id:last-child should set properties
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; padding: 0; background: transparent; }
html { font: 20px Arial, sans-serif; }
body { background: white; }
p:last-child { background: red; }
</style></head><body>
<p style=""width:80px;height:40px"">Last</p>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 300, 200);

        int redPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.R > 200 && p.G < 80 && p.B < 80) redPixels++;
            }

        _output.WriteLine($"Red pixels from :last-child: {redPixels}");
        Assert.True(redPixels > 100, $"Expected red pixels from :last-child rule, got {redPixels}");
    }

    [Fact]
    public void H1_FirstChild_Rule_Applied()
    {
        // h1:first-child should match the h1 element
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; padding: 0; background: transparent; }
html { font: 20px Arial, sans-serif; }
body { background: white; }
h1:first-child { background: blue; }
</style></head><body>
<h1 style=""width:100px;height:40px"">Title</h1>
<p>Other</p>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 300, 200);

        int bluePixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.B > 200 && p.R < 80 && p.G < 80) bluePixels++;
            }

        _output.WriteLine($"Blue pixels from h1:first-child: {bluePixels}");
        Assert.True(bluePixels > 100, $"Expected blue pixels from h1:first-child rule, got {bluePixels}");
    }

    [Fact]
    public void Acid3_Bucket_Gets_InlineBlock_From_FirstChild_Rule()
    {
        // The exact Acid3 pattern: :first-child + * .buckets p should now apply
        // display:inline-block and vertical-align to bucket elements.
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

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 800, 600);

        int redPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.R > 200 && p.G < 80 && p.B < 80) redPixels++;
            }

        _output.WriteLine($"Red pixels (bucket background): {redPixels}");
        // With the fix, the bucket should have display:inline-block and show its red background
        Assert.True(redPixels > 0, $"Expected red pixels from bucket background, got {redPixels}");
    }
}
