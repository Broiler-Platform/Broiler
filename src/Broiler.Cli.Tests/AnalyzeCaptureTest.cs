using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class AnalyzeCaptureTest
{
    private readonly ITestOutputHelper _output;
    public AnalyzeCaptureTest(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void AnalyzeGoogleCliCapture()
    {
        var path = "/tmp/google_cli_capture.png";
        if (!System.IO.File.Exists(path))
        {
            _output.WriteLine("No capture file found");
            return;
        }
        using var bmp = SKBitmap.Decode(path);
        _output.WriteLine($"Image size: {bmp.Width}x{bmp.Height}");
        
        int fullWidthLines = 0;
        _output.WriteLine("\n=== ALL TEXT ROWS ===");
        for (int y = 0; y < bmp.Height; y++)
        {
            int left = bmp.Width, right = 0;
            int dark = 0;
            int leftAll = bmp.Width, rightAll = 0;
            bool hasBorder = false;
            bool hasGrayBg = false;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                bool nonWhite = px.Red < 250 || px.Green < 250 || px.Blue < 250;
                if (nonWhite) { if (x < leftAll) leftAll = x; if (x > rightAll) rightAll = x; }
                if (px.Red < 80 && px.Green < 80 && px.Blue < 80) { dark++; if (x < left) left = x; if (x > right) right = x; }
                if (px.Red >= 0xD0 && px.Red <= 0xD4 && px.Green >= 0xD0 && px.Green <= 0xD4) hasBorder = true;
                if (px.Red >= 0xF0 && px.Green >= 0xF0 && px.Blue >= 0xF0 && px.Red < 0xF8) hasGrayBg = true;
            }
            int wAll = (leftAll <= rightAll) ? rightAll - leftAll + 1 : 0;
            if (wAll > 700) fullWidthLines++;
            
            if (dark >= 3 || (hasBorder && y % 10 == 0) || (hasGrayBg && y % 20 == 0))
            {
                string desc = "";
                if (hasBorder) desc += " BORDER";
                if (hasGrayBg) desc += " GRAY";
                if (wAll > 700) desc += " FULLWIDTH!";
                _output.WriteLine($"y={y,3}: dark=[{left,3},{right,3}] w={right-left+1,3} cnt={dark,3} | all=[{leftAll,3},{rightAll,3}] w={wAll,3}{desc}");
            }
        }
        _output.WriteLine($"\nFull-width lines: {fullWidthLines}");
    }
}
