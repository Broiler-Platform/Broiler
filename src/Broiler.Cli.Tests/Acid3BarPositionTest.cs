using Broiler.HTML.Image;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class Acid3BarPositionTest
{
    private readonly ITestOutputHelper _output;
    public Acid3BarPositionTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DiagnoseBarPositions_WithImportant()
    {
        // Full Acid3 CSS including the critical !important rule
        var html = @"<!DOCTYPE html>
<html><head>
<style>
  * { margin: 0; border: 1px blue; padding: 0; border-spacing: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }
  html { font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; }
  :root { background: silver; color: black; border-width: 0 0.2em 0.2em 0; }
  body { padding: 2em 2em 0; background: white; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }
  h1:first-child { cursor: help; font-size: 5em; font-weight: bolder; margin-bottom: -0.4em; }
  .buckets { font: 0/0 Arial, sans-serif; }
  .buckets { padding: 0 0 150px 3px; }
  :first-child + * .buckets p { display: inline-block; vertical-align: 2em; border: 2em dotted red; padding: 1.0em 0 1.0em 2em; }
  * + * > * > p { margin: 0; border: 1px solid !important; }
  #bucket1 { font-size: 20px; margin-left: 0.2em; padding-left: 1.3em; padding-right: 1.3em; margin-right: 0.0001px; }
  #bucket2 { font-size: 24px; margin-left: 0.375em; padding-left: 30px; padding-right: 32px; margin-right: 2px; }
  #bucket3 { font-size: 28px; margin-left: 8.9999px; padding-left: 17px; padding-right: 55px; margin-right: 12px; }
  #bucket4 { font-size: 32px; margin-left: 0; padding-left: 84px; padding-right: 0; margin-right: 0; }
  #bucket5 { font-size: 36px; margin-left: 13px; padding-left: 0; padding-right: 94px; margin-right: 25px; }
  #bucket6 { font-size: 40px; margin-left: -10px; padding-left: 104px; padding-right: -10px; }
  #bucket1 { background: red; }
  #bucket2 { background: orange; }
  #bucket3 { background: yellow; }
  #bucket4 { background: lime; }
  #bucket5 { background: blue; }
  #bucket6 { background: purple; }
  #result { font-weight: bolder; width: 5.68em; text-align: right; }
  #result { font-size: 5em; margin: -2.19em 0 0; }
</style>
</head>
<body>
  <h1>Acid3</h1>
  <div class='buckets'><p id='bucket1'></p><p id='bucket2'></p><p id='bucket3'></p><p id='bucket4'></p><p id='bucket5'></p><p id='bucket6'></p></div>
  <p id='result'><span id='score'>100</span>/100</p>
</body>
</html>";

        using var bmp = HtmlRender.RenderToImage(html, 1024, 768);

        var colors = new Dictionary<string, (int minY, int maxY, int minX, int maxX)>();
        
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var p = bmp.GetPixel(x, y);
                string colorName = null;
                if (p.Red > 200 && p.Green < 80 && p.Blue < 80) colorName = "red";
                else if (p.Red > 200 && p.Green > 120 && p.Green < 200 && p.Blue < 80) colorName = "orange";
                else if (p.Red > 200 && p.Green > 200 && p.Blue < 80) colorName = "yellow";
                else if (p.Red < 80 && p.Green > 200 && p.Blue < 80) colorName = "lime";
                else if (p.Red < 80 && p.Green < 80 && p.Blue > 200) colorName = "blue-bar";
                else if (p.Red > 80 && p.Red < 200 && p.Green < 30 && p.Blue > 80 && p.Blue < 200) colorName = "purple";
                
                if (colorName != null)
                {
                    if (!colors.ContainsKey(colorName))
                        colors[colorName] = (y, y, x, x);
                    else
                    {
                        var c = colors[colorName];
                        colors[colorName] = (Math.Min(c.minY, y), Math.Max(c.maxY, y), Math.Min(c.minX, x), Math.Max(c.maxX, x));
                    }
                }
            }
        }

        _output.WriteLine("=== Colored bar positions (WITH border !important override) ===");
        foreach (var kv in colors.OrderBy(x => x.Value.minY))
        {
            int w = kv.Value.maxX - kv.Value.minX + 1;
            int h = kv.Value.maxY - kv.Value.minY + 1;
            _output.WriteLine($"  {kv.Key}: rows {kv.Value.minY}-{kv.Value.maxY} ({h}px), cols {kv.Value.minX}-{kv.Value.maxX} ({w}px)");
        }

        _output.WriteLine("\n=== Reference positions (from Acid3 spec) ===");
        _output.WriteLine("  All bars: rows 19-152 (133px)");
        _output.WriteLine("  Expected score at: y~230");
        _output.WriteLine("  Body content edge: y~57");
        
        int maxBarBottom = 0, minBarTop = int.MaxValue;
        foreach (var kv in colors)
        {
            if (kv.Value.maxY > maxBarBottom) maxBarBottom = kv.Value.maxY;
            if (kv.Value.minY < minBarTop) minBarTop = kv.Value.minY;
        }
        if (colors.Count > 0)
            _output.WriteLine($"\n  All bars span: rows {minBarTop}-{maxBarBottom} ({maxBarBottom - minBarTop + 1}px total)");
    }
}
