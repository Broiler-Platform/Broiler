using System;
using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// CSS Transforms 1 §4 / CSS Containment §2: a <c>transform</c>, a layout/paint containment, or
/// <c>will-change: transform</c> makes an element the containing block for <em>all</em> its
/// descendants — <c>position: fixed</c> ones included, not just absolutely-positioned ones. The
/// engine resolved every fixed box against the viewport instead, so a <c>width/height: 100%</c>
/// fixed child of a transformed 100×100 block covered the whole page.
///
/// <para>Regression for WPT <c>css/css-contain/contain-paint-012</c> (1.3% pixel match against the
/// Chromium reference — Broiler painted 100% green where the reference is a green 100px square on
/// white). That test also pins the exclusion: its <c>contain: paint</c> sits on a
/// <c>&lt;span&gt;</c>, and paint containment does not apply to a non-atomic inline, so the span
/// must <em>not</em> capture the fixed child — the transformed block above it does.</para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class FixedPositionContainingBlockWptTests
{
    private const int Size = 200;

    private static BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-fixed-cb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "fixed-cb.html");
            File.WriteAllText(file, html);
            return new WptTestRunner(Size, Size).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    // Mirrors the WPT fixture: a transformed 100x100 red block, a contain:paint <span> inside it,
    // and a fixed 100%x100% green child. Correct output is a green 100px square with no red and no
    // green outside it.
    private const string Html = """
<!DOCTYPE html><meta charset="utf-8">
<style>
  html, body { margin: 0; }
  #containing-block { transform: translateX(0); background: red; width: 100px; height: 100px; }
  #contain-paint { contain: paint; }
  #fixed { position: fixed; bottom: 0; right: 0; background: green; width: 100%; height: 100%; }
</style>
<div id="containing-block"><span id="contain-paint"><div id="fixed"></div></span></div>
""";

    [Fact]
    public void FixedChild_ResolvesAgainstTransformedAncestor_NotTheViewport()
    {
        using var bmp = Render(Html);

        // Inside the 100x100 containing block: green, and no red left showing through.
        foreach (var (x, y) in new[] { (5, 5), (50, 50), (95, 95) })
        {
            var px = bmp.GetPixel(x, y);
            Assert.True(px is { R: 0, G: 128, B: 0 },
                $"({x},{y}) was rgb({px.R},{px.G},{px.B}) — expected the green fixed box.");
        }

        // Outside it the fixed box must not reach: sizing against the viewport painted here too.
        foreach (var (x, y) in new[] { (150, 50), (50, 150), (190, 190) })
        {
            var px = bmp.GetPixel(x, y);
            Assert.False(px is { R: 0, G: 128, B: 0 },
                $"({x},{y}) was green — the fixed box sized against the viewport instead of its " +
                "transformed containing block.");
        }
    }

    [Fact]
    public void FixedChild_WithNoEstablishingAncestor_StillUsesTheViewport()
    {
        // The other direction: with nothing establishing a containing block, a fixed box keeps
        // resolving against the viewport, so the change is a narrowing rather than a rewrite.
        const string PlainHtml = """
<!DOCTYPE html><meta charset="utf-8">
<style>
  html, body { margin: 0; }
  #wrap { width: 100px; height: 100px; background: red; }
  #fixed { position: fixed; top: 0; left: 0; background: green; width: 100%; height: 100%; }
</style>
<div id="wrap"><div id="fixed"></div></div>
""";
        using var bmp = Render(PlainHtml);

        foreach (var (x, y) in new[] { (5, 5), (150, 150), (Size - 3, Size - 3) })
        {
            var px = bmp.GetPixel(x, y);
            Assert.True(px is { R: 0, G: 128, B: 0 },
                $"({x},{y}) was rgb({px.R},{px.G},{px.B}) — a fixed box with no transformed/contained " +
                "ancestor must still fill the viewport.");
        }
    }
}
