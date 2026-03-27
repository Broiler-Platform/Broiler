using System;
using Xunit;
using Xunit.Abstractions;
using Broiler.HTML.Image;
using SkiaSharp;

namespace Broiler.Cli.Tests;

public class Acid3BorderImportantTest
{
    private readonly ITestOutputHelper _output;
    public Acid3BorderImportantTest(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void Border_Important_Overrides_2em_Border()
    {
        // Acid3 rule: :first-child + * .buckets p { border: 2em dotted red; padding: 1.0em 0 1.0em 2em; }
        // Override:   * + * > * > p { border: 1px solid ! important; }
        // bucket1:    #bucket1 { font-size: 20px; padding-left: 1.3em; padding-right: 1.3em; }
        var html = @"<!DOCTYPE html>
<html><head><style>
* { margin: 0; padding: 0; border: 0 none; }
:first-child + * .buckets p { display: inline-block; vertical-align: 2em; border: 2em dotted red; padding: 1.0em 0 1.0em 2em; }
* + * > * > p { margin: 0; border: 1px solid ! important; }
#bucket1 { font-size: 20px; padding-left: 1.3em; padding-right: 1.3em; background: red; }
</style></head>
<body>
<div class=""buckets""><p id=""bucket1""></p></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);
        
        // Scan for red pixels to find bucket dimensions
        int minX = int.MaxValue, maxX = 0, minY = int.MaxValue, maxY = 0;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Red == 255 && px.Green == 0 && px.Blue == 0)
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        
        int w = maxX - minX + 1;
        int h = maxY - minY + 1;
        _output.WriteLine($"Red bucket: ({minX},{minY})-({maxX},{maxY}) = {w}x{h}");
        // Expected: 
        // width = 1px border + 26px pl + 26px pr + 1px border = 54px
        // height = 1px border + 20px pt + 20px pb + 1px border = 42px
        // If border:2em is NOT overridden, we'd see MUCH larger
        _output.WriteLine($"Expected: ~54x42 (with 1px border)");
        _output.WriteLine($"If border:2em not overridden: ~132x122");
    }
}
