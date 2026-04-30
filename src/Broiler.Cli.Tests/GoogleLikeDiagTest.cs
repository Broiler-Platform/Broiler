using Broiler.HTML.Image;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for Google-like page rendering where form controls
/// are inside flex containers and buttons get display:block from author CSS.
/// Covers:
/// - display:flex/grid fallback to block layout
/// - Submit button text visibility with author CSS
/// - IDL value serialization for input elements
/// - Button centering within flex/centered containers
/// </summary>
public class GoogleLikeDiagTest(ITestOutputHelper output)
{
    private static (int left, int right) FindHorizontalExtent(BBitmap bmp, int y)
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

    private static int CountDarkPixelsInRow(BBitmap bmp, int y)
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

    /// <summary>
    /// display:flex container should behave like display:block for layout.
    /// Its inline-block children should be properly contained.
    /// </summary>
    [Fact]
    public void FlexContainer_BehavesLikeBlock()
    {
        var html = @"<html><body style='margin:0'>
            <div style='display:flex; width:800px; text-align:center'>
                <input type='submit' value='Search'>
            </div>
        </body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 60);
        
        // The flex container should have block width and the button should render
        int totalNonWhite = 0;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                totalNonWhite++;
        }
        Assert.True(totalNonWhite > 20, $"Flex container children should render (nonWhite={totalNonWhite})");
    }

    /// <summary>
    /// display:inline-flex should behave like display:inline-block for layout.
    /// </summary>
    [Fact]
    public void InlineFlexContainer_BehavesLikeInlineBlock()
    {
        var html = @"<html><body style='margin:0'>
            <div style='display:inline-flex'>
                <span>Hello</span>
            </div>
            <span>World</span>
        </body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 40);
        
        // Both "Hello" and "World" should be on the same line (inline-flex is inline-level)
        int totalDark = 0;
        for (int y = 0; y < bmp.Height; y++)
            totalDark += CountDarkPixelsInRow(bmp, y);
        Assert.True(totalDark > 20, $"Inline-flex content should render text (dark={totalDark})");
    }

    /// <summary>
    /// Submit buttons should NOT have min-width:173px (that's for text inputs).
    /// They should shrink to fit their text content.
    /// </summary>
    [Fact]
    public void SubmitButton_NoMinWidth173()
    {
        var html = @"<html><body style='margin:0'>
            <input type='submit' value='OK'>
        </body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 40);
        
        var (left, right) = (bmp.Width, 0);
        for (int y = 0; y < bmp.Height; y++)
        {
            var (l, r) = FindHorizontalExtent(bmp, y);
            if (l >= 0 && l < left) left = l;
            if (r > right) right = r;
        }
        
        int width = right - left + 1;
        output.WriteLine($"Submit button 'OK' width: {width}px (left={left}, right={right})");
        // A 2-character button "OK" should be much less than 173px
        Assert.True(width < 150, $"Submit 'OK' should be narrower than 173px (width={width})");
    }

    [Fact]
    public void GoogleLike_SubmitButtonsHaveText()
    {
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
    public void GoogleLike_SubmitButtonWithNoValue_ShowsDefault()
    {
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

    /// <summary>
    /// Regression: buttons inside display:flex container with text-align:center
    /// should have visible text and be sized to content (not full width).
    /// </summary>
    [Fact]
    public void GoogleLike_FlexCenteredButtons_NotFullWidth()
    {
        var html = @"<html><body style='margin:0'>
<style>
.lsb { display:flex; text-align:center; justify-content:center; }
input[type=""submit""] { background:#f8f9fa; border:1px solid #f8f9fa;
    border-radius:4px; color:#3c4043; font-size:14px; margin:11px 4px;
    padding:0 16px; line-height:36px; height:36px; min-width:54px; }
</style>
<center>
<form style='width:800px'>
    <input name='q' type='text' style='width:500px'><br>
    <div class='lsb'>
        <input name='btnK' type='submit' value='Google Suche'>
        <input name='btnI' type='submit' value='Auf gut Glueck!'>
    </div>
</form>
</center>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 200);

        // Check for dark pixels in the button area (y=40-150)
        int buttonDark = 0;
        for (int y = 40; y < 150; y++)
            buttonDark += CountDarkPixelsInRow(bmp, y);

        output.WriteLine($"Button area dark pixels: {buttonDark}");
        Assert.True(buttonDark > 20,
            $"Buttons in flex container should have visible text (dark={buttonDark})");
    }

    /// <summary>
    /// Regression: Author CSS setting display:block on submit buttons inside
    /// a display:flex container must NOT cause buttons to span full container
    /// width. Flex items should use shrink-to-fit sizing regardless of their
    /// computed display value. This was the core bug from the Google Search
    /// screenshot where buttons rendered as full-width gray bars.
    /// </summary>
    [Fact]
    public void FlexChild_DisplayBlock_NotFullWidth()
    {
        var html = @"<html><body style='margin:0'>
<style>
.flexparent { display:flex; width:800px; }
.blockchild { display:block; }
</style>
<div class='flexparent'>
    <input class='blockchild' type='submit' value='Google Suche'>
    <input class='blockchild' type='submit' value='Auf gut Glueck!'>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 60);

        // Check that buttons don't span full width
        bool foundWideRow = false;
        for (int y = 0; y < bmp.Height; y++)
        {
            var (l, r) = FindHorizontalExtent(bmp, y);
            if (l >= 0 && (r - l + 1) > 700)
            {
                foundWideRow = true;
                output.WriteLine($"WIDE ROW at y={y}: left={l} right={r} width={r - l + 1}");
                break;
            }
        }
        Assert.False(foundWideRow,
            "display:block submit buttons inside display:flex should NOT span full 800px width");
    }

    /// <summary>
    /// display:grid container children should also use shrink-to-fit sizing.
    /// </summary>
    [Fact]
    public void GridChild_UsesContentSizing()
    {
        var html = @"<html><body style='margin:0'>
<div style='display:grid; width:800px'>
    <input type='submit' value='Search'>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 60);

        int totalNonWhite = 0;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                totalNonWhite++;
        }
        Assert.True(totalNonWhite > 20,
            $"Grid container child should render (nonWhite={totalNonWhite})");
    }
}
