using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;
using TheArtOfDev.HtmlRenderer.Core.Dom;

namespace HtmlRenderer.Image.Tests;

public class SmileDiagnostic3
{
    [Fact]
    public void DiagnoseElementsAfterSmile()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Broiler.slnx")))
            dir = Directory.GetParent(dir)?.FullName;
        Assert.NotNull(dir);

        var html = File.ReadAllText(Path.Combine(dir, "acid", "acid2", "acid2.html"));
        int w = 1024;

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

        var output = new System.Text.StringBuilder();

        CssBox FindByClass(CssBox r, string cls) {
            if (r.HtmlTag?.TryGetAttribute("class") == cls) return r;
            foreach (var c in r.Boxes) { var x = FindByClass(c, cls); if (x != null) return x; }
            return null;
        }

        void DumpTree(CssBox box, string indent, int depth) {
            if (depth > 6) return;
            string cls = box.HtmlTag?.TryGetAttribute("class") ?? "";
            string tag = box.HtmlTag?.Name ?? "anon";
            string pos = $"Y={box.Location.Y - scrollY:F0}-{box.ActualBottom - scrollY:F0}";
            string sz = $"sz={box.Size.Width:F0}x{box.Size.Height:F0}";
            string extras = "";
            if (box.Float != "none") extras += $" float={box.Float}";
            if (box.Position != "static") extras += $" pos={box.Position}";
            if (box.Clear != "none") extras += $" clear={box.Clear}";
            if (box.Display != "block" && box.Display != "inline") extras += $" display={box.Display}";
            if (!string.IsNullOrEmpty(cls)) extras += $" .{cls}";
            string margins = $" m={box.ActualMarginTop:F0}/{box.ActualMarginRight:F0}/{box.ActualMarginBottom:F0}/{box.ActualMarginLeft:F0}";
            string borders = "";
            if (box.ActualBorderTopWidth > 0 || box.ActualBorderRightWidth > 0 || box.ActualBorderBottomWidth > 0 || box.ActualBorderLeftWidth > 0)
                borders = $" b={box.ActualBorderTopWidth:F0}/{box.ActualBorderRightWidth:F0}/{box.ActualBorderBottomWidth:F0}/{box.ActualBorderLeftWidth:F0}";
            output.AppendLine($"{indent}<{tag}> {pos} {sz}{margins}{borders}{extras}");
            foreach (var child in box.Boxes)
                DumpTree(child, indent + "  ", depth + 1);
        }

        // Find the face container (the div that contains nose, empty, smile, chin, parser-container)
        var smile = FindByClass(root, "smile");
        if (smile?.ParentBox != null) {
            var parent = smile.ParentBox;
            output.AppendLine($"FACE CONTAINER (parent of .smile):");
            output.AppendLine($"  Location: ({parent.Location.X:F0}, {parent.Location.Y - scrollY:F0})");
            output.AppendLine($"  ActualBottom: {parent.ActualBottom - scrollY:F0}");
            output.AppendLine($"  Children:");
            foreach (var child in parent.Boxes) {
                DumpTree(child, "    ", 0);
            }
        }

        throw new Exception("DIAGNOSTIC:\n" + output.ToString());
    }
}
