using System;
using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;
using TheArtOfDev.HtmlRenderer.Image.Adapters;
using Xunit;
using Xunit.Abstractions;

namespace HtmlRenderer.Image.Tests;

public class DiagRegionTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _acid2Html;
    private readonly string _referencePath;
    private static bool _fontLoaded;
    private static readonly object _lock = new();

    public DiagRegionTests(ITestOutputHelper output)
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
    public void PrintRegionMatches()
    {
        using var actual = RenderAtAnchorTop();
        using var baseline = SKBitmap.Decode(_referencePath);
        int tol = 5;
        void Measure(string name, int ys, int ye)
        {
            int total = 0, match = 0;
            for (int y = ys; y <= ye; y++)
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
                    }
                }
            double pct = total > 0 ? (double)match / total * 100 : 0;
            _output.WriteLine($"{name}: {pct:F2}% ({match}/{total})");
        }
        Measure("Forehead (51-68)", 51, 68);
        Measure("Eyes     (69-129)", 69, 129);
        Measure("Nose     (130-210)", 130, 210);
        Measure("Smile    (196-260)", 196, 260);
        Measure("Chin     (261-275)", 261, 275);
        Measure("Content  (all)", 0, 767);
    }

    public void Dispose() {}
}
