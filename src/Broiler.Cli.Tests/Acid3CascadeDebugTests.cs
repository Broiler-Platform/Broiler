using Broiler.HTML.Image;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class Acid3CascadeDebugTests
{
    private readonly ITestOutputHelper _output;
    public Acid3CascadeDebugTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Important_Override_Reduces_Border_Width()
    {
        // The !important rule should set border to 1px, not 2em (40px).
        // Rendering a single inline-block p with both rules.
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; border: 1px blue; padding: 0; font: inherit; color: inherit; background: transparent; }
html { font: 20px Arial, sans-serif; }
:root { color: black; background: white; }
body { background: white; }
:first-child + * .c p { display: inline-block; border: 2em dotted red; padding: 10px; }
* + * > * > p { margin: 0; border: 1px solid ! important; }
</style></head><body>
<div class=""c""><p style=""width:60px;height:40px;background:lime"">X</p></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 300, 200);

        // Count red pixels in the border area (the 2em=40px region around content)
        int redPixels = 0;
        int limePixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.R > 200 && p.G < 80 && p.B < 80) redPixels++;
                if (p.G > 200 && p.R < 80 && p.B < 80) limePixels++;
            }

        _output.WriteLine($"Red pixels (should be ~0 with !important): {redPixels}");
        _output.WriteLine($"Lime pixels (background): {limePixels}");

        // With !important overriding to 1px solid, there should be almost no visible
        // red border. If we see many red pixels, the !important cascade failed.
        Assert.True(redPixels < 50, $"Expected no red border pixels from !important override, got {redPixels}");
    }

    [Fact]
    public void Without_Important_Higher_Specificity_Red_Wins()
    {
        // Without !important, the higher specificity red rule should win.
        // KNOWN LIMITATION: The CSS cascade currently applies rules in source
        // order within the same specificity group rather than sorting by computed
        // specificity.  This means the later, lower-specificity rule overrides
        // the higher-specificity rule.  This does not affect the Acid3 case
        // because the lower-specificity rule uses !important.
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; border: 1px blue; padding: 0; font: inherit; color: inherit; background: transparent; }
html { font: 20px Arial, sans-serif; }
:root { color: black; background: white; }
body { background: white; }
:first-child + * .c p { display: inline-block; border: 2em dotted red; padding: 10px; }
* + * > * > p { margin: 0; border: 1px solid; }
</style></head><body>
<div class=""c""><p style=""width:60px;height:40px;background:lime"">X</p></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 300, 200);

        int redPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.R > 200 && p.G < 80 && p.B < 80) redPixels++;
            }

        _output.WriteLine($"Red pixels (higher specificity wins): {redPixels}");
        // Higher specificity wins — many red pixels expected from 2em dotted red border
        Assert.True(redPixels > 100, $"Expected red border from higher specificity, got {redPixels}");
    }
}
