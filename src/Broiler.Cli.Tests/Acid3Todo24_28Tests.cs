using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for completed Acid3 compliance items 24–28. Current
/// standards work is tracked in docs/ROADMAP.md#standards-and-test-infrastructure.
/// </summary>
public class Acid3Todo24_28Tests
{
    // ────────────── TODO-24: Background shorthand with data-URI ──────────────

    /// <summary>
    /// TODO-24 (CSSOM): Verifies that the <c>background</c> shorthand with a
    /// data-URI image, no-repeat, position, and color keyword is correctly
    /// expanded into individual longhand properties accessible via
    /// <c>getComputedStyle</c>.
    /// </summary>
    [Fact]
    public void Todo24_Background_Shorthand_DataUri_Expands_BackgroundColor()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background: url(data:image/gif;base64,iVBORw0KGgoAAAANSUhEUgAAABQAAAAUCAIAAAAC64paAAAABGdBTUEAAK%2FINwWK6QAAAAlwSFlzAAAASAAAAEgARslrPgAAABtJREFUOMtj%2FM9APmCiQO%2Bo5lHNo5pHNVNBMwAinAEnIWw89gAAACJ6VFh0U29mdHdhcmUAAHjac0zJT0pV8MxNTE8NSk1MqQQAL5wF1K4MqU0AAAAASUVORK5CYII%3D) no-repeat 99.8392283% 1px white; }
</style></head>
<body>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.body);
document.getElementById('result').textContent = 'bg-color=' + (cs.backgroundColor || cs.getPropertyValue('background-color'));
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("bg-color=white", result);
    }

    /// <summary>
    /// TODO-24 (CSSOM): Verifies background-repeat is expanded from the shorthand.
    /// </summary>
    [Fact]
    public void Todo24_Background_Shorthand_DataUri_Expands_BackgroundRepeat()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background: url(data:image/gif;base64,R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==) no-repeat center white; }
</style></head>
<body>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.body);
document.getElementById('result').textContent = 'bg-repeat=' + (cs.backgroundRepeat || cs.getPropertyValue('background-repeat'));
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("bg-repeat=no-repeat", result);
    }

    /// <summary>
    /// TODO-24 (CSSOM): Verifies background-image is expanded from the shorthand
    /// and preserves the data-URI content.
    /// </summary>
    [Fact]
    public void Todo24_Background_Shorthand_DataUri_Expands_BackgroundImage()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background: url(data:image/gif;base64,R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==) no-repeat center white; }
</style></head>
<body>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.body);
var bgImg = cs.backgroundImage || cs.getPropertyValue('background-image') || '';
document.getElementById('result').textContent = 'has-data-uri=' + (bgImg.indexOf('data:') >= 0);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("has-data-uri=true", result);
    }

    /// <summary>
    /// TODO-24 (CSSOM): Verifies simple background shorthand (no data URI) expands color.
    /// </summary>
    [Fact]
    public void Todo24_Background_Shorthand_No_DataUri_Expands_Color()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background: no-repeat center white; }
</style></head>
<body>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.body);
document.getElementById('result').textContent = 'bg-color=' + (cs.backgroundColor || cs.getPropertyValue('background-color'));
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("bg-color=white", result);
    }

    /// <summary>
    /// TODO-24 (Rendering): Verifies the HtmlRenderer renders a white
    /// background when the body uses the Acid3 background shorthand pattern.
    /// </summary>
    [Fact]
    public void Todo24_Background_Shorthand_Renders_White()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { margin: 0; padding: 0; background: transparent; }
html { background: silver; }
body { background: url(data:image/gif;base64,R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==) no-repeat 99.8392283% 1px white; }
</style></head>
<body>
<div style=""width:100px;height:100px;"">test</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);
        var pixel = bitmap.GetPixel(50, 50);
        // Should be white (255,255,255) not silver (192,192,192)
        Assert.Equal(255, pixel.R);
        Assert.Equal(255, pixel.G);
        Assert.Equal(255, pixel.B);
    }

    /// <summary>
    /// TODO-24 (CSSOM): Verifies that background-position is correctly extracted
    /// from the shorthand — both the non-integer percentage (99.8392283%) and the
    /// 1px length value.
    /// </summary>
    [Fact]
    public void Todo24_Background_Shorthand_DataUri_Expands_BackgroundPosition()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background: url(data:image/gif;base64,R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==) no-repeat 99.8392283% 1px white; }
</style></head>
<body>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.body);
document.getElementById('result').textContent = 'bg-position=' + (cs.backgroundPosition || cs.getPropertyValue('background-position'));
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("bg-position=99.8392283% 1px", result);
    }

    /// <summary>
    /// TODO-24 (CSSOM): Verifies that background-attachment defaults to "scroll"
    /// when not explicitly specified in the shorthand.
    /// </summary>
    [Fact]
    public void Todo24_Background_Shorthand_DataUri_Expands_BackgroundAttachment()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background: url(data:image/gif;base64,R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==) no-repeat 99.8392283% 1px white; }
</style></head>
<body>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.body);
document.getElementById('result').textContent = 'bg-attachment=' + (cs.backgroundAttachment || cs.getPropertyValue('background-attachment'));
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("bg-attachment=scroll", result);
    }

    /// <summary>
    /// TODO-24 (CSSOM): Verifies that explicit background-color longhand is NOT
    /// overridden by background shorthand expansion (CSS cascade precedence).
    /// </summary>
    [Fact]
    public void Todo24_Background_Shorthand_Does_Not_Override_Longhand()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { background: no-repeat center white; background-color: red; }
</style></head>
<body>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.body);
document.getElementById('result').textContent = 'bg-color=' + (cs.backgroundColor || cs.getPropertyValue('background-color'));
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // The explicit longhand "background-color: red" should take precedence
        Assert.Contains("bg-color=red", result);
    }

    // ────────────── TODO-25: :first-child + * selector matching ──────────────

    /// <summary>
    /// TODO-25 (CSSOM): Verifies that <c>:first-child + * .buckets p</c>
    /// correctly matches bucket elements and applies display: inline-block.
    /// </summary>
    [Fact]
    public void Todo25_FirstChild_Adjacent_Sibling_Selector_Applies_InlineBlock()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
:first-child + * .buckets p { display: inline-block; vertical-align: 2em; border: 2em dotted red; padding: 1em 0 1em 2em; }
</style></head>
<body>
<h1>Title</h1>
<div>
  <div class=""buckets"">
    <p id=""bucket1"">B1</p>
    <p id=""bucket2"">B2</p>
  </div>
</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('bucket1'));
var r = [];
r.push('display=' + (cs.display || 'NULL'));
r.push('verticalAlign=' + (cs.verticalAlign || 'NULL'));
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("display=inline-block", result);
        Assert.Contains("verticalAlign=2em", result);
    }

    /// <summary>
    /// TODO-25 (Rendering): Verifies that the :first-child + * selector
    /// causes bucket elements to render as inline-block (horizontal layout).
    /// </summary>
    [Fact]
    public void Todo25_FirstChild_Adjacent_Sibling_Renders_InlineBlock()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { margin: 0; padding: 0; }
:first-child + * .buckets p { display: inline-block; background: red; width: 40px; height: 40px; }
</style></head>
<body>
<h1 style=""font-size:10px;line-height:1;"">T</h1>
<div>
  <div class=""buckets"">
    <p id=""b1"">1</p>
    <p id=""b2"">2</p>
  </div>
</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);
        // If inline-block works, b1 is at ~(0,12) and b2 is at ~(40,12)
        // If block, both would be stacked vertically
        // Check that b2's position has a red pixel to the right of b1
        var rightOfFirst = bitmap.GetPixel(50, 30);
        // Should be red (part of b2's inline-block layout)
        Assert.Equal(255, rightOfFirst.R);
    }

    // ────────────── TODO-26: Bucket color investigation ──────────────

    /// <summary>
    /// TODO-26: Verifies that bucket color CSS rules are correctly parsed.
    /// The Acid3 CSS assigns final colors when class reaches "zPPPPPPPPPPPPPPPP".
    /// </summary>
    [Fact]
    public void Todo26_Bucket_Color_Rules_Parsed_Correctly()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
.z, .zP, .zPP { background: black; }
.zPPP, .zPPPP { background: grey; }
#bucket4.zPPPPPPPPPPPPPPPP { background: lime; }
#bucket5.zPPPPPPPPPPPPPPPP { background: blue; }
</style></head>
<body>
<p id=""bucket4"" class=""zPPPPPPPPPPPPPPPP"">B4</p>
<p id=""bucket5"" class=""zPPPPPPPPPPPPPPPP"">B5</p>
<div id=""result""></div>
<script>
var cs4 = window.getComputedStyle(document.getElementById('bucket4'));
var cs5 = window.getComputedStyle(document.getElementById('bucket5'));
var r = [];
r.push('b4bg=' + (cs4.backgroundColor || cs4.getPropertyValue('background-color')));
r.push('b5bg=' + (cs5.backgroundColor || cs5.getPropertyValue('background-color')));
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("b4bg=lime", result);
        Assert.Contains("b5bg=blue", result);
    }

    /// <summary>
    /// TODO-26 (Rendering): Verifies that compound #id.class selectors with
    /// background-color longhand render the correct colors.
    /// </summary>
    [Fact]
    public void Todo26_Bucket_Colors_Render_Correctly()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
body { margin: 0; padding: 0; }
p { margin: 0; padding: 0; display: block; width: 80px; height: 40px; }
#bucket4.zPPPPPPPPPPPPPPPP { background-color: lime; }
#bucket5.zPPPPPPPPPPPPPPPP { background-color: blue; }
</style></head>
<body>
<p id=""bucket4"" class=""zPPPPPPPPPPPPPPPP"">B4</p>
<p id=""bucket5"" class=""zPPPPPPPPPPPPPPPP"">B5</p>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);
        // Bucket4 should be lime (0, 128+, 0) in top area
        var b4pixel = bitmap.GetPixel(40, 10);
        Assert.True(b4pixel.G > 100, $"Expected green for bucket4, got R={b4pixel.R} G={b4pixel.G} B={b4pixel.B}");
        Assert.True(b4pixel.R < 50, $"Expected low red for bucket4, got R={b4pixel.R}");
    }

    // ────────────── TODO-27: h1:first-child selector and margin-bottom ──────────────

    /// <summary>
    /// TODO-27 (CSSOM): Verifies that <c>h1:first-child</c> is matched and
    /// <c>margin-bottom: -0.4em</c> is correctly applied.
    /// </summary>
    [Fact]
    public void Todo27_H1_FirstChild_NegativeMarginBottom()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
h1:first-child { margin-bottom: -0.4em; }
</style></head>
<body>
<h1 id=""title"">Title</h1>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('title'));
document.getElementById('result').textContent = 'margin-bottom=' + (cs.marginBottom || cs.getPropertyValue('margin-bottom'));
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("margin-bottom=-0.4em", result);
    }

    /// <summary>
    /// TODO-27 (CSSOM): Verifies that h1:first-child selector matches
    /// when h1 is the first child of body (typical Acid3 structure).
    /// </summary>
    [Fact]
    public void Todo27_H1_FirstChild_Selector_Matches()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
h1:first-child { font-size: 5em; font-weight: bolder; cursor: help; }
</style></head>
<body>
<h1 id=""title"">Title</h1>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('title'));
var r = [];
r.push('fontSize=' + (cs.fontSize || 'NULL'));
r.push('cursor=' + (cs.cursor || 'NULL'));
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("fontSize=5em", result);
        Assert.Contains("cursor=help", result);
    }

    // ────────────── TODO-28: document.write() element visibility ──────────────

    /// <summary>
    /// TODO-28 (CSSOM): Verifies that <c>iframe { float: left; height: 0; width: 0; }</c>
    /// produces zero-size layout.
    /// </summary>
    [Fact]
    public void Todo28_Iframe_ZeroSize_Layout()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
iframe { float: left; height: 0; width: 0; }
</style></head>
<body>
<iframe id=""target"" src=""about:blank""></iframe>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push('width=' + (cs.width || 'NULL'));
r.push('height=' + (cs.height || 'NULL'));
r.push('float=' + (cs.cssFloat || cs.getPropertyValue('float') || 'NULL'));
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("width=0", result);
        Assert.Contains("height=0", result);
        Assert.Contains("float=left", result);
    }

    /// <summary>
    /// TODO-28 (CSSOM): Verifies that <c>&lt;map&gt;</c> elements have no
    /// visual representation (they are semantic-only elements).
    /// </summary>
    [Fact]
    public void Todo28_Map_Element_Not_Visible()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { border: 1px blue; }
</style></head>
<body>
<map id=""target"" name=""test""><area shape=""rect"" coords=""0,0,10,10"" href=""#""></map>
<div id=""result""></div>
<script>
var el = document.getElementById('target');
var cs = window.getComputedStyle(el);
var r = [];
r.push('display=' + (cs.display || 'NULL'));
r.push('offsetWidth=' + (el.offsetWidth || 0));
r.push('offsetHeight=' + (el.offsetHeight || 0));
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // Map elements should have zero effective dimensions
        Assert.Contains("offsetWidth=0", result);
        Assert.Contains("offsetHeight=0", result);
    }

    /// <summary>
    /// TODO-28 (Rendering): Verifies that a zero-size iframe does not affect layout.
    /// </summary>
    [Fact]
    public void Todo28_Iframe_ZeroSize_No_Visual_Box()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { margin: 0; padding: 0; }
iframe { float: left; height: 0; width: 0; }
div { background: lime; width: 50px; height: 50px; }
</style></head>
<body>
<iframe src=""about:blank""></iframe>
<div>test</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 100, 100);
        // The div should be at the top-left since iframe takes no space
        var pixel = bitmap.GetPixel(25, 25);
        // Should be lime green (0, 255 or 128, 0) since the div is rendered there
        Assert.Equal(0, pixel.R);
        Assert.True(pixel.G > 100); // lime green
        Assert.Equal(0, pixel.B);
    }

    /// <summary>
    /// TODO-28 (CSSOM): Verifies that <c>&lt;form&gt;</c> elements do not render
    /// visible borders when styled with zero-size or default styling.
    /// Acid3 uses <c>document.write()</c> to inject form elements that must remain
    /// invisible.
    /// </summary>
    [Fact]
    public void Todo28_Form_Element_No_Visible_Border()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { margin: 0; padding: 0; }
body { background: white; }
form { display: block; width: 0; height: 0; overflow: hidden; }
</style></head>
<body>
<form id=""target""><input type=""hidden"" /></form>
<div id=""result""></div>
<script>
var el = document.getElementById('target');
var cs = window.getComputedStyle(el);
var r = [];
r.push('width=' + (cs.width || 'NULL'));
r.push('height=' + (cs.height || 'NULL'));
r.push('overflow=' + (cs.overflow || 'NULL'));
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("width=0", result);
        Assert.Contains("height=0", result);
        Assert.Contains("overflow=hidden", result);
    }

    /// <summary>
    /// TODO-28 (Rendering): Verifies that <c>&lt;table&gt;</c> elements with zero
    /// dimensions do not produce visible boxes.
    /// </summary>
    [Fact]
    public void Todo28_Table_Element_No_Visible_Box()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>
* { margin: 0; padding: 0; }
body { background: white; }
table { width: 0; height: 0; border: none; border-collapse: collapse; }
</style></head>
<body>
<table id=""target""><tr><td></td></tr></table>
<div style=""background: lime; width: 50px; height: 50px;"">test</div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 100, 100);
        // With zero-size table, the lime div should be visible in the upper portion
        var limePixel = bitmap.GetPixel(25, 25);
        Assert.True(limePixel.G > 100, $"Expected green for lime div, got R={limePixel.R} G={limePixel.G} B={limePixel.B}");
    }
}
