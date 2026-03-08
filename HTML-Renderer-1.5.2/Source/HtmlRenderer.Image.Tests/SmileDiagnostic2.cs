using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;
using TheArtOfDev.HtmlRenderer.Core.Dom;

namespace HtmlRenderer.Image.Tests;

public class SmileDiagnostic2
{
    [Fact]
    public void DiagnoseFullDiff()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Broiler.slnx")))
            dir = Directory.GetParent(dir)?.FullName;
        Assert.NotNull(dir);

        var html = File.ReadAllText(Path.Combine(dir, "acid", "acid2", "acid2.html"));
        int w = 1024, h = 768;

        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(w, 99999);
        container.SetHtml(html);

        using var bmp = new SKBitmap(w, 2000, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        container.PerformLayout(canvas, new RectangleF(0, 0, w, 99999));

        var topRect = container.GetElementRectangle("top");
        float scrollY = topRect.Value.Y;

        container.Location = new PointF(0, scrollY);
        container.MaxSize = new SizeF(w, h);
        using var actual = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var renderCanvas = new SKCanvas(actual);
        renderCanvas.Clear(SKColors.White);
        renderCanvas.Save();
        renderCanvas.Translate(0, -scrollY);
        container.PerformPaint(renderCanvas, new RectangleF(0, scrollY, w, h));
        renderCanvas.Restore();

        var refPath = Path.Combine(dir, "acid", "acid2", "acid2-reference.png");
        using var baseline = SKBitmap.Decode(refPath);

        var output = new System.Text.StringBuilder();
        int tolerance = 5;
        
        // Find all rows with mismatches
        int totalDiff = 0, totalContent = 0, totalContentMatch = 0;
        for (int y = 0; y < Math.Min(actual.Height, baseline.Height); y++) {
            int rowDiff = 0, rowContent = 0, rowMatch = 0;
            for (int x = 0; x < Math.Min(actual.Width, baseline.Width); x++) {
                var a = actual.GetPixel(x, y);
                var r = baseline.GetPixel(x, y);
                bool isContent = a.Red < 250 || a.Green < 250 || a.Blue < 250
                              || r.Red < 250 || r.Green < 250 || r.Blue < 250;
                if (isContent) {
                    rowContent++;
                    totalContent++;
                    bool match = Math.Abs(a.Red - r.Red) <= tolerance
                              && Math.Abs(a.Green - r.Green) <= tolerance
                              && Math.Abs(a.Blue - r.Blue) <= tolerance;
                    if (match) { rowMatch++; totalContentMatch++; }
                    else { rowDiff++; totalDiff++; }
                }
            }
            if (rowDiff > 0)
                output.AppendLine($"Row {y}: content={rowContent}, match={rowMatch}, diff={rowDiff}");
        }
        
        output.AppendLine($"\nTotal: content={totalContent}, match={totalContentMatch}, diff={totalDiff}");
        output.AppendLine($"Content match: {(double)totalContentMatch / totalContent:P2}");

        throw new Exception("DIAGNOSTIC:\n" + output.ToString());
    }
}
