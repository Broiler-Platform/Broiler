using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class GapAnalysisTest
{
    private readonly ITestOutputHelper _output;
    public GapAnalysisTest(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void AnalyzeGap()
    {
        var path = "/tmp/google_cli_capture.png";
        if (!System.IO.File.Exists(path)) { _output.WriteLine("No file"); return; }
        using var bmp = SKBitmap.Decode(path);
        
        _output.WriteLine("=== ALL non-white rows in y=0-600 ===");
        for (int y = 0; y < bmp.Height; y++)
        {
            int left = bmp.Width, right = 0;
            int nonWhite = 0;
            int dark = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                {
                    nonWhite++;
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
                if (px.Red < 128 && px.Green < 128 && px.Blue < 128) dark++;
            }
            if (nonWhite > 0)
            {
                int w = right - left + 1;
                _output.WriteLine($"y={y,3}: x=[{left,3},{right,3}] w={w,3} nonWhite={nonWhite,4} dark={dark,3}");
            }
        }
    }
}
