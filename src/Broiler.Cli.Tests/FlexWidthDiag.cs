using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class FlexWidthDiag(ITestOutputHelper output)
{
    private void DumpRows(SKBitmap bmp, string label)
    {
        output.WriteLine($"=== {label} ===");
        for (int y = 0; y < bmp.Height; y += 3)
        {
            int left = -1, right = -1;
            string color = "";
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 252 || px.Green < 252 || px.Blue < 252)
                {
                    if (left < 0) { left = x; color = $"#{px.Red:X2}{px.Green:X2}{px.Blue:X2}"; }
                    right = x;
                }
            }
            if (left >= 0)
                output.WriteLine($"y={y:D3}: L={left}, R={right}, W={right-left+1}, color={color}");
        }
    }
    
    [Fact]
    public void FlexChild_Width100Pct()
    {
        // Google often uses width:100% on buttons inside flex containers
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px'>
    <input type='submit' value='Button' style='width:100%; background:#f0f0f0'>
</div>
</body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 40);
        DumpRows(bmp, "width:100% button in flex");
    }
    
    [Fact]
    public void FlexChild_WidthAuto()
    {
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px'>
    <input type='submit' value='Button' style='width:auto; background:#f0f0f0'>
</div>
</body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 40);
        DumpRows(bmp, "width:auto button in flex");
    }

    [Fact]
    public void FlexChild_MaxWidth()
    {
        // max-width should constrain the button
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px'>
    <input type='submit' value='This is a long button label' style='max-width:200px; background:#f0f0f0'>
</div>
</body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 40);
        DumpRows(bmp, "max-width:200px button in flex");
    }
    
    [Fact]
    public void FlexChild_NestedCenterBlock()
    {
        // Google wraps buttons in <center> which is display:block
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px'>
    <center>
        <input type='submit' value='Google Suche' style='background:#f8f9fa'>
        <input type='submit' value='Auf gut Glueck!' style='background:#f8f9fa'>
    </center>
</div>
</body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 60);
        DumpRows(bmp, "buttons in <center> in flex");
    }

    [Fact]
    public void FlexDirection_Column()
    {
        // flex-direction:column should stack children vertically
        var html = @"<html><body style='margin:0'>
<div style='display:flex; flex-direction:column; width:600px; background:yellow'>
    <input type='submit' value='Button A' style='background:#f0f0f0'>
    <input type='submit' value='Button B' style='background:#f0f0f0'>
</div>
</body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 80);
        DumpRows(bmp, "flex-direction:column");
    }
    
    [Fact]
    public void FlexChild_BlockDiv_Width100()
    {
        // div with width:100% inside flex
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px'>
    <div style='display:block; width:100%; background:#f0f0f0; padding:10px'>Block div 100%</div>
</div>
</body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 40);
        DumpRows(bmp, "block div width:100% in flex");
    }

    [Fact]
    public void FlexChild_InDiv_Width100()
    {
        // Buttons inside a div that has width:100% inside flex
        var html = @"<html><body style='margin:0'>
<div style='display:flex; flex-direction:column; align-items:center; width:600px'>
    <div style='width:100%; text-align:center; background:lightyellow'>
        <input type='submit' value='Google Suche' style='background:#f8f9fa; padding:0 16px; height:36px'>
        <input type='submit' value='Auf gut Glueck!' style='background:#f8f9fa; padding:0 16px; height:36px'>
    </div>
</div>
</body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 80);
        DumpRows(bmp, "buttons in div(width:100%) in flex(column)");
    }
}
