using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class FlexWordsDiagTest(ITestOutputHelper output)
{
    private void ScanBmp(SKBitmap bmp)
    {
        for (int y = 0; y < bmp.Height; y++)
        {
            int redLeft=-1, redRight=-1, blueLeft=-1, blueRight=-1;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                { if (redLeft < 0) redLeft = x; redRight = x; }
                if (px.Red < 50 && px.Green < 50 && px.Blue > 200)
                { if (blueLeft < 0) blueLeft = x; blueRight = x; }
            }
            if (redLeft >= 0 || blueLeft >= 0)
                output.WriteLine($"y={y}: red=[{redLeft},{redRight}] blue=[{blueLeft},{blueRight}]");
        }
    }

    [Fact]
    public void FlexDivExplicitInlineBlock()
    {
        // Force display:inline-block on flex children
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px'>
    <div style='display:inline-block; background:red; padding:10px'><span>Short</span></div>
    <div style='display:inline-block; background:blue; padding:10px; color:white'><span>Longer</span></div>
</div></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 60);
        output.WriteLine("=== Explicit inline-block ===");
        ScanBmp(bmp);
    }

    [Fact]
    public void FlexDivNoDisplay()
    {
        // No display specified - div defaults to block from UA
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px'>
    <div style='background:red; padding:10px'><span>Short</span></div>
    <div style='background:blue; padding:10px; color:white'><span>Longer</span></div>
</div></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 60);
        output.WriteLine("=== No display (default block) ===");
        ScanBmp(bmp);
    }

    [Fact]
    public void FlexDivDisplayBlock()
    {
        // Explicit display:block on children
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px'>
    <div style='display:block; background:red; padding:10px'><span>Short</span></div>
    <div style='display:block; background:blue; padding:10px; color:white'><span>Longer</span></div>
</div></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 60);
        output.WriteLine("=== Explicit display:block ===");
        ScanBmp(bmp);
    }

    [Fact]
    public void FlexEmptyDiv()
    {
        // Empty divs - just backgrounds
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px'>
    <div style='background:red; width:100px; height:30px'></div>
    <div style='background:blue; width:100px; height:30px'></div>
</div></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 60);
        output.WriteLine("=== Empty fixed-size divs ===");
        ScanBmp(bmp);
    }
}
