using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;
using TheArtOfDev.HtmlRenderer.Core.IR;
using Xunit;
using Xunit.Abstractions;

namespace HtmlRenderer.Image.Tests;

public class RedPixelDiagnostic
{
    private readonly ITestOutputHelper _output;
    
    public RedPixelDiagnostic(ITestOutputHelper output) => _output = output;
    
    [Fact]
    public void CountRedPixelsByRegion()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Broiler.slnx")))
            dir = Directory.GetParent(dir)?.FullName;
        
        var htmlPath = Path.Combine(dir, "acid", "acid2", "acid2.html");
        var html = File.ReadAllText(htmlPath);
        
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
        float scrollY = topRect.Value.Y;

        container.Location = new PointF(0, scrollY);
        container.MaxSize = new SizeF(w, h);

        using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.Translate(0, -scrollY);
        container.PerformPaint(canvas, new RectangleF(0, scrollY, w, h));
        canvas.Restore();

        int totalRed = 0;
        // Divide into vertical bands
        int[] bandCounts = new int[16]; // 48px bands
        int bandH = h / 16;
        
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var px = bitmap.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 50 && px.Blue < 50)
                {
                    totalRed++;
                    int band = Math.Min(y / bandH, 15);
                    bandCounts[band]++;
                }
            }
        }

        _output.WriteLine($"Total red pixels: {totalRed}");
        _output.WriteLine($"ScrollY: {scrollY}");
        for (int i = 0; i < 16; i++)
        {
            if (bandCounts[i] > 0)
                _output.WriteLine($"Band {i} (y {i*bandH}-{(i+1)*bandH}): {bandCounts[i]} red pixels");
        }
        
        // Save bitmap for visual inspection
        var outPath = Path.Combine(dir, "acid", "acid2", "broiler-render.png");
        using var stream = File.OpenWrite(outPath);
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        _output.WriteLine($"Saved render to: {outPath}");
    }
}
