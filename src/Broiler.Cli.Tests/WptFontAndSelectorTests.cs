namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for WPT (Web Platform Tests) compliance:
/// - css/css-fonts/font-palette-empty-font-family.html
/// - css/selectors/selectors-case-sensitive-001.html
/// </summary>
public class WptFontAndSelectorTests
{
    // ────────── font-palette-empty-font-family ──────────────────────────

    /// <summary>
    /// WPT: css/css-fonts/font-palette-empty-font-family.html
    /// The 'font' shorthand with an empty font-family (font: 48px '') is
    /// invalid per CSS 2.1 §15.8 because font-family is required.
    /// The entire declaration must be discarded.
    /// </summary>
    [Fact]
    public void FontShorthand_EmptyFontFamily_DeclarationDiscarded()
    {
        // The font shorthand sets font-size=48px only if the font-family
        // is valid.  An empty quoted family '' makes the whole shorthand
        // invalid, so font-size must remain at the default (medium).
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@font-face {
    font-family: """";
    src: url(""resources/test.ttf"") format(""truetype"");
}
@font-palette-values --MyPalette {
    font-family: """";
    base-palette: 1;
}
</style>
</head><body>
<div id=""target"" style=""font: 48px ''; font-palette: --MyPalette;"">A</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var fs = cs.getPropertyValue('font-size') || cs.fontSize || '';
document.getElementById('result').textContent = 'FONTSIZE:' + fs + ':END';
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // The font shorthand is invalid → font-size remains at default,
        // NOT 48px.  Extract the FONTSIZE marker to avoid matching the
        // style attribute text.
        var marker = "FONTSIZE:";
        int idx = result.IndexOf(marker);
        Assert.True(idx >= 0, "Result should contain FONTSIZE marker");
        var afterMarker = result.Substring(idx + marker.Length);
        int endIdx = afterMarker.IndexOf(":END");
        var fontSize = endIdx >= 0 ? afterMarker.Substring(0, endIdx) : afterMarker;
        Assert.DoesNotContain("48", fontSize);
    }

    /// <summary>
    /// Verifies that a valid font shorthand (font: 24px serif) still works
    /// correctly after the empty-font-family fix.
    /// </summary>
    [Fact]
    public void FontShorthand_ValidFontFamily_StillWorks()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"" style=""font: 24px serif;"">B</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var fs = cs.getPropertyValue('font-size') || cs.fontSize || '';
document.getElementById('result').textContent = 'fs=' + fs;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // Valid font shorthand → font-size should be 24px.
        Assert.Contains("24", result);
    }

    /// <summary>
    /// Verifies that unknown at-rules like @font-palette-values are
    /// silently ignored and don't break other CSS rules.
    /// </summary>
    [Fact]
    public void UnknownAtRule_FontPaletteValues_Ignored()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@font-palette-values --MyPalette {
    font-family: ""TestFont"";
    base-palette: 1;
}
div { color: blue; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.color;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // The @font-palette-values should be ignored, and div { color: blue } should apply.
        Assert.Contains("blue", result);
    }

    // ────────── selectors-case-sensitive-001 ────────────────────────────

    /// <summary>
    /// WPT: css/selectors/selectors-case-sensitive-001.html
    /// CSS type selectors use ASCII case-insensitive matching.
    /// U+212A (Kelvin sign) must NOT be matched by 'k' or 'K' selectors,
    /// because Unicode case-folding is not applied.
    /// </summary>
    [Fact]
    public void TypeSelector_UnicodeKelvinSign_NotMatchedByAsciiK()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
// U+212A is the Kelvin sign — its Unicode lowercase is 'k',
// but CSS must NOT fold it to ASCII 'k'.
var el = document.createElement('\u212A');
document.body.appendChild(el);
var matched = document.querySelector('k');
document.getElementById('result').textContent = 'match=' + (matched !== null);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // querySelector('k') must NOT match an element with tag name U+212A.
        Assert.Contains("match=false", result);
    }

    /// <summary>
    /// CSS escape sequences in selectors must be decoded properly.
    /// The selector \212A should match elements with tag name U+212A.
    /// </summary>
    [Fact]
    public void CssEscape_InSelector_DecodedToUnicode()
    {
        // The CSS rule uses \212A (CSS escape for U+212A Kelvin sign).
        // It should apply to elements created with tag name '\u212A'.
        var html = @"<!DOCTYPE html>
<html><head>
<style>
\212A {
  display: block;
  background: lime;
  width: 200px;
  height: 100px;
}
</style>
</head><body>
<div id=""container""></div>
<div id=""result""></div>
<script>
var el = document.createElement('\u212A');
document.getElementById('container').appendChild(el);
var cs = window.getComputedStyle(el);
var h = cs.getPropertyValue('height') || cs.height || 'none';
document.getElementById('result').textContent = 'HEIGHT:' + h + ':END';
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // The CSS rule \212A should match the element → height = 100px.
        Assert.Contains("HEIGHT:100px:END", result);
    }

    /// <summary>
    /// Verifies that normal ASCII type selectors remain case-insensitive
    /// (e.g. DIV and div should match the same elements).
    /// </summary>
    [Fact]
    public void TypeSelector_AsciiCaseInsensitive_StillWorks()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
DIV { color: red; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.color;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // DIV selector should match <div> element (ASCII case-insensitive).
        Assert.Contains("red", result);
    }
}
