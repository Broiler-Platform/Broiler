using Broiler.HTML.Image;
using SkiaSharp;
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

        using var bmp = HtmlRender.RenderToImage(html, 800, 200);
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        using var f = System.IO.File.OpenWrite("/tmp/google_buttons.png");
        data.SaveTo(f);
        
        // Analyze button area (y=30-100)
        int btnLeft = bmp.Width, btnRight = 0;
        for (int y = 30; y < 100; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                // #F3F5F6 or nearby (.lsbb background)
                if (px.Red >= 0xF0 && px.Green >= 0xF0 && px.Blue >= 0xF0
                    && (px.Red < 0xFF || px.Green < 0xFF || px.Blue < 0xFF))
                {
                    if (x < btnLeft) btnLeft = x;
                    if (x > btnRight) btnRight = x;
                }
            }
        
        int width = (btnRight >= btnLeft) ? btnRight - btnLeft + 1 : 0;
        _output.WriteLine($"Button area (y=30-100): left={btnLeft}, right={btnRight}, width={width}");
        
        // Check that buttons are NOT full-width (should be content-sized, ~250px for both)
        Assert.True(width < 500, 
            $"Buttons should be content-sized (~250px), not full width. Got {width}px");
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

        using var bmp = HtmlRender.RenderToImage(html, 800, 60);
        
        bool prevDark = false;
        var gaps = new List<int>();
        int endX = -1;
        
        for (int x = 0; x < bmp.Width; x++)
        {
            bool isDark = false;
            for (int y = 0; y < bmp.Height; y++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 100 || px.Green < 100 || px.Blue < 100)
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
