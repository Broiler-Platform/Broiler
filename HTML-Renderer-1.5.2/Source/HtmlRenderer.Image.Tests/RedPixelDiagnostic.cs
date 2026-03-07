using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;
using Xunit;

namespace HtmlRenderer.Image.Tests;

public class RedPixelDiagnostic
{
    [Fact]
    public void DumpAbsoluteFragmentDetails()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Broiler.slnx")))
            dir = Directory.GetParent(dir)?.FullName;

        var html = File.ReadAllText(Path.Combine(dir!, "acid", "acid2", "acid2.html"));
        int w = 1024;

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

        var fragmentTree = container.LatestFragmentTree!;

        // Dump ALL absolute and fixed fragments with Style.Top, Style.Left details
        Console.WriteLine("=== All positioned fragments with style details ===");
        DumpAllPositioned(fragmentTree, scrollY, 0);
    }

    private void DumpAllPositioned(Fragment f, float scrollY, int depth)
    {
        if (f.Style.Position is "absolute" or "fixed")
        {
            var b = f.Bounds;
            float vpY = b.Y - scrollY;
            var bg = f.Style.ActualBackgroundColor;
            string bgStr = bg.A > 0 ? $"bg=({bg.R},{bg.G},{bg.B})" : "bg=transparent";
            Console.WriteLine($"  [{f.Style.Position}] doc=({b.X:F1},{b.Y:F1},{b.Width:F1}x{b.Height:F1}) " +
                $"vp=({b.X:F1},{vpY:F1}) {bgStr} " +
                $"top={f.Style.Top} left={f.Style.Left} " +
                $"display={f.Style.Display} float={f.Style.Float} " +
                $"width={f.Style.Width} height={f.Style.Height} " +
                $"border=({f.Border.Top:F1},{f.Border.Right:F1},{f.Border.Bottom:F1},{f.Border.Left:F1}) " +
                $"bdrBotColor=({f.Style.ActualBorderBottomColor.R},{f.Style.ActualBorderBottomColor.G},{f.Style.ActualBorderBottomColor.B})" +
                $" children={f.Children.Count}");
            
            // Dump children of positioned elements
            foreach (var child in f.Children)
            {
                var cb = child.Bounds;
                var cbg = child.Style.ActualBackgroundColor;
                Console.WriteLine($"    child [{child.Style.Display}] pos={child.Style.Position} " +
                    $"doc=({cb.X:F1},{cb.Y:F1},{cb.Width:F1}x{cb.Height:F1}) " +
                    $"bg=({cbg.R},{cbg.G},{cbg.B},{cbg.A}) float={child.Style.Float} " +
                    $"width={child.Style.Width} height={child.Style.Height}");
            }
        }

        foreach (var child in f.Children)
            DumpAllPositioned(child, scrollY, depth + 1);
    }
}
