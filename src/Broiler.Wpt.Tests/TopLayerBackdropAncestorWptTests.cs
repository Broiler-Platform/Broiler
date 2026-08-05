using System;
using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// CSS Position 4 §top-layer: a <c>::backdrop</c> is a top-layer box, so nothing its DOM
/// ancestors do can reach it. An ancestor transform must not move it, an ancestor filter must
/// not tint it, and a zero-sized <c>overflow: clip</c> ancestor must neither clip it nor become
/// the containing block its <c>inset</c> resolves against — the scrim covers the viewport in
/// every case.
///
/// <para>Locks in the fix for the WPT reftests
/// <c>html/semantics/interactive-elements/the-dialog-element/top-layer-parent-{transform,
/// filter,overflow-clip}</c>, which all rendered the red body instead of the green scrim
/// (~1% pixel match against the Chromium reference). Two independent defects:</para>
/// <list type="number">
///   <item>the bridge's synthesized backdrop <c>&lt;div&gt;</c> carried no
///   <c>data-broiler-top-layer</c> marker, so it painted in ordinary in-tree stacking order —
///   inside its ancestors' transform/filter/clip — while the dialog above it painted correctly
///   detached by the renderer's top-layer pass; and</item>
///   <item>a top-layer box resolved its positioned containing block by walking its DOM
///   ancestors, so an <c>overflow: clip</c> parent sized 0×0 captured it.</item>
/// </list>
///
/// <para>Runs through <see cref="WptTestRunner"/> rather than a bare
/// <c>DomBridge</c>+<c>HtmlRender</c> pair on purpose: the runner's lever configuration is what
/// CI measures (notably <c>NativeBackdrop</c> off, so the synthesized-div path is the one under
/// test), and asserting against the default bridge would have exercised the native ::backdrop
/// path instead and passed without the fix.</para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class TopLayerBackdropAncestorWptTests
{
    private const int Size = 200;

    // Body red, scrim green: any red pixel means the backdrop failed to cover that point.
    private static string Fixture(string parentStyle) => $$"""
<!DOCTYPE html><meta charset="utf-8">
<style>
  html, body { margin: 0; background: rgb(255, 0, 0); }
  #parent { {{parentStyle}} }
  dialog::backdrop { background: rgb(0, 128, 0); }
  dialog { width: 10px; height: 10px; margin: 0; border: 0; padding: 0; background: rgb(0, 128, 0); outline: none; }
</style>
<div id="parent"><dialog id="dlg">x</dialog></div>
<script>dlg.showModal();</script>
""";

    private static BBitmap Render(string parentStyle)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-top-layer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "top-layer.html");
            File.WriteAllText(file, Fixture(parentStyle));
            return new WptTestRunner(Size, Size).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Theory]
    // top-layer-parent-transform (the WPT fixture's own `scale(0)`, plus a translate that would
    // shift a non-detached backdrop somewhere visibly wrong rather than collapsing it).
    [InlineData("transform: scale(0);")]
    [InlineData("transform: translateX(60px);")]
    // top-layer-parent-filter.
    [InlineData("filter: invert(1);")]
    // top-layer-parent-overflow-clip.
    [InlineData("max-width: 0; max-height: 0; width: 0; height: 0; overflow: clip; position: absolute;")]
    public void Backdrop_CoversViewport_RegardlessOfAncestorTransformFilterOrClip(string parentStyle)
    {
        using var bmp = Render(parentStyle);

        foreach (var (x, y) in new[] { (2, 2), (Size - 3, 2), (2, Size - 3), (Size - 3, Size - 3) })
        {
            var px = bmp.GetPixel(x, y);
            Assert.True(px is { R: 0, G: 128, B: 0 },
                $"({x},{y}) was rgb({px.R},{px.G},{px.B}) — expected the green ::backdrop scrim " +
                $"for #parent {{ {parentStyle} }}");
        }
    }
}
