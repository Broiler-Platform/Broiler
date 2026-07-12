namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 10 Acid3 compliance: computed-style behavior —
/// text-shadow / @font-face / visibility / display:inline-block / position /
/// border-style as observed through <c>getComputedStyle</c>.
///
/// RF-BRIDGE-1a: the tests that exercised the bridge's parallel paint pipeline
/// (<c>CssBoxModel.BuildLayoutTree</c>, <c>Painter</c>, <c>Compositor</c>,
/// <c>RenderOutput</c>, <c>CssFontFaceCollection</c>) were removed — that pipeline is
/// unused at runtime (the renderer's CssLayoutEngine paints) and is deprecated for
/// removal at the next htmlbridge-public-surface major.
///
/// The bridge's parallel image-format detector (<c>ImageDecoder</c> / <c>DecodedImage</c>
/// / <c>ImageFormat</c> in the former <c>ImagePipeline.cs</c>) was likewise removed once
/// raster decoding was consolidated onto the Broiler.Media codec catalog; its data-URI
/// and format-detection assertions went with it. SVG detection now asserts against the
/// live <c>BSvgRasterizer.IsSvgData</c> primitive in <see cref="SvgImageRenderingTests"/>.
/// </summary>
public class RenderingPipelineTests
{
    // ────────────────────── 10.1 text-shadow (computed style) ──────────────────────

    [Fact]
    public void TextShadow_Parsed_In_ComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { text-shadow: rgba(0,0,0,0.5) 2px 3px; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.getPropertyValue('text-shadow') !== '');
r.push(cs['text-shadow'] !== undefined);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void TextShadow_Set_Via_Style()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var d = document.createElement('div');
d.style.setProperty('text-shadow', 'rgba(0,0,0,0.5) 2px 3px');
document.getElementById('result').textContent = d.style.getPropertyValue('text-shadow');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("rgba(0,0,0,0.5) 2px 3px", result);
    }

    // ────────────────────── 10.2 @font-face (computed style) ──────────────────────

    [Fact]
    public void FontFace_ComputedStyle_Returns_FontFamily()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@font-face { font-family: 'CustomFont'; src: url('test.woff2'); }
#target { font-family: 'CustomFont'; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.getPropertyValue('font-family');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("CustomFont", result);
    }

    // ────────────────────── 10.3 visibility:hidden (computed style) ──────────────────────

    [Fact]
    public void Visibility_Hidden_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { visibility: hidden; }
</style>
</head><body>
<div id=""target"">hidden text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.getPropertyValue('visibility');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("hidden", result);
    }

    // ────────────────────── 10.4 display:inline-block (computed style) ──────────────────────

    [Fact]
    public void InlineBlock_ComputedStyle_Display()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { display: inline-block; width: 100px; height: 50px; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.getPropertyValue('display');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("inline-block", result);
    }

    // ────────────────────── 10.5 position:fixed (computed style) ──────────────────────

    [Fact]
    public void Position_Fixed_ComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { position: fixed; left: 10px; top: 20px; }
</style>
</head><body>
<div id=""target"">fixed</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.getPropertyValue('position');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("fixed", result);
    }

    // ────────────────────── 10.6 border-style (computed style) ──────────────────────

    [Fact]
    public void Border_Style_ComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { border: 2px dotted red; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.getPropertyValue('border-style');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("dotted", result);
    }

    // ────────────────────── 10.9 ::after pseudo-element ──────────────────────

    [Fact]
    public void PseudoElement_After_Selector_Matched()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target::after { content: 'after-text'; }
#target { color: red; }
</style>
</head><body>
<div id=""target"">main</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.getPropertyValue('color');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("red", result);
    }

    [Fact]
    public void PseudoElement_After_Content_Via_Style()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var d = document.createElement('div');
d.style.setProperty('content', '""hello""');
document.getElementById('result').textContent = d.style.getPropertyValue('content');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("hello", result);
    }

}
