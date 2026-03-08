using System;
using System.Drawing;
using System.IO;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

public class SmileRegionDiagnostic
{
    [Fact]
    public void SmileRegion_PixelDump()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Broiler.slnx")))
            dir = Directory.GetParent(dir)?.FullName;

        var htmlPath = Path.Combine(dir, "acid", "acid2", "acid2.html");
        var html = File.ReadAllText(htmlPath);
        var refPath = Path.Combine(dir, "acid", "acid2", "acid2-reference.png");

        int w = 1024, h = 768;

        // Render
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

        var actual = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(actual);
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.Translate(0, -scrollY);
        container.PerformPaint(canvas, new RectangleF(0, scrollY, w, h));
        canvas.Restore();

        // Load reference
        using var baseline = SKBitmap.Decode(refPath);

        // Save actual render
        using (var stream = File.OpenWrite("/tmp/acid2_actual.png"))
            actual.Encode(stream, SKEncodedImageFormat.Png, 100);

        // Scan smile region (y=190-230 in reference, roughly x=85-215)
        Console.WriteLine("=== Smile Region Comparison (y=190-230) ===");
        Console.WriteLine("Row | Ref Black | Act Black | Ref Yellow | Act Yellow | Diff");
        
        for (int y = 190; y <= 230; y++)
        {
            int refBlack = 0, actBlack = 0, refYellow = 0, actYellow = 0, diffPx = 0;
            for (int x = 70; x <= 230; x++)
            {
                var r = baseline.GetPixel(x, y);
                var a = actual.GetPixel(x, y);
                
                if (r.Red < 20 && r.Green < 20 && r.Blue < 20) refBlack++;
                if (a.Red < 20 && a.Green < 20 && a.Blue < 20) actBlack++;
                if (r.Red > 230 && r.Green > 230 && r.Blue < 50) refYellow++;
                if (a.Red > 230 && a.Green > 230 && a.Blue < 50) actYellow++;
                
                if (Math.Abs(r.Red - a.Red) > 5 || Math.Abs(r.Green - a.Green) > 5 || Math.Abs(r.Blue - a.Blue) > 5)
                    diffPx++;
            }
            Console.WriteLine($"{y,3} | {refBlack,9} | {actBlack,9} | {refYellow,10} | {actYellow,10} | {diffPx,4}");
        }

        // Extended scan for face height analysis
        Console.WriteLine("\n=== Face Bottom Scan (y=240-310) ===");
        Console.WriteLine("Row | Ref Black | Act Black | Ref Yellow | Act Yellow | Diff");
        
        for (int y = 240; y <= 310; y++)
        {
            int refBlack = 0, actBlack = 0, refYellow = 0, actYellow = 0, diffPx = 0;
            for (int x = 70; x <= 230; x++)
            {
                var r = baseline.GetPixel(x, y);
                var a = actual.GetPixel(x, y);
                
                if (r.Red < 20 && r.Green < 20 && r.Blue < 20) refBlack++;
                if (a.Red < 20 && a.Green < 20 && a.Blue < 20) actBlack++;
                if (r.Red > 230 && r.Green > 230 && r.Blue < 50) refYellow++;
                if (a.Red > 230 && a.Green > 230 && a.Blue < 50) actYellow++;
                
                if (Math.Abs(r.Red - a.Red) > 5 || Math.Abs(r.Green - a.Green) > 5 || Math.Abs(r.Blue - a.Blue) > 5)
                    diffPx++;
            }
            Console.WriteLine($"{y,3} | {refBlack,9} | {actBlack,9} | {refYellow,10} | {actYellow,10} | {diffPx,4}");
        }

        // Overall content match
        int totalContent = 0, matchContent = 0;
        for (int y = 0; y < Math.Min(actual.Height, baseline.Height); y++)
        {
            for (int x = 0; x < Math.Min(actual.Width, baseline.Width); x++)
            {
                var a = actual.GetPixel(x, y);
                var r = baseline.GetPixel(x, y);
                bool isContent = a.Red < 250 || a.Green < 250 || a.Blue < 250
                              || r.Red < 250 || r.Green < 250 || r.Blue < 250;
                if (isContent)
                {
                    totalContent++;
                    if (Math.Abs(a.Red - r.Red) <= 5 && Math.Abs(a.Green - r.Green) <= 5 && Math.Abs(a.Blue - r.Blue) <= 5)
                        matchContent++;
                }
            }
        }
        
        double contentMatch = totalContent > 0 ? (double)matchContent / totalContent : 0;
        Console.WriteLine($"\nContent-area match: {contentMatch:P2} ({matchContent}/{totalContent})");

        actual.Dispose();
    }
}
