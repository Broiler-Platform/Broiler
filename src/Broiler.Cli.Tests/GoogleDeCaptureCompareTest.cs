using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

/// <summary>
/// Capture/compare tests for Google.de rendering.
/// These tests render the exact HTML/CSS structure from www.google.de and
/// verify the output matches expected pixel-level characteristics:
///   - Buttons visible with text (not missing)
///   - No full-width gray lines (no BFC escape)
///   - Search box centered with borders
///   - Hidden inputs invisible
///   - Proper centering of controls
/// </summary>
public class GoogleDeCaptureCompareTest
{
    private readonly ITestOutputHelper _output;
    public GoogleDeCaptureCompareTest(ITestOutputHelper output) { _output = output; }

    /// <summary>
    /// Google.de HTML including the REAL CSS with background:url() on .lsb
    /// buttons. The url() cannot be loaded, but button text must still render.
    /// Also includes author CSS <c>input{font-family:inherit}</c> which must
    /// not break hidden input <c>display:none</c>.
    /// </summary>
    private const string GoogleDeHtml = @"<!doctype html><html><head><style>
body{margin:0;overflow-y:scroll}
input{font-family:inherit}
.ds{display:inline-box;display:inline-block;margin:3px 0 4px;margin-left:4px}
.lsbb{background:#f3f5f6;border:solid 1px;border-color:#d2d2d2 #70757a #70757a #d2d2d2;height:30px}
.lsbb{display:block}
.lsb{background:url(/images/nav_logo229.png) 0 -261px repeat-x;color:#1f1f1f;border:none;cursor:pointer;height:30px;margin:0;outline:0;font:15px sans-serif;vertical-align:top}
.lst{height:25px;width:496px}
.gsfi,.lst{font:18px sans-serif}
</style></head><body bgcolor='#fff'>
<center>
<div><span style='font-size:48px;font-weight:bold;color:#4285f4'>G</span><span style='font-size:48px;font-weight:bold;color:#ea4335'>o</span><span style='font-size:48px;font-weight:bold;color:#fbbc05'>o</span><span style='font-size:48px;font-weight:bold;color:#4285f4'>g</span><span style='font-size:48px;font-weight:bold;color:#34a853'>l</span><span style='font-size:48px;font-weight:bold;color:#ea4335'>e</span><br><br></div>
<form action='/search' name='f'>
<table cellpadding='0' cellspacing='0'>
<tr valign='top'>
<td width='25%'>&nbsp;</td>
<td align='center' nowrap=''>
<input name='ie' value='ISO-8859-1' type='hidden'>
<input value='en' name='hl' type='hidden'>
<input name='source' type='hidden' value='hp'>
<input name='biw' type='hidden'>
<input name='bih' type='hidden'>
<div class='ds' style='height:32px;margin:4px 0'>
<input class='lst' style='margin:0;padding:5px 8px 0 6px;vertical-align:top;color:#1f1f1f' autocomplete='off' value='' title='Google Search' maxlength='2048' name='q' size='57'>
</div>
<br style='line-height:0'>
<span class='ds'><span class='lsbb'><input class='lsb' value='Google Suche' name='btnG' type='submit'></span></span>
<span class='ds'><span class='lsbb'><input class='lsb' value='Auf gut Glueck!' name='btnI' type='submit'><input value='xxx' name='iflsig' type='hidden'></span></span>
</td>
<td class='fl' align='left' nowrap='' width='25%'><a href='/advanced_search'>Erweiterte Suche</a></td>
</tr>
</table>
<input id='gbv' name='gbv' type='hidden' value='1'>
</form>
<div style='font-size:83%;min-height:3.5em'><br></div>
<span id='footer'>
<div style='font-size:10pt'>
<div style='margin:19px auto;text-align:center'>
<a href='/intl/de/ads/'>Werbeprogramme</a>
<a href='/services/'>Unternehmensangebote</a>
<a href='/intl/de/about.html'>Ueber Google</a>
<a href='https://www.google.de'>Google.de</a>
</div>
</div>
</span>
</center>
</body></html>";

    /// <summary>
    /// Helper: count pixels where text colour is visible (dark enough to read).
    /// </summary>
    private static int CountDarkPixels(BBitmap bmp, int yFrom, int yTo)
    {
        int count = 0;
        for (int y = yFrom; y < yTo && y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 128 && px.Green < 128 && px.Blue < 128) count++;
            }
        return count;
    }

    /// <summary>
    /// Helper: count rows where non-white content spans more than threshold width.
    /// </summary>
    private static int CountFullWidthLines(BBitmap bmp, int threshold)
    {
        int count = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            int left = bmp.Width, right = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                {
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
            }
            if (right - left + 1 > threshold) count++;
        }
        return count;
    }

    /// <summary>
    /// Helper: find the bounding box of a region with gray background (#f0-f7)
    /// which corresponds to the .lsbb button wrappers.
    /// </summary>
    private static (int top, int bottom, int left, int right) FindGrayRegion(
        BBitmap bmp, int yFrom, int yTo)
    {
        int top = -1, bottom = -1, left = bmp.Width, right = 0;
        for (int y = yFrom; y < yTo && y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red >= 0xF0 && px.Red <= 0xF8
                    && px.Green >= 0xF0 && px.Green <= 0xF8
                    && px.Blue >= 0xF0 && px.Blue <= 0xF8
                    && !(px.Red == 0xFF && px.Green == 0xFF && px.Blue == 0xFF))
                {
                    if (top < 0) top = y;
                    bottom = y;
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
            }
        return (top, bottom, left, right);
    }

    [Fact]
    public void Capture_ButtonsVisible()
    {
        using var bmp = HtmlRender.RenderToImage(GoogleDeHtml, 800, 500);

        // Scan entire image for dark text pixels in the button region (lower half)
        int dark = CountDarkPixels(bmp, bmp.Height / 4, bmp.Height * 3 / 4);
        _output.WriteLine($"Dark pixels in middle half: {dark}");
        Assert.True(dark > 20,
            $"Buttons must render visible text (dark pixels={dark})");
    }

    [Fact]
    public void Capture_NoFullWidthGrayLines()
    {
        using var bmp = HtmlRender.RenderToImage(GoogleDeHtml, 800, 500);

        int fullWidth = CountFullWidthLines(bmp, 700);
        _output.WriteLine($"Full-width lines (>700px): {fullWidth}");
        Assert.Equal(0, fullWidth);
    }

    [Fact]
    public void Capture_ButtonsContentSized()
    {
        using var bmp = HtmlRender.RenderToImage(GoogleDeHtml, 800, 500);

        // Find button gray background region
        var (top, bottom, left, right) = FindGrayRegion(bmp, 50, bmp.Height);
        int width = (left <= right) ? right - left + 1 : 0;
        _output.WriteLine($"Button region: y=[{top},{bottom}] x=[{left},{right}] w={width}");

        Assert.True(width > 50 && width < 400,
            $"Buttons must be content-sized (w={width}), " +
            "not full-width (>700) or invisible (0)");
    }

    [Fact]
    public void Capture_ButtonsCentered()
    {
        using var bmp = HtmlRender.RenderToImage(GoogleDeHtml, 800, 500);

        var (_, _, left, right) = FindGrayRegion(bmp, 50, bmp.Height);
        if (left > right) { Assert.Fail("No button region found"); return; }

        int center = (left + right) / 2;
        int viewportCenter = bmp.Width / 2;
        int offset = System.Math.Abs(center - viewportCenter);
        _output.WriteLine($"Button center: {center}, viewport center: {viewportCenter}, offset: {offset}");

        Assert.True(offset < 80,
            $"Buttons must be centered (offset={offset}px from viewport center)");
    }

    [Fact]
    public void Capture_SearchBoxBordersVisible()
    {
        using var bmp = HtmlRender.RenderToImage(GoogleDeHtml, 800, 500);

        // The search input (.lst) has UA border: 1px solid #767676 and width:496px.
        // Look for horizontal rows with non-white pixels spanning > 300px
        // in the area above the buttons (top half of image).
        int wideNonWhiteRows = 0;
        for (int y = 0; y < bmp.Height / 2; y++)
        {
            int left = bmp.Width, right = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 240 || px.Green < 240 || px.Blue < 240)
                {
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
            }
            int w = (left <= right) ? right - left + 1 : 0;
            if (w > 300) wideNonWhiteRows++;
        }
        _output.WriteLine($"Search box wide non-white rows: {wideNonWhiteRows}");
        Assert.True(wideNonWhiteRows >= 2,
            $"Search box borders must be visible (found {wideNonWhiteRows} wide rows)");
    }

    [Fact]
    public void Capture_HiddenInputsInvisible()
    {
        // Render ONLY hidden inputs — they must produce zero visible pixels
        var html = @"<html><body>
<input name='ie' value='ISO-8859-1' type='hidden'>
<input value='en' name='hl' type='hidden'>
<input name='source' type='hidden' value='hp'>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        int nonWhite = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250) nonWhite++;
            }
        _output.WriteLine($"Hidden input non-white pixels: {nonWhite}");
        Assert.Equal(0, nonWhite);
    }

    [Fact]
    public void Capture_BackgroundUrlDoesNotBreakButtonText()
    {
        // background:url() with failed image load must not prevent text rendering
        var html = @"<html><body>
<style>
.btn { background:url(/images/nav_logo229.png) 0 -261px repeat-x;
       color:#1f1f1f; border:none; height:30px; font:15px sans-serif; }
</style>
<div style='display:inline-block;background:#f3f5f6;border:1px solid #d2d2d2'>
  <input class='btn' type='submit' value='Test Button'>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        int dark = CountDarkPixels(bmp, 0, bmp.Height);
        _output.WriteLine($"Button dark pixels: {dark}");
        Assert.True(dark > 5,
            $"Button text must be visible despite failed background:url() (dark={dark})");
    }

    [Fact]
    public void Capture_GoogleLogoTextVisible()
    {
        using var bmp = HtmlRender.RenderToImage(GoogleDeHtml, 800, 500);

        // The colored "Google" text uses large font (48px) with specific colors.
        // Check for colored pixels in the logo area (top quarter of image).
        int colored = 0;
        for (int y = 0; y < bmp.Height / 4; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                // Look for the Google brand colors (blue, red, yellow, green)
                bool isBlue = px.Blue > 200 && px.Red < 100 && px.Green < 150;
                bool isRed = px.Red > 200 && px.Green < 100 && px.Blue < 100;
                bool isYellow = px.Red > 200 && px.Green > 150 && px.Blue < 100;
                bool isGreen = px.Green > 150 && px.Red < 100 && px.Blue < 100;
                if (isBlue || isRed || isYellow || isGreen) colored++;
            }
        _output.WriteLine($"Google logo colored pixels: {colored}");
        Assert.True(colored > 50,
            $"Google logo text must render in brand colors (colored={colored})");
    }

    [Fact]
    public void Capture_FooterLinksVisible()
    {
        using var bmp = HtmlRender.RenderToImage(GoogleDeHtml, 800, 600);

        // First, find button bottom by looking for the gray button region
        var (_, btnBottom, _, _) = FindGrayRegion(bmp, 50, bmp.Height);
        int scanStart = btnBottom > 0 ? btnBottom + 20 : bmp.Height / 3;

        // Footer links should be below buttons
        int nonWhite = 0;
        for (int y = scanStart; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250) nonWhite++;
            }
        _output.WriteLine($"Footer non-white pixels (y>{scanStart}): {nonWhite}");
        Assert.True(nonWhite > 50,
            $"Footer links must be visible below buttons (nonWhite={nonWhite})");
    }
}
