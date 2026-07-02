using System.Drawing;
using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

public class TextFidelityThresholdTests
{
    private const int RepresentativeInkThreshold = 180;
    private static readonly object FontLoadLock = new();
    private static bool _ahemLoaded;
    private static bool _probeSansLoaded;

    [Fact]
    public void PixelDiffRunner_Compare_Matches_Ahem_Text_Prototype_Exactly()
    {
        EnsureAhemLoaded();

        const string html = """
            <html><body style='margin:0;background:#ffffff'>
            <div style='margin-left:10px;margin-top:12px;font:20px/1 Ahem;color:#000000'>XXX</div>
            </body></html>
            """;

        using var rendered = HtmlRender.RenderToImageWithStyleSet(html, 100, 60, backgroundColor: BColor.White);
        using var expected = new BBitmap(100, 60);
        expected.Clear(BColor.White);
        FillRect(expected, 10, 12, 60, 20, new BColor(0, 0, 0, 255));

        using var diff = PixelDiffRunner.Compare(rendered, expected);

        Assert.True(diff.IsMatch);
        Assert.Equal(0, diff.DiffPixelCount);
        Assert.Equal(0d, diff.DiffRatio);
        Assert.Null(diff.DiffBitmap);
    }

    [Fact]
    public void Representative_Text_Page_Stays_Within_M3_Bounds_Threshold()
    {
        EnsureProbeSansLoaded();

        const string html = """
            <!DOCTYPE html>
            <html><body style='margin:0;padding:16px;background:#ffffff;font:16px ProbeSans,sans-serif;color:#000000'>
            <h1 style='margin:0 0 12px;font:700 32px/1.2 ProbeSans,sans-serif'>Broiler Text</h1>
            <p style='margin:0;width:220px'>The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs.</p>
            </body></html>
            """;

        using var rendered = HtmlRender.RenderToImageWithStyleSet(html, 280, 160, backgroundColor: BColor.White);

        var inkBounds = FindInkBounds(rendered, RepresentativeInkThreshold);

        Assert.NotNull(inkBounds);
        Assert.InRange(Math.Abs(inkBounds!.Value.Left - 16), 0, 2);
        Assert.InRange(Math.Abs(inkBounds.Value.Top - 31), 0, 2);
        Assert.InRange(Math.Abs(inkBounds.Value.Right - 239), 0, 2);
        Assert.InRange(Math.Abs(inkBounds.Value.Bottom - 84), 0, 2);
    }

    private static void EnsureAhemLoaded()
    {
        lock (FontLoadLock)
        {
            if (_ahemLoaded)
                return;

            HtmlRender.LoadFontFromFile(Path.Combine(GetRepoRoot(), "tests", "wpt", "fonts", "Ahem.ttf"), "Ahem");
            _ahemLoaded = true;
        }
    }

    private static void EnsureProbeSansLoaded()
    {
        lock (FontLoadLock)
        {
            if (_probeSansLoaded)
                return;

            HtmlRender.LoadFontFromFile(Path.Combine(GetRepoRoot(), "acid", "fonts", "DejaVuSans.ttf"), "ProbeSans");
            _probeSansLoaded = true;
        }
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Broiler.slnx")))
            current = current.Parent;

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }

    private static Rectangle? FindInkBounds(BBitmap bitmap, byte threshold)
    {
        int minX = bitmap.Width;
        int minY = bitmap.Height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.R >= threshold && pixel.G >= threshold && pixel.B >= threshold)
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX >= minX && maxY >= minY
            ? Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1)
            : null;
    }

    private static void FillRect(BBitmap bitmap, int x, int y, int width, int height, BColor color)
    {
        for (int py = y; py < y + height; py++)
        {
            for (int px = x; px < x + width; px++)
                bitmap.SetPixel(px, py, color);
        }
    }
}
