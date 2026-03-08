using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;
using TheArtOfDev.HtmlRenderer.Core.Dom;

namespace DiagTest;

public class Diag
{
    [Fact]
    public void DumpBoxProperties()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Broiler.slnx")))
            dir = Directory.GetParent(dir)?.FullName;

        var htmlPath = Path.Combine(dir!, "acid", "acid2", "acid2.html");
        var html = File.ReadAllText(htmlPath);

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

        var root = container.HtmlContainerInt.Root;

        var parser = FindByClass(root, "parser");
        Assert.NotNull(parser);
        
        // Log parser border info
        var msg = $"PARSER: BorderLeftWidth='{parser.BorderLeftWidth}' " +
                  $"BorderRightWidth='{parser.BorderRightWidth}' " +
                  $"BorderTopWidth='{parser.BorderTopWidth}' " +
                  $"BorderBottomWidth='{parser.BorderBottomWidth}' " +
                  $"BorderLeftStyle='{parser.BorderLeftStyle}' " +
                  $"BorderRightStyle='{parser.BorderRightStyle}' " +
                  $"BorderTopStyle='{parser.BorderTopStyle}' " +
                  $"BorderBottomStyle='{parser.BorderBottomStyle}' " +
                  $"ActualBorderLeft={parser.ActualBorderLeftWidth} " +
                  $"ActualBorderRight={parser.ActualBorderRightWidth} " +
                  $"ActualBorderTop={parser.ActualBorderTopWidth} " +
                  $"ActualBorderBottom={parser.ActualBorderBottomWidth} " +
                  $"Location={parser.Location} " +
                  $"Size={parser.Size} " +
                  $"ActualBottom={parser.ActualBottom} " +
                  $"Height='{parser.Height}' Width='{parser.Width}' " +
                  $"Background='{parser.BackgroundColor}'";
        
        var chin = FindByClass(root, "chin");
        Assert.NotNull(chin);
        var chinMsg = $"CHIN: Location={chin.Location} Size={chin.Size} " +
                      $"ActualBottom={chin.ActualBottom} " +
                      $"Margin=top:{chin.ActualMarginTop}/right:{chin.ActualMarginRight}/bottom:{chin.ActualMarginBottom}/left:{chin.ActualMarginLeft} " +
                      $"BorderLeft={chin.ActualBorderLeftWidth} BorderRight={chin.ActualBorderRightWidth}";

        var empty = FindByClass(root, "empty");
        var emptyMsg = empty != null ? $"EMPTY: Location={empty.Location} ActualBottom={empty.ActualBottom} " +
                      $"Height='{empty.Height}' " +
                      $"Margin=top:{empty.ActualMarginTop}/bottom:{empty.ActualMarginBottom}" : "EMPTY: not found";

        var smile = FindByClass(root, "smile");
        var smileMsg = smile != null ? $"SMILE: Location={smile.Location} ActualBottom={smile.ActualBottom} " +
                      $"Clear={smile.Clear} " +
                      $"Margin=top:{smile.ActualMarginTop}/bottom:{smile.ActualMarginBottom}" : "SMILE: not found";

        var nose = FindByClass(root, "nose");
        var noseMsg = nose != null ? $"NOSE: Location={nose.Location} ActualBottom={nose.ActualBottom} " +
                      $"Float={nose.Float} " +
                      $"MarginBottom={nose.ActualMarginBottom} " +
                      $"MaxHeight='{nose.MaxHeight}' Height='{nose.Height}'" : "NOSE: not found";

        // UL
        var uls = FindByTag(root, "ul");
        var ulMsg = "";
        foreach (var ul in uls)
        {
            ulMsg += $"UL: Display={ul.Display} Location={ul.Location} Size={ul.Size} " +
                     $"ActualBottom={ul.ActualBottom} Margin=top:{ul.ActualMarginTop}\n";
            foreach (var li in ul.Boxes)
            {
                ulMsg += $"  LI: class='{li.HtmlTag?.TryGetAttribute("class")}' Display={li.Display} " +
                         $"Location={li.Location} Size={li.Size} ActualBottom={li.ActualBottom} " +
                         $"Height='{li.Height}'\n";
            }
        }

        var topRect = container.GetElementRectangle("top");
        double scrollY = topRect?.Y ?? 0;

        // Throw with all info
        throw new Exception($"\n#top Y={scrollY}\n{msg}\n{chinMsg}\n{emptyMsg}\n{smileMsg}\n{noseMsg}\n{ulMsg}");
    }

    private static CssBox? FindByClass(CssBox box, string cls)
    {
        if (box.HtmlTag?.TryGetAttribute("class") == cls) return box;
        foreach (var child in box.Boxes)
        {
            var r = FindByClass(child, cls);
            if (r != null) return r;
        }
        return null;
    }

    private static List<CssBox> FindByTag(CssBox box, string tag)
    {
        var results = new List<CssBox>();
        if (box.HtmlTag?.Name == tag) results.Add(box);
        foreach (var child in box.Boxes) results.AddRange(FindByTag(child, tag));
        return results;
    }
}
