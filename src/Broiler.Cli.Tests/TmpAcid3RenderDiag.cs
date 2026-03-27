using System;
using System.IO;
using SkiaSharp;
using Broiler.HTML.Image;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class TmpAcid3RenderDiag
{
    private readonly ITestOutputHelper _out;
    public TmpAcid3RenderDiag(ITestOutputHelper output) { _out = output; }

    [Fact]
    public void Diag_Full_Acid3_Issues()
    {
        var html = File.ReadAllText("/home/runner/work/Broiler/Broiler/acid/acid3/acid3.html");
        var postJsHtml = CaptureService.ExecuteScriptsWithDom(html, "file:///acid3/acid3.html");
        postJsHtml = Broiler.App.Rendering.HtmlPostProcessor.Process(postJsHtml);
        
        using var bitmap = HtmlRender.RenderToImageAutoSized(postJsHtml, maxWidth: 800, maxHeight: 600);
        _out.WriteLine($"Size: {bitmap.Width}x{bitmap.Height}");
        
        // Issue 1: Background - check body area (should be white, NOT red)
        _out.WriteLine("=== ISSUE 1: Background ===");
        _out.WriteLine($"(300,20): {bitmap.GetPixel(300,20)}");  // Above buckets
        _out.WriteLine($"(600,20): {bitmap.GetPixel(600,20)}");  // Right side top
        _out.WriteLine($"(600,400): {bitmap.GetPixel(600,400)}"); // Right side bottom
        
        // Issue 2: Check if buckets are inline-block (side by side)
        _out.WriteLine("=== ISSUE 2-4: Bucket layout ===");
        // Scan horizontal line at y=250 (where buckets should be)
        for (int x = 50; x < 650; x += 50)
        {
            var p = bitmap.GetPixel(x, 250);
            _out.WriteLine($"  y=250,x={x}: R={p.Red} G={p.Green} B={p.Blue}");
        }
        
        // Count unique colors at y=250 to detect bucket presence
        var colors = new HashSet<string>();
        for (int x = 0; x < bitmap.Width; x += 5)
        {
            var p = bitmap.GetPixel(x, 250);
            colors.Add($"{p.Red},{p.Green},{p.Blue}");
        }
        _out.WriteLine($"Unique colors at y=250: {colors.Count} -> {string.Join(" | ", colors)}");
        
        // Check if font: 0/0 is parsed (parent container should have font-size 0)
        // Scan for the "Acid3" text area
        _out.WriteLine("=== ISSUE 5: Text position ===");
        for (int y = 30; y < 200; y += 20)
        {
            var p = bitmap.GetPixel(100, y);
            _out.WriteLine($"  x=100,y={y}: R={p.Red} G={p.Green} B={p.Blue}");
        }
    }

    [Fact]
    public void Diag_Border_1px_Blue_Parsing()
    {
        // Test the * rule's "border: 1px blue" - does it parse correctly?
        var html = @"<!DOCTYPE html>
<html><head><style>
* { border: 1px blue; background: transparent; }
body { background: white; }
</style></head>
<body><div style='width:100px;height:100px;background:lime;'>Test</div></body></html>";
        
        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        var bodyPx = bitmap.GetPixel(150, 150);
        var divPx = bitmap.GetPixel(50, 50);
        var borderPx = bitmap.GetPixel(1, 50);  // Left edge of div
        
        _out.WriteLine($"body(150,150): R={bodyPx.Red} G={bodyPx.Green} B={bodyPx.Blue}");
        _out.WriteLine($"div(50,50): R={divPx.Red} G={divPx.Green} B={divPx.Blue}");
        _out.WriteLine($"border(1,50): R={borderPx.Red} G={borderPx.Green} B={borderPx.Blue}");
        
        // Border should be invisible (style defaults to none)
        // Div should be lime
        Assert.True(divPx.Green > 200, $"Div should be lime, got R={divPx.Red} G={divPx.Green} B={divPx.Blue}");
    }

    [Fact]
    public void Diag_Font_Zero_Parsing()
    {
        // Test if "font: 0/0 Arial, sans-serif" is parsed correctly
        var html = @"<!DOCTYPE html>
<html><head><style>
html { font: 20px Arial, sans-serif; }
body { background: white; }
.container { font: 0/0 Arial, sans-serif; background: yellow; }
.container p { display: inline-block; font-size: 20px; width: 50px; height: 50px; background: red; }
</style></head>
<body>
<div class=""container""><p>A</p><p>B</p><p>C</p></div>
</body></html>";
        
        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);
        using var stream = File.Create("/tmp/font_zero_test.png");
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        
        // Check if inline-block elements are side by side with no whitespace gaps
        // At y=25 (middle of elements), scan horizontally
        int redCount = 0;
        int gapCount = 0;
        bool wasRed = false;
        for (int x = 0; x < 400; x++)
        {
            var p = bitmap.GetPixel(x, 25);
            bool isRed = p.Red > 200 && p.Green < 50 && p.Blue < 50;
            if (isRed) { redCount++; wasRed = true; }
            else if (wasRed) { gapCount++; wasRed = false; }
        }
        _out.WriteLine($"Red pixels: {redCount}, Gaps after red: {gapCount}");
        
        // If font: 0/0 works, there should be no whitespace gaps between inline-blocks
        // (the gaps would show as yellow or white pixels between red blocks)
    }
}
