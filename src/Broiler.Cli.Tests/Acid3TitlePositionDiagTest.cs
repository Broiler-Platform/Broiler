using Broiler.HTML.Image;
using Xunit;

namespace Broiler.Cli.Tests;

public class Acid3TitlePositionDiagTest
{
    [Fact]
    public void InlineStyle_Plus_Stylesheet_Border_Conflict()
    {
        // This simulates the actual scenario: html has inline style with
        // both border and border-width, AND the <style> block also has
        // the same rules. Does the stylesheet override inline style?
        var html = @"<!DOCTYPE html>
<html style=""font: 20px Arial, sans-serif; border: 2cm solid gray; margin: 1em; width: 32em; background: silver; border-width: 0 0.2em 0.2em 0"">
<style>
* { margin: 0; border: 1px blue; padding: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }
html { font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; }
:root { background: silver; color: black; border-width: 0 0.2em 0.2em 0; }
body { padding: 2em 2em 0; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }
h1:first-child { font-size: 5em; font-weight: bolder; margin-bottom: -0.4em; }
</style>
<body><h1>X</h1></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 800, 300);
        int topDark = FindTopDark(bitmap);
        System.Console.WriteLine($"[Inline + stylesheet conflict] Top dark at y={topDark}");
        // If inline styles win (correct CSS behavior), y should be ~16
        // If stylesheet overrides inline (BUG), y would be ~91
    }

    [Fact]
    public void InlineStyle_Only_No_Stylesheet()
    {
        // Same inline style but NO conflicting stylesheet rules
        var html = @"<!DOCTYPE html>
<html style=""font: 20px Arial, sans-serif; border: 2cm solid gray; margin: 1em; width: 32em; background: silver; border-width: 0 0.2em 0.2em 0"">
<style>
body { padding: 2em 2em 0; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }
h1 { font-size: 5em; font-weight: bolder; }
</style>
<body><h1>X</h1></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 800, 300);
        int topDark = FindTopDark(bitmap);
        System.Console.WriteLine($"[Inline only, no stylesheet conflict] Top dark at y={topDark}");
    }

    private static int FindTopDark(SkiaSharp.SKBitmap bitmap)
    {
        for (int y = 0; y < 250; y++)
        for (int x = 50; x < 400; x++)
        {
            var px = bitmap.GetPixel(x, y);
            if (px.Red < 80 && px.Green < 80 && px.Blue < 80)
                return y;
        }
        return -1;
    }
}
