using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

/// <summary>Diagnostic test to understand Google-like rendering issues.</summary>
public class GoogleLikeDiagTest(ITestOutputHelper output)
{
    private static (int left, int right) FindHorizontalExtent(SKBitmap bmp, int y)
    {
        int left = -1, right = -1;
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
            {
                if (left < 0) left = x;
                right = x;
            }
        }
        return (left, right);
    }

    private static int CountDarkPixelsInRow(SKBitmap bmp, int y)
    {
        int count = 0;
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 100 && px.Green < 100 && px.Blue < 100)
                count++;
        }
        return count;
    }

    [Fact]
    public void GoogleLike_AuthorBlockButtons_ShouldNotSpanFullWidth()
    {
        // Google's CSS sets display:block on submit buttons, making them 
        // full-width. This mimics the scenario in the screenshot.
        var html = @"<html><body style='margin:0'>
            <center style='width:800px'>
                <form>
                    <input type='text' name='q' style='width:400px'><br><br>
                    <input type='submit' value='Google Suche'>
                    <input type='submit' value='Auf gut Glueck!'>
                </form>
            </center>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 150);

        // Dump row info
        for (int y = 0; y < bmp.Height; y += 2)
        {
            var (l, r) = FindHorizontalExtent(bmp, y);
            if (l >= 0)
            {
                int dark = CountDarkPixelsInRow(bmp, y);
                output.WriteLine($"y={y}: left={l} right={r} width={r - l + 1} dark={dark}");
            }
        }

        // The submit buttons (somewhere below the text input) must NOT span 800px
        // Find the button row (below y=30 approximately)
        bool foundWideRow = false;
        for (int y = 30; y < bmp.Height; y++)
        {
            var (l, r) = FindHorizontalExtent(bmp, y);
            if (l >= 0 && (r - l + 1) > 700)
            {
                foundWideRow = true;
                output.WriteLine($"WIDE ROW at y={y}: left={l} right={r} width={r - l + 1}");
            }
        }
        Assert.False(foundWideRow, "Submit buttons should not span the full 800px width");
    }

    [Fact]
    public void GoogleLike_ButtonsWithExplicitBlockDisplay()
    {
        // What happens when author CSS explicitly overrides inline-block with block?
        var html = @"<html><body style='margin:0'>
            <center style='width:800px'>
                <input type='submit' value='Google Suche' style='display:block'>
            </center>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 60);

        for (int y = 0; y < bmp.Height; y += 2)
        {
            var (l, r) = FindHorizontalExtent(bmp, y);
            if (l >= 0)
            {
                int dark = CountDarkPixelsInRow(bmp, y);
                output.WriteLine($"y={y}: left={l} right={r} width={r - l + 1} dark={dark}");
            }
        }
    }

    [Fact]
    public void GoogleLike_SubmitButtonsHaveText()
    {
        // Verify button text is visible
        var html = @"<html><body style='margin:0'>
            <input type='submit' value='Google Suche'>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 400, 40);

        int totalDark = 0;
        for (int y = 0; y < bmp.Height; y++)
            totalDark += CountDarkPixelsInRow(bmp, y);

        output.WriteLine($"Total dark pixels: {totalDark}");
        Assert.True(totalDark > 20, $"Submit button should have visible text (dark={totalDark})");
    }
    
    [Fact]
    public void GoogleLike_SubmitButtonWithNoValue()
    {
        // What if submit button has no value attribute? Should show "Submit"
        var html = @"<html><body style='margin:0'>
            <input type='submit'>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 400, 40);

        int totalDark = 0;
        for (int y = 0; y < bmp.Height; y++)
            totalDark += CountDarkPixelsInRow(bmp, y);

        output.WriteLine($"Total dark pixels (no value): {totalDark}");
        Assert.True(totalDark > 20, $"Submit button with no value should show 'Submit' text (dark={totalDark})");
    }

    [Fact]
    public void GoogleLike_FullPageWithCSS()
    {
        // Closer to the actual Google page scenario
        var html = @"<html><body style='margin:0'>
<style>
.lsb { text-align:center }
.gNO89b { display:block }
input[type=""submit""] { background:#f8f9fa; border:1px solid #f8f9fa; 
    border-radius:4px; color:#3c4043; font-size:14px; margin:11px 4px; 
    padding:0 16px; line-height:36px; height:36px; min-width:54px; }
</style>
<center>
<form style='width:800px'>
    <input name='q' type='text' style='width:500px'><br>
    <div class='lsb'>
        <input class='gNO89b' name='btnK' type='submit' value='Google Suche'>
        <input class='gNO89b' name='btnI' type='submit' value='Auf gut Glueck!'>
    </div>
</form>
</center>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 200);

        // Dump all rows
        for (int y = 0; y < bmp.Height; y += 2)
        {
            var (l, r) = FindHorizontalExtent(bmp, y);
            if (l >= 0)
            {
                int dark = CountDarkPixelsInRow(bmp, y);
                output.WriteLine($"y={y}: left={l} right={r} width={r - l + 1} dark={dark}");
            }
        }

        // After ~y=20 (text input), check the submit buttons aren't full-width
        bool foundWideRow = false;
        for (int y = 25; y < bmp.Height; y++)
        {
            var (l, r) = FindHorizontalExtent(bmp, y);
            if (l >= 0 && (r - l + 1) > 750)
            {
                foundWideRow = true;
                break;
            }
        }
        
        // Check for dark pixels in the button area (y=30-100)
        int buttonDark = 0;
        for (int y = 30; y < 100; y++)
            buttonDark += CountDarkPixelsInRow(bmp, y);
        
        output.WriteLine($"Button area dark pixels: {buttonDark}");
        output.WriteLine($"Found wide row: {foundWideRow}");
    }
}
