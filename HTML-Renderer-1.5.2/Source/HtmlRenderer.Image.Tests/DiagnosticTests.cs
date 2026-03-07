using System;
using System.Drawing;
using System.IO;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;
using Xunit;
using Xunit.Abstractions;
using TheArtOfDev.HtmlRenderer.Core.IR;

namespace HtmlRenderer.Image.Tests;

public class DiagnosticTests
{
    private readonly ITestOutputHelper _output;
    public DiagnosticTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Diag_RedPixelCount_And_EyesLayout()
    {
        int w = 1024, h = 768;
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Broiler.slnx")))
            dir = Directory.GetParent(dir)?.FullName;

        var html = File.ReadAllText(Path.Combine(dir, "acid", "acid2", "acid2.html"));
        var refPath = Path.Combine(dir, "acid", "acid2", "acid2-reference.png");

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
        _output.WriteLine($"#top Y = {topRect?.Y}");
        float scrollY = topRect!.Value.Y;

        container.Location = new PointF(0, scrollY);
        container.MaxSize = new SizeF(w, h);

        using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.Translate(0, -scrollY);
        container.PerformPaint(canvas, new RectangleF(0, scrollY, w, h));
        canvas.Restore();

        // Count red pixels
        int redCount = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                    redCount++;
            }
        _output.WriteLine($"Red pixel count: {redCount}");

        // Check element rects
        var eyesA = container.GetElementRectangle("eyes-a");
        _output.WriteLine($"#eyes-a rect = {eyesA}");

        var eyesB = container.GetElementRectangle("eyes-b");
        _output.WriteLine($"#eyes-b rect = {eyesB}");

        var eyesC = container.GetElementRectangle("eyes-c");
        _output.WriteLine($"#eyes-c rect = {eyesC}");

        // Pixel match against reference
        using var baseline = SKBitmap.Decode(refPath);
        var config = new DeterministicRenderConfig
        {
            ViewportWidth = w,
            ViewportHeight = h,
            PixelDiffThreshold = 1.0,
            ColorTolerance = 5
        };
        using var result = PixelDiffRunner.Compare(bitmap, baseline, config);
        double matchRatio = 1.0 - result.DiffRatio;
        _output.WriteLine($"Pixel match: {matchRatio:P2}");
        _output.WriteLine($"Diff pixels: {result.DiffPixelCount}/{result.TotalPixelCount}");
    }
}
