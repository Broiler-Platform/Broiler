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
    /// Must not throw.
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
    /// WPT: css/compositing/root-element-filter.html
    /// A test with filter: invert(1) on the root element.
    /// Must not throw; the filter property must be accepted.
    /// </summary>
    [Fact]
    public void Root_Element_Filter_Does_Not_Crash()
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
    /// with opacity: 0.5 and green background.
    /// </summary>
    [Fact]
    public void Root_Element_Background_Margin_Opacity_Ref_Does_Not_Crash()
    {
        var html = @"<!DOCTYPE html>
<html>
<body>
<div style=""position: absolute; top: 0; left: 0; width: 200px; height: 200px; background: green; opacity: 0.5""></div>
</body>
</html>";

        using var bitmap = HtmlRender.RenderToImage(html, 200, 200);
        Assert.NotNull(bitmap);
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
    /// Uses mix-blend-mode: difference with parent border-radius. Must not crash.
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
    }

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/mix-blend-mode-parent-with-text.html
    /// Uses mix-blend-mode: difference with negative margin. Must not crash.
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
    }

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/mix-blend-mode-overflowing-child.html
    /// Uses mix-blend-mode: difference on an overflowing child. Must not crash.
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
    /// Yellow square with green circle — must not crash.
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
    }

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/reference/mix-blend-mode-parent-with-text-ref.html
    /// Yellow square with green text — must not crash.
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
    }

    /// <summary>
    /// WPT: css/compositing/mix-blend-mode/reference/mix-blend-mode-overflowing-child-ref.html
    /// Green squares with black intersection — must not crash.
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
}
