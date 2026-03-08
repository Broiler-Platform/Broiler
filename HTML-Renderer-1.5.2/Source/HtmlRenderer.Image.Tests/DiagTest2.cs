using System.Drawing;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;
using TheArtOfDev.HtmlRenderer.Core.Dom;

namespace DiagTest;

public class Diag2
{
    [Fact]
    public void DumpFaceStructure()
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

        var topRect = container.GetElementRectangle("top");
        double scrollY = topRect?.Y ?? 0;

        // Walk the .picture children and dump their layout info
        var picture = FindByClass(root, "picture");
        Assert.NotNull(picture);
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"#top Y={scrollY}");
        sb.AppendLine($"picture Location={picture.Location} ActualBottom={picture.ActualBottom}");
        sb.AppendLine($"picture relative Y={picture.Location.Y - scrollY}");
        sb.AppendLine();
        
        // Dump immediate children of .picture's content (through the abs-pos wrapper)
        // Find the first div child (which is "first one" abs-pos wrapper containing forehead)
        var firstOne = FindByClass(root, "first");
        if (firstOne != null)
        {
            sb.AppendLine($"first.one: Y_rel={firstOne.Location.Y - scrollY} Bottom_rel={firstOne.ActualBottom - scrollY} Position={firstOne.Position}");
        }
        
        // Walk picture's children
        foreach (var child in picture.Boxes)
        {
            string cls = child.HtmlTag?.TryGetAttribute("class") ?? child.HtmlTag?.Name ?? child.Display;
            sb.AppendLine($"  child '{cls}' Display={child.Display} Float={child.Float} Clear={child.Clear} " +
                         $"Position={child.Position} " +
                         $"Y_rel={child.Location.Y - scrollY:F1} Bottom_rel={child.ActualBottom - scrollY:F1} " +
                         $"Height={child.ActualBottom - child.Location.Y:F1} " +
                         $"MarginTop={child.ActualMarginTop:F1} MarginBottom={child.ActualMarginBottom:F1}");
            
            // For the first.one, dump its children (second > children)
            if (cls == "first one")
            {
                var second = child.Boxes.FirstOrDefault();
                if (second != null)
                {
                    sb.AppendLine($"    second: Y_rel={second.Location.Y - scrollY:F1} Bottom_rel={second.ActualBottom - scrollY:F1}");
                    foreach (var grandchild in second.Boxes)
                    {
                        string gcCls = grandchild.HtmlTag?.TryGetAttribute("class") ?? grandchild.HtmlTag?.TryGetAttribute("id") ?? grandchild.Display;
                        sb.AppendLine($"      '{gcCls}': Y_rel={grandchild.Location.Y - scrollY:F1} Bottom_rel={grandchild.ActualBottom - scrollY:F1}");
                    }
                }
            }
            
            // For empty, dump its child
            if (cls == "empty")
            {
                foreach (var ec in child.Boxes)
                {
                    sb.AppendLine($"    empty-child: Y_rel={ec.Location.Y - scrollY:F1} Bottom_rel={ec.ActualBottom - scrollY:F1} " +
                                 $"MarginTop={ec.ActualMarginTop:F1} MarginBottom={ec.ActualMarginBottom:F1}");
                }
            }
            
            // For smile, dump structure
            if (cls == "smile")
            {
                foreach (var sc in child.Boxes)
                {
                    sb.AppendLine($"    smile-child: Y_rel={sc.Location.Y - scrollY:F1} Bottom_rel={sc.ActualBottom - scrollY:F1} " +
                                 $"Position={sc.Position} MarginTop={sc.ActualMarginTop:F1}");
                }
            }
            
            // For parser-container, dump parser
            if (cls == "parser-container")
            {
                foreach (var pc in child.Boxes)
                {
                    string pcCls = pc.HtmlTag?.TryGetAttribute("class") ?? "?";
                    sb.AppendLine($"    '{pcCls}': Y_rel={pc.Location.Y - scrollY:F1} Bottom_rel={pc.ActualBottom - scrollY:F1} " +
                                 $"BorderL={pc.ActualBorderLeftWidth} BorderR={pc.ActualBorderRightWidth}");
                }
            }
            
            // For UL, dump children
            if (child.HtmlTag?.Name == "ul")
            {
                sb.AppendLine($"    UL children: {child.Boxes.Count()}");
                foreach (var li in child.Boxes)
                {
                    string liCls = li.HtmlTag?.TryGetAttribute("class") ?? li.Display;
                    sb.AppendLine($"    '{liCls}': Display={li.Display} Y_rel={li.Location.Y - scrollY:F1} " +
                                 $"Bottom_rel={li.ActualBottom - scrollY:F1} Size={li.Size}");
                    foreach (var lic in li.Boxes)
                    {
                        string licCls = lic.HtmlTag?.TryGetAttribute("class") ?? lic.Display;
                        sb.AppendLine($"      '{licCls}': Display={lic.Display} Y_rel={lic.Location.Y - scrollY:F1} " +
                                     $"Bottom_rel={lic.ActualBottom - scrollY:F1} Size={lic.Size}");
                    }
                }
            }
        }
        
        // Calculate expected reference positions
        sb.AppendLine();
        sb.AppendLine("=== REFERENCE COMPARISON ===");
        sb.AppendLine("Reference face: Y=51 to Y=276 (height=225)");
        
        var chin = FindByClass(root, "chin");
        var smile = FindByClass(root, "smile");
        var nose = FindByClass(root, "nose");
        
        sb.AppendLine($"Broiler nose: Y_rel={nose!.Location.Y - scrollY:F1} to {nose.ActualBottom - scrollY:F1}");
        sb.AppendLine($"Broiler smile: Y_rel={smile!.Location.Y - scrollY:F1} to {smile.ActualBottom - scrollY:F1}");
        sb.AppendLine($"Broiler chin: Y_rel={chin!.Location.Y - scrollY:F1} to {chin.ActualBottom - scrollY:F1}");
        
        // Compute what clearance should be for smile
        sb.AppendLine();
        sb.AppendLine("=== CLEARANCE ANALYSIS ===");
        // Nose float bottom outer edge
        double noseFloatBottom = nose.ActualBottom + nose.ActualMarginBottom;
        sb.AppendLine($"Nose float bottom (outer edge): {noseFloatBottom - scrollY:F1}");
        sb.AppendLine($"Smile margin-top: {smile.ActualMarginTop:F1}");
        sb.AppendLine($"Smile clear: {smile.Clear}");
        
        // CollapsedMarginTop of smile
        sb.AppendLine($"Smile CollapsedMarginTop: {smile.CollapsedMarginTop:F1}");
        
        throw new Exception("\n" + sb.ToString());
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
}
