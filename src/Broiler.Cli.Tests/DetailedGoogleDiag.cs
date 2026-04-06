using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class DetailedGoogleDiag(ITestOutputHelper output)
{
    [Fact]
    public void DiagnoseGrayBars()
    {
        var html = @"<html><body style='margin:0; font-family:arial,sans-serif; font-size:14px'>
<div style='text-align:right; padding:10px'>
    <a href='#'>Gmail</a> <a href='#'>Bilder</a>
    <div><a href='#' style='color:blue'>Anmelden</a></div>
</div>
<center>
<form style='width:600px'>
    <input name='q' type='text' style='width:500px; height:44px; border:1px solid #dfe1e5; border-radius:24px; padding:0 20px; font-size:16px'><br>
    <div style='display:flex; justify-content:center; margin-top:18px'>
        <input type='submit' name='btnK' value='Google Suche' style='background:#f8f9fa; border:1px solid #f8f9fa; border-radius:4px; color:#3c4043; font-size:14px; margin:0 4px; padding:0 16px; height:36px; min-width:54px'>
        <input type='submit' name='btnI' value='Auf gut Glueck!' style='background:#f8f9fa; border:1px solid #f8f9fa; border-radius:4px; color:#3c4043; font-size:14px; margin:0 4px; padding:0 16px; height:36px; min-width:54px'>
    </div>
</form>
</center>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 400);
        
        // Analyze every y line for content
        output.WriteLine("=== Full vertical scan (every 3px) ===");
        for (int y = 0; y < Math.Min(bmp.Height, 300); y += 3)
        {
            int nonWhiteCount = 0;
            int leftMost = -1, rightMost = -1;
            string sampleColor = "";
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 252 || px.Green < 252 || px.Blue < 252)
                {
                    nonWhiteCount++;
                    if (leftMost < 0)
                    {
                        leftMost = x;
                        sampleColor = $"#{px.Red:X2}{px.Green:X2}{px.Blue:X2}";
                    }
                    rightMost = x;
                }
            }
            if (nonWhiteCount > 0)
                output.WriteLine($"y={y:D3}: left={leftMost}, right={rightMost}, w={rightMost-leftMost+1}, nonWhite={nonWhiteCount}, color@left={sampleColor}");
        }
    }

    [Fact]
    public void DiagnoseFormMargins()
    {
        // The <form> element has UA margin: 1.12em 0 which causes vertical spacing
        // and may introduce gray backgrounds in unexpected places
        var html = @"<html><body style='margin:0'>
<form style='width:600px; margin:0'>
    <div style='display:flex; justify-content:center; margin-top:18px'>
        <input type='submit' value='Google Suche' style='background:#f8f9fa; border:1px solid #f8f9fa; color:#3c4043; font-size:14px; margin:0 4px; padding:0 16px; height:36px'>
        <input type='submit' value='Auf gut Glueck!' style='background:#f8f9fa; border:1px solid #f8f9fa; color:#3c4043; font-size:14px; margin:0 4px; padding:0 16px; height:36px'>
    </div>
</form>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        
        output.WriteLine("=== Form with flex buttons scan ===");
        for (int y = 0; y < bmp.Height; y += 2)
        {
            int leftMost = -1, rightMost = -1;
            string sampleColor = "";
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 252 || px.Green < 252 || px.Blue < 252)
                {
                    if (leftMost < 0)
                    {
                        leftMost = x;
                        sampleColor = $"#{px.Red:X2}{px.Green:X2}{px.Blue:X2}";
                    }
                    rightMost = x;
                }
            }
            if (leftMost >= 0)
                output.WriteLine($"y={y:D3}: left={leftMost}, right={rightMost}, w={rightMost-leftMost+1}, color@left={sampleColor}");
        }
        
        // Check no row is wider than 400px
        for (int y = 0; y < bmp.Height; y++)
        {
            int leftMost = bmp.Width, rightMost = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 252 || px.Green < 252 || px.Blue < 252)
                {
                    if (x < leftMost) leftMost = x;
                    if (x > rightMost) rightMost = x;
                }
            }
            if (leftMost < bmp.Width)
            {
                int w = rightMost - leftMost + 1;
                Assert.True(w < 600, $"Row y={y} is too wide: {w}px (buttons should be ~200px each)");
            }
        }
    }

    [Fact]
    public void DiagnoseFlexDivWidth()
    {
        // The flex div itself may be expanding to full width
        var html = @"<html><body style='margin:0'>
<div style='display:flex; justify-content:center; width:auto; background:yellow'>
    <input type='submit' value='Button A' style='background:#f8f9fa; margin:0 4px'>
    <input type='submit' value='Button B' style='background:#f8f9fa; margin:0 4px'>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 60);
        
        output.WriteLine("=== Flex div with auto width ===");
        for (int y = 0; y < bmp.Height; y += 2)
        {
            int yellowCount = 0;
            int grayCount = 0;
            int leftYellow = -1, rightYellow = -1;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Green > 200 && px.Blue < 100) // yellow
                {
                    yellowCount++;
                    if (leftYellow < 0) leftYellow = x;
                    rightYellow = x;
                }
                if (px.Red >= 230 && px.Red <= 250 && px.Green >= 230 && px.Green <= 250 && px.Blue >= 230 && px.Blue <= 250)
                    grayCount++;
            }
            if (yellowCount > 0 || grayCount > 0)
                output.WriteLine($"y={y:D3}: yellow={yellowCount}(l={leftYellow},r={rightYellow}), gray={grayCount}");
        }
    }
}
