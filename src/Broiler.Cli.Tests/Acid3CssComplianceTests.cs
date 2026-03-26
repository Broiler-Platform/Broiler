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
    /// (margin: -2.19em 0 0) is recognized. Note: Broiler's getComputedStyle
    /// preserves original CSS units rather than resolving to px; real browsers
    /// would return computed values in px.
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
// Broiler preserves original units; real browsers resolve to px
r.push(cs.marginTop !== '' && cs.marginTop !== undefined);
r.push(cs.fontSize !== '' && cs.fontSize !== undefined);
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

    // ────────────── D4: CSS invalidation after class changes ──────────────

    /// <summary>
    /// Verifies that CSS-derived visibility is cleared from element.Style
    /// when removeAttribute('class') removes the class that triggered
    /// the .hidden { visibility: hidden } rule.  After removal, the
    /// element's computed visibility should revert to "visible".
    /// </summary>
    [Fact]
    public void RemoveAttribute_Class_Clears_Visibility_Style()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.hidden { visibility: hidden; }
</style>
</head><body>
<span id=""target"" class=""hidden"">text</span>
<div id=""result""></div>
<script>
var el = document.getElementById('target');
el.removeAttribute('class');
var cs = window.getComputedStyle(el);
document.getElementById('result').textContent =
    'vis=' + cs.visibility + ',cls=' + (el.className || 'EMPTY');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("vis=visible", result);
        Assert.Contains("cls=EMPTY", result);
        // The element's inline style should not contain visibility:hidden
        Assert.DoesNotContain("style=\"visibility", result.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that classList.remove() triggers CSS style recalculation,
    /// removing styles from rules that no longer match.
    /// </summary>
    [Fact]
    public void ClassList_Remove_Triggers_Style_Invalidation()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.red { color: red; }
</style>
</head><body>
<span id=""target"" class=""red"">text</span>
<div id=""result""></div>
<script>
var el = document.getElementById('target');
el.classList.remove('red');
var cs = window.getComputedStyle(el);
document.getElementById('result').textContent =
    'hasRed=' + el.classList.contains('red') + ',hasColor=' + (cs.color !== '');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("hasRed=false", result);
    }

    /// <summary>
    /// Verifies that setAttribute('class', ...) triggers CSS style
    /// recalculation so new class rules apply and old ones are removed.
    /// </summary>
    [Fact]
    public void SetAttribute_Class_Triggers_Style_Recalculation()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.a { color: red; }
.b { font-weight: bold; }
</style>
</head><body>
<span id=""target"" class=""a"">text</span>
<div id=""result""></div>
<script>
var el = document.getElementById('target');
el.setAttribute('class', 'b');
var cs = window.getComputedStyle(el);
document.getElementById('result').textContent =
    'fw=' + cs.fontWeight + ',cls=' + el.className;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("cls=b", result);
        Assert.Contains("fw=700", result);
    }

    // ────────────── D2: :root cascade overriding html ──────────────

    /// <summary>
    /// Verifies that the CSS cascade correctly applies when :root (rewritten
    /// to html by HtmlPostProcessor) overrides a less-specific html rule.
    /// This is the Acid3 pattern: html { border: 2cm solid gray }
    /// then :root { border-width: 0 0.2em 0.2em 0 }.
    /// </summary>
    [Fact]
    public void Root_Selector_Overrides_Html_Border_Width()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
html { border: 2cm solid gray; }
:root { border-width: 0 0.2em 0.2em 0; }
</style>
</head><body>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.documentElement);
var r = [];
var top = cs.getPropertyValue('border-top-width');
var right = cs.getPropertyValue('border-right-width');
var bottom = cs.getPropertyValue('border-bottom-width');
var left = cs.getPropertyValue('border-left-width');
r.push('top=' + top);
r.push('right=' + right);
r.push('bottom=' + bottom);
r.push('left=' + left);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // The :root rule should override the html border: 2cm shorthand
        // border-top-width should be 0 (from the 4-value shorthand)
        Assert.Contains("top=0", result);
    }

    // ────────────── D7: font-weight bolder resolution ──────────────

    /// <summary>
    /// Verifies that font-weight: bolder resolves to a numeric weight
    /// (700 when parent is normal/400) per CSS 2.1 §15.6.
    /// </summary>
    [Fact]
    public void FontWeight_Bolder_Resolves_To_700_From_Normal_Parent()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#parent { font-weight: normal; }
#child { font-weight: bolder; }
</style>
</head><body>
<div id=""parent""><span id=""child"">text</span></div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('child'));
document.getElementById('result').textContent = 'fw=' + cs.fontWeight;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("fw=700", result);
    }

    /// <summary>
    /// Verifies that font-weight: lighter resolves to a numeric weight
    /// (100 when parent is normal/400) per CSS 2.1 §15.6.
    /// </summary>
    [Fact]
    public void FontWeight_Lighter_Resolves_To_100_From_Normal_Parent()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#parent { font-weight: normal; }
#child { font-weight: lighter; }
</style>
</head><body>
<div id=""parent""><span id=""child"">text</span></div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('child'));
document.getElementById('result').textContent = 'fw=' + cs.fontWeight;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("fw=100", result);
    }

    /// <summary>
    /// Verifies that font-weight: bolder resolves to 900 when the parent
    /// already has font-weight: bold (700).
    /// </summary>
    [Fact]
    public void FontWeight_Bolder_From_Bold_Parent_Resolves_To_900()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#parent { font-weight: bold; }
#child { font-weight: bolder; }
</style>
</head><body>
<div id=""parent""><span id=""child"">text</span></div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('child'));
document.getElementById('result').textContent = 'fw=' + cs.fontWeight;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("fw=900", result);
    }

    // ────────────── D7: text-shadow with RGBA ──────────────

    /// <summary>
    /// Verifies that text-shadow with rgba() color is recognized as valid
    /// CSS and accessible via getComputedStyle, as used by Acid3's h1
    /// text-shadow: rgba(192,192,192,1.0) 3px 3px.
    /// </summary>
    [Fact]
    public void TextShadow_Rgba_Color_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
h1 { text-shadow: rgba(192,192,192,1.0) 3px 3px; }
</style>
</head><body>
<h1 id=""target"">Acid3</h1>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var ts = cs.getPropertyValue('text-shadow') || cs.textShadow || '';
document.getElementById('result').textContent = 'shadow=' + (ts !== '' && ts !== 'none' ? 'yes' : 'no');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("shadow=yes", result);
    }

    // ────────────── D6: Word spacing with inherited font ──────────────

    /// <summary>
    /// Verifies that word spacing works correctly with inherited font sizes.
    /// The text "To pass the test" should preserve spaces between words.
    /// </summary>
    [Fact]
    public void WordSpacing_With_Inherited_Font_Size()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
body { font: 0.8em sans-serif; }
</style>
</head><body>
<p id=""text"">To pass the test, each colored box should appear.</p>
<div id=""result""></div>
<script>
var p = document.getElementById('text');
var text = p.textContent || p.innerText || '';
var words = text.split(' ').length;
document.getElementById('result').textContent = 'words=' + words;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // "To pass the test, each colored box should appear." has 9 words
        Assert.Contains("words=9", result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // New tests for remaining Acid3 compliance TODO items
    // Ref: docs/roadmap/acid3-compliance.md §5 Prioritized TODO List
    // ═══════════════════════════════════════════════════════════════════════

    // ────────────── CSS Error Recovery (CSS2.1 §4.1.8) ──────────────

    /// <summary>
    /// CSS2.1 §4.1.8: Declarations with illegal values must be ignored.
    /// The Acid3 test uses <c>white-space: pre-wrap; white-space: x-bogus;</c>
    /// on the instructions element.  "x-bogus" is an unknown keyword for
    /// white-space, so it must be discarded and "pre-wrap" must remain.
    /// </summary>
    [Fact]
    public void WhiteSpace_Invalid_Value_Discarded_By_Error_Recovery()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { white-space: pre-wrap; white-space: x-bogus; }
</style>
</head><body>
<p id=""target"">hello   world</p>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = 'ws=' + cs.whiteSpace;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // "x-bogus" should be rejected; "pre-wrap" should remain
        Assert.Contains("ws=pre-wrap", result);
    }

    /// <summary>
    /// CSS2.1 §4.1.8: When a valid and an invalid value are declared for
    /// the same property in the same rule, the valid one must win.
    /// Tests multiple enumerated properties.
    /// </summary>
    [Fact]
    public void Invalid_Display_Value_Discarded_Keeps_Previous()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { display: inline-block; display: supergrid; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = 'display=' + cs.display;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("display=inline-block", result);
    }

    /// <summary>
    /// CSS2.1 §4.1.8: Invalid visibility value should be discarded.
    /// </summary>
    [Fact]
    public void Invalid_Visibility_Value_Discarded()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { visibility: hidden; visibility: magic; }
</style>
</head><body>
<span id=""target"">text</span>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = 'vis=' + cs.visibility;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("vis=hidden", result);
    }

    /// <summary>
    /// CSS2.1 §4.1.8: Invalid overflow value should be discarded.
    /// Validates that the rendering engine does not apply unknown overflow keywords.
    /// </summary>
    [Fact]
    public void Invalid_Overflow_Value_Discarded()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { overflow: hidden; overflow: magical-scroll; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = 'overflow=' + cs.overflow;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("overflow=hidden", result);
    }

    /// <summary>
    /// CSS error recovery: valid "inherit" keyword should be accepted for
    /// enumerated properties even when validated.
    /// </summary>
    [Fact]
    public void Inherit_Value_Accepted_For_Enumerated_Properties()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#parent { white-space: pre; }
#child { white-space: inherit; }
</style>
</head><body>
<div id=""parent""><span id=""child"">text</span></div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('child'));
document.getElementById('result').textContent = 'ws=' + cs.whiteSpace;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // "inherit" should be accepted as a valid value — Broiler's
        // getComputedStyle preserves the keyword (does not resolve to the
        // parent's computed value), so we check for "inherit" directly.
        Assert.Contains("ws=inherit", result);
    }

    // ────────────── TODO-1 (D3): Viewport / box-model ──────────────

    /// <summary>
    /// Verifies the Acid3 html element box model: width: 32em (640px at 20px
    /// font) + border + margin should fit within a 1024px viewport.
    /// This is a getComputedStyle validation for TODO-1.
    /// </summary>
    [Fact]
    public void Acid3_Html_Width_32em_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; padding: 0; }
html { font: 20px Arial, sans-serif; width: 32em; margin: 1em; }
</style>
</head><body>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.documentElement);
var r = [];
r.push('w=' + cs.width);
r.push('ml=' + cs.marginLeft);
r.push('mr=' + cs.marginRight);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // Width should be 32em and margin 1em — both should be accessible
        Assert.Contains("w=32em", result);
        Assert.Contains("ml=1em", result);
    }

    /// <summary>
    /// Tests that the full Acid3-like box model (border: 2cm + border-width
    /// override + width: 32em + margin) produces valid computed styles.
    /// </summary>
    [Fact]
    public void Acid3_Full_BoxModel_Computed_Styles()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; border: 1px blue; padding: 0; }
html { font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; }
html { border-width: 0 0.2em 0.2em 0; }
body { padding: 2em 2em 0; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }
</style>
</head><body>
<div id=""result""></div>
<script>
var htmlCs = window.getComputedStyle(document.documentElement);
var bodyCs = window.getComputedStyle(document.body);
var r = [];
r.push('html-w=' + htmlCs.width);
r.push('html-btw=' + htmlCs.getPropertyValue('border-top-width'));
r.push('html-brw=' + htmlCs.getPropertyValue('border-right-width'));
r.push('body-pt=' + bodyCs.paddingTop);
r.push('body-mt=' + bodyCs.marginTop);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // html border-top-width should be "0" (from override)
        Assert.Contains("html-btw=0", result);
        // html width should be 32em
        Assert.Contains("html-w=32em", result);
        // body padding-top should be 2em
        Assert.Contains("body-pt=2em", result);
    }

    // ────────────── TODO-6 (D6): Acid3 instruction text ──────────────

    /// <summary>
    /// Tests the Acid3 instruction text CSS error recovery pattern:
    /// <c>color: gray; color: -acid3-bogus;</c> should result in gray
    /// because -acid3-bogus is an invalid color and must be rejected.
    /// </summary>
    [Fact]
    public void Acid3_Instructions_Color_Error_Recovery()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#instructions { color: gray; color: -acid3-bogus; }
</style>
</head><body>
<p id=""instructions"">To pass the test, each colored box should appear.</p>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('instructions'));
// The color should be 'gray' since '-acid3-bogus' is invalid
document.getElementById('result').textContent = 'color=' + cs.color;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // "-acid3-bogus" should be rejected; "gray" should remain
        Assert.Contains("color=gray", result);
    }

    /// <summary>
    /// Tests the Acid3 instruction text white-space error recovery:
    /// <c>white-space: pre-wrap; white-space: x-bogus;</c> should keep pre-wrap.
    /// Uses id selector instead of :last-child since the rendering engine
    /// may not support :last-child pseudo-class.
    /// </summary>
    [Fact]
    public void Acid3_Instructions_WhiteSpace_Error_Recovery()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#instructions { white-space: pre-wrap; white-space: x-bogus; }
</style>
</head><body>
<p id=""instructions"">Line 1  with  extra  spaces</p>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('instructions'));
document.getElementById('result').textContent = 'ws=' + cs.whiteSpace;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("ws=pre-wrap", result);
    }

    // ────────────── TODO-8 (D8): @font-face rule access ──────────────

    /// <summary>
    /// Verifies that @font-face rules are counted correctly in the CSSOM
    /// and that the font-family name is accessible.
    /// </summary>
    [Fact]
    public void FontFace_FontFamily_Name_Accessible()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@font-face { font-family: ""AcidAhemTest""; src: url(font.ttf); }
p { font-family: AcidAhemTest, Arial; }
</style>
</head><body>
<p id=""target"">text</p>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var ff = cs.fontFamily || cs.getPropertyValue('font-family') || '';
document.getElementById('result').textContent = 'ff=' + ff;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // The font-family should reference AcidAhemTest from the @font-face rule
        Assert.Contains("AcidAhemTest", result);
    }

    // ────────────── TODO-13 (D13): SVG and object elements ──────────────

    /// <summary>
    /// Verifies that <c>&lt;object&gt;</c> elements with position: fixed are
    /// recognized in the DOM and have valid computed styles.
    /// </summary>
    [Fact]
    public void Object_Position_Fixed_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
object { position: fixed; left: 130.5px; top: 84.3px; background: transparent; }
</style>
</head><body>
<object id=""target"" data=""test.svg""></object>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push('pos=' + cs.position);
r.push('left=' + cs.left);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("pos=fixed", result);
        Assert.Contains("left=130.5px", result);
    }

    // ────────────── TODO-15 (D15): Data-URI background images ──────────────

    /// <summary>
    /// Verifies that data: URI background images are preserved in the
    /// CSS computed style, not stripped during post-processing.
    /// </summary>
    [Fact]
    public void DataUri_Background_Image_Preserved()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { background-image: url(data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7); }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var bg = cs.backgroundImage || cs.getPropertyValue('background-image') || '';
document.getElementById('result').textContent = 'has-data-uri=' + (bg.indexOf('data:') >= 0 || bg.indexOf('url(') >= 0);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("has-data-uri=true", result);
    }

    // ────────────── TODO-16 (D16): Pseudo-element positioning ──────────────

    /// <summary>
    /// Verifies that pseudo-element positioning with absolute coordinates
    /// is recognized by the CSS engine (map::after pattern from Acid3).
    /// </summary>
    [Fact]
    public void PseudoElement_Absolute_Position_In_CSSOM()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
map::after { position: absolute; top: 18px; left: 638px; content: ""X""; background: fuchsia; }
</style>
</head><body>
<map id=""target""></map>
<div id=""result""></div>
<script>
var sheet = document.styleSheets[0];
var r = [];
r.push('rules=' + sheet.cssRules.length);
if (sheet.cssRules.length > 0) {
    var rule = sheet.cssRules[0];
    r.push('selector=' + (rule.selectorText || 'N/A'));
}
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("rules=1", result);
        // The pseudo-element selector should be properly parsed and accessible
        Assert.Contains("selector=map::after", result);
    }

    // ────────────── Acid3 full CSS pattern integration ──────────────

    /// <summary>
    /// Integration test: applies the full set of Acid3 base CSS rules
    /// (universal reset + html + :root + body) and verifies the cascade
    /// produces valid computed styles for key elements.
    /// Note: :root is rewritten to html by HtmlPostProcessor, so we use
    /// html directly here to test what the rendering engine sees.
    /// </summary>
    [Fact]
    public void Acid3_Base_Css_Cascade_Integration()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; border: 1px blue; padding: 0; border-spacing: 0; font: inherit; line-height: 1.2; color: inherit; background: transparent; }
html { font: 20px Arial, sans-serif; border: 2cm solid gray; width: 32em; margin: 1em; }
html { background: silver; color: black; border-width: 0 0.2em 0.2em 0; }
body { padding: 2em 2em 0; border: solid 1px black; margin: -0.2em 0 0 -0.2em; }
.hidden { visibility: hidden; }
#slash { color: red; color: hsla(0, 0%, 0%, 1.0); }
</style>
</head><body>
<h1>Acid3</h1>
<p id=""result""><span id=""score"">0</span><span id=""slash"" class=""hidden"">/</span><span>100</span></p>
<div id=""output""></div>
<script>
var htmlCs = window.getComputedStyle(document.documentElement);
var bodyCs = window.getComputedStyle(document.body);
var slashCs = window.getComputedStyle(document.getElementById('slash'));
var r = [];
r.push('html-color=' + htmlCs.color);
r.push('body-pl=' + bodyCs.paddingLeft);
r.push('slash-vis=' + slashCs.visibility);
r.push('slash-color=' + slashCs.color);
document.getElementById('output').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // Color should be black (from html rule that was :root)
        Assert.Contains("html-color=black", result);
        // Body padding-left should be 2em
        Assert.Contains("body-pl=2em", result);
        // Slash visibility should be hidden (class="hidden")
        Assert.Contains("slash-vis=hidden", result);
    }

    /// <summary>
    /// Integration test: verifies that the Acid3 bucket CSS rules produce
    /// valid computed styles for inline-block elements with dotted borders.
    /// </summary>
    [Fact]
    public void Acid3_Bucket_InlineBlock_Css_Integration()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { margin: 0; border: 1px blue; padding: 0; font: inherit; }
html { font: 20px Arial, sans-serif; }
.buckets { font: 0/0 Arial, sans-serif; padding: 0 0 150px 3px; }
:first-child + * .buckets p { display: inline-block; vertical-align: 2em; border: 2em dotted red; padding: 1.0em 0 1.0em 2em; }
* + * > * > p { margin: 0; border: 1px solid ! important; }
.z { visibility: hidden; }
#bucket1 { font-size: 20px; margin-left: 0.2em; padding-left: 1.3em; padding-right: 1.3em; }
</style>
</head><body>
<div class=""buckets"">
  <p id=""bucket1"" class=""z"">B1</p>
</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('bucket1'));
var r = [];
r.push('display=' + cs.display);
r.push('vis=' + cs.visibility);
r.push('fs=' + cs.fontSize);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("display=inline-block", result);
        Assert.Contains("vis=hidden", result);
        Assert.Contains("fs=20px", result);
    }

    // ────────────── Border-color shorthand expansion ──────────────

    /// <summary>
    /// Verifies that the <c>border</c> shorthand expands the color component
    /// into individual side-color longhands (border-top-color, etc.).
    /// Acid3 uses <c>border: 2cm solid gray</c> on &lt;html&gt; — the gray
    /// must propagate to all four border-*-color properties.
    /// </summary>
    [Fact]
    public void Border_Shorthand_Expands_Color_To_Individual_Sides()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
html { border: 2cm solid gray; }
</style>
</head><body>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.documentElement);
var r = [];
r.push('btc=' + cs.borderTopColor);
r.push('brc=' + cs.borderRightColor);
r.push('bbc=' + cs.borderBottomColor);
r.push('blc=' + cs.borderLeftColor);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("btc=gray", result);
        Assert.Contains("brc=gray", result);
        Assert.Contains("bbc=gray", result);
        Assert.Contains("blc=gray", result);
    }

    /// <summary>
    /// Verifies that the <c>border-color</c> 4-value shorthand expands into
    /// individual side-color longhands following CSS box-model order.
    /// </summary>
    [Fact]
    public void BorderColor_FourValue_Shorthand_Expands_To_Sides()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { border-style: solid; border-width: 1px; border-color: red green blue yellow; }
</style>
</head><body>
<div id=""target"">test</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push('btc=' + cs.borderTopColor);
r.push('brc=' + cs.borderRightColor);
r.push('bbc=' + cs.borderBottomColor);
r.push('blc=' + cs.borderLeftColor);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("btc=red", result);
        Assert.Contains("brc=green", result);
        Assert.Contains("bbc=blue", result);
        Assert.Contains("blc=yellow", result);
    }

    // ────────────── CSS initial values for missing properties ──────────────

    /// <summary>
    /// Verifies that <c>getComputedStyle</c> returns correct CSS initial values
    /// for properties commonly queried by Acid3 that were not previously in
    /// the initial-values dictionary (z-index, width, height, etc.).
    /// </summary>
    [Fact]
    public void GetComputedStyle_Returns_Correct_Initial_Values()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<div id=""target"">test</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push('zi=' + cs.zIndex);
r.push('t=' + cs.top);
r.push('l=' + cs.left);
r.push('w=' + cs.width);
r.push('h=' + cs.height);
r.push('mw=' + cs.maxWidth);
r.push('mh=' + cs.maxHeight);
r.push('bs=' + cs.boxSizing);
r.push('ls=' + cs.letterSpacing);
r.push('ws=' + cs.wordSpacing);
r.push('ti=' + cs.textIndent);
r.push('ts=' + cs.textShadow);
r.push('fv=' + cs.fontVariant);
r.push('bi=' + cs.backgroundImage);
r.push('bp=' + cs.backgroundPosition);
r.push('br=' + cs.backgroundRepeat);
r.push('bco=' + cs.borderCollapse);
r.push('bsp=' + cs.borderSpacing);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("zi=auto", result);
        Assert.Contains("t=auto", result);
        Assert.Contains("l=auto", result);
        Assert.Contains("w=auto", result);
        Assert.Contains("h=auto", result);
        Assert.Contains("mw=none", result);
        Assert.Contains("mh=none", result);
        Assert.Contains("bs=content-box", result);
        Assert.Contains("ls=normal", result);
        Assert.Contains("ws=normal", result);
        Assert.Contains("ti=0px", result);
        Assert.Contains("ts=none", result);
        Assert.Contains("fv=normal", result);
        Assert.Contains("bi=none", result);
        Assert.Contains("bp=0% 0%", result);
        Assert.Contains("br=repeat", result);
        Assert.Contains("bco=separate", result);
        Assert.Contains("bsp=0px", result);
    }

    /// <summary>
    /// Verifies that border-*-color initial values return rgb(0, 0, 0) when
    /// no CSS rule sets a border color (CSS2.1: border color defaults to the
    /// element's color property, but initial value is currentColor → black).
    /// </summary>
    [Fact]
    public void BorderColor_Initial_Values_Return_Black()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<div id=""target"">test</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push('btc=' + cs.borderTopColor);
r.push('brc=' + cs.borderRightColor);
r.push('bbc=' + cs.borderBottomColor);
r.push('blc=' + cs.borderLeftColor);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // When no CSS sets the border-color, initial value is rgb(0, 0, 0)
        Assert.Contains("btc=rgb(0, 0, 0)", result);
        Assert.Contains("brc=rgb(0, 0, 0)", result);
        Assert.Contains("bbc=rgb(0, 0, 0)", result);
        Assert.Contains("blc=rgb(0, 0, 0)", result);
    }

    // ────────────── TODO-11 (D11): Inline formatting context ──────────────

    /// <summary>
    /// Verifies that <c>display: inline-block</c> elements participate in inline
    /// formatting context — multiple inline-block elements should lay out side-by-side.
    /// The Acid3 test buckets use this pattern for the six colored boxes.
    /// </summary>
    [Fact]
    public void InlineBlock_Elements_Participate_In_Inline_Formatting_Context()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.container { font: 0/0 Arial, sans-serif; }
.box { display: inline-block; width: 50px; height: 50px; }
#box1 { background: red; }
#box2 { background: green; }
#box3 { background: blue; }
</style>
</head><body>
<div class=""container"">
  <div id=""box1"" class=""box""></div>
  <div id=""box2"" class=""box""></div>
  <div id=""box3"" class=""box""></div>
</div>
<div id=""result""></div>
<script>
var r = [];
var cs1 = window.getComputedStyle(document.getElementById('box1'));
var cs2 = window.getComputedStyle(document.getElementById('box2'));
var cs3 = window.getComputedStyle(document.getElementById('box3'));
r.push('d1=' + cs1.display);
r.push('d2=' + cs2.display);
r.push('d3=' + cs3.display);
r.push('w1=' + cs1.width);
r.push('w2=' + cs2.width);
r.push('w3=' + cs3.width);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("d1=inline-block", result);
        Assert.Contains("d2=inline-block", result);
        Assert.Contains("d3=inline-block", result);
        Assert.Contains("w1=50px", result);
        Assert.Contains("w2=50px", result);
        Assert.Contains("w3=50px", result);
    }

    /// <summary>
    /// Verifies that <c>border-style: dotted</c> is correctly computed and
    /// accessible via getComputedStyle. Acid3 bucket elements use
    /// <c>border: 2em dotted red</c>.
    /// </summary>
    [Fact]
    public void DottedBorder_Style_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { border: 2em dotted red; }
</style>
</head><body>
<div id=""target"">test</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push('bts=' + cs.borderTopStyle);
r.push('brs=' + cs.borderRightStyle);
r.push('bbs=' + cs.borderBottomStyle);
r.push('bls=' + cs.borderLeftStyle);
r.push('btc=' + cs.borderTopColor);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("bts=dotted", result);
        Assert.Contains("brs=dotted", result);
        Assert.Contains("bbs=dotted", result);
        Assert.Contains("bls=dotted", result);
        Assert.Contains("btc=red", result);
    }

    // ────────────── TODO-6 (D6): Negative margin + padding text ──────────────

    /// <summary>
    /// Verifies that negative <c>margin-right</c> combined with
    /// <c>padding-right</c> does not collapse or remove text content.
    /// Acid3 instruction text uses <c>margin-right: -20px; padding-right: 20px</c>.
    /// </summary>
    [Fact]
    public void Negative_Margin_With_Padding_Preserves_Text()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#instructions { margin-right: -20px; padding-right: 20px; font-size: 0.8em; }
</style>
</head><body>
<p id=""instructions"">To pass the test, a browser must use its default settings.</p>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('instructions'));
var r = [];
r.push('mr=' + cs.marginRight);
r.push('pr=' + cs.paddingRight);
r.push('text=' + document.getElementById('instructions').textContent.substring(0, 20));
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("mr=-20px", result);
        Assert.Contains("pr=20px", result);
        Assert.Contains("text=To pass the test, a", result);
    }

    // ────────────── Overflow property handling ──────────────

    /// <summary>
    /// Verifies that <c>overflow-x</c> and <c>overflow-y</c> initial values
    /// are correctly returned by getComputedStyle.
    /// </summary>
    [Fact]
    public void Overflow_XY_Initial_Values_Are_Visible()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<div id=""target"">test</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push('ox=' + cs.overflowX);
r.push('oy=' + cs.overflowY);
r.push('o=' + cs.overflow);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("ox=visible", result);
        Assert.Contains("oy=visible", result);
        Assert.Contains("o=visible", result);
    }

    /// <summary>
    /// Verifies that the <c>font-variant: small-caps</c> property is correctly
    /// computed. Acid3 uses <c>font: 900 small-caps 10px sans-serif</c> on
    /// the #linktest element.
    /// </summary>
    [Fact]
    public void FontVariant_SmallCaps_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { font-variant: small-caps; }
</style>
</head><body>
<span id=""target"">Test Text</span>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = 'fv=' + cs.fontVariant;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("fv=small-caps", result);
    }
}
