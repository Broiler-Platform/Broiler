using Broiler.HTML.Image;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class FlexColumnDiag(ITestOutputHelper output)
{
    [Fact]
    public void FlexDirectionColumn_DiagImage()
    {
        var html = @"<html><body style='margin:0'>
<div style='display:flex; flex-direction:column; width:600px; background:yellow'>
    <input type='submit' value='Button A' style='background:#f0f0f0'>
    <input type='submit' value='Button B' style='background:#cccccc'>
</div>
</body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 80);
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.File.WriteAllBytes("/tmp/flex_col.png", data.ToArray());

        // Detailed pixel scan
        for (int y = 0; y < bmp.Height; y++)
        {
            var colors = new Dictionary<string, int>();
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                var c = $"#{px.Red:X2}{px.Green:X2}{px.Blue:X2}";
                colors.TryGetValue(c, out int cnt);
                colors[c] = cnt + 1;
            }
            var nonWhite = colors.Where(kv => kv.Key != "#FFFFFF").OrderByDescending(kv => kv.Value).Take(3);
            if (nonWhite.Any())
            {
                var desc = string.Join(", ", nonWhite.Select(kv => $"{kv.Key}:{kv.Value}px"));
                output.WriteLine($"y={y:D3}: {desc}");
            }
        }
    }
    
    [Fact]
    public void FlexRow_TwoButtons()
    {
        // Same as column but without flex-direction - should be side-by-side
        var html = @"<html><body style='margin:0'>
<div style='display:flex; width:600px; background:yellow'>
    <input type='submit' value='Button A' style='background:#f0f0f0'>
    <input type='submit' value='Button B' style='background:#cccccc'>
</div>
</body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 80);
        
        for (int y = 0; y < bmp.Height; y++)
        {
            int leftA = -1, rightA = -1, leftB = -1, rightB = -1;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red == 0xF0 && px.Green == 0xF0 && px.Blue == 0xF0)
                {
                    if (leftA < 0) leftA = x;
                    rightA = x;
                }
                if (px.Red == 0xCC && px.Green == 0xCC && px.Blue == 0xCC)
                {
                    if (leftB < 0) leftB = x;
                    rightB = x;
                }
            }
            if (leftA >= 0 || leftB >= 0)
                output.WriteLine($"y={y:D3}: A=[{leftA},{rightA}] w={rightA-leftA+1}, B=[{leftB},{rightB}] w={rightB-leftB+1}");
        }
    }
}
