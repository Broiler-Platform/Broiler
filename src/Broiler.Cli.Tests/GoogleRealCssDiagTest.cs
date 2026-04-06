using Broiler.HTML.Image;
using SkiaSharp;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

/// <summary>
/// Test with EXACT Google.de CSS including background:url() on .lsb buttons.
/// The url() points to an image that won't load - tests how the renderer handles this.
/// </summary>
public class GoogleRealCssDiagTest
{
    private readonly ITestOutputHelper _output;
    public GoogleRealCssDiagTest(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void GoogleDe_RealCss_ButtonsVisible()
    {
        // Test 1: With background:url() that fails to load (REAL Google CSS)
        var html = @"<!doctype html><html><head><style>
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
</center>
</body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 400);
        
        using (var data = bmp.Encode(SKEncodedImageFormat.Png, 100))
        using (var f = File.OpenWrite(Path.Combine(Path.GetTempPath(), "google_real_css.png")))
            data.SaveTo(f);
        
        _output.WriteLine($"Saved: {Path.Combine(Path.GetTempPath(), "google_real_css.png")}");
        
        // Check for dark text pixels in button area (y=150-250)
        int btnDark = 0;
        int fullWidthLines = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            int left = bmp.Width, right = 0;
            int dark = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                {
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
                if (px.Red < 80 && px.Green < 80 && px.Blue < 80) dark++;
            }
            int w = (left <= right) ? right - left + 1 : 0;
            if (y >= 150 && y <= 250) btnDark += dark;
            if (w > 700) 
            {
                fullWidthLines++;
                _output.WriteLine($"FULL-WIDTH at y={y}: [{left},{right}] w={w}");
            }
            if (dark > 0 && y >= 100 && y <= 300 && y % 5 == 0)
                _output.WriteLine($"y={y}: [{left},{right}] w={w} dark={dark}");
        }
        _output.WriteLine($"\nButton text pixels (y=150-250): {btnDark}");
        _output.WriteLine($"Full-width lines: {fullWidthLines}");
        
        Assert.True(btnDark > 10, $"Button text missing (dark={btnDark})");
        Assert.True(fullWidthLines < 3, $"Full-width lines ({fullWidthLines})");
    }
    
    [Fact]
    public void GoogleDe_BackgroundUrlParsing()
    {
        // Test that background:url() doesn't break the color property
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
        
        int dark = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 80 && px.Green < 80 && px.Blue < 80) dark++;
            }
        
        _output.WriteLine($"Dark pixels: {dark}");
        Assert.True(dark > 5, $"Button text not visible through background:url() (dark={dark})");
    }
}
