using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for HTML §4.8.4 <c>&lt;object&gt;</c> replacement (WPT
/// <c>html/semantics/interactive-elements/the-dialog-element/modal-dialog-in-object</c>).
///
/// An <c>&lt;object&gt;</c> whose resource loads shows that resource and its children are
/// fallback that must not render. Broiler did neither: the image resource painted nothing while
/// the fallback painted anyway — and a modal <c>&lt;dialog&gt;</c> in the fallback was hoisted
/// into the top layer, painting a dialog plus backdrop where the reference is a bare green
/// rectangle. Implemented in <c>DomBridge.ReplaceImageObjectsWithResource</c> (render-prep),
/// gated to an explicit <c>type="image/*"</c> so Acid2's deliberately-failing fallback objects
/// (which carry no image type) are untouched.
/// </summary>
public class ObjectImageResourceTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private const int W = 300;
    private const int H = 300;

    private static (int Green, int NonWhite, int MinX, int MinY, int MaxX, int MaxY) Render(string body)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-objimg-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "rect.svg"),
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\" version=\"1.1\">" +
                "<rect x=\"0\" y=\"0\" width=\"100\" height=\"100\" fill=\"green\"/></svg>");

            string html = "<!DOCTYPE html><html><head><style>dialog{background:green;border-color:green}</style>" +
                          "</head><body>" + body + "</body></html>";
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);

            var runner = new WptTestRunner(W, H);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            int green = 0, nonWhite = 0, minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.G > 100 && p.R < 100)
                    {
                        green++;
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                    else if (!(p.R > 240 && p.G > 240 && p.B > 240))
                    {
                        nonWhite++;
                    }
                }

            return (green, nonWhite, minX, minY, maxX, maxY);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact(Timeout = 600000)]
    public void ModalDialogInObjectFallback_DoesNotRender_AndResourceDoes()
    {
        var test = Render(
            "<object width=\"200\" type=\"image/svg+xml\" data=\"rect.svg\">" +
            "<dialog id=\"dialog\"></dialog></object>" +
            "<script>document.getElementById('dialog').showModal();</script>");
        var reference = Render("<object width=\"200\" type=\"image/svg+xml\" data=\"rect.svg\"></object>");

        // The reference is a 200x200 green square (the object's authored width, aspect preserved).
        Assert.Equal(200 * 200, reference.Green);
        Assert.Equal(0, reference.NonWhite);

        // The test renders identically: the fallback dialog contributes neither a box nor a backdrop.
        Assert.Equal(reference, test);
    }

    [Fact(Timeout = 600000)]
    public void ObjectWithoutImageType_KeepsFallback()
    {
        // Guard the gate: fallback exists so a failed/non-image resource can degrade (Acid2's
        // nested eyes). An object with no image/* type must keep rendering its fallback.
        var fallback = Render(
            "<object data=\"data:application/x-unknown,ERROR\">" +
            "<div style=\"width:50px;height:50px;background:green\"></div></object>");

        Assert.Equal(50 * 50, fallback.Green);
    }
}
