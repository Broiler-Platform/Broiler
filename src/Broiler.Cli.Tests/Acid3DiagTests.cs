using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

public class Acid3DiagTests
{
    private readonly ITestOutputHelper _output;
    public Acid3DiagTests(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void Diag_Body_Background_Shorthand_With_DataUri()
    {
        // Exact CSS from acid3 for body
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; border: 0; padding: 0; background: transparent; }
body { background: url(data:image/gif;base64,iVBORw0KGgoAAAANSUhEUgAAABQAAAAUCAIAAAAC64paAAAABGdBTUEAAK%2FINwWK6QAAAAlwSFlzAAAASAAAAEgARslrPgAAABtJREFUOMtj%2FM9APmCiQO%2Bo5lHNo5pHNVNBMwAinAEnIWw89gAAACJ6VFh0U29mdHdhcmUAAHjac0zJT0pV8MxNTE8NSk1MqQQAL5wF1K4MqU0AAAAASUVORK5CYII%3D) no-repeat 99.8392283% 1px white; }
</style></head><body><p>Test</p></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        
        // Check body background color at various points
        for (int y = 5; y < 100; y += 20) {
            for (int x = 5; x < 100; x += 20) {
                var px = bitmap.GetPixel(x, y);
                _output.WriteLine($"Pixel at ({x},{y}): R={px.Red} G={px.Green} B={px.Blue} A={px.Alpha}");
            }
        }
        
        // The background should be white (255,255,255) not red
        var center = bitmap.GetPixel(50, 50);
        _output.WriteLine($"\nCenter pixel: R={center.Red} G={center.Green} B={center.Blue}");
        Assert.True(center.Red == 255 && center.Green == 255 && center.Blue == 255, 
            $"Expected white (255,255,255), got ({center.Red},{center.Green},{center.Blue})");
    }

    [Fact]
    public void Diag_Background_Shorthand_Simple_White()
    {
        // Simple case - no data URI
        var html = @"<!DOCTYPE html><html><head><style>
* { background: transparent; }
body { background: white; }
</style></head><body><p>Test</p></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        var center = bitmap.GetPixel(50, 50);
        _output.WriteLine($"Simple white: R={center.Red} G={center.Green} B={center.Blue}");
        Assert.True(center.Red == 255 && center.Green == 255 && center.Blue == 255, 
            $"Expected white, got ({center.Red},{center.Green},{center.Blue})");
    }
    
    [Fact]
    public void Diag_Background_Shorthand_NoRepeat()
    {
        // Test that no-repeat works - use a red data URI with no-repeat
        // Body should be white at (50,50) since the red image should only appear at ~100% 1px
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; padding: 0; background: transparent; }
body { background: url(data:image/gif;base64,R0lGODlhAQABAIAAAP8AAAAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==) no-repeat 99% 1px white; }
</style></head><body><p style='color:black'>Test</p></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        var center = bitmap.GetPixel(50, 50);
        _output.WriteLine($"NoRepeat center: R={center.Red} G={center.Green} B={center.Blue}");
        // Should be white, not red tiled
        Assert.True(center.Green > 200 && center.Blue > 200, 
            $"Expected white-ish at center (no-repeat should prevent tiling), got ({center.Red},{center.Green},{center.Blue})");
    }
    
    [Fact]
    public void Diag_Inline_Block_Display()
    {
        // Test if display:inline-block makes elements flow horizontally
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; padding: 0; }
.container { font: 0/0 Arial; }
.container p { display: inline-block; width: 50px; height: 50px; }
#p1 { background: red; }
#p2 { background: lime; }
</style></head><body>
<div class='container'><p id='p1'></p><p id='p2'></p></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        // p1 (red) should be at left, p2 (lime) should be to the right
        var left = bitmap.GetPixel(25, 25);
        var right = bitmap.GetPixel(75, 25);
        _output.WriteLine($"Left (should be red): R={left.Red} G={left.Green} B={left.Blue}");
        _output.WriteLine($"Right (should be green): R={right.Red} G={right.Green} B={right.Blue}");
    }

    [Fact]
    public void Diag_FirstChild_Adjacent_Sibling_Selector()
    {
        // Test :first-child + * selector
        var html = @"<!DOCTYPE html><html><head><style>
* { margin: 0; padding: 0; }
div { font: 0/0 Arial; }
:first-child + * p { display: inline-block; width: 50px; height: 50px; background: lime; }
</style></head><body>
<h1>Title</h1>
<div class='buckets'><p id='b1'></p><p id='b2'></p></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        // The p elements should be inline-block (side by side)
        var at25 = bitmap.GetPixel(25, 25);
        var at75 = bitmap.GetPixel(75, 25);
        _output.WriteLine($"At (25,25): R={at25.Red} G={at25.Green} B={at25.Blue}");
        _output.WriteLine($"At (75,25): R={at75.Red} G={at75.Green} B={at75.Blue}");
    }
}
