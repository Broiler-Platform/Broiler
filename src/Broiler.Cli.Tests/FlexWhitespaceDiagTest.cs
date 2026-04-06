using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class FlexWhitespaceDiagTest(ITestOutputHelper output)
{
    private void ScanBmp(SKBitmap bmp, string label)
    {
        output.WriteLine($"=== {label} ===");
        int firstRed = -1, firstBlue = -1;
        for (int y = 0; y < bmp.Height; y++)
        {
            bool hasRed = false, hasBlue = false;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50) hasRed = true;
                if (px.Red < 50 && px.Green < 50 && px.Blue > 200) hasBlue = true;
            }
            if (hasRed && firstRed < 0) firstRed = y;
            if (hasBlue && firstBlue < 0) firstBlue = y;
        }
        output.WriteLine($"  First red: y={firstRed}, First blue: y={firstBlue}, diff={firstBlue-firstRed}");
    }

    [Fact]
    public void NoWhitespace_Between()
    {
        // No whitespace at all between the divs
        var html = "<html><body style='margin:0'><div style='display:flex; width:600px'><div style='background:red; padding:10px'><span>Short</span></div><div style='background:blue; padding:10px; color:white'><span>Longer</span></div></div></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        ScanBmp(bmp, "No whitespace");
    }

    [Fact]
    public void WithWhitespace_Between()
    {
        // Whitespace between divs (newlines + spaces)
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px'>
    <div style='background:red; padding:10px'><span>Short</span></div>
    <div style='background:blue; padding:10px; color:white'><span>Longer</span></div>
</div></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        ScanBmp(bmp, "With whitespace");
    }

    [Fact]
    public void InlineBlock_Children()
    {
        // Explicit inline-block on children (should work in normal inline flow)
        var html = @"<html><body style='margin:0'>
<div style='width:600px'>
    <div style='display:inline-block; background:red; padding:10px'><span>Short</span></div>
    <div style='display:inline-block; background:blue; padding:10px; color:white'><span>Longer</span></div>
</div></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        ScanBmp(bmp, "Normal inline-block (no flex)");
    }

    [Fact]
    public void FlexWithInlineBlockChildren()
    {
        // Flex parent with inline-block children
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px'>
    <div style='display:inline-block; background:red; padding:10px'><span>Short</span></div>
    <div style='display:inline-block; background:blue; padding:10px; color:white'><span>Longer</span></div>
</div></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        ScanBmp(bmp, "Flex with inline-block children");
    }
}
