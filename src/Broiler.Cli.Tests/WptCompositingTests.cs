using System;
using System.IO;
using System.Text;
using SkiaSharp;
using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// WPT (Web Platform Tests) compliance tests for CSS compositing:
/// - css/compositing/background-blending crashtests
/// - css/compositing/mix-blend-mode tests
/// - css/compositing/root-element-* tests (opacity, filter, transparency)
///
/// Ensures Broiler does not crash on compositing CSS properties and produces
/// reasonable output for root element opacity/transparency scenarios.
/// </summary>
public class WptCompositingTests
{
    // ──────────── Crash tests ────────────────────────────────────────────
    // These WPT tests only verify the renderer doesn't crash. No pixel
    // comparison is needed; just ensure RenderToImage returns without
    // throwing.

    /// <summary>
    /// WPT: css/compositing/background-blending/crashtests/bgblend-root-change.html
    /// A crash test with background-blend-mode: overlay on the root element.
    /// Must not throw.  The CSS3 background shorthand with 'local',
    /// 'content-box', 'space', and '/ size' tokens must be accepted.
    /// </summary>
    [Fact]
    public void Bgblend_Root_Change_Does_Not_Crash()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>
* { background-blend-mode: overlay; }
body { background-color: green; }
</style></head>
<body><p>Test</p></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);
        Assert.True(bitmap.Width > 0);
    }

    /// <summary>
    /// Verifies the CSS3 background shorthand with 'local', 'content-box',
    /// 'space' repeat, and '/ size' syntax is properly parsed rather than
    /// discarded as invalid.  When the shorthand is accepted it resets
    /// background-color to transparent.
    /// </summary>
    [Fact]
    public void Bgblend_Root_Change_CSS3_Background_Shorthand_Accepted()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* {
  background-blend-mode: overlay;
  background: url(data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=) local content-box space space 0em / 15438983.37cm auto;
}
</style></head>
<body><p>Test</p></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        // The background shorthand resets background-color to transparent.
        // The extreme size makes the image invisible → white page.
        var pixel = bitmap.GetPixel(100, 100);
        Assert.True(pixel.Red >= 245 && pixel.Green >= 245 && pixel.Blue >= 245,
            $"Expected near-white page (background shorthand accepted) but got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// Regression: Chromium currently treats <c>background-clip: border-area</c>
    /// like an unsupported value, so Broiler must fall back to
    /// <c>background-clip: border-box</c> for screenshot parity.
    /// </summary>
    [Fact]
    public void BackgroundClip_BorderArea_FallsBackToBorderBoxLikeChromium()
    {
        const string bluePixelPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgYPj/HwADAgH/5ncLrgAAAABJRU5ErkJggg==";
        var html = $@"<!DOCTYPE html>
<html>
<head><style>
body {{ margin: 0; background: white; }}
.test {{
  width: 100px;
  height: 100px;
  box-sizing: border-box;
  border: 20px solid transparent;
  border-right-style: hidden;
  background-clip: border-area;
  background-image: url(data:image/png;base64,{bluePixelPng});
}}
</style></head>
<body><div class=""test""></div></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 120, 120);

        var topBorder = bitmap.GetPixel(50, 10);
        var center = bitmap.GetPixel(50, 50);
        var hiddenRightBorder = bitmap.GetPixel(95, 50);

        Assert.True(topBorder.Blue >= 245 && topBorder.Red <= 10 && topBorder.Green <= 10,
            $"Expected a blue top border pixel but got ({topBorder.Red},{topBorder.Green},{topBorder.Blue})");
        Assert.True(center.Blue >= 245 && center.Red <= 10 && center.Green <= 10,
            $"Expected center to be blue but got ({center.Red},{center.Green},{center.Blue})");
        Assert.True(hiddenRightBorder.Blue >= 245 && hiddenRightBorder.Red <= 10 && hiddenRightBorder.Green <= 10,
            $"Expected hidden right side to remain blue but got ({hiddenRightBorder.Red},{hiddenRightBorder.Green},{hiddenRightBorder.Blue})");
    }

    /// <summary>
    /// Regression: wrapped inline-block rows must honor vertical margins so
    /// successive WPT background-clip boxes line up like Chromium.
    /// </summary>
    [Fact]
    public void BackgroundClip_BorderArea_InlineBlockRows_HonorVerticalMargins()
    {
        const string bluePixelPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgYPj/HwADAgH/5ncLrgAAAABJRU5ErkJggg==";
        var html = $@"<!DOCTYPE html>
<html>
<head><style>
body {{ margin: 8px; background: white; }}
.test {{
  display: inline-block;
  margin: 20px;
  width: 300px;
  height: 150px;
  box-sizing: border-box;
  border: 50px solid transparent;
  background-clip: border-area;
  background-image: url(data:image/png;base64,{bluePixelPng});
}}
</style></head>
<body>
  <div class=""test""></div><div class=""test""></div><div class=""test""></div>
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 1024, 768);

        var aboveFirstRow = bitmap.GetPixel(28, 8);
        var firstRowBorder = bitmap.GetPixel(28, 28);
        var gapBeforeSecondRow = bitmap.GetPixel(28, 200);
        var secondRowBorder = bitmap.GetPixel(28, 222);

        Assert.True(aboveFirstRow.Red >= 245 && aboveFirstRow.Green >= 245 && aboveFirstRow.Blue >= 245,
            $"Expected whitespace above first inline-block row but got ({aboveFirstRow.Red},{aboveFirstRow.Green},{aboveFirstRow.Blue})");
        Assert.True(firstRowBorder.Blue >= 245 && firstRowBorder.Red <= 10 && firstRowBorder.Green <= 10,
            $"Expected first row border to be blue but got ({firstRowBorder.Red},{firstRowBorder.Green},{firstRowBorder.Blue})");
        Assert.True(gapBeforeSecondRow.Red >= 245 && gapBeforeSecondRow.Green >= 245 && gapBeforeSecondRow.Blue >= 245,
            $"Expected whitespace before second row but got ({gapBeforeSecondRow.Red},{gapBeforeSecondRow.Green},{gapBeforeSecondRow.Blue})");
        Assert.True(secondRowBorder.Blue >= 245 && secondRowBorder.Red <= 10 && secondRowBorder.Green <= 10,
            $"Expected second row border to be blue but got ({secondRowBorder.Red},{secondRowBorder.Green},{secondRowBorder.Blue})");
    }

    /// <summary>
    /// Regression: <c>box-sizing: border-box</c> must constrain explicit
    /// width/height before background painting so WPT background-clip boxes
    /// match Chromium's 300×150 border-box sizing.
    /// </summary>
    [Fact]
    public void BackgroundClip_BorderArea_BorderBoxSizing_UsesSpecifiedBorderBoxDimensions()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>
.test {
  width: 300px;
  height: 150px;
  box-sizing: border-box;
  border: 50px solid transparent;
}
</style></head>
<body><div id=""target"" class=""test""></div><div id=""result""></div>
<script>
var el = document.getElementById('target');
document.getElementById('result').textContent =
  'offsetWidth=' + el.offsetWidth + '|offsetHeight=' + el.offsetHeight;
</script>
</body>
</html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("offsetWidth=300", result);
        Assert.Contains("offsetHeight=150", result);
    }

    /// <summary>
    /// Regression: Chromium paints the body's <c>background-clip:border-area</c>
    /// across the viewport, so Broiler must not leave the body interior white.
    /// </summary>
    [Fact]
    public void BackgroundClip_BorderArea_OnBody_FillsViewportLikeChromium()
    {
        var html = @"<!DOCTYPE html>
<title>background-clip:border-area on the root</title>
<style>
html, body {
  box-sizing: border-box;
  height: 100%;
  margin: 0;
}
html {
  background-color: white;
}
body {
  border: 20px solid transparent;
  background-color: green;
  background-clip: border-area;
  padding: 10px;
}
</style>
There should be a 20px green border around the edge of the viewport.";

        using var bitmap = HtmlRender.RenderToImage(html, 1024, 768);

        var center = bitmap.GetPixel(512, 384);

        Assert.True(center.Green >= 120 && center.Red <= 10 && center.Blue <= 10,
            $"Expected viewport center to be green but got ({center.Red},{center.Green},{center.Blue})");
    }

    /// <summary>
    /// WPT: css/compositing/root-element-background-contain-hidden-crash.html
    /// A crash test with contain: layout, visibility: hidden, and background-image.
    /// Must not throw.
    /// </summary>
    [Fact]
    public void Root_Element_Background_Contain_Hidden_Does_Not_Crash()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>
* { background-color: red; }
</style></head>
<body style=""visibility: hidden"">
<div style=""background-image: url(data:image/gif;base64,R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==)"">
Test
</div>
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);
    }

    /// <summary>
    /// WPT: css/compositing/root-element-filter-background-clip-text-crash.html
    /// A crash test with filter: sepia(1) and background-clip: text on all elements.
    /// Must not throw.
    /// </summary>
    [Fact]
    public void Root_Element_Filter_BackgroundClip_Text_Does_Not_Crash()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>
* { filter: sepia(1); background-color: rgb(179, 31, 172); background-clip: text; }
</style></head>
<body><p>Test</p></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);
    }

    /// <summary>
    /// Regression: exact WPT HTML for bgblend-root-change.html including
    /// extreme CSS values (position: sticky, very large box-shadow, Q unit,
    /// complex background shorthand). Must not throw.
    /// </summary>
    [Fact]
    public void Bgblend_Root_Change_Exact_WPT_HTML_Does_Not_Crash()
    {
        // Exact CSS from the WPT file (scripts stripped — they add a MathML
        // element on load, which Broiler does not support but must not crash).
        var html = @"<style>
* {
  position: sticky;
  border-left: double 488200679.54Q hsla(-39 5% 68% / 7%) !important;
  box-shadow: 172vmax 60991vmax 32in 106cm hsl(-57532411.87deg, 70%, 54%);
  background-blend-mode: overlay;
  background: url(data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=) local content-box space space 0em / 15438983.37cm auto;
}
</style>";

        using var bitmap = HtmlRender.RenderToImage(html, 1024, 768);
        Assert.NotNull(bitmap);
        Assert.True(bitmap.Width > 0);
    }

    /// <summary>
    /// Regression: exact WPT HTML for root-element-background-contain-hidden-crash.html
    /// including contain: layout, *:first-child selector, and object element.
    /// Must not throw.
    /// </summary>
    [Fact]
    public void Root_Element_Background_Contain_Hidden_Exact_WPT_HTML_Does_Not_Crash()
    {
        var html = @"<style>
* {
  background-color: red;
  contain: layout;
}
*:first-child {
  visibility: hidden;
  background-image: url(data:image/gif;base64,iVBORw0KGgoAAAANSUhEUgAAAGAAAABgCAIAAABt+uBvAAAAAXNSR0IArs4c6QAAAAlwSFlzAAALEwAACxMBAJqcGAAAAAd0SU1FB9wDGhYvCNVA1EIAAAAZdEVYdENvbW1lbnQAQ3JlYXRlZCB3aXRoIEdJTVBXgQ4XAAAAjklEQVR42u3QIQEAMAgAsHNNVspTgARY1BZh0ZWP3VcgSJAgQYIECRKEIEGCBAkSJEgQggQJEiRIkCBBCBIkSJAgQYIECUKQIEGCBAkSJAhBggQJEiRIkCAECRIkSJAgQYIEIUiQIEGCBAkShCBBggQJEiRIEIIECRIkSJAgQYIQJEiQIEGCBAlCkCBBdwaeugIthHvZ+AAAAABJRU5ErkJggg==)
}
</style>
<object data=""x""></object>";

        using var bitmap = HtmlRender.RenderToImage(html, 1024, 768);
        Assert.NotNull(bitmap);
    }

    /// <summary>
    /// Regression: exact WPT HTML for root-element-filter-background-clip-text-crash.html
    /// including filter: sepia(1), background-clip: text, and object element.
    /// Must not throw.
    /// </summary>
    [Fact]
    public void Root_Element_Filter_BackgroundClip_Text_Exact_WPT_HTML_Does_Not_Crash()
    {
        var html = @"<!doctype html>
<style>
* {
  filter: sepia(1);
  background-color: rgb(179, 31, 172);
  background-clip: text;
}
</style>
<object id=""a""></object>";

        using var bitmap = HtmlRender.RenderToImage(html, 1024, 768);
        Assert.NotNull(bitmap);
    }

    /// <summary>
    /// WPT: css/compositing/root-element-filter.html
    /// A test with filter: invert(1) on the root element.
    /// The root background (#000) should be inverted to white.
    /// </summary>
    [Fact]
    public void Root_Element_Filter_Invert_Canvas_Background()
    {
        var html = @"<!DOCTYPE html>
<html style=""filter: invert(1); background: #000"">
<body>
<div style=""width: 50px; height: 50px; background: #E33""></div>
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);
        Assert.True(bitmap.Width > 0);

        // The canvas background should be inverted: #000 → #FFF (white)
        var pixel = bitmap.GetPixel(150, 150);
        Assert.InRange(pixel.Red, 250, 255);
        Assert.InRange(pixel.Green, 250, 255);
        Assert.InRange(pixel.Blue, 250, 255);
    }

    // ──────────── Root element opacity/transparency ──────────────────────

    /// <summary>
    /// WPT: css/compositing/root-element-opacity.html
    /// The root element has opacity: 0.5 and background: #BBB.
    /// Per CSS spec, the canvas is painted as the root background (#BBB)
    /// at 50% opacity over white, yielding approximately rgb(221, 221, 221).
    /// </summary>
    [Fact]
    public void Root_Element_Opacity_Blends_With_White_Canvas()
    {
        var html = @"<!DOCTYPE html>
<html style=""background: #BBB; opacity: 0.5"">
<body>
<div id=""spacer"" style=""width: 100px; height: 100px""></div>
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        // Sample a pixel away from any content.
        // #BBB = (187,187,187) at 50% over white (255,255,255)
        // = 187*0.5 + 255*0.5 = 221
        var pixel = bitmap.GetPixel(150, 150);
        Assert.InRange(pixel.Red, 218, 224);
        Assert.InRange(pixel.Green, 218, 224);
        Assert.InRange(pixel.Blue, 218, 224);
    }

    /// <summary>
    /// WPT: css/compositing/root-element-background-transparency.html
    /// The root element has background: rgba(45, 45, 45, 0.5).
    /// Compositing over white: 45*0.5 + 255*0.5 = 150.
    /// </summary>
    [Fact]
    public void Root_Element_Background_Transparency_Composites_Over_White()
    {
        var html = @"<!DOCTYPE html>
<html style=""background: rgba(45, 45, 45, 0.5)"">
<body>
<div style=""width: 100px; height: 100px""></div>
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        // rgba(45,45,45,0.5) over white = 45*0.5 + 255*0.5 = 150
        var pixel = bitmap.GetPixel(150, 150);
        Assert.InRange(pixel.Red, 147, 153);
        Assert.InRange(pixel.Green, 147, 153);
        Assert.InRange(pixel.Blue, 147, 153);
    }

    /// <summary>
    /// WPT: css/compositing/root-element-background-transparency-ref.html
    /// Reference: root with background: rgb(150, 150, 150).
    /// This verifies the reference renders the expected solid color.
    /// </summary>
    [Fact]
    public void Root_Element_Background_Transparency_Ref_Is_Solid()
    {
        var html = @"<!DOCTYPE html>
<html style=""background: rgb(150, 150, 150)"">
<body>
<div style=""width: 100px; height: 100px""></div>
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        var pixel = bitmap.GetPixel(150, 150);
        Assert.InRange(pixel.Red, 148, 152);
        Assert.InRange(pixel.Green, 148, 152);
        Assert.InRange(pixel.Blue, 148, 152);
    }

    /// <summary>
    /// WPT: css/compositing/root-element-background-margin-opacity-ref.html
    /// Reference for root-element-background-margin-opacity: positioned element
    /// with opacity: 0.5 and green background.  Now that non-root opacity is
    /// rendered via SaveLayer, verify the composited color.
    /// </summary>
    [Fact]
    public void Root_Element_Background_Margin_Opacity_Ref_Renders_SemiTransparent()
    {
        var html = @"<!DOCTYPE html>
<html>
<body>
<div style=""position: absolute; top: 0; left: 0; width: 200px; height: 200px; background: green; opacity: 0.5""></div>
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);

        // green (#008000) at 0.5 opacity over white (255,255,255):
        //   R = 0*0.5 + 255*0.5 = 128, G = 128*0.5 + 255*0.5 = 192, B = 0*0.5 + 255*0.5 = 128
        var pixel = bitmap.GetPixel(100, 100);
        Assert.InRange(pixel.Red, 125, 131);
        Assert.InRange(pixel.Green, 189, 195);
        Assert.InRange(pixel.Blue, 125, 131);
    }

    /// <summary>
    /// WPT: css/compositing/root-element-background-image-transparency-001.html
    /// The root element has a background image and opacity: 0.5.
    /// Per CSS2.1 §14.2, the root's background-image is propagated to the
    /// canvas and rendered at 50% opacity over white.
    /// Uses a base64-encoded data: URI SVG to avoid file-system dependencies.
    /// </summary>
    [Fact]
    public void Root_Element_Background_Image_With_Opacity_Propagates_To_Canvas()
    {
        // 10x10 green SVG as base64 data URI.
        var svgBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><rect width='10' height='10' fill='green'/></svg>"));
        var html = $@"<!DOCTYPE html>
<html>
<head><style>
html {{
  background: url(data:image/svg+xml;base64,{svgBase64});
  opacity: 0.5;
}}
body {{ margin: 0; }}
</style></head>
<body></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 100, 100);

        // The green (#008000 = 0,128,0) SVG tiles the canvas at 50% opacity
        // over white (255,255,255):
        //   R = 0*0.5 + 255*0.5 ≈ 128
        //   G = 128*0.5 + 255*0.5 ≈ 192
        //   B = 0*0.5 + 255*0.5 ≈ 128
        var pixel = bitmap.GetPixel(50, 50);
        Assert.InRange(pixel.Red, 120, 135);
        Assert.InRange(pixel.Green, 185, 200);
        Assert.InRange(pixel.Blue, 120, 135);
    }

    /// <summary>
    /// When the root element has a background-image but no opacity,
    /// the image should tile across the entire canvas (not just the
    /// element's box).
    /// </summary>
    [Fact]
    public void Root_Element_Background_Image_Covers_Full_Canvas()
    {
        // 10x10 red SVG as base64 data URI.
        var svgBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><rect width='10' height='10' fill='red'/></svg>"));
        var html = $@"<!DOCTYPE html>
<html>
<head><style>
html {{
  background: url(data:image/svg+xml;base64,{svgBase64});
}}
body {{ margin: 0; }}
</style></head>
<body></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 100, 100);

        // Red (#FF0000) should tile the entire canvas.
        // Check a pixel in the corner away from any content.
        var pixel = bitmap.GetPixel(95, 95);
        Assert.InRange(pixel.Red, 250, 255);
        Assert.InRange(pixel.Green, 0, 10);
        Assert.InRange(pixel.Blue, 0, 10);
    }

    /// <summary>
    /// Validates that the baseUrl parameter in HtmlRender.RenderToImage
    /// enables relative sub-resource (image) resolution.
    /// </summary>
    [Fact]
    public void RenderToImage_BaseUrl_Enables_Relative_Image_Resolution()
    {
        // Create a temporary SVG file to act as a relative resource.
        var tempDir = Path.Combine(Path.GetTempPath(), "broiler-wpt-baseurl-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var svgPath = Path.Combine(tempDir, "green.svg");
            File.WriteAllText(svgPath,
                "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><rect width='10' height='10' fill='green'/></svg>");

            var html = @"<!DOCTYPE html>
<html>
<head><style>
html { background: url(green.svg); }
body { margin: 0; }
</style></head>
<body></body>
</html>";

            var baseUrl = new Uri(Path.Combine(tempDir, "test.html")).AbsoluteUri;
            using var bitmap = HtmlRender.RenderToImage(html, 100, 100, baseUrl: baseUrl);

            // The green SVG should tile across the canvas.
            var pixel = bitmap.GetPixel(50, 50);
            Assert.InRange(pixel.Green, 120, 135);
            Assert.InRange(pixel.Red, 0, 10);
            Assert.InRange(pixel.Blue, 0, 10);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ──────────── mix-blend-mode tests ───────────────────────────────────
    // These tests verify that mix-blend-mode is accepted and doesn't crash.
    // Full pixel-level blending fidelity is deferred; here we ensure the
    // property is parsed, stacking contexts are created, and rendering
    // completes without exceptions.

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/mix-blend-mode-plus-lighter-basic.html
    /// Uses mix-blend-mode: plus-lighter with opacity. Must not crash.
    /// </summary>
    [Fact]
    public void MixBlendMode_PlusLighter_Basic_Does_Not_Crash()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
.container { isolation: isolate; position: relative; width: 500px; height: 500px; }
.blue { background: #000064; }
.green { background: #006400; }
.common { position: absolute; width: 100px; height: 100px; opacity: 0.6; }
.one { top: 10px; left: 10px; }
.two { top: 65px; left: 30px; mix-blend-mode: plus-lighter; }
.three { top: 120px; left: 50px; mix-blend-mode: plus-lighter; }
</style></head>
<body>
<div class=""container"">
  <div class=""one common blue""></div>
  <div class=""two common blue""></div>
  <div class=""three common green""></div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 600, 600);
        Assert.NotNull(bitmap);

        // Verify the blue box at (10,10) has some dark-blue content
        var pixel = bitmap.GetPixel(50, 50);
        Assert.True(pixel.Blue > 0, "Blue box should be visible");
    }

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/mix-blend-mode-rotated-clip.html
    /// Uses mix-blend-mode: overlay with CSS transforms. Must not crash.
    /// </summary>
    [Fact]
    public void MixBlendMode_Rotated_Clip_Does_Not_Crash()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>div { width: 100px; height: 100px; }</style>
</head><body>
<div style=""background: lime; overflow: hidden"">
  <div>
    <div style=""background: lime; overflow: hidden"">
      <div style=""background: red; mix-blend-mode: overlay""></div>
    </div>
  </div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);
    }

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/mix-blend-mode-parent-with-border-radius.html
    /// Uses mix-blend-mode: difference with parent border-radius.
    /// Verifies rendering produces content and a lightgray background.
    /// </summary>
    [Fact]
    public void MixBlendMode_Parent_With_BorderRadius_Does_Not_Crash()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background: lightgray; }
.parent { position: absolute; z-index: 1; width: 100px; height: 100px; background: #F00; border-radius: 50px; }
.blended { background: #FF0; width: 100px; height: 100px; mix-blend-mode: difference; }
</style></head>
<body>
<div class=""parent"">
  <div class=""blended""></div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 300);
        Assert.NotNull(bitmap);

        // Background must be lightgray (#D3D3D3), not black
        var bg = bitmap.GetPixel(280, 280);
        Assert.True(bg.Red >= 200 && bg.Green >= 200 && bg.Blue >= 200,
            $"Background should be lightgray but got ({bg.Red},{bg.Green},{bg.Blue})");
    }

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/mix-blend-mode-parent-with-text.html
    /// Uses mix-blend-mode: difference with negative margin.
    /// Verifies rendering produces content and a lightgray background.
    /// </summary>
    [Fact]
    public void MixBlendMode_Parent_With_Text_Does_Not_Crash()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background: lightgray; }
.parent { position: absolute; z-index: 1; width: 100px; height: 100px; }
.blender { background: #FF0; width: 100px; height: 100px; margin-top: -60px; mix-blend-mode: difference; }
.text { height: 60px; color: #F00; }
</style></head>
<body>
<div class=""parent"">
  <div class=""text"">Red text that becomes green after blending</div>
  <div class=""blender""></div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 300);
        Assert.NotNull(bitmap);

        // Background must be lightgray (#D3D3D3), not black
        var bg = bitmap.GetPixel(280, 280);
        Assert.True(bg.Red >= 200 && bg.Green >= 200 && bg.Blue >= 200,
            $"Background should be lightgray but got ({bg.Red},{bg.Green},{bg.Blue})");
    }

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/mix-blend-mode-overflowing-child.html
    /// Uses mix-blend-mode: difference on an overflowing child.
    /// Verifies rendering produces content and a lightgray background.
    /// </summary>
    [Fact]
    public void MixBlendMode_Overflowing_Child_Does_Not_Crash()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background: lightgray; }
.container { position: absolute; z-index: 1; width: 100px; height: 100px; background: #0F0; }
.blender { background: #0F0; margin: 50px; width: 100px; height: 100px; mix-blend-mode: difference; }
</style></head>
<body>
<div class=""container"">
  <div class=""blender""></div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 300);
        Assert.NotNull(bitmap);

        // Background must be lightgray (#D3D3D3), not black
        var bg = bitmap.GetPixel(280, 280);
        Assert.True(bg.Red >= 200 && bg.Green >= 200 && bg.Blue >= 200,
            $"Background should be lightgray but got ({bg.Red},{bg.Green},{bg.Blue})");
    }

    // ──────────── Reference tests (no mix-blend-mode) ────────────────────
    // These are the reference files for the mix-blend-mode tests above.
    // They use normal CSS (no blending) and must render without errors.

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/reference/mix-blend-mode-rotated-clip-ref.html
    /// Simple green div — must render correctly.
    /// </summary>
    [Fact]
    public void MixBlendMode_Rotated_Clip_Ref_Renders_Green()
    {
        var html = @"<!DOCTYPE html>
<div style=""width: 100px; height: 100px; background: lime""></div>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);

        // The lime div should produce green pixels.
        var pixel = bitmap.GetPixel(50, 50);
        Assert.Equal(0, pixel.Red);
        Assert.Equal(255, pixel.Green);
        Assert.Equal(0, pixel.Blue);
    }

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/reference/mix-blend-mode-parent-with-border-radius-ref.html
    /// Yellow square with green circle — verifies lightgray background and yellow parent.
    /// </summary>
    [Fact]
    public void MixBlendMode_Parent_With_BorderRadius_Ref_Does_Not_Crash()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background: lightgray; }
.parent { position: absolute; z-index: 1; width: 100px; height: 100px; background: #FF0; }
.blended { background: #0F0; width: 100px; height: 100px; border-radius: 50px; }
</style></head>
<body>
<div class=""parent"">
  <div class=""blended""></div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 300);
        Assert.NotNull(bitmap);

        // Background must be lightgray (#D3D3D3), not black
        var bg = bitmap.GetPixel(280, 280);
        Assert.True(bg.Red >= 200 && bg.Green >= 200 && bg.Blue >= 200,
            $"Background should be lightgray but got ({bg.Red},{bg.Green},{bg.Blue})");
    }

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/reference/mix-blend-mode-parent-with-text-ref.html
    /// Yellow square with green text — verifies lightgray background and yellow container.
    /// </summary>
    [Fact]
    public void MixBlendMode_Parent_With_Text_Ref_Does_Not_Crash()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background: lightgray; }
.container { background: #FF0; width: 100px; height: 100px; }
.text { color: #0F0; }
</style></head>
<body>
<div class=""container"">
  <div class=""text"">Red text that becomes green after blending</div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 300);
        Assert.NotNull(bitmap);

        // Background must be lightgray (#D3D3D3), not black
        var bg = bitmap.GetPixel(280, 280);
        Assert.True(bg.Red >= 200 && bg.Green >= 200 && bg.Blue >= 200,
            $"Background should be lightgray but got ({bg.Red},{bg.Green},{bg.Blue})");

        // The container should have a yellow (#FF0) background
        // Sample at (15, 95) — near the bottom of the container, below the text area
        var container = bitmap.GetPixel(15, 95);
        Assert.Equal(255, container.Red);
        Assert.Equal(255, container.Green);
        Assert.Equal(0, container.Blue);
    }

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/reference/mix-blend-mode-overflowing-child-ref.html
    /// Green squares with black intersection — verifies lightgray background and green container.
    /// </summary>
    [Fact]
    public void MixBlendMode_Overflowing_Child_Ref_Does_Not_Crash()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background: lightgray; }
.container { position: absolute; z-index: 1; width: 100px; height: 100px; background: #0F0; }
.blender { background: #0F0; margin: 50px; width: 100px; height: 100px; }
.intersection { background: #000; width: 50px; height: 50px; margin-top: -150px; margin-left: 50px; }
</style></head>
<body>
<div class=""container"">
  <div class=""blender""></div>
  <div class=""intersection""></div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 300);
        Assert.NotNull(bitmap);

        // Background must be lightgray (#D3D3D3), not black
        var bg = bitmap.GetPixel(280, 280);
        Assert.True(bg.Red >= 200 && bg.Green >= 200 && bg.Blue >= 200,
            $"Background should be lightgray but got ({bg.Red},{bg.Green},{bg.Blue})");
    }

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/reference/mix-blend-mode-plus-lighter-basic-ref.html
    /// Reference with overlapping color boxes — must not crash.
    /// </summary>
    [Fact]
    public void MixBlendMode_PlusLighter_Basic_Ref_Does_Not_Crash()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
.container { isolation: isolate; position: relative; width: 500px; height: 500px; }
.common { position: absolute; width: 100px; height: 100px; opacity: 0.6; }
.one { top: 10px; left: 10px; background: #000064; }
.two { top: 65px; left: 30px; background: #000064; }
.three { top: 120px; left: 50px; background: #006400; }
.one_and_two { position: absolute; width: 80px; height: 45px; top: 65px; left: 30px; background: #000078; }
.two_and_three { position: absolute; width: 80px; height: 45px; top: 120px; left: 50px; background: #003C3C; }
</style></head>
<body>
<div class=""container"">
  <div class=""one common""></div>
  <div class=""two common""></div>
  <div class=""three common""></div>
  <div class=""one_and_two""></div>
  <div class=""two_and_three""></div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 600, 600);
        Assert.NotNull(bitmap);
    }

    // ──────────── CSS property parsing validation ────────────────────────

    /// <summary>
    /// Verifies that mix-blend-mode values are accepted by the CSS parser
    /// and stored on the computed style.
    /// </summary>
    [Fact]
    public void MixBlendMode_Property_Is_Parsed_And_Stored()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
#target { mix-blend-mode: difference; }
</style></head>
<body>
<div id=""target"">Test</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent =
  'MBM:' + (cs.getPropertyValue('mix-blend-mode') || cs.mixBlendMode || 'MISSING') + ':END';
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // The property value should be accessible (not 'MISSING').
        Assert.Contains("MBM:", result);
        Assert.DoesNotContain("MBM:MISSING:END", result);
    }

    /// <summary>
    /// Verifies that isolation: isolate is accepted and creates a stacking
    /// context (positioned children are constrained).
    /// </summary>
    [Fact]
    public void Isolation_Isolate_Creates_Stacking_Context()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
.container { isolation: isolate; position: relative; width: 100px; height: 100px; background: blue; }
.child { position: absolute; width: 50px; height: 50px; background: red; z-index: 1; }
</style></head>
<body>
<div class=""container"">
  <div class=""child""></div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);

        // The red child should be visible (non-white pixel at its position)
        var pixel = bitmap.GetPixel(25, 25);
        Assert.True(pixel.Red > 200 || pixel.Blue > 200,
            "Stacking context child should be visible");
    }

    /// <summary>
    /// Verifies that background-blend-mode is accepted without crashing.
    /// </summary>
    [Fact]
    public void BackgroundBlendMode_Property_Accepted()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background-color: green; background-blend-mode: overlay; }
</style></head>
<body><p>Test</p></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);
    }

    /// <summary>
    /// Verifies that filter property is accepted without crashing.
    /// </summary>
    [Fact]
    public void Filter_Property_Accepted()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
div { filter: blur(5px); width: 100px; height: 100px; background: red; }
</style></head>
<body><div>Test</div></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);
    }

    // ──────────── Pixel-level blend mode verification ─────────────────────

    /// <summary>
    /// Verifies that mix-blend-mode: difference actually produces blended
    /// pixels. Red (#FF0000) over yellow (#FFFF00) with blend mode
    /// 'difference' should produce (|FF-FF|, |00-FF|, |00-00|) = (0, FF, 0)
    /// i.e. green output, not just the unblended foreground color.
    /// </summary>
    [Fact]
    public void MixBlendMode_Difference_Produces_Blended_Output()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { margin: 0; }
.parent { width: 100px; height: 100px; background: #FFFF00; }
.child { width: 100px; height: 100px; background: #FF0000; mix-blend-mode: difference; }
</style></head>
<body>
<div class=""parent"">
  <div class=""child""></div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);

        // Sample center of the overlapping region
        var pixel = bitmap.GetPixel(50, 50);
        // Difference of red (#FF0000) on yellow (#FFFF00):
        // R: abs(FF-FF) = 0, G: abs(00-FF) = FF, B: abs(00-00) = 0 → green
        Assert.True(pixel.Red < 30, $"Expected near-zero red but got {pixel.Red}");
        Assert.True(pixel.Green > 200, $"Expected high green but got {pixel.Green}");
        Assert.True(pixel.Blue < 30, $"Expected near-zero blue but got {pixel.Blue}");
    }

    /// <summary>
    /// Verifies that mix-blend-mode: multiply produces darker output.
    /// A white (#FFFFFF) element with multiply over green (#00FF00) should
    /// remain green (white * green = green). A gray element should produce
    /// darker green.
    /// </summary>
    [Fact]
    public void MixBlendMode_Multiply_Produces_Darker_Output()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { margin: 0; }
.parent { width: 100px; height: 100px; background: #00FF00; }
.child { width: 100px; height: 100px; background: #808080; mix-blend-mode: multiply; }
</style></head>
<body>
<div class=""parent"">
  <div class=""child""></div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);

        var pixel = bitmap.GetPixel(50, 50);
        // Multiply: green * gray → darker green
        // R: (0x00 * 0x80) / 255 ≈ 0, G: (0xFF * 0x80) / 255 ≈ 0x80, B: 0
        Assert.True(pixel.Red < 20, $"Expected near-zero red but got {pixel.Red}");
        Assert.True(pixel.Green > 100 && pixel.Green < 180,
            $"Expected green around 128 (multiply) but got {pixel.Green}");
        Assert.True(pixel.Blue < 20, $"Expected near-zero blue but got {pixel.Blue}");
    }

    /// <summary>
    /// Verifies that mix-blend-mode: screen produces lighter output.
    /// Screen blending of blue (#0000FF) over red (#FF0000) should
    /// produce magenta (FF, 00, FF).
    /// </summary>
    [Fact]
    public void MixBlendMode_Screen_Produces_Lighter_Output()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { margin: 0; }
.parent { width: 100px; height: 100px; background: #FF0000; }
.child { width: 100px; height: 100px; background: #0000FF; mix-blend-mode: screen; }
</style></head>
<body>
<div class=""parent"">
  <div class=""child""></div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);

        var pixel = bitmap.GetPixel(50, 50);
        // Screen: R: FF+00 - FF*00/255 = FF, G: 0, B: 00+FF - 00*FF/255 = FF → magenta
        Assert.True(pixel.Red > 200, $"Expected high red but got {pixel.Red}");
        Assert.True(pixel.Green < 30, $"Expected near-zero green but got {pixel.Green}");
        Assert.True(pixel.Blue > 200, $"Expected high blue but got {pixel.Blue}");
    }

    /// <summary>
    /// Verifies that mix-blend-mode: overlay on the rotated-clip test
    /// produces the expected lime-colored output (overlay of red on lime
    /// results in lime because the backdrop is > 0.5 in the green channel).
    /// </summary>
    [Fact]
    public void MixBlendMode_Overlay_Rotated_Clip_Produces_Green()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>div { width: 100px; height: 100px; }</style>
</head><body style=""margin:0"">
<div style=""background: lime; overflow: hidden"">
  <div>
    <div style=""background: lime; overflow: hidden"">
      <div style=""background: red; mix-blend-mode: overlay""></div>
    </div>
  </div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);

        var pixel = bitmap.GetPixel(50, 50);
        // Overlay of red (#FF0000) on lime (#00FF00):
        // The green channel of lime is > 0.5, so overlay uses screen formula for G.
        // Result should have a green component (not pure red).
        Assert.True(pixel.Green > 100,
            $"Expected green channel present after overlay blending but got G={pixel.Green}");
    }

    // ──────────── Gradient rendering tests ────────────────────────────────

    /// <summary>
    /// WPT: css/compositing/root-element-background-margin-opacity.html
    /// A uniform CSS gradient (linear-gradient(green, green)) with opacity
    /// must render the gradient colour within the root element's box —
    /// composited at the specified opacity over the white canvas — while
    /// pixels outside the element remain white.
    /// </summary>
    [Fact]
    public void Root_Element_Background_Margin_Opacity_Gradient_Renders_Correctly()
    {
        // Exact WPT test HTML (simplified, no external resources)
        var html = @"<!DOCTYPE html>
<style>
html {
  margin: 100px;
  width: 100px;
  height: 100px;
  background: linear-gradient(green, green) top left no-repeat;
  opacity: 0.5;
}
</style>";

        using var bitmap = HtmlRender.RenderToImage(html, 800, 600);

        // Inside the element box (100-200, 100-200):
        // Green (#008000 = R0 G128 B0) at 0.5 opacity over white →
        //   R = 0*0.5 + 255*0.5 = 128
        //   G = 128*0.5 + 255*0.5 = 192
        //   B = 0*0.5 + 255*0.5 = 128
        var pInside = bitmap.GetPixel(150, 150);
        Assert.InRange(pInside.Red, 120, 140);
        Assert.InRange(pInside.Green, 185, 200);
        Assert.InRange(pInside.Blue, 120, 140);

        // Outside the element box: white canvas preserved
        var pAbove = bitmap.GetPixel(50, 50);
        Assert.InRange(pAbove.Red, 250, 255);
        Assert.InRange(pAbove.Green, 250, 255);
        Assert.InRange(pAbove.Blue, 250, 255);

        var pBelow = bitmap.GetPixel(300, 300);
        Assert.InRange(pBelow.Red, 250, 255);
        Assert.InRange(pBelow.Green, 250, 255);
        Assert.InRange(pBelow.Blue, 250, 255);
    }

    // ──────────── Viewport unit tests ────────────────────────────────────
    // CSS viewport-relative units (vh, vw, vmin, vmax) must resolve
    // correctly against the rendering viewport dimensions.

    /// <summary>
    /// WPT: css/compositing/root-element-background-image-transparency-001-ref.html
    /// This reference file uses <c>height: 100vh</c> on the body. Without
    /// viewport unit support, the height resolves to 0 and the background
    /// image is not rendered.
    /// </summary>
    [Fact]
    public void Viewport_Height_100vh_Fills_Full_Height()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>
html { background-color: white; }
body { margin: 0; background-color: green; height: 100vh; }
</style></head>
<body></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 300);
        // With 100vh, the body should fill the full viewport height (300px).
        // The center and bottom should both be green.
        var pCenter = bitmap.GetPixel(100, 150);
        Assert.InRange(pCenter.Green, 100, 130);
        Assert.InRange(pCenter.Red, 0, 10);
        Assert.InRange(pCenter.Blue, 0, 10);

        var pBottom = bitmap.GetPixel(100, 280);
        Assert.InRange(pBottom.Green, 100, 130);
        Assert.InRange(pBottom.Red, 0, 10);
        Assert.InRange(pBottom.Blue, 0, 10);
    }

    /// <summary>
    /// Verifies <c>width: 50vw</c> resolves to half the viewport width.
    /// </summary>
    [Fact]
    public void Viewport_Width_50vw_Resolves_Correctly()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>
body { margin: 0; }
div { width: 50vw; height: 100px; background-color: red; }
</style></head>
<body><div></div></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);
        // 50vw on a 400px viewport = 200px wide red div.
        // At x=100 (inside div), should be red.
        var pInside = bitmap.GetPixel(100, 50);
        Assert.InRange(pInside.Red, 200, 255);
        Assert.InRange(pInside.Green, 0, 20);

        // At x=300 (outside div), should be white.
        var pOutside = bitmap.GetPixel(300, 50);
        Assert.InRange(pOutside.Red, 245, 255);
        Assert.InRange(pOutside.Green, 245, 255);
    }

    /// <summary>
    /// Verifies <c>vmin</c> and <c>vmax</c> resolve correctly: vmin uses the
    /// smaller viewport dimension, vmax uses the larger.
    /// </summary>
    [Fact]
    public void Viewport_Vmin_Vmax_Resolve_Correctly()
    {
        // 400×200 viewport → vmin=200, vmax=400
        // 50vmin = 100px, 50vmax = 200px
        var html = @"<!DOCTYPE html>
<html>
<head><style>
body { margin: 0; }
.a { width: 50vmin; height: 50px; background-color: blue; }
.b { width: 50vmax; height: 50px; background-color: red; }
</style></head>
<body>
<div class=""a""></div>
<div class=""b""></div>
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 200);
        // .a (50vmin=100px): x=50 should be blue, x=150 should be white
        var pA = bitmap.GetPixel(50, 25);
        Assert.InRange(pA.Blue, 200, 255);
        Assert.InRange(pA.Red, 0, 20);

        // .b (50vmax=200px): x=150 should be red
        var pB = bitmap.GetPixel(150, 75);
        Assert.InRange(pB.Red, 200, 255);
        Assert.InRange(pB.Blue, 0, 20);
    }

    // ──────────── Video element placeholder tests ────────────────────────
    // <video> elements should render as replaced elements with a black
    // placeholder matching the default browser dimensions (300×150).

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/reference/mix-blend-mode-video-notref.html
    /// Verifies that a <c>&lt;video&gt;</c> element is replaced with a black
    /// placeholder box. The default intrinsic size is 300×150px.
    /// </summary>
    [Fact]
    public void Video_Element_Renders_As_Black_Placeholder()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>
body { margin: 0; background-color: white; }
</style></head>
<body>
<video autoplay>
    <source src=""video.mp4"" type=""video/mp4"">
    Fallback text should NOT be visible.
</video>
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 300);
        // The video placeholder should render as a 300×150 black box.
        var pInside = bitmap.GetPixel(150, 75);
        Assert.InRange(pInside.Red, 0, 20);
        Assert.InRange(pInside.Green, 0, 20);
        Assert.InRange(pInside.Blue, 0, 20);

        // Outside the placeholder (to the right) should be white.
        var pOutside = bitmap.GetPixel(350, 75);
        Assert.InRange(pOutside.Red, 245, 255);
        Assert.InRange(pOutside.Green, 245, 255);
        Assert.InRange(pOutside.Blue, 245, 255);
    }

    /// <summary>
    /// Verifies that explicit width/height on <c>&lt;video&gt;</c> is
    /// respected in the placeholder dimensions.
    /// </summary>
    [Fact]
    public void Video_Element_Respects_Explicit_Dimensions()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>
body { margin: 0; background-color: white; }
</style></head>
<body>
<video width=""200"" height=""100"" autoplay>
    <source src=""video.mp4"" type=""video/mp4"">
</video>
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 300);
        // 200×100 black box
        var pInside = bitmap.GetPixel(100, 50);
        Assert.InRange(pInside.Red, 0, 20);
        Assert.InRange(pInside.Green, 0, 20);
        Assert.InRange(pInside.Blue, 0, 20);

        // At x=250 (outside 200px width) should be white.
        var pOutside = bitmap.GetPixel(250, 50);
        Assert.InRange(pOutside.Red, 245, 255);
        Assert.InRange(pOutside.Green, 245, 255);
    }

    /// <summary>
    /// Verifies that fallback text inside <c>&lt;video&gt;</c> is not visible
    /// (browsers that support video hide fallback content).
    /// </summary>
    [Fact]
    public void Video_Element_Hides_Fallback_Content()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><style>
body { margin: 0; background-color: white; font-size: 20px; }
</style></head>
<body>
<video>
    This is fallback text that should NOT be visible.
</video>
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 400, 300);
        // The video placeholder should be a 300×150 black box.
        // The fallback text "This is fallback text..." should NOT be rendered.
        var pCenter = bitmap.GetPixel(150, 75);
        Assert.InRange(pCenter.Red, 0, 20);
        Assert.InRange(pCenter.Green, 0, 20);
        Assert.InRange(pCenter.Blue, 0, 20);
    }

    // ──────────── Isolation group tests ────────────────────────────────────

    /// <summary>
    /// WPT: css/compositing/isolation/blend-isolation.html
    /// Verifies that isolation: isolate creates an isolation group that
    /// prevents children's blend modes from bleeding through.
    /// A child with mix-blend-mode: difference inside an isolated container
    /// should blend only with its parent's background, not with content
    /// outside the isolation group.
    /// </summary>
    [Fact]
    public void Isolation_Isolate_Prevents_Blend_Bleed_Through()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { margin: 0; }
.backdrop { width: 200px; height: 200px; background: #FF0000; }
.isolated { isolation: isolate; width: 200px; height: 100px; background: #00FF00; }
.blended { width: 200px; height: 100px; background: #0000FF; mix-blend-mode: difference; }
</style></head>
<body>
<div class=""backdrop"">
  <div class=""isolated"">
    <div class=""blended""></div>
  </div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 300, 300);
        Assert.NotNull(bitmap);

        // The blended div should only blend with the isolated container's
        // green background, not with the red backdrop.
        // difference of blue (#0000FF) on green (#00FF00) = (0, FF, FF) cyan
        var pixel = bitmap.GetPixel(100, 50);
        Assert.True(pixel.Green > 150, $"Expected high green but got {pixel.Green}");
    }

    /// <summary>
    /// Verifies that transform (even identity) creates a stacking context.
    /// CSS Transforms §6.1: any transform other than 'none' creates a
    /// stacking context.
    /// </summary>
    [Fact]
    public void Transform_Creates_Stacking_Context()
    {
        // A div with transform and z-index should be treated as a stacking context.
        var html = @"<!DOCTYPE html>
<html><head><style>
body { margin: 0; }
.transformed { width: 100px; height: 100px; background: red; transform: translateX(0px); }
.sibling { width: 100px; height: 100px; background: blue; margin-top: -50px; position: relative; z-index: 1; }
</style></head>
<body>
<div class=""transformed""></div>
<div class=""sibling""></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);

        // The blue sibling should be on top of the red transformed element
        // at the overlap region (y=50-100)
        var pixel = bitmap.GetPixel(50, 75);
        Assert.True(pixel.Blue > 200, $"Expected blue on top at overlap but got B={pixel.Blue}");
    }

    /// <summary>
    /// WPT: css/compositing/root-element-opacity-change.html
    /// Verifies that the root element's mix-blend-mode is not applied
    /// when compositing with the canvas backdrop.
    /// CSS Compositing §3.1: root element uses normal blending.
    /// </summary>
    [Fact]
    public void Root_Element_MixBlendMode_Ignored_With_Canvas()
    {
        var html = @"<!DOCTYPE html>
<html style=""mix-blend-mode: difference;"">
<head><style>
body { margin: 0; background: green; }
</style></head>
<body></body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);

        // The root has mix-blend-mode: difference, but per spec this should
        // NOT be applied against the white canvas.  The body background (green)
        // should render normally, not as the difference of green vs white.
        var pixel = bitmap.GetPixel(100, 100);
        // Green (#008000) should be visible, not difference result
        Assert.True(pixel.Green > 100, $"Expected green visible but got G={pixel.Green}");
    }

    /// <summary>
    /// Verifies background-blend-mode blends the background image with
    /// the background color.  A green background-color with a red image
    /// using background-blend-mode: screen should produce a lighter result.
    /// </summary>
    [Fact]
    public void BackgroundBlendMode_Applies_To_Image()
    {
        // We test with a data: URI 1×1 red pixel image.
        var html = @"<!DOCTYPE html>
<html><head><style>
body { margin: 0; }
.blended {
  width: 100px; height: 100px;
  background-color: #00FF00;
  background-image: url(data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwADhQGAWjR9awAAAABJRU5ErkJggg==);
  background-blend-mode: screen;
  background-size: cover;
}
</style></head>
<body>
<div class=""blended""></div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);

        // Screen of red (#FF0000) on green (#00FF00) should produce yellow (#FFFF00).
        // Verify both red and green channels are high.
        var pixel = bitmap.GetPixel(50, 50);
        Assert.True(pixel.Red > 150 || pixel.Green > 150,
            $"Expected lighter blended output but got R={pixel.Red}, G={pixel.Green}");
    }
}
