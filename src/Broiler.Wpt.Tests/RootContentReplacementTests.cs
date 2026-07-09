using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for CSS Content 3 root element replacement
/// (WPT css/css-content/element-replacement-root-canvas-bg-from-body, #1316).
///
/// When <c>:root { content: url(img) }</c> replaces the document root with an
/// image, the root's descendants generate no boxes, so the <c>&lt;body&gt;</c>
/// background must NOT propagate to the canvas (CSS Backgrounds 3
/// §body-background). Broiler previously rendered the body's background across
/// the whole canvas plus the body text; it now renders only the replaced image
/// at the origin on the default (white) canvas — matching the reference, which
/// is simply <c>&lt;img&gt;</c> in a <c>margin:0</c> body.
///
/// Implemented in <c>DomBridge.ReplaceRootWithReplacedContent</c> (render-prep),
/// gated to a root whose computed <c>content</c> is a <c>url()</c>.
/// </summary>
public class RootContentReplacementTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private const int W = 400;
    private const int H = 300;

    [Fact]
    public void RootContentUrl_ReplacesRoot_BodyBackgroundDoesNotReachCanvas()
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-rootcontent-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "resources"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "resources", "rect.svg"),
                "<svg width=\"100\" height=\"100\" version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\">" +
                "<rect x=\"0\" y=\"0\" width=\"100\" height=\"100\" fill=\"green\" /></svg>");

            const string html =
                "<!DOCTYPE html><title>t</title>" +
                "<style>:root{content:url('resources/rect.svg')} body{background-color:aquamarine}</style>" +
                "<p>This text should not be visible</p>";
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);

            var runner = new WptTestRunner(W, H);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            int aqua = 0, green = 0, greenTopLeft = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    // aquamarine ~ (127,255,212)
                    if (p.R is > 100 and < 180 && p.G > 220 && p.B is > 170 and < 240)
                        aqua++;
                    if (p.G > 100 && p.R < 80 && p.B < 80)
                    {
                        green++;
                        if (x < 100 && y < 100) greenTopLeft++;
                    }
                }

            // The body's aquamarine background must not fill the canvas.
            Assert.True(aqua < W * H * 0.02, $"aquamarine pixels={aqua}; body background reached the canvas.");
            // The replaced image (green 100x100 square) must render at the origin.
            Assert.True(greenTopLeft > 100 * 100 * 0.8, $"green-top-left={greenTopLeft}; replaced-content image not rendered at the origin.");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    // A normal page (no root `content`) must be unaffected: the body background
    // still propagates to the canvas.
    [Fact]
    public void NormalPage_BodyBackgroundStillPropagatesToCanvas()
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-rootcontent-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            const string html =
                "<!DOCTYPE html><title>t</title>" +
                "<style>body{background-color:aquamarine}</style><p>hi</p>";
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);

            var runner = new WptTestRunner(W, H);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            int aqua = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.R is > 100 and < 180 && p.G > 220 && p.B is > 170 and < 240)
                        aqua++;
                }
            Assert.True(aqua > W * H * 0.8, $"aquamarine pixels={aqua}; body background propagation regressed.");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
