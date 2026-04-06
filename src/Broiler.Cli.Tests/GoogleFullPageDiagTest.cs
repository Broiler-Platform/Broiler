using Broiler.HTML.Image;
using SkiaSharp;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

/// <summary>
/// Diagnostic test with COMPLETE Google.de HTML from live fetch.
/// This reproduces the exact rendering the user sees.
/// </summary>
public class GoogleFullPageDiagTest
{
    private readonly ITestOutputHelper _output;
    public GoogleFullPageDiagTest(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void GoogleDe_FullPage_ButtonsAndAlignment()
    {
        // This is the EXACT HTML structure from a Google.de fetch, with:
        // - Complete header bar (Gmail/Bilder/Anmelden)
        // - Google logo (as text since image won't load)
        // - Form with table layout
        // - Hidden inputs in same td as buttons
        // - .ds/.lsbb/.lsb CSS classes
        // - Full footer structure
        var html = @"<!doctype html>
<html>
<head>
<style>
body{margin:0;overflow-y:scroll}
input{font-family:inherit}
.ds{display:inline-box;display:inline-block;margin:3px 0 4px;margin-left:4px}
.lsbb{background:#f3f5f6;border:solid 1px;border-color:#d2d2d2 #70757a #70757a #d2d2d2;height:30px}
.lsbb{display:block}
.lsb{background:transparent;color:#1f1f1f;border:none;cursor:pointer;height:30px;margin:0;outline:0;font:15px sans-serif;vertical-align:top}
.lst{height:25px;width:496px}
.gsfi,.lst{font:18px sans-serif}
.gb_Ha{background-color:#fff}
.gb_Z{color:#1f1f1f;display:inline-block;font-size:13px;padding:0 6px;text-decoration:none}
.gb_ce{background:#1a73e8;border:1px solid transparent;border-radius:4px;color:#fff;display:inline-block;font-size:14px;padding:0 24px;line-height:36px;text-decoration:none}
.fl{font-size:80%}
</style>
</head>
<body bgcolor='#fff'>
<div style='padding:6px'>
  <div class='gb_Ha' id='gb'>
    <div>
      <div><a class='gb_Z' href='https://mail.google.com'>Gmail</a><a class='gb_Z' href='https://www.google.com/imghp'>Bilder</a></div>
    </div>
    <div>
      <a class='gb_ce' href='https://accounts.google.com/ServiceLogin'>Anmelden</a>
    </div>
  </div>
</div>
<center>
<br clear='all' id='lgpd'>
<div>
  <span style='font-size:48px;font-weight:bold;color:#4285f4'>G</span><span style='font-size:48px;font-weight:bold;color:#ea4335'>o</span><span style='font-size:48px;font-weight:bold;color:#fbbc05'>o</span><span style='font-size:48px;font-weight:bold;color:#4285f4'>g</span><span style='font-size:48px;font-weight:bold;color:#34a853'>l</span><span style='font-size:48px;font-weight:bold;color:#ea4335'>e</span>
  <br><br>
</div>
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
    <div style='margin:19px auto;text-align:center' id='WqQANb'>
      <a href='/intl/de/ads/'>Werbeprogramme</a>
      <a href='/services/'>Unternehmensangebote</a>
      <a href='/intl/de/about.html'>Ueber Google</a>
      <a href='https://www.google.de'>Google.de</a>
    </div>
  </div>
  <p style='font-size:8pt;color:#636363'>&copy; 2026 - <a href='/intl/de/policies/privacy/'>Datenschutzerklaerung</a> - <a href='/intl/de/policies/terms/'>Nutzungsbedingungen</a></p>
</span>
</center>
</body>
</html>";

        using var bmp = HtmlRender.RenderToImage(html, 800, 600);
        
        // Save to temp for visual inspection
        using (var data = bmp.Encode(SKEncodedImageFormat.Png, 100))
        using (var f = File.OpenWrite(Path.Combine(Path.GetTempPath(), "google_fullpage_diag.png")))
            data.SaveTo(f);
        
        _output.WriteLine($"Image saved to {Path.Combine(Path.GetTempPath(), "google_fullpage_diag.png")}");
        
        // ===== ROW-BY-ROW ANALYSIS =====
        _output.WriteLine("\n=== ROW ANALYSIS (every 5th row) ===");
        for (int y = 0; y < bmp.Height; y += 5)
        {
            int left = bmp.Width, right = 0;
            int dark = 0;
            bool hasGrayBg = false;
            bool hasBorder = false;
            bool hasBlue = false;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                {
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
                if (px.Red < 80 && px.Green < 80 && px.Blue < 80) dark++;
                if (px.Red >= 0xF0 && px.Green >= 0xF3 && px.Blue >= 0xF3
                    && px.Red <= 0xF5 && px.Green <= 0xF7 && px.Blue <= 0xF9) hasGrayBg = true;
                if (px.Red == 0xD2 && px.Green == 0xD2 && px.Blue == 0xD2) hasBorder = true;
                if (px.Blue > 200 && px.Red < 100 && px.Green < 100) hasBlue = true;
            }
            if (left <= right)
            {
                int w = right - left + 1;
                var desc = "";
                if (dark > 2) desc += " TEXT";
                if (hasGrayBg) desc += " GRAY_BG";
                if (hasBorder) desc += " BORDER";
                if (hasBlue) desc += " BLUE";
                if (w > 600) desc += " FULLWIDTH!";
                _output.WriteLine($"y={y,3}: [{left,3},{right,3}] w={w,3} dark={dark,3}{desc}");
            }
        }
        
        // ===== BUTTON AREA ANALYSIS (y=200-400 where buttons should be) =====
        _output.WriteLine("\n=== BUTTON AREA (y=200-350, every 2nd row) ===");
        int totalBtnDark = 0;
        for (int y = 200; y < 350; y += 2)
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
            totalBtnDark += dark;
            if (left <= right)
            {
                int w = right - left + 1;
                _output.WriteLine($"y={y,3}: [{left,3},{right,3}] w={w,3} dark={dark,3}");
            }
        }
        _output.WriteLine($"\nTotal dark pixels in button area: {totalBtnDark}");
        
        // ===== FULL-WIDTH LINE CHECK =====
        int fullWidthLines = 0;
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
            if (right - left + 1 > 700)
            {
                fullWidthLines++;
                if (fullWidthLines <= 5)
                    _output.WriteLine($"FULL-WIDTH LINE at y={y}: [{left},{right}] w={right-left+1}");
            }
        }
        _output.WriteLine($"\nFull-width lines: {fullWidthLines}");
        
        // ===== ASSERTIONS =====
        Assert.True(totalBtnDark > 20,
            $"Buttons must render visible text in area y=200-350 (dark={totalBtnDark})");
        Assert.True(fullWidthLines < 5,
            $"No full-width gray lines (found {fullWidthLines})");
    }
}
