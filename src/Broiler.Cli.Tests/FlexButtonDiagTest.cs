using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class FlexButtonDiagTest(ITestOutputHelper output)
{
    [Fact]
    public void DiagGoogleButtons()
    {
        // Google.de uses display:flex on button wrapper and display:block on buttons
        var html = @"<html><body style='margin:0'>
<style>
.lsb { display:flex; }
.gNO89b { display:block; background:#f8f9fa; border:1px solid #f8f9fa;
    border-radius:4px; color:#3c4043; font-size:14px; margin:11px 4px;
    padding:0 16px; line-height:36px; height:36px; min-width:54px; }
</style>
<form style='width:600px'>
    <div class='lsb'>
        <input class='gNO89b' name='btnK' type='submit' value='Google Suche'>
        <input class='gNO89b' name='btnI' type='submit' value='Auf gut Glueck!'>
    </div>
</form>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 200);
        
        // Scan for gray button bars
        for (int y = 0; y < bmp.Height; y += 2)
        {
            int grayCount = 0;
            int leftmost = -1, rightmost = -1;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red >= 240 && px.Green >= 240 && px.Blue >= 240
                    && px.Red < 255 && Math.Abs(px.Red - px.Green) < 5)
                {
                    grayCount++;
                    if (leftmost < 0) leftmost = x;
                    rightmost = x;
                }
            }
            if (grayCount > 20)
                output.WriteLine($"y={y}: gray={grayCount}px, left={leftmost}, right={rightmost}, width={rightmost-leftmost+1}");
        }
        
        // Scan for dark text
        for (int y = 0; y < bmp.Height; y += 2)
        {
            int darkCount = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 80 && px.Green < 80 && px.Blue < 80)
                    darkCount++;
            }
            if (darkCount > 3)
                output.WriteLine($"y={y} DARK: {darkCount}px");
        }
    }

    [Fact]
    public void DiagFlexChildren_ShrinkToFit()
    {
        // Flex parent with display:block children - should use shrink-to-fit
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px; background:yellow'>
    <div style='display:block; background:red; padding:10px'>Short</div>
    <div style='display:block; background:blue; padding:10px; color:white'>Longer text here</div>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        
        // Check if children are side-by-side (shrink-to-fit) or stacked (full-width)
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
            {
                output.WriteLine($"y={y}: BOTH red and blue on same row - side by side!");
            }
        }
        output.WriteLine($"First red row: {redRow}, First blue row: {blueRow}");
        
        // They should be on the same row if flex is working
        Assert.True(Math.Abs(redRow - blueRow) < 5, 
            $"Flex children should be roughly on same row. Red={redRow}, Blue={blueRow}");
    }
}
