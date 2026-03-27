using System;
using Xunit;
using Xunit.Abstractions;
using Broiler.HTML.Image;
using SkiaSharp;

namespace Broiler.Cli.Tests;

public class H1PositionDiagnostics
{
    [Fact]
    public void H1_Text_Position_With_Background()
    {
        var html = @"<!DOCTYPE html>
<html>
<style>
body { margin: 0; padding: 40px; background: white; font: 20px Arial, sans-serif; }
h1 { margin: 0; padding: 0; font-size: 5em; font-weight: bold; line-height: 1.2; background: lightyellow; }
</style>
<body><h1>Acid3</h1></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 800, 300);
        Assert.NotNull(bitmap);
        
        // Find first dark pixel
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red < 50 && px.Green < 50 && px.Blue < 50)
                {
                    // With background, text should be near (40, 40)
                    Assert.True(y < 60, $"H1 with bg: text starts at y={y}, expected y<60 (near body padding 40px)");
                    return;
                }
            }
        }
        Assert.Fail("No dark text found");
    }

    [Fact]
    public void H1_Text_Position_Without_Background()
    {
        var html = @"<!DOCTYPE html>
<html>
<style>
body { margin: 0; padding: 40px; background: white; font: 20px Arial, sans-serif; }
h1 { margin: 0; padding: 0; font-size: 5em; font-weight: bold; line-height: 1.2; }
</style>
<body><h1>Acid3</h1></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 800, 300);
        Assert.NotNull(bitmap);
        
        // Find first dark pixel
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red < 50 && px.Green < 50 && px.Blue < 50)
                {
                    // Without background, text should ALSO be near (40, 40)
                    Assert.True(y < 60, $"H1 without bg: text starts at y={y}, expected y<60 (near body padding 40px). BUG: text displaced down!");
                    return;
                }
            }
        }
        Assert.Fail("No dark text found");
    }
}
