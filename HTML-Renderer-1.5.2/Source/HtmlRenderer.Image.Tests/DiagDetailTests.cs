using System;
using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;
using TheArtOfDev.HtmlRenderer.Image.Adapters;
using Xunit;
using Xunit.Abstractions;

namespace HtmlRenderer.Image.Tests;

public class DiagDetailTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _acid2Html;
    private readonly string _referencePath;
    private static bool _fontLoaded;
    private static readonly object _lock = new();

    public DiagDetailTests(ITestOutputHelper output)
    {
        _output = output;
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Broiler.slnx")))
            dir = Directory.GetParent(dir)?.FullName;
        lock (_lock)
        {
            if (!_fontLoaded)
            {
                var fp = Path.Combine(dir!, "acid", "fonts", "DejaVuSans.ttf");
                if (File.Exists(fp)) SkiaImageAdapter.Instance.LoadFontFromFile(fp, "sans-serif");
                _fontLoaded = true;
            }
        }
        _acid2Html = File.ReadAllText(Path.Combine(dir!, "acid", "acid2", "acid2.html"));
        _referencePath = Path.Combine(dir!, "acid", "acid2", "acid2-reference.png");
    }

    private SKBitmap RenderAtAnchorTop()
    {
        int w = 1024, h = 768;
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(_acid2Html);
        using var lb = new SKBitmap(w, 2000, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var lc = new SKCanvas(lb);
        lc.Clear(SKColors.White);
        container.PerformLayout(lc, new RectangleF(0, 0, w, 99999));
        var topRect = container.GetElementRectangle("top");
        float scrollY = topRect!.Value.Y;
        container.Location = new PointF(0, scrollY);
        container.MaxSize = new SizeF(w, h);
        var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.Translate(0, -scrollY);
        container.PerformPaint(canvas, new RectangleF(0, scrollY, w, h));
        canvas.Restore();
        return bitmap;
    }

    [Fact]
    public void PerScanlineDiffs()
    {
        using var actual = RenderAtAnchorTop();
        using var baseline = SKBitmap.Decode(_referencePath);
        int tol = 5;

        _output.WriteLine("=== Per-scanline diff counts for Eyes (69-129) ===");
        for (int y = 69; y <= 129; y++)
        {
            int total = 0, match = 0;
            int firstDiffX = -1, lastDiffX = -1;
            for (int x = 0; x < Math.Min(actual.Width, baseline.Width); x++)
            {
                var a = actual.GetPixel(x, y);
                var r = baseline.GetPixel(x, y);
                bool c = a.Red < 250 || a.Green < 250 || a.Blue < 250
                      || r.Red < 250 || r.Green < 250 || r.Blue < 250;
                if (c)
                {
                    total++;
                    if (Math.Abs(a.Red - r.Red) <= tol && Math.Abs(a.Green - r.Green) <= tol && Math.Abs(a.Blue - r.Blue) <= tol)
                        match++;
                    else
                    {
                        if (firstDiffX < 0) firstDiffX = x;
                        lastDiffX = x;
                    }
                }
            }
            if (total > 0 && total - match > 0)
            {
                double pct = (double)match / total * 100;
                _output.WriteLine($"  y={y}: {pct:F1}% ({total-match} diff) x=[{firstDiffX}..{lastDiffX}]");
            }
        }

        _output.WriteLine("\n=== Per-scanline diff counts for Smile (196-260) ===");
        for (int y = 196; y <= 260; y++)
        {
            int total = 0, match = 0;
            int firstDiffX = -1, lastDiffX = -1;
            for (int x = 0; x < Math.Min(actual.Width, baseline.Width); x++)
            {
                var a = actual.GetPixel(x, y);
                var r = baseline.GetPixel(x, y);
                bool c = a.Red < 250 || a.Green < 250 || a.Blue < 250
                      || r.Red < 250 || r.Green < 250 || r.Blue < 250;
                if (c)
                {
                    total++;
                    if (Math.Abs(a.Red - r.Red) <= tol && Math.Abs(a.Green - r.Green) <= tol && Math.Abs(a.Blue - r.Blue) <= tol)
                        match++;
                    else
                    {
                        if (firstDiffX < 0) firstDiffX = x;
                        lastDiffX = x;
                    }
                }
            }
            if (total > 0 && total - match > 0)
            {
                double pct = (double)match / total * 100;
                _output.WriteLine($"  y={y}: {pct:F1}% ({total-match} diff) x=[{firstDiffX}..{lastDiffX}]");
            }
        }

        _output.WriteLine("\n=== Per-scanline diff counts for Chin (261-275) ===");
        for (int y = 261; y <= 275; y++)
        {
            int total = 0, match = 0;
            int firstDiffX = -1, lastDiffX = -1;
            for (int x = 0; x < Math.Min(actual.Width, baseline.Width); x++)
            {
                var a = actual.GetPixel(x, y);
                var r = baseline.GetPixel(x, y);
                bool c = a.Red < 250 || a.Green < 250 || a.Blue < 250
                      || r.Red < 250 || r.Green < 250 || r.Blue < 250;
                if (c)
                {
                    total++;
                    if (Math.Abs(a.Red - r.Red) <= tol && Math.Abs(a.Green - r.Green) <= tol && Math.Abs(a.Blue - r.Blue) <= tol)
                        match++;
                    else
                    {
                        if (firstDiffX < 0) firstDiffX = x;
                        lastDiffX = x;
                    }
                }
            }
            if (total > 0 && total - match > 0)
            {
                double pct = (double)match / total * 100;
                _output.WriteLine($"  y={y}: {pct:F1}% ({total-match} diff) x=[{firstDiffX}..{lastDiffX}]");
            }
        }
    }

    public void Dispose() {}
}
