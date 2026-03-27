using System;
using Xunit;
using Xunit.Abstractions;
using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

public class Acid3FullCssTest
{
    private readonly ITestOutputHelper _output;
    public Acid3FullCssTest(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void Acid3_Full_CSS_Height()
    {
        // Use exact Acid3 CSS rules
        var html = @"<!DOCTYPE html>
<html>
<style type=""text/css"">
  * { margin: 0; border: 1px blue; padding: 0; border-spacing: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }
  html { font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; }
  :root { background: silver; color: black; border-width: 0 0.2em 0.2em 0; }
  body { padding: 2em 2em 0; background: white; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }
  .buckets { font: 0/0 Arial, sans-serif; }
  .buckets { padding: 0 0 150px 3px; }
  :first-child + * .buckets p { display: inline-block; vertical-align: 2em; border: 2em dotted red; padding: 1.0em 0 1.0em 2em; }
  * + * > * > p { margin: 0; border: 1px solid ! important; }
  .z { visibility: hidden; }
  #bucket1 { font-size: 20px; margin-left: 0.2em; padding-left: 1.3em; padding-right: 1.3em; margin-right: 0.0001px; }
  #bucket1.zPPPPPPPPPPPPPPPP { background: red; }
</style>
<body>
<h1>Acid3</h1>
<div class=""buckets""><p id=""bucket1"" class=""zPPPPPPPPPPPPPPPP""></p></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 800, 600);
        
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
        int w = maxX - minX + 1;
        int h = maxY - minY + 1;
        _output.WriteLine($"Bucket1: ({minX},{minY})-({maxX},{maxY}) = {w}x{h}");
        _output.WriteLine($"Expected: ~52x42 (52 wide = border1+padding26+padding26+border1, 42 high = border1+padding20+padding20+border1)");
        _output.WriteLine($"Height of 64 would mean extra ~22px (one em of padding somewhere)");
    }
}
