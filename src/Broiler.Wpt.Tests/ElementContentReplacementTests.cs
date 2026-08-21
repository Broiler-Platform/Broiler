using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for CSS Content 3 element replacement on a non-root element
/// (WPT html/semantics/interactive-elements/the-dialog-element/modal-dialog-in-replaced-renderer).
///
/// A <c>&lt;div&gt;</c> whose computed <c>content</c> is an image <c>url()</c> is replaced by
/// that image, and its children generate no boxes. The test nests an open modal
/// <c>&lt;dialog&gt;</c> (green background/border) inside such a div; the reference is simply the
/// replaced image with no dialog at all, so the dialog must not be hoisted into the top layer
/// and painted.
///
/// Broiler previously did neither half: the replaced image painted nothing, and the nested modal
/// dialog was stamped into the top layer and painted as a wide green band. Implemented in
/// <c>DomBridge.ReplaceElementsWithReplacedContent</c> (render-prep), which runs before the
/// dialog/popover top-layer passes.
/// </summary>
public class ElementContentReplacementTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private const int W = 200;
    private const int H = 200;

    // 15x15 solid-green PNG (the image the WPT test/reference use).
    private const string GreenPng =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAA8AAAAPAQMAAAABGAcJAAAAA1BMVEUAgACc" +
        "+aWRAAAADElEQVR42mNgIAEAAAAtAAH7KhMqAAAAAElFTkSuQmCC";

    private const string Preamble =
        "<p>The test passes if you see a green square near the top of the viewport.";

    private static (int Count, int MinX, int MinY, int MaxX, int MaxY) RenderGreen(string body)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-elemcontent-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string html =
                "<!DOCTYPE html><html><head><style>" +
                "dialog { background: green; border-color: green; }" +
                "div { content: url(" + GreenPng + "); }" +
                "</style></head><body>" + body + "</body></html>";
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);

            var runner = new WptTestRunner(W, H);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            int count = 0, minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.G > 100 && p.R < 80 && p.B < 80)
                    {
                        count++;
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }

            return (count, minX, minY, maxX, maxY);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact(Timeout = 600000)]
    public void ModalDialogInsideReplacedElement_GeneratesNoBox_AndImageRenders()
    {
        var test = RenderGreen(
            Preamble +
            "<div><dialog id=\"dialog\"></dialog></div>" +
            "<script>document.getElementById('dialog').showModal();</script>");
        var reference = RenderGreen(Preamble + "<div></div>");

        // The replaced image renders at its 15x15 intrinsic size ...
        Assert.Equal(15 * 15, reference.Count);
        // ... and the test (open modal dialog nested in the replaced div) renders identically:
        // the dialog contributes no box, so no extra green appears.
        Assert.Equal(reference, test);
    }

    [Fact(Timeout = 600000)]
    public void ModalDialog_OutsideReplacedElement_StillPaints()
    {
        // Guard the suppression above against over-reach: a modal dialog that is NOT inside a
        // replaced element must still be hoisted into the top layer and painted.
        var normal = RenderGreen(
            "<dialog id=\"dialog\"></dialog>" +
            "<script>document.getElementById('dialog').showModal();</script>");

        Assert.True(normal.Count > 15 * 15,
            $"expected a painted modal dialog larger than the 15x15 image, saw {normal.Count} green px");
    }
}
