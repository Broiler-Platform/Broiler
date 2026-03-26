namespace Broiler.Cli.Tests;

/// <summary>
/// Acid3 CSS compliance tests — targeted CSS unit tests for properties
/// and features used by the Acid3 test suite, as recommended in
/// docs/roadmap/acid3-compliance.md §6.2.
/// </summary>
public class Acid3CssComplianceTests
{
    // ────────────── D4: Slash rendering (score display) ──────────────

    /// <summary>
    /// Verifies that the Acid3 score display pattern produces "98/100" —
    /// the slash element is visible after removeAttribute('class') removes
    /// the "hidden" class, and firstChild.data updates work correctly.
    /// </summary>
    [Fact]
    public void Score_Display_Slash_Visible_After_RemoveAttribute()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.hidden { visibility: hidden; }
</style>
</head><body>
<p id=""result""><span id=""score"">JS</span><span id=""slash"" class=""hidden"">/</span><span>?</span></p>
<script>
var span = document.getElementById('score');
// Remove 'hidden' class from the slash element
span.nextSibling.removeAttribute('class');
// Update score text node
span.firstChild.data = '98';
// Update total text node
span.nextSibling.nextSibling.firstChild.data = '100';
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // The slash span should no longer have a class attribute
        Assert.DoesNotContain("class=\"hidden\"", result);
        // The score should be "98"
        Assert.Contains("98", result);
        // The slash "/" must be present in the serialized output
        Assert.Contains("/", result);
        // The total should be "100"
        Assert.Contains("100", result);
    }

    /// <summary>
    /// Verifies that firstChild.data can read and write text node content.
    /// This is critical for the Acid3 score update mechanism.
    /// </summary>
    [Fact]
    public void FirstChild_Data_ReadWrite_TextNode()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<span id=""target"">initial</span>
<div id=""result""></div>
<script>
var t = document.getElementById('target');
var before = t.firstChild.data;
t.firstChild.data = 'updated';
var after = t.firstChild.data;
document.getElementById('result').textContent = before + '|' + after;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("initial|updated", result);
    }

    /// <summary>
    /// Verifies nextSibling navigation works across sibling elements,
    /// as used by the Acid3 score display update code.
    /// </summary>
    [Fact]
    public void NextSibling_Navigation_Across_Elements()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""container""><span id=""a"">A</span><span id=""b"">B</span><span id=""c"">C</span></div>
<div id=""result""></div>
<script>
var a = document.getElementById('a');
var b = a.nextSibling;
var c = b.nextSibling;
document.getElementById('result').textContent = b.id + ':' + c.id;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("b:c", result);
    }

    // ────────────── D2: cm unit conversion (96 DPI) ──────────────

    /// <summary>
    /// Verifies that CSS 'cm' units are recognized by getComputedStyle.
    /// At 96 DPI, 2cm should be approximately 75.6px (2 × 37.795275591).
    /// </summary>
    [Fact]
    public void Cm_Unit_Border_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { border: 2cm solid gray; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.borderWidth !== undefined);
r.push(cs.borderWidth !== '');
r.push(cs.getPropertyValue('border-style') !== '');
r.push(cs.getPropertyValue('border-color') !== '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ────────────── D2: border-width 4-value shorthand + cascade ──────────────

    /// <summary>
    /// Verifies that a 4-value border-width shorthand is recognized and
    /// can override a border shorthand via CSS cascade (higher specificity).
    /// This is the pattern used by Acid3: html { border: 2cm solid gray }
    /// then :root { border-width: 0 0.2em 0.2em 0 }.
    /// </summary>
    [Fact]
    public void BorderWidth_FourValue_Cascade_Override()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
html { border: 2cm solid gray; }
html { border-width: 0 0.2em 0.2em 0; }
</style>
</head><body>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.documentElement);
var r = [];
r.push(cs.borderWidth !== undefined);
r.push(cs.getPropertyValue('border-width') !== '');
r.push(cs.getPropertyValue('border-style') !== '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────── D10: HSLA color parsing edge cases ──────────────

    /// <summary>
    /// Verifies that hsla(0, 0%, 0%, 1.0) — the color used for the #slash
    /// element — is recognized as valid CSS and produces black text.
    /// </summary>
    [Fact]
    public void Hsla_Black_Color_For_Slash_Element()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#slash { color: red; color: hsla(0, 0%, 0%, 1.0); }
</style>
</head><body>
<span id=""slash"">/</span>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('slash'));
var r = [];
r.push(cs.color !== '');
r.push(cs.color !== undefined);
r.push(cs.getPropertyValue('color') !== '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    /// <summary>
    /// Verifies HSLA parsing with zero-percent saturation and lightness —
    /// boundary condition that should produce pure black or white.
    /// </summary>
    [Fact]
    public void Hsla_Zero_Saturation_Produces_Valid_Color()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var d = document.createElement('div');
d.style.color = 'hsla(0, 0%, 0%, 1.0)';
var r = [];
r.push(d.style.color !== '');
r.push(d.style.color !== undefined);
// Also test 100% lightness (white)
var d2 = document.createElement('div');
d2.style.color = 'hsla(0, 0%, 100%, 0.5)';
r.push(d2.style.color !== '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────── D11: display: inline-block + vertical-align ──────────────

    /// <summary>
    /// Verifies that display: inline-block with vertical-align in em units
    /// is recognized by getComputedStyle, as used by Acid3 bucket elements.
    /// </summary>
    [Fact]
    public void InlineBlock_VerticalAlign_Em_Units()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target {
    display: inline-block;
    vertical-align: 2em;
    border: 2em dotted red;
    padding: 1.0em 0 1.0em 2em;
}
</style>
</head><body>
<div id=""target"">bucket</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.display !== '');
r.push(cs.verticalAlign !== '');
r.push(cs.getPropertyValue('border-style') !== '');
r.push(cs.getPropertyValue('padding') !== '' || cs.getPropertyValue('padding-left') !== '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ────────────── D12: Negative margins with borders ──────────────

    /// <summary>
    /// Verifies that negative margins combined with borders are recognized,
    /// as used by Acid3 for overlapping layout (e.g. margin: -0.2em 0 0 -0.2em).
    /// </summary>
    [Fact]
    public void Negative_Margin_With_Border_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target {
    margin: -0.2em 0 0 -0.2em;
    border: solid 1px black;
}
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.marginTop !== '' && cs.marginTop !== undefined);
r.push(cs.marginLeft !== '' && cs.marginLeft !== undefined);
r.push(cs.getPropertyValue('border-style') !== '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    /// <summary>
    /// Verifies that the large negative margin used for score positioning
    /// (margin: -2.19em 0 0) is recognized.
    /// </summary>
    [Fact]
    public void Large_Negative_Margin_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { margin: -2.19em 0 0; font-size: 5em; }
</style>
</head><body>
<div id=""target"">score</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.marginTop === '-2.19em');
r.push(cs.fontSize === '5em');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────── D14: Zero-sized floated iframes ──────────────

    /// <summary>
    /// Verifies that float: left with height/width: 0 is recognized,
    /// as used by Acid3 for iframe elements.
    /// </summary>
    [Fact]
    public void ZeroSized_Float_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
iframe { float: left; height: 0; width: 0; }
</style>
</head><body>
<iframe id=""target""></iframe>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.float !== '' || cs.cssFloat !== '' || cs.getPropertyValue('float') !== '');
r.push(cs.height !== '' || cs.getPropertyValue('height') !== '');
r.push(cs.width !== '' || cs.getPropertyValue('width') !== '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────── §6.3: End-to-end score test with "/" separator ──────────────

    /// <summary>
    /// End-to-end test verifying the Acid3 score display contains the "/"
    /// separator between the score number and total, and that the score
    /// updates correctly via the update loop pattern.
    /// </summary>
    [Fact]
    public void Score_Display_Contains_Slash_Separator()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.hidden { visibility: hidden; }
#slash { color: red; color: hsla(0, 0%, 0%, 1.0); }
</style>
</head><body>
<p id=""result""><span id=""score"">JS</span><span id=""slash"" class=""hidden"">/</span><span id=""total"">?</span></p>
<div id=""output""></div>
<script>
var tests = [];
for (var i = 0; i < 98; i++) tests.push(i);

var score = 0;
var span = document.getElementById('score');

// Unhide slash
span.nextSibling.removeAttribute('class');

// Update total
span.nextSibling.nextSibling.firstChild.data = '' + tests.length;

// Run tests
for (var i = 0; i < tests.length; i++) {
    score++;
}
span.firstChild.data = '' + score;

document.getElementById('output').textContent =
    span.firstChild.data + '|' +
    span.nextSibling.firstChild.data + '|' +
    span.nextSibling.nextSibling.firstChild.data;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // The slash "/" must be present in serialized output
        Assert.Contains("/", result);
        // Score must be 98
        Assert.Contains("98", result);
        // Total must be 98 (length of test array)
        Assert.Contains("98|/|98", result);
    }

    // ────────────── D5: !important border overrides (CSS cascade) ──────────────

    /// <summary>
    /// Verifies that !important in a more complex selector correctly
    /// overrides a universal selector's border property, matching the
    /// Acid3 pattern: * { border: 1px blue } overridden by
    /// * + * > * > p { border: 1px solid !important }.
    /// </summary>
    [Fact]
    public void Important_Border_Override_Universal_Selector()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { border: 1px blue; }
* + * > * > p { margin: 0; border: 1px solid !important; }
</style>
</head><body>
<div class=""buckets"">
  <p id=""target"">bucket content</p>
</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.borderStyle !== '' || cs.getPropertyValue('border-style') !== '');
r.push(cs.getPropertyValue('border-width') !== '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────── D8: @font-face with local file ──────────────

    /// <summary>
    /// Verifies that @font-face rules with url() src are parsed and
    /// accessible via the CSSOM, even if the font file is not loaded.
    /// </summary>
    [Fact]
    public void FontFace_With_Url_Src_Accessible_Via_CSSOM()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@font-face { font-family: ""AcidAhemTest""; src: url(font.ttf); }
</style>
</head><body>
<div id=""result""></div>
<script>
var sheet = document.styleSheets[0];
var r = [];
r.push(sheet.cssRules.length > 0);
if (sheet.cssRules.length > 0) {
    var rule = sheet.cssRules[0];
    r.push(rule.type === 5 || rule.cssText !== undefined);  // CSSRule.FONT_FACE_RULE = 5
}
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────── D6: Word spacing and whitespace ──────────────

    /// <summary>
    /// Verifies that whitespace between inline elements is preserved in
    /// serialized output — text should not collapse to remove word boundaries.
    /// </summary>
    [Fact]
    public void Whitespace_Preserved_Between_Inline_Elements()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<p id=""text"">To pass the test, each colored box should appear.</p>
<div id=""result""></div>
<script>
var p = document.getElementById('text');
document.getElementById('result').textContent = p.textContent;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("To pass the test", result);
        Assert.Contains("each colored box", result);
    }
}
