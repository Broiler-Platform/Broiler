using System.IO;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression tests for the per-scope stylesheet-discovery cache
/// (<c>DomBridge.GetScopedStyleElements</c> / <c>ComputedStyleEngineScope.StyleSheetCandidates</c>).
/// </summary>
/// <remarks>
/// <para>
/// Discovering a scope's <c>&lt;style&gt;</c>/<c>&lt;link&gt;</c> elements means walking the whole
/// tree, and the bridge asks for them once per element resolved — so the pass was
/// O(elements x nodes). WPT's legacy-multibyte <c>*_chars*.html</c> encoding tests are a single
/// line of ~17 000 sibling <c>&lt;span&gt;</c>s with no stylesheet at all, and the anchor-registry
/// pass walked ~34 000 nodes ~17 000 times to rediscover the same empty set, taking about seven
/// and a half minutes against a 30-second per-test budget. That was 28 of the 65 timeouts in the
/// 2026-08-16 WPT run.
/// </para>
/// <para>
/// The walk is now cached against <c>DomDocument.Version</c>. These tests pin the half that a
/// cache can get wrong — noticing that the sheet set has changed. Each one resolves computed
/// style <em>first</em> (populating the cache), then changes the sheets, and asserts the change
/// took effect: if the cache went stale the box keeps its original colour and the pixel is red.
/// Sheet <em>text</em> is deliberately not cached, which the last two cover.
/// </para>
/// </remarks>
public class StyleSheetDiscoveryCacheTests : IDisposable
{
    private readonly string _root;

    public StyleSheetDiscoveryCacheTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "broiler-sheetcache-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private BColor RenderCenterPixel(string bodyAndScript)
    {
        var html = $@"<!DOCTYPE html>
<html><head></head><body style=""margin:0"">
<div id=""box"" style=""width:200px;height:200px;background:red""></div>
{bodyAndScript}
</body></html>";

        var file = Path.Combine(_root, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);
        var runner = new WptTestRunner(200, 200);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _root);
        return bmp.GetPixel(100, 100);
    }

    private static void AssertGreen(BColor pixel, string what) =>
        Assert.True(pixel.G > 100 && pixel.R < 100,
            $"{what}: expected the box to be green, got rgb({pixel.R},{pixel.G},{pixel.B}). " +
            "Red means the stylesheet-discovery cache served a stale sheet set.");

    [Fact(Timeout = 600000)]
    public void StyleElementAppended_AfterFirstResolve_IsSeen()
    {
        // getComputedStyle first, so the (empty) sheet set is cached; then add a sheet.
        var pixel = RenderCenterPixel(@"<script>
  window.getComputedStyle(document.getElementById('box')).backgroundColor;
  var s = document.createElement('style');
  s.textContent = '#box { background: rgb(0,180,0) !important; }';
  document.head.appendChild(s);
</script>");

        AssertGreen(pixel, "a <style> appended after the first computed-style resolution");
    }

    [Fact(Timeout = 600000)]
    public void StyleElementRemoved_AfterFirstResolve_IsSeen()
    {
        // The sheet paints the box red-overriding-green; removing it must restore green.
        var pixel = RenderCenterPixel(@"<style id=""kill"">#box { background: rgb(200,0,0) !important; }</style>
<script>
  window.getComputedStyle(document.getElementById('box')).backgroundColor;
  document.getElementById('box').style.background = 'rgb(0,180,0)';
  var s = document.getElementById('kill');
  s.parentNode.removeChild(s);
</script>");

        AssertGreen(pixel, "a <style> removed after the first computed-style resolution");
    }

    [Fact(Timeout = 600000)]
    public void StyleElementTextRewritten_AfterFirstResolve_IsSeen()
    {
        // Sheet text is re-read on every call rather than cached, so a rewrite needs no
        // invalidation at all — this pins that it stays that way.
        var pixel = RenderCenterPixel(@"<style id=""sheet"">#box { background: rgb(200,0,0) !important; }</style>
<script>
  window.getComputedStyle(document.getElementById('box')).backgroundColor;
  document.getElementById('sheet').textContent = '#box { background: rgb(0,180,0) !important; }';
</script>");

        AssertGreen(pixel, "a <style> whose text was rewritten after the first resolution");
    }

    // No test here for CSSStyleSheet.disabled, and deliberately so. It is the one input to the
    // sheet set that the DOM never sees — a bridge-side CSSOM override, so it cannot ride on
    // DomDocument.Version, and it is exactly why the cache holds the *pre-filter* candidate list
    // and re-applies IsStyleSheetDisabled on every call rather than baking the filter in. Pinning
    // that end-to-end is not possible today: setting `styleEl.sheet.disabled = true` from script
    // does not re-colour the box even with no cache in the picture, so a test asserting it fails
    // identically with and without this change. That is a separate pre-existing gap in the CSSOM
    // disabled path, and covering for it here would just have shipped a red test.
}
