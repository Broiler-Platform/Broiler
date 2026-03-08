using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;
using TheArtOfDev.HtmlRenderer.Core.Dom;

namespace HtmlRenderer.Image.Tests;

public class SmileDiagnostic
{
    [Fact]
    public void DiagnoseSmileLayout()
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

        var root = container.HtmlContainerInt.Root;
        
        CssBox FindByClass(CssBox r, string cls) {
            if (r.HtmlTag?.TryGetAttribute("class") == cls) return r;
            foreach (var c in r.Boxes) { var x = FindByClass(c, cls); if (x != null) return x; }
            return null;
        }

        var nose = FindByClass(root, "nose");
        var empty = FindByClass(root, "empty");
        var smile = FindByClass(root, "smile");
        var chin = FindByClass(root, "chin");

        var output = new System.Text.StringBuilder();
        
        void Dump(string name, CssBox box) {
            if (box == null) { output.AppendLine($"{name}: NOT FOUND"); return; }
            output.AppendLine($"{name}:");
            output.AppendLine($"  Location: ({box.Location.X:F1}, {box.Location.Y:F1}) relY={box.Location.Y - scrollY:F1}");
            output.AppendLine($"  Size: ({box.Size.Width:F1}, {box.Size.Height:F1})");
            output.AppendLine($"  ActualBottom: {box.ActualBottom:F1} relY={box.ActualBottom - scrollY:F1}");
            output.AppendLine($"  Margin: T={box.ActualMarginTop:F1} R={box.ActualMarginRight:F1} B={box.ActualMarginBottom:F1} L={box.ActualMarginLeft:F1}");
            output.AppendLine($"  Border: T={box.ActualBorderTopWidth:F1} R={box.ActualBorderRightWidth:F1} B={box.ActualBorderBottomWidth:F1} L={box.ActualBorderLeftWidth:F1}");
            output.AppendLine($"  Padding: T={box.ActualPaddingTop:F1} R={box.ActualPaddingRight:F1} B={box.ActualPaddingBottom:F1} L={box.ActualPaddingLeft:F1}");
            output.AppendLine($"  Float={box.Float}, Position={box.Position}, Clear={box.Clear}");
            output.AppendLine($"  Display={box.Display}, Height={box.Height}");
            output.AppendLine($"  Top={box.Top}, Bottom={box.Bottom}");
            output.AppendLine($"  CollapsedMarginTop={box.CollapsedMarginTop:F1}");
        }
        
        output.AppendLine($"ScrollY: {scrollY}");
        Dump("NOSE", nose);
        Dump("EMPTY", empty);
        if (empty != null) foreach (var c in empty.Boxes) Dump("  EMPTY-child", c);
        Dump("SMILE", smile);
        if (smile != null) {
            foreach (var c in smile.Boxes) {
                Dump("  SMILE>div", c);
                foreach (var c2 in c.Boxes) {
                    Dump("    SMILE>div>div", c2);
                    foreach (var c3 in c2.Boxes) {
                        Dump("      SMILE>div>div>child", c3);
                        foreach (var c4 in c3.Boxes) {
                            Dump("        SMILE>div>div>child>child", c4);
                        }
                    }
                }
            }
        }
        Dump("CHIN", chin);

        // Render and analyze pixels
        container.Location = new PointF(0, scrollY);
        container.MaxSize = new SizeF(w, h);
        using var renderBmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var renderCanvas = new SKCanvas(renderBmp);
        renderCanvas.Clear(SKColors.White);
        renderCanvas.Save();
        renderCanvas.Translate(0, -scrollY);
        container.PerformPaint(renderCanvas, new RectangleF(0, scrollY, w, h));
        renderCanvas.Restore();

        output.AppendLine("\n--- Pixel rows in smile area ---");
        for (int y = 190; y <= 240; y++) {
            int black = 0, yellow = 0, white = 0, other = 0;
            for (int x = 60; x <= 230; x++) {
                var px = renderBmp.GetPixel(x, y);
                if (px.Red < 30 && px.Green < 30 && px.Blue < 30) black++;
                else if (px.Red > 200 && px.Green > 200 && px.Blue < 80) yellow++;
                else if (px.Red > 240 && px.Green > 240 && px.Blue > 240) white++;
                else other++;
            }
            output.AppendLine($"  Row {y}: black={black}, yellow={yellow}, white={white}, other={other}");
        }

        // Also check reference
        var refPath = Path.Combine(dir, "acid", "acid2", "acid2-reference.png");
        using var refBmp = SKBitmap.Decode(refPath);
        
        output.AppendLine("\n--- Reference pixel rows in smile area ---");
        for (int y = 190; y <= 240; y++) {
            int black = 0, yellow = 0, white = 0, other = 0;
            for (int x = 60; x <= 230; x++) {
                var px = refBmp.GetPixel(x, y);
                if (px.Red < 30 && px.Green < 30 && px.Blue < 30) black++;
                else if (px.Red > 200 && px.Green > 200 && px.Blue < 80) yellow++;
                else if (px.Red > 240 && px.Green > 240 && px.Blue > 240) white++;
                else other++;
            }
            output.AppendLine($"  Row {y}: black={black}, yellow={yellow}, white={white}, other={other}");
        }

        throw new Exception("DIAGNOSTIC OUTPUT:\n" + output.ToString());
    }
}
