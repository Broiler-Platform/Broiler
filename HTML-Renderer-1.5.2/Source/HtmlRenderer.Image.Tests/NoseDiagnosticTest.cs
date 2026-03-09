using System;
using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;
using TheArtOfDev.HtmlRenderer.Image.Adapters;
using Xunit;
using Xunit.Abstractions;

namespace HtmlRenderer.Image.Tests;

public class NoseDiagnosticTest : IDisposable
{
    private readonly string _acid2Html;
    private readonly string _referencePath;
    private readonly ITestOutputHelper _output;
    private static bool _fontLoaded;

    public NoseDiagnosticTest(ITestOutputHelper output)
    {
        _output = output;
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Broiler.slnx")))
            dir = Directory.GetParent(dir)?.FullName;
        Assert.NotNull(dir);
        if (!_fontLoaded)
        {
            var fontPath = Path.Combine(dir, "acid", "fonts", "DejaVuSans.ttf");
            if (File.Exists(fontPath))
                SkiaImageAdapter.Instance.LoadFontFromFile(fontPath, "sans-serif");
            _fontLoaded = true;
        }
        _acid2Html = File.ReadAllText(Path.Combine(dir, "acid", "acid2", "acid2.html"));
        _referencePath = Path.Combine(dir, "acid", "acid2", "acid2-reference.png");
    }

    private static SKBitmap RenderAtAnchorTop(string html)
    {
        int w = 1024, h = 768;
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(html);
        using var layoutBmp = new SKBitmap(w, 2000, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var layoutCanvas = new SKCanvas(layoutBmp);
        layoutCanvas.Clear(SKColors.White);
        container.PerformLayout(layoutCanvas, new RectangleF(0, 0, w, 99999));
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
    public void DiagnoseNoseRegion()
    {
        using var actual = RenderAtAnchorTop(_acid2Html);
        using var baseline = SKBitmap.Decode(_referencePath);
        int tolerance = 5;
        
        // Detailed pixel analysis for the worst rows
        int[] detailRows = { 150, 155, 168 };
        foreach (int y in detailRows)
        {
            _output.WriteLine($"\n=== Row y={y} mismatched pixels ===");
            for (int x = 100; x < 220; x++)
            {
                var a = actual.GetPixel(x, y);
                var r = baseline.GetPixel(x, y);
                bool isContent = a.Red < 250 || a.Green < 250 || a.Blue < 250
                              || r.Red < 250 || r.Green < 250 || r.Blue < 250;
                if (isContent)
                {
                    bool match = Math.Abs(a.Red - r.Red) <= tolerance
                              && Math.Abs(a.Green - r.Green) <= tolerance
                              && Math.Abs(a.Blue - r.Blue) <= tolerance;
                    if (!match)
                        _output.WriteLine($"  x={x}: actual=({a.Red},{a.Green},{a.Blue}) ref=({r.Red},{r.Green},{r.Blue})");
                }
            }
        }
        
        // Check vertical offset: compare actual row y with reference row y-1 and y+1
        _output.WriteLine($"\n=== Row y=168: checking if actual matches ref at y=167 or y=169 ===");
        int matchAt167 = 0, matchAt169 = 0, total168 = 0;
        for (int x = 0; x < Math.Min(actual.Width, baseline.Width); x++)
        {
            var a = actual.GetPixel(x, 168);
            var r167 = baseline.GetPixel(x, 167);
            var r168 = baseline.GetPixel(x, 168);
            var r169 = baseline.GetPixel(x, 169);
            bool isContent = a.Red < 250 || a.Green < 250 || a.Blue < 250
                          || r168.Red < 250 || r168.Green < 250 || r168.Blue < 250;
            if (isContent)
            {
                total168++;
                if (Math.Abs(a.Red - r167.Red) <= tolerance
                 && Math.Abs(a.Green - r167.Green) <= tolerance
                 && Math.Abs(a.Blue - r167.Blue) <= tolerance) matchAt167++;
                if (Math.Abs(a.Red - r169.Red) <= tolerance
                 && Math.Abs(a.Green - r169.Green) <= tolerance
                 && Math.Abs(a.Blue - r169.Blue) <= tolerance) matchAt169++;
            }
        }
        _output.WriteLine($"  Match at ref y=167: {matchAt167}/{total168}");
        _output.WriteLine($"  Match at ref y=168: 24/{total168}");
        _output.WriteLine($"  Match at ref y=169: {matchAt169}/{total168}");
    }

    public void Dispose() { }
}
