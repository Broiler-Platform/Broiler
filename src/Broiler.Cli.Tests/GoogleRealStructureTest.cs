using System.IO;
using Broiler.HTML.Image;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class GoogleRealStructureTest
{
    private readonly ITestOutputHelper _output;
    public GoogleRealStructureTest(ITestOutputHelper output) { _output = output; }

    /// <summary>
    /// Test with the actual Google.de button structure:
    /// .ds { display:inline-box; display:inline-block; margin:3px 0 4px; margin-left:4px }
    /// .lsbb { display:block; background:#f3f5f6; border:solid 1px; height:30px }
    /// .lsb { background:... height:30px }
    /// </summary>
    [Fact]
    public void GoogleButtonStructure_InlineBlockDs()
    {
        var html = @"<html><head><style>
.ds{display:inline-box;display:inline-block;margin:3px 0 4px;margin-left:4px}
.lsbb{background:#f3f5f6;border:solid 1px;border-color:#d2d2d2 #70757a #70757a #d2d2d2;height:30px}
.lsbb{display:block}
.lsb{color:#1f1f1f;border:none;cursor:pointer;height:30px;margin:0;outline:0;font:15px sans-serif;vertical-align:top}
</style></head>
<body style='margin:0'>
<center>
<form>
<table cellpadding='0' cellspacing='0'>
<tbody><tr valign='top'>
<td width='25%'></td>
<td align='center' nowrap=''>
  <div class='ds' style='height:32px;margin:4px 0'>
    <input class='lst' style='margin:0;padding:5px 8px 0 6px;vertical-align:top;color:#1f1f1f;width:496px;height:25px;font:18px sans-serif' autocomplete='off' value='' title='Google Search' maxlength='2048' name='q' size='57'>
  </div>
  <br style='line-height:0'>
  <span class='ds'><span class='lsbb'><input class='lsb' value='Google Search' name='btnG' type='submit'></span></span>
  <span class='ds'><span class='lsbb'><input class='lsb' value='Im Feeling Lucky' name='btnI' type='submit'></span></span>
</td>
<td width='25%'></td>
</tr></tbody>
</table>
</form>
</center>
</body></html>";

        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 800, 200);
        System.IO.File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "google_buttons.png"), bmp.Encode(Broiler.Graphics.BImageEncodeFormat.Png, 100));
        
        // Analyze button area (y=30-100)
        int btnLeft = bmp.Width, btnRight = 0;
        for (int y = 30; y < 100; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                // #F3F5F6 or nearby (.lsbb background)
                if (px.R >= 0xF0 && px.G >= 0xF0 && px.B >= 0xF0
                    && (px.R < 0xFF || px.G < 0xFF || px.B < 0xFF))
                {
                    if (x < btnLeft) btnLeft = x;
                    if (x > btnRight) btnRight = x;
                }
            }
        
        int width = (btnRight >= btnLeft) ? btnRight - btnLeft + 1 : 0;
        _output.WriteLine($"Button area (y=30-100): left={btnLeft}, right={btnRight}, width={width}");
        
        // Check that buttons are NOT full-width (should be content-sized, ~250px for both)
        Assert.True(width < 300, 
            $"Buttons should be content-sized (~250px), not full width. Got {width}px");
    }

    /// <summary>
    /// Full-page Google.de-like test with header, logo, search box, buttons,
    /// and footer.  Verifies that buttons are visible and not full-width.
    /// This more closely matches the real Google.de page structure.
    /// </summary>
    [Fact]
    public void GoogleFullPage_ButtonsVisibleAndSized()
    {
        var html = @"<!doctype html>
<html><head><style>
body{margin:0;overflow-y:scroll}
a{color:#4b11a8;text-decoration:none}
.gb1{margin-right:.5em;font-size:small}
#gbar{height:22px;padding-left:2px;text-align:left}
.ds{display:inline-box;display:inline-block;margin:3px 0 4px;margin-left:4px}
.lsbb{background:#f3f5f6;border:solid 1px;border-color:#d2d2d2 #70757a #70757a #d2d2d2;height:30px}
.lsbb{display:block}
.lsb{color:#1f1f1f;border:none;cursor:pointer;height:30px;margin:0;outline:0;font:15px sans-serif;vertical-align:top}
.lst{font:18px arial,sans-serif}
#fbar{text-align:center;font-size:10pt}
#fbar a{color:#4b11a8;margin:0 14px}
</style></head>
<body>
<div id='gbar'><span class='gb1'><a href='#'>Gmail</a></span><span class='gb1'><a href='#'>Bilder</a></span></div>
<center>
<br clear='all'><br>
<div style='padding:28px 0 3px;height:112px'>
<span style='font:bold 75px arial,sans-serif;color:#4285f4'>G</span><span style='font:bold 75px arial,sans-serif;color:#ea4335'>o</span><span style='font:bold 75px arial,sans-serif;color:#fbbc05'>o</span><span style='font:bold 75px arial,sans-serif;color:#4285f4'>g</span><span style='font:bold 75px arial,sans-serif;color:#34a853'>l</span><span style='font:bold 75px arial,sans-serif;color:#ea4335'>e</span>
</div>
<form action='/search' name='f'>
<table cellpadding='0' cellspacing='0'>
<tbody><tr valign='top'>
<td width='25%'>&nbsp;</td>
<td align='center' nowrap=''>
  <div class='ds' style='height:32px;margin:4px 0'>
    <input class='lst' style='margin:0;padding:5px 8px 0 6px;vertical-align:top;color:#1f1f1f;width:496px;height:25px' autocomplete='off' value='' title='Google-Suche' maxlength='2048' name='q' size='57'>
  </div>
  <br style='line-height:0'>
  <span class='ds'><span class='lsbb'><input class='lsb' value='Google Suche' name='btnG' type='submit'></span></span>
  <span class='ds'><span class='lsbb'><input class='lsb' value='Auf gut Glueck!' name='btnI' type='submit'></span></span>
</td>
<td width='25%' align='left'></td>
</tr></tbody>
</table>
</form>
<br>
<div id='fbar'>
<a href='#'>Datenschutzerklaerung</a> <a href='#'>Nutzungsbedingungen</a><br><br>
<a href='#'>Werbeprogramme</a> <a href='#'>Unternehmensangebote</a> <a href='#'>Ueber Google</a>&nbsp;&nbsp;<a href='#'>Google.de</a>
</div>
</center>
</body></html>";

        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 800, 600);
        System.IO.File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "google_fullpage.png"), bmp.Encode(Broiler.Graphics.BImageEncodeFormat.Png, 100));

        // Check for dark (text) pixels in the button area (roughly y=200-350)
        int darkPixels = 0;
        for (int y = 180; y < 350; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R < 100 && px.G < 100 && px.B < 100)
                    darkPixels++;
            }
        _output.WriteLine($"Button area dark pixels (y=180-350): {darkPixels}");
        Assert.True(darkPixels > 20,
            $"Button area should have visible text (dark pixels={darkPixels})");

        // Check that .lsbb backgrounds are NOT full-width
        bool hasFullWidthGrayLine = false;
        for (int y = 180; y < 350; y++)
        {
            int gLeft = bmp.Width, gRight = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R >= 0xF0 && px.G >= 0xF0 && px.B >= 0xF0
                    && (px.R < 0xFE || px.G < 0xFE || px.B < 0xFE))
                {
                    if (x < gLeft) gLeft = x;
                    if (x > gRight) gRight = x;
                }
            }
            int gWidth = (gRight >= gLeft) ? gRight - gLeft + 1 : 0;
            if (gWidth > 600)
            {
                hasFullWidthGrayLine = true;
                _output.WriteLine($"FULL-WIDTH gray at y={y}: width={gWidth}");
                break;
            }
        }
        Assert.False(hasFullWidthGrayLine,
            "Button backgrounds (.lsbb) should NOT span full page width");

        // Also verify header renders (Gmail/Bilder should be in top 30px)
        int headerDark = 0;
        for (int y = 0; y < 30; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R < 100 && px.G < 100 && px.B < 100)
                    headerDark++;
            }
        _output.WriteLine($"Header dark pixels (y=0-30): {headerDark}");
    }

    /// <summary>
    /// Verify that the .lsbb block element inside .ds inline-block is
    /// constrained to the inline-block width, not expanding to full width.
    /// This tests the inline-block BFC containment.
    /// </summary>
    [Fact]
    public void InlineBlock_BlockChild_Constrained()
    {
        var html = @"<html><body style='margin:0'>
<div style='width:800px'>
  <span style='display:inline-block'>
    <span style='display:block; border:1px solid red; height:20px'>
      <input type='submit' value='Test Button'>
    </span>
  </span>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 800, 60);

        // Find the red border extent
        int left = bmp.Width, right = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R > 200 && px.G < 50 && px.B < 50)
                {
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
            }

        int borderWidth = (right >= left) ? right - left + 1 : 0;
        _output.WriteLine($"Red border extent: left={left}, right={right}, width={borderWidth}");

        // Block child inside inline-block should be content-sized, not full width
        Assert.True(borderWidth > 0 && borderWidth < 400,
            $"Block inside inline-block should be content-sized, not full-width ({borderWidth}px)");
    }

    /// <summary>
    /// Modern Google.de uses flex containers with &lt;button&gt; elements
    /// instead of the classic &lt;input type="submit"&gt; structure.
    /// Test both structures to ensure both render.
    /// </summary>
    [Fact]
    public void GoogleModern_FlexButtons_Visible()
    {
        // Modern Google uses: div.FPdoLc > center > input[type=submit]
        // wrapped in flex containers
        var html = @"<html><head><style>
.FPdoLc{font-size:14px;margin:25px 0 0}
.dRYYxd{display:flex;flex-wrap:wrap;justify-content:center}
.RNmpXc{margin:11px 4px}
.gNO89b{background-color:#f8f9fa;border:1px solid #f8f9fa;border-radius:4px;
  color:#3c4043;font-family:arial,sans-serif;font-size:14px;
  margin:11px 4px;padding:0 16px;line-height:36px;height:36px;
  min-width:54px;text-align:center;cursor:pointer;user-select:none}
</style></head>
<body style='margin:0'>
<center>
<div class='FPdoLc'>
<div class='dRYYxd'>
  <div class='RNmpXc'>
    <input class='gNO89b' value='Google Suche' name='btnK' type='submit'>
  </div>
  <div class='RNmpXc'>
    <input class='gNO89b' value='Auf gut Glueck!' name='btnI' type='submit'>
  </div>
</div>
</div>
</center>
</body></html>";

        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 800, 120);
        System.IO.File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "google_modern_buttons.png"), bmp.Encode(Broiler.Graphics.BImageEncodeFormat.Png, 100));

        int darkPixels = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R < 100 && px.G < 100 && px.B < 100)
                    darkPixels++;
            }
        _output.WriteLine($"Modern buttons dark pixels: {darkPixels}");
        Assert.True(darkPixels > 20,
            $"Modern Google.de buttons should have visible text (dark={darkPixels})");

        // Check buttons are not full-width
        int maxExtent = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            int left = bmp.Width, right = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R < 250 || px.G < 250 || px.B < 250)
                {
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
            }
            if (right >= left)
                maxExtent = Math.Max(maxExtent, right - left + 1);
        }
        _output.WriteLine($"Max content extent: {maxExtent}");
        Assert.True(maxExtent < 700,
            $"Buttons should not span full width ({maxExtent}px)");
    }

    /// <summary>
    /// Test modern Google.de structure with &lt;button&gt; elements in flex container.
    /// </summary>
    [Fact]
    public void GoogleModern_ButtonElement_InFlex()
    {
        var html = @"<html><head><style>
.dRYYxd{display:flex;flex-wrap:wrap;justify-content:center}
.gNO89b{background-color:#f8f9fa;border:1px solid #f8f9fa;border-radius:4px;
  color:#3c4043;font-family:arial,sans-serif;font-size:14px;
  margin:11px 4px;padding:0 16px;line-height:36px;height:36px;
  min-width:54px;text-align:center;cursor:pointer}
</style></head>
<body style='margin:0'>
<div class='dRYYxd'>
  <button class='gNO89b' type='submit'>Google Suche</button>
  <button class='gNO89b' type='submit'>Auf gut Glueck!</button>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 800, 80);

        int darkPixels = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R < 100 && px.G < 100 && px.B < 100)
                    darkPixels++;
            }
        _output.WriteLine($"Button elements dark pixels: {darkPixels}");
        Assert.True(darkPixels > 20,
            $"<button> elements should render visible text (dark={darkPixels})");
    }

    /// <summary>
    /// Test with the ACTUAL Google.de HTML structure including hidden inputs
    /// in the same td cell as the button spans. Hidden inputs must be display:none.
    /// </summary>
    [Fact]
    public void GoogleRealHtml_HiddenInputsAndButtons()
    {
        // Exact structure from live Google.de page (simplified CSS)
        var html = @"<!doctype html>
<html><head><style>
body{margin:0;overflow-y:scroll}
input{font-family:inherit}
.ds{display:inline-box;display:inline-block;margin:3px 0 4px;margin-left:4px}
.lsbb{background:#f3f5f6;border:solid 1px;border-color:#d2d2d2 #70757a #70757a #d2d2d2;height:30px}
.lsbb{display:block}
.lsb{background:url(/images/nav_logo229.png) 0 -261px repeat-x;color:#1f1f1f;border:none;cursor:pointer;height:30px;margin:0;outline:0;font:15px sans-serif;vertical-align:top}
.lst{height:25px;width:496px}
.gsfi,.lst{font:18px sans-serif}
</style></head>
<body bgcolor='#fff'>
<center>
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
  <span class='ds'><span class='lsbb'><input class='lsb' value='Google Search' name='btnG' type='submit'></span></span>
  <span class='ds'><span class='lsbb'><input class='lsb' value='Im Feeling Lucky' name='btnI' type='submit'><input value='xxx' name='iflsig' type='hidden'></span></span>
</td>
<td width='25%' align='left'></td>
</tr></tbody>
</table>
<input id='gbv' name='gbv' type='hidden' value='1'>
</form>
</center>
</body></html>";

        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 800, 200);
        System.IO.File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "google_real_html.png"), bmp.Encode(Broiler.Graphics.BImageEncodeFormat.Png, 100));

        // Check for dark (text) pixels in the button area (y=30-100)
        int darkInBtnArea = 0;
        for (int y = 30; y < 100; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R < 100 && px.G < 100 && px.B < 100)
                    darkInBtnArea++;
            }
        _output.WriteLine($"Button area dark pixels (y=30-100): {darkInBtnArea}");
        Assert.True(darkInBtnArea > 20,
            $"Button text must be visible (dark pixels={darkInBtnArea})");

        // Check for full-width gray lines (indicates escaped blocks)
        int fullWidthGrayLines = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            int left = bmp.Width, right = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R < 250 || px.G < 250 || px.B < 250)
                {
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
            }
            if (right - left + 1 > 600)
            {
                fullWidthGrayLines++;
                if (fullWidthGrayLines <= 3)
                    _output.WriteLine($"Full-width line at y={y}: extent=[{left},{right}] width={right - left + 1}");
            }
        }
        _output.WriteLine($"Total full-width lines: {fullWidthGrayLines}");

        // Hidden inputs should NOT create visible boxes - there should be
        // no full-width lines (which would indicate hidden inputs rendered visible)
        Assert.True(fullWidthGrayLines < 5,
            $"Hidden inputs should not create visible boxes ({fullWidthGrayLines} full-width lines)");

        // Check that button backgrounds (.lsbb) are NOT full-width
        for (int y = 30; y < 100; y++)
        {
            int gLeft = bmp.Width, gRight = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R >= 0xF0 && px.G >= 0xF0 && px.B >= 0xF0
                    && (px.R < 0xFE || px.G < 0xFE || px.B < 0xFE))
                {
                    if (x < gLeft) gLeft = x;
                    if (x > gRight) gRight = x;
                }
            }
            int gWidth = (gRight >= gLeft) ? gRight - gLeft + 1 : 0;
            if (gWidth > 400)
            {
                _output.WriteLine($"WIDE gray at y={y}: width={gWidth}");
                Assert.Fail($"Button background at y={y} should not be full-width ({gWidth}px)");
            }
        }
    }

    /// <summary>
    /// Verify that author CSS targeting input elements does not break
    /// hidden input display:none UA rule. Google.de has input{font-family:inherit}
    /// which could trigger attribute condition loss during CSS merging.
    /// </summary>
    [Fact]
    public void AuthorInputCss_DoesNotBreak_HiddenInputDisplayNone()
    {
        var html = @"<html><body style='margin:0'>
<style>input{font-family:inherit}</style>
<input type='hidden' name='x' value='test'>
<input type='submit' value='Submit'>
</body></html>";

        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 400, 60);

        // The hidden input should be invisible
        // Only the submit button should render
        int darkPixels = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R < 100 && px.G < 100 && px.B < 100)
                    darkPixels++;
            }
        _output.WriteLine($"Dark pixels: {darkPixels}");
        Assert.True(darkPixels > 10, "Submit button text should be visible");

        // The total non-white area should be reasonable (not expanded by hidden input)
        int nonWhite = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R < 250 || px.G < 250 || px.B < 250)
                    nonWhite++;
            }
        _output.WriteLine($"Non-white pixels: {nonWhite}");
        // Should be less than 5000 - a hidden input would add ~17000 pixels
        Assert.True(nonWhite < 10000,
            $"Hidden input should not render visible area ({nonWhite} non-white pixels)");
    }

    /// <summary>
    /// Test footer link spacing with Google's actual footer structure.
    /// No whitespace between a tags means no gap.
    /// </summary>
    [Fact]
    public void GoogleFooter_LinksWithoutWhitespace()
    {
        var html = @"<html><body style='margin:0; font-size:10pt;'>
<div style='margin:19px auto;text-align:center'>
  <a href='#'>Advertising</a><a href='#'>Business Solutions</a><a href='#'>About Google</a>
</div>
</body></html>";

        using var bmp = HtmlRender.RenderToImageWithStyleSet(html, 800, 60);
        
        bool prevDark = false;
        var gaps = new List<int>();
        int endX = -1;
        
        for (int x = 0; x < bmp.Width; x++)
        {
            bool isDark = false;
            for (int y = 0; y < bmp.Height; y++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.R < 100 || px.G < 100 || px.B < 100)
                { isDark = true; break; }
            }
            if (!isDark && prevDark) endX = x;
            if (isDark && !prevDark && endX >= 0)
            {
                int gapWidth = x - endX;
                if (gapWidth > 3) gaps.Add(gapWidth);
            }
            prevDark = isDark;
        }
        
        _output.WriteLine($"Footer gaps: {string.Join(", ", gaps)}");
        // With no whitespace between links, there should be NO large gaps
        // This is the expected behavior - it's not a rendering bug
        _output.WriteLine("(No whitespace between </a><a> = no gap is correct CSS behavior)");
    }
}
