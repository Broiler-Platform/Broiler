using System;
using Xunit;
using Xunit.Abstractions;
using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

public class PaddingShorthandTest
{
    private readonly ITestOutputHelper _output;
    public PaddingShorthandTest(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void Padding_Shorthand_Height_Check()
    {
        // padding: 1.0em 0 1.0em 2em means top=1em right=0 bottom=1em left=2em
        // After bucket-specific override: padding-left: 1.3em; padding-right: 1.3em;
        // So: top=1em right=1.3em bottom=1em left=1.3em
        // At font-size 20px: top=20 right=26 bottom=20 left=26
        // Height = 1px border + 20px top + 20px bottom + 1px border = 42px
        var html = @"<!DOCTYPE html>
<html><head><style>
* { margin: 0; padding: 0; border: 0 none; }
body { font: 20px Arial, sans-serif; }
.buckets { font: 0/0 Arial, sans-serif; padding: 0 0 150px 3px; }
.buckets p { display: inline-block; vertical-align: 2em; border: 2em dotted red; padding: 1.0em 0 1.0em 2em; }
.buckets > p { margin: 0; border: 1px solid; }
#bucket1 { font-size: 20px; padding-left: 1.3em; padding-right: 1.3em; background: red; }
</style></head>
<body>
<div class=""buckets""><p id=""bucket1""></p></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);
        
        int minY = int.MaxValue, maxY = 0;
        int minX = int.MaxValue, maxX = 0;
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
        _output.WriteLine($"Bucket: ({minX},{minY})-({maxX},{maxY}) = {maxX-minX+1}x{maxY-minY+1}");
        _output.WriteLine($"Expected height: 1+20+20+1=42px");
    }
}
