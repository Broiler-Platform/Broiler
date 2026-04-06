using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class FlexFlowDiagTest(ITestOutputHelper output)
{
    [Fact]
    public void FlexRow_ChildrenWithSpan_SideBySide()
    {
        // Use span children inside divs to avoid direct text on block elements
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px; background:yellow'>
    <div style='background:red; padding:10px'><span>Short</span></div>
    <div style='background:blue; padding:10px; color:white'><span>Longer text</span></div>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        int redRow = -1, blueRow = -1;
        for (int y = 0; y < bmp.Height; y++)
        {
            bool hasRed = false, hasBlue = false;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50) hasRed = true;
                if (px.Red < 50 && px.Green < 50 && px.Blue > 200) hasBlue = true;
            }
            if (hasRed && redRow < 0) redRow = y;
            if (hasBlue && blueRow < 0) blueRow = y;
            if (hasRed && hasBlue)
                output.WriteLine($"y={y}: BOTH red and blue on same row!");
        }
        output.WriteLine($"First red: y={redRow}, First blue: y={blueRow}");
    }

    [Fact]
    public void FlexRow_InputButtons_SideBySide()
    {
        // Submit buttons inside flex container
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px; background:yellow'>
    <input type='submit' value='Button One' style='background:red; padding:10px'>
    <input type='submit' value='Button Two' style='background:blue; color:white; padding:10px'>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        int redRow = -1, blueRow = -1;
        for (int y = 0; y < bmp.Height; y++)
        {
            bool hasRed = false, hasBlue = false;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50) hasRed = true;
                if (px.Red < 50 && px.Green < 50 && px.Blue > 200) hasBlue = true;
            }
            if (hasRed && redRow < 0) redRow = y;
            if (hasBlue && blueRow < 0) blueRow = y;
            if (hasRed && hasBlue)
                output.WriteLine($"y={y}: BOTH red and blue on same row!");
        }
        output.WriteLine($"First red: y={redRow}, First blue: y={blueRow}");
    }

    [Fact]
    public void FlexRow_DirectTextDiv_WordsCheck()
    {
        // Check if <div>text</div> has words directly on it or on a child box
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px'>
    <div style='display:block; background:red'>Hello</div>
    <div style='display:block; background:blue; color:white'>World</div>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 60);
        // Scan every row for red and blue
        for (int y = 0; y < bmp.Height; y++)
        {
            int redLeft = -1, redRight = -1, blueLeft = -1, blueRight = -1;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                {
                    if (redLeft < 0) redLeft = x;
                    redRight = x;
                }
                if (px.Red < 50 && px.Green < 50 && px.Blue > 200)
                {
                    if (blueLeft < 0) blueLeft = x;
                    blueRight = x;
                }
            }
            if (redLeft >= 0 || blueLeft >= 0)
                output.WriteLine($"y={y}: red=[{redLeft},{redRight}] blue=[{blueLeft},{blueRight}]");
        }
    }
}
