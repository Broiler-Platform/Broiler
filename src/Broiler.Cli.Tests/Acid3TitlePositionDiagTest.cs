using Broiler.HTML.Image;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for the :root pseudo-class selector in the rendering engine.
/// The Acid3 test page uses :root { border-width: 0 0.2em 0.2em 0; } to
/// override the html { border: 2cm solid gray; } rule. Without :root
/// support, the html element retains a 2cm (~76px) top border, pushing
/// the "Acid3" title approximately 76px too far down.
/// </summary>
public class Acid3TitlePositionDiagTest
{
    [Fact]
    public void Root_Selector_Overrides_Html_Border_Top()
    {
        // Acid3 CSS: html { border: 2cm solid gray } 
        //            :root { border-width: 0 0.2em 0.2em 0 }
        // The :root rule should override border-top-width to 0
        var html = @"<!DOCTYPE html><html>
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

        // Without :root support, title was at y~91 (2cm border).
        // With :root support, title should be at y~16 (no top border).
        Assert.True(topDark > 0, "Should find dark title text");
        Assert.True(topDark < 30, $"Title should start before y=30 (found y={topDark}). " +
            "The :root selector must override html border-top-width to 0.");
    }

    [Fact]
    public void Root_Selector_Does_Not_Match_Non_Html()
    {
        // :root should only match the html element, not body or div.
        // A :root { color: red } rule should color html red but body/div
        // should inherit.
        var html = @"<!DOCTYPE html><html>
<style>
* { margin: 0; padding: 0; border: none; }
html { font: 20px Arial, sans-serif; }
:root { background: red; }
body { background: white; }
div { background: white; width: 50px; height: 50px; }
</style>
<body><div>X</div></body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        // The div at (0,0) should be white, not red
        var px = bitmap.GetPixel(25, 25);
        Assert.True(px.Red > 200 && px.Green > 200 && px.Blue > 200,
            $"div should be white, found ({px.Red},{px.Green},{px.Blue})");
    }

    private static int FindTopDark(BBitmap bitmap)
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
