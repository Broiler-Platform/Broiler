using Broiler.HtmlBridge;
using RenderImageFormat = Broiler.HtmlBridge.ImageFormat;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 10 Acid3 compliance: computed-style and image-decoding behavior —
/// text-shadow / @font-face / visibility / display:inline-block / position /
/// border-style as observed through <c>getComputedStyle</c>, plus data: URI and
/// image-format detection.
///
/// RF-BRIDGE-1a: the tests that exercised the bridge's parallel paint pipeline
/// (<c>CssBoxModel.BuildLayoutTree</c>, <c>Painter</c>, <c>Compositor</c>,
/// <c>RenderOutput</c>, <c>CssFontFaceCollection</c>) were removed — that pipeline is
/// unused at runtime (the renderer's CssLayoutEngine paints) and is deprecated for
/// removal at the next htmlbridge-public-surface major. The live computed-style and
/// image-decoder coverage is retained below.
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

    // ────────────────────── 10.8 data: URI / image-format detection ──────────────────────

    [Fact]
    public void DataUri_Detected_As_Png()
    {
        var format = ImageDecoder.DetectFormat("data:image/png;base64,iVBOR");
        Assert.Equal(RenderImageFormat.Png, format);
    }

    [Fact]
    public void DataUri_Decode_Base64()
    {
        // "AQID" is base64 for bytes [1, 2, 3]
        var bytes = ImageDecoder.DecodeDataUri("data:image/png;base64,AQID");
        Assert.NotNull(bytes);
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
    }

    [Fact]
    public void DataUri_Decode_Invalid_Returns_Null()
    {
        var bytes = ImageDecoder.DecodeDataUri("not-a-data-uri");
        Assert.Null(bytes);
    }

    [Fact]
    public void DataUri_Decode_No_Base64_Returns_Null()
    {
        var bytes = ImageDecoder.DecodeDataUri("data:text/plain,hello");
        Assert.Null(bytes);
    }

    [Fact]
    public void ImageDecoder_DetectFormat_Png_Extension()
    {
        Assert.Equal(RenderImageFormat.Png, ImageDecoder.DetectFormat("image.png"));
    }

    [Fact]
    public void ImageDecoder_DetectFormat_Jpeg_Extension()
    {
        Assert.Equal(RenderImageFormat.Jpeg, ImageDecoder.DetectFormat("photo.jpg"));
    }

    [Fact]
    public void ImageDecoder_DetectFormatFromBytes_Png()
    {
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0 };
        Assert.Equal(RenderImageFormat.Png, ImageDecoder.DetectFormatFromBytes(pngHeader));
    }

    [Fact]
    public void ImageDecoder_CreatePlaceholder_Correct_Size()
    {
        var img = ImageDecoder.CreatePlaceholder(10, 10, RenderImageFormat.Png);
        Assert.Equal(10, img.Width);
        Assert.Equal(10, img.Height);
        Assert.Equal(400, img.PixelData.Length); // 10*10*4
    }
}
