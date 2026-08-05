using System;
using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// An author <c>::backdrop</c> rule can set painted properties beyond the background, and they
/// have to reach the scrim. Only <c>background</c> / <c>background-color</c> were carried across
/// (folded into the resolved backdrop colour by <c>DomBridge.GetBackdropBackground</c>), so the
/// rest of the <c>::backdrop</c> cascade was dropped on the floor.
///
/// <para>Regression for WPT
/// <c>html/semantics/interactive-elements/the-dialog-element/modal-dialog-backdrop-opacity</c>
/// (2.2% pixel match against the Chromium reference): <c>opacity: 0.5</c> on a green scrim
/// painted fully opaque <c>rgb(0,128,0)</c> across the viewport instead of compositing to
/// <c>rgb(127,191,127)</c> over the white canvas.</para>
///
/// <para>Driven through <see cref="WptTestRunner"/> so it exercises the lever configuration CI
/// measures — <c>NativeBackdrop</c> off, i.e. the synthesized backdrop <c>&lt;div&gt;</c> path
/// where the drop happened.</para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class BackdropPaintingPropertiesWptTests
{
    private const int Size = 200;

    private static BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-backdrop-paint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "backdrop.html");
            File.WriteAllText(file, html);
            return new WptTestRunner(Size, Size).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void BackdropOpacity_CompositesScrimOverTheCanvas()
    {
        // The WPT fixture: an opaque green ::backdrop at opacity 0.5 over the white canvas.
        const string Html = """
<!DOCTYPE html><meta charset="utf-8">
<style>
  dialog::backdrop { background-color: rgb(0, 128, 0); opacity: 0.5; }
  dialog { outline: none; }
</style>
<dialog>backdrop at half opacity</dialog>
<script>document.querySelector('dialog').showModal();</script>
""";
        using var bmp = Render(Html);

        // 0.5 × green over white → 255×0.5 + {0,128,0}×0.5 = (127.5, 191.5, 127.5). Chromium
        // renders rgb(127,191,127); allow the runner's own ±5 per-channel comparison tolerance.
        foreach (var (x, y) in new[] { (2, 2), (Size - 3, 2), (2, Size - 3), (Size - 3, Size - 3) })
        {
            var px = bmp.GetPixel(x, y);
            Assert.True(Math.Abs(px.R - 127) <= 5 && Math.Abs(px.G - 191) <= 5 && Math.Abs(px.B - 127) <= 5,
                $"({x},{y}) was rgb({px.R},{px.G},{px.B}) — expected the half-opacity green scrim " +
                "rgb(127,191,127); a fully opaque rgb(0,128,0) means ::backdrop opacity was dropped.");
        }
    }

    [Fact]
    public void BackdropWithoutOpacity_StaysFullyOpaque()
    {
        // Guards the other direction: carrying painting properties across must not invent an
        // opacity for the common case, so an undeclared opacity leaves the scrim opaque.
        const string Html = """
<!DOCTYPE html><meta charset="utf-8">
<style>
  dialog::backdrop { background-color: rgb(0, 128, 0); }
  dialog { outline: none; }
</style>
<dialog>opaque backdrop</dialog>
<script>document.querySelector('dialog').showModal();</script>
""";
        using var bmp = Render(Html);

        var corner = bmp.GetPixel(2, 2);
        Assert.True(corner is { R: 0, G: 128, B: 0 },
            $"(2,2) was rgb({corner.R},{corner.G},{corner.B}) — expected opaque rgb(0,128,0).");
    }
}
