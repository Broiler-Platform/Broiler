namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 7 Acid3 compliance: CSS rendering improvements —
/// hsl()/hsla() color parsing, @font-face rule handling, text-shadow
/// property in getComputedStyle, position: fixed support, data: URI
/// backgrounds, and :root selector styling.
/// </summary>
public class CssRenderingTests
{
    // ────────────────────── hsl() color parsing ──────────────────────

    [Fact]
    public void Hsl_Color_Applied_Via_Style()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var d = document.createElement('div');
d.style.color = 'hsl(0, 100%, 50%)';
document.getElementById('result').textContent = d.style.color;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("hsl(0, 100%, 50%)", result);
    }

    [Fact]
    public void Hsl_Color_GetComputedStyle_Returns_Value()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { color: hsl(120, 100%, 50%); }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.color !== '');
r.push(cs.getPropertyValue('color') !== '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Hsla_Color_Applied_Via_Style()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var d = document.createElement('div');
d.style.color = 'hsla(0, 0%, 0%, 1.0)';
document.getElementById('result').textContent = d.style.color;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("hsla(0, 0%, 0%, 1.0)", result);
    }

    [Fact]
    public void Hsla_Color_GetComputedStyle_Returns_Value()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { color: hsla(0, 0%, 0%, 1.0); }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.color === 'hsla(0, 0%, 0%, 1.0)');
r.push(cs.getPropertyValue('color') === 'hsla(0, 0%, 0%, 1.0)');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Hsla_Color_Without_Percent_Signs()
    {
        // Acid3 uses: color: hsla(0, 0, 0, 1) — without % on saturation/lightness
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { color: hsla(0, 0, 0, 1); }
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
        Assert.Contains("hsla(0, 0, 0, 1)", result);
    }

    [Fact]
    public void Hsl_Color_Override_Previous_Value()
    {
        // Acid3 pattern: color: red; color: hsla(0, 0%, 0%, 1.0);
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { color: red; color: hsla(0, 0%, 0%, 1.0); }
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
        Assert.Contains("hsla(0, 0%, 0%, 1.0)", result);
    }

    // ────────────────────── @font-face rule handling ──────────────────────

    [Fact]
    public void FontFace_Rule_In_StyleSheet_CssRules()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@font-face { font-family: ""TestFont""; src: url(test.ttf); }
body { color: black; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rules = sheet.cssRules;
r.push(rules.length >= 2);
// Find the @font-face rule
var fontRule = null;
for (var i = 0; i < rules.length; i++) {
    if (rules[i].type === 5) { fontRule = rules[i]; break; }
}
r.push(fontRule !== null);
if (fontRule) {
    r.push(fontRule.cssText.indexOf('@font-face') >= 0);
}
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void FontFace_Rule_Style_Property_Access()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@font-face { font-family: ""AcidTest""; src: url(font.ttf); }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rules = sheet.cssRules;
var fontRule = null;
for (var i = 0; i < rules.length; i++) {
    if (rules[i].type === 5) { fontRule = rules[i]; break; }
}
r.push(fontRule !== null);
if (fontRule && fontRule.style) {
    r.push(fontRule.style['font-family'] !== undefined);
    r.push(fontRule.style.fontFamily !== undefined);
}
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void CssFontFaceRule_Exposes_Style_Backreferences_And_Style_Methods()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@font-face { font-family: ""RoadmapFont""; src: url(font.ttf); font-weight: 700; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rule = sheet.cssRules[0];
r.push(rule.type === 5);
r.push(rule.parentStyleSheet === sheet);
r.push(rule.parentRule === null);
r.push(rule.style.parentRule === rule);
r.push(rule.style.getPropertyValue('font-family').indexOf('RoadmapFont') >= 0);
r.push(rule.style.getPropertyValue('src') === 'url(font.ttf)');
r.push(rule.style.getPropertyValue('font-weight') === '700');
rule.style.setProperty('font-style', 'italic');
r.push(rule.style.getPropertyValue('font-style') === 'italic');
r.push(rule.cssText.indexOf('font-style: italic') >= 0);
r.push(rule.style.removeProperty('src') === 'url(font.ttf)');
r.push(rule.style.getPropertyValue('src') === '');
r.push(rule.cssText.indexOf('src: url(font.ttf)') === -1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssFontFaceRule_Preserves_Mixed_Rule_Order_With_Charset_And_Page()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@charset ""utf-8"";
@import url(""base.css"");
@page cover { margin-top: 1in; }
@font-face { font-family: ""RoadmapFont""; src: url(font.ttf); }
.test { color: blue; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rules = document.styleSheets[0].cssRules;
r.push(rules.length === 5);
r.push(rules[0].type === 2);
r.push(rules[1].type === 3);
r.push(rules[2].type === 6);
r.push(rules[3].type === 5);
r.push(rules[4].type === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssPropertyRule_Exposes_Name_Syntax_Inherits_InitialValue_And_CssText()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@property --roadmap-color {
  syntax: '<color>';
  inherits: false;
  initial-value: teal;
}
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rule = sheet.cssRules[0];
r.push(rule.type === 25);
r.push(rule.parentStyleSheet === sheet);
r.push(rule.parentRule === null);
r.push(rule.name === '--roadmap-color');
r.push(rule.syntax === '<color>');
r.push(rule.inherits === false);
r.push(rule.initialValue === 'teal');
r.push(rule.cssText.indexOf('@property --roadmap-color') === 0);
r.push(rule.cssText.indexOf('syntax: ""<color>""') >= 0);
r.push(rule.cssText.indexOf('inherits: false') >= 0);
r.push(rule.cssText.indexOf('initial-value: teal') >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssPropertyRule_Preserves_Mixed_Rule_Order_With_Charset_FontFace_And_Style()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@charset ""utf-8"";
@import url(""base.css"");
@property --roadmap-gap {
  syntax: '<length>';
  inherits: true;
  initial-value: 1px;
}
@font-face { font-family: ""RoadmapFont""; src: url(font.ttf); }
.test { gap: var(--roadmap-gap); }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rules = document.styleSheets[0].cssRules;
r.push(rules.length === 5);
r.push(rules[0].type === 2);
r.push(rules[1].type === 3);
r.push(rules[2].type === 25);
r.push(rules[3].type === 5);
r.push(rules[4].type === 1);
r.push(rules[2].name === '--roadmap-gap');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssCounterStyleRule_Exposes_Name_Descriptors_And_CssText()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@counter-style roadmap-counter {
  system: numeric;
  symbols: ""⓪"" ""①"" ""②"";
  suffix: "": "";
  fallback: decimal;
}
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rule = sheet.cssRules[0];
r.push(rule.type === 10);
r.push(rule.parentStyleSheet === sheet);
r.push(rule.parentRule === null);
r.push(rule.name === 'roadmap-counter');
r.push(rule.system === 'numeric');
r.push(rule.symbols.indexOf('①') >= 0);
r.push(rule.suffix.indexOf(':') >= 0);
r.push(rule.fallback === 'decimal');
r.push(rule.cssText.indexOf('@counter-style roadmap-counter') === 0);
r.push(rule.cssText.indexOf('symbols: ""⓪"" ""①"" ""②""') >= 0);
r.push(rule.cssText.indexOf('fallback: decimal') >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssCounterStyleRule_Preserves_Mixed_Rule_Order_With_Charset_Property_FontFace_And_Style()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@charset ""utf-8"";
@import url(""base.css"");
@counter-style roadmap-counter {
  system: numeric;
  symbols: ""⓪"" ""①"" ""②"";
}
@property --roadmap-gap {
  syntax: '<length>';
  inherits: true;
  initial-value: 1px;
}
@font-face { font-family: ""RoadmapFont""; src: url(font.ttf); }
.test { list-style: roadmap-counter; gap: var(--roadmap-gap); }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rules = document.styleSheets[0].cssRules;
r.push(rules.length === 6);
r.push(rules[0].type === 2);
r.push(rules[1].type === 3);
r.push(rules[2].type === 10);
r.push(rules[3].type === 25);
r.push(rules[4].type === 5);
r.push(rules[5].type === 1);
r.push(rules[2].name === 'roadmap-counter');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void StyleRule_Has_Type_1()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
body { color: black; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rules = sheet.cssRules;
r.push(rules.length >= 1);
r.push(rules[0].type === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void StyleRule_SelectorText_Property()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
div.test { color: red; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rules = sheet.cssRules;
r.push(rules[0].selectorText === 'div.test');
r.push(rules[0].style !== undefined);
r.push(rules[0].style.color === 'red');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── text-shadow property ──────────────────────

    [Fact]
    public void TextShadow_GetComputedStyle_Returns_Value()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { text-shadow: rgba(192, 192, 192, 1.0) 3px 3px; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.textShadow !== undefined);
r.push(cs.textShadow !== '');
r.push(cs.getPropertyValue('text-shadow') !== '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void TextShadow_Set_Via_Style()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var el = document.getElementById('target');
el.style.textShadow = '2px 2px #ff0000';
var r = [];
r.push(el.style.textShadow === '2px 2px #ff0000');
r.push(el.style.getPropertyValue('text-shadow') === '2px 2px #ff0000');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void TextShadow_CssText_Includes_Property()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"" style=""text-shadow: 1px 1px black;"">text</div>
<div id=""result""></div>
<script>
var el = document.getElementById('target');
document.getElementById('result').textContent = el.style.cssText.indexOf('text-shadow') >= 0;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    // ────────────────────── position: fixed ──────────────────────

    [Fact]
    public void PositionFixed_GetComputedStyle_Returns_Fixed()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { position: fixed; left: 10px; top: 20px; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.position === 'fixed');
r.push(cs.left === '10px');
r.push(cs.top === '20px');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void PositionFixed_Set_Via_Style()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var el = document.getElementById('target');
el.style.position = 'fixed';
el.style.left = '130.5px';
el.style.top = '84.3px';
var r = [];
r.push(el.style.position === 'fixed');
r.push(el.style.left === '130.5px');
r.push(el.style.top === '84.3px');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void PositionFixed_With_Percentage_Values()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { position: fixed; top: 10%; left: 10%; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.position === 'fixed');
r.push(cs.top === '10%');
r.push(cs.left === '10%');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── data: URI backgrounds ──────────────────────

    [Fact]
    public void DataUri_Background_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { background: url(data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7) no-repeat; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.background !== undefined);
r.push(cs.background !== '');
r.push(cs.background.indexOf('data:image') >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void DataUri_Background_Set_Via_Style()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var el = document.getElementById('target');
el.style.background = 'url(data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7)';
var r = [];
r.push(el.style.background.indexOf('data:image') >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    // ────────────────────── :root selector styling ──────────────────────

    [Fact]
    public void Root_Selector_GetComputedStyle_Applies()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
:root { background: silver; color: black; }
</style>
</head><body>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.documentElement);
var r = [];
r.push(cs.background === 'silver');
r.push(cs.color === 'black');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Root_Selector_Does_Not_Apply_To_Body()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
:root { background: silver; }
body { background: white; }
</style>
</head><body>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.body);
var r = [];
r.push(cs.background === 'white');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    // ────────────────────── CSS rule style object ──────────────────────

    [Fact]
    public void CssRule_Style_CamelCase_Access()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.test { background-color: red; font-size: 14px; z-index: 1; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rule = sheet.cssRules[0];
r.push(rule.style.backgroundColor === 'red');
r.push(rule.style.fontSize === '14px');
r.push(rule.style.zIndex === '1');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void CssRule_Style_KebabCase_Access()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.test { background-color: red; font-size: 14px; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rule = sheet.cssRules[0];
r.push(rule.style['background-color'] === 'red');
r.push(rule.style['font-size'] === '14px');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void CssRule_Style_Cssom_Methods_Enumerate_And_Read_Priority()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.test { color: red !important; font-size: 14px; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rule = document.styleSheets[0].cssRules[0];
r.push(rule.style.getPropertyValue('color'));
r.push(rule.style.getPropertyPriority('color'));
r.push(rule.style.length);
r.push(rule.style.item(0));
r.push(rule.style.item(1));
r.push(rule.style.item(99) === '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("red,important,2,color,font-size,true", result);
    }

    [Fact]
    public void CssRule_Style_SetProperty_And_RemoveProperty_Modify_Rule_Style()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.test { color: red; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rule = document.styleSheets[0].cssRules[0];
rule.style.setProperty('margin-left', '2px', 'important');
r.push(rule.style.getPropertyValue('margin-left'));
r.push(rule.style.getPropertyPriority('margin-left'));
r.push(rule.style.marginLeft);
r.push(rule.cssText.indexOf('margin-left: 2px !important;') >= 0);
r.push(rule.style.removeProperty('color'));
r.push(rule.style.getPropertyValue('color') === '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("2px,important,2px,true,red,true", result);
    }

    [Fact]
    public void CssRule_Style_CssText_Getter_And_Setter_Work()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.test { color: red; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rule = document.styleSheets[0].cssRules[0];
r.push(rule.style.cssText.indexOf('color: red;') >= 0);
rule.style.cssText = 'background-color: blue; float: left;';
r.push(rule.style.backgroundColor);
r.push(rule.style.cssFloat);
r.push(rule.style.length);
r.push(rule.cssText.indexOf('background-color: blue;') >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,blue,left,2,true", result);
    }

    [Fact]
    public void CssRule_Parent_Backreferences_Are_Exposed()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.test { color: red; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rule = sheet.cssRules[0];
r.push(rule.parentStyleSheet === sheet);
r.push(rule.parentRule === null);
r.push(rule.style.parentRule === rule);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Element_And_Computed_Style_ParentRule_Is_Null()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { color: red; }
</style>
</head><body>
<div id=""target"" style=""margin-left: 2px""></div>
<div id=""result""></div>
<script>
var r = [];
var target = document.getElementById('target');
r.push(target.style.parentRule === null);
r.push(window.getComputedStyle(target).parentRule === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void CssMediaRule_Exposes_Type_Media_And_Nested_CssRules()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@media screen and (min-width: 1px) { .test { color: red; } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rule = document.styleSheets[0].cssRules[0];
r.push(rule.type === 4);
r.push(rule.media.indexOf('screen') >= 0);
r.push(rule.cssRules.length === 1);
r.push(rule.cssRules[0].type === 1);
r.push(rule.cssRules[0].selectorText === '.test');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void CssMediaRule_Nested_Rules_Expose_Backreferences()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@media all and (min-width: 1px) { .test { color: red; } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var mediaRule = sheet.cssRules[0];
var nestedRule = mediaRule.cssRules[0];
r.push(mediaRule.parentRule === null);
r.push(nestedRule.parentRule === mediaRule);
r.push(nestedRule.parentStyleSheet === sheet);
r.push(nestedRule.style.parentRule === nestedRule);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void CssMediaRule_CssText_Rebuilds_From_Nested_Rules()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@media (min-width: 1px) { .test { color: red; } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var mediaRule = document.styleSheets[0].cssRules[0];
r.push(mediaRule.cssText.indexOf('@media') >= 0);
r.push(mediaRule.cssText.indexOf('(min-width: 1px)') >= 0);
mediaRule.cssRules[0].style.setProperty('margin-left', '2px');
r.push(mediaRule.cssText.indexOf('margin-left: 2px;') >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void CssMediaRule_CssRules_Item_InsertRule_And_DeleteRule_Update_Nested_List_And_CssText()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@media (min-width: 1px) { .first { color: red; } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var mediaRule = document.styleSheets[0].cssRules[0];
var nestedRules = mediaRule.cssRules;
r.push(typeof nestedRules.item === 'function');
r.push(nestedRules.item(0).selectorText === '.first');
r.push(nestedRules.insertRule('.second { color: blue; }', 1) === 1);
r.push(nestedRules.length === 2);
r.push(nestedRules[1].selectorText === '.second');
r.push(mediaRule.cssText.indexOf('.second') >= 0);
nestedRules.deleteRule(0);
r.push(nestedRules.length === 1);
r.push(nestedRules[0].selectorText === '.second');
r.push(mediaRule.cssText.indexOf('.first') === -1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssImportRule_Exposes_Type_Href_Media_And_CssText()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@import url(""screen.css"") screen and (min-width: 1px);
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rule = document.styleSheets[0].cssRules[0];
r.push(rule.type === 3);
r.push(rule.href.indexOf('screen.css') >= 0);
r.push(rule.media.indexOf('screen') >= 0);
r.push(rule.cssText.indexOf('@import') >= 0);
r.push(rule.cssText.indexOf('screen.css') >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void CssImportRule_Can_Use_Quoted_Href_Syntax()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@import ""theme.css"";
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rule = document.styleSheets[0].cssRules[0];
r.push(rule.type === 3);
r.push(rule.href === 'theme.css');
r.push(rule.media === '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void CssImportRule_Preserves_Mixed_Rule_Order()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@import url(""base.css"");
@media screen { .test { color: red; } }
@font-face { font-family: ""RoadmapTest""; src: url(font.ttf); }
.test { color: blue; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rules = document.styleSheets[0].cssRules;
r.push(rules.length === 4);
r.push(rules[0].type === 3);
r.push(rules[1].type === 4);
r.push(rules[2].type === 5);
r.push(rules[3].type === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void CssKeyframesRule_Exposes_Type_Name_And_Nested_CssRules()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@keyframes pulse { 0% { opacity: 0; } 50%, to { opacity: 1; } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rule = document.styleSheets[0].cssRules[0];
r.push(rule.type === 7);
r.push(rule.name === 'pulse');
r.push(rule.cssRules.length === 2);
r.push(rule.cssRules[0].type === 8);
r.push(rule.cssRules[0].keyText === '0%');
r.push(rule.cssRules[1].keyText === '50%, to');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssKeyframesRule_Keyframe_Rules_Expose_Style_And_Backreferences()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@keyframes slide { from { transform: translateX(0px); } to { transform: translateX(10px); } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var keyframesRule = sheet.cssRules[0];
var keyframeRule = keyframesRule.cssRules[0];
r.push(keyframeRule.type === 8);
r.push(keyframeRule.keyText === 'from');
r.push(keyframeRule.style.getPropertyValue('transform') === 'translateX(0px)');
r.push(keyframeRule.parentRule === keyframesRule);
r.push(keyframeRule.parentStyleSheet === sheet);
r.push(keyframeRule.style.parentRule === keyframeRule);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssKeyframesRule_CssText_Rebuilds_From_Nested_Keyframes()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@keyframes pulse { from { opacity: 0; } to { opacity: 1; } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var keyframesRule = document.styleSheets[0].cssRules[0];
r.push(keyframesRule.cssText.indexOf('@keyframes pulse') >= 0);
r.push(keyframesRule.cssText.indexOf('from') >= 0);
keyframesRule.cssRules[0].style.setProperty('opacity', '0.25');
r.push(keyframesRule.cssText.indexOf('opacity: 0.25;') >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void CssKeyframesRule_CssRules_InsertRule_And_DeleteRule_Update_Keyframes_And_CssText()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@keyframes pulse { from { opacity: 0; } to { opacity: 1; } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var keyframesRule = document.styleSheets[0].cssRules[0];
var nestedRules = keyframesRule.cssRules;
r.push(typeof nestedRules.item === 'function');
r.push(nestedRules.item(0).keyText === 'from');
r.push(nestedRules.insertRule('50% { opacity: 0.5; }', 1) === 1);
r.push(nestedRules.length === 3);
r.push(nestedRules[1].keyText === '50%');
r.push(keyframesRule.cssText.indexOf('50%') >= 0);
nestedRules.deleteRule(0);
r.push(nestedRules.length === 2);
r.push(nestedRules[0].keyText === '50%');
r.push(keyframesRule.cssText.indexOf('from') === -1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssKeyframesRule_Preserves_Mixed_Rule_Order()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@import url(""base.css"");
@media screen { .test { color: red; } }
@keyframes pulse { from { opacity: 0; } to { opacity: 1; } }
@font-face { font-family: ""RoadmapKeyframeTest""; src: url(font.ttf); }
.test { color: blue; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rules = document.styleSheets[0].cssRules;
r.push(rules.length === 5);
r.push(rules[0].type === 3);
r.push(rules[1].type === 4);
r.push(rules[2].type === 7);
r.push(rules[3].type === 5);
r.push(rules[4].type === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssSupportsRule_Exposes_Type_ConditionText_And_Nested_CssRules()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@supports (display: flex) { .flex { display: flex; } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rule = document.styleSheets[0].cssRules[0];
r.push(rule.type === 11);
r.push(rule.conditionText === '(display: flex)');
r.push(rule.cssRules.length === 1);
r.push(rule.cssRules[0].type === 1);
r.push(rule.cssRules[0].selectorText === '.flex');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void CssSupportsRule_Nested_Rules_Expose_Backreferences()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@supports (display: grid) { .grid { display: grid; } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var supportsRule = sheet.cssRules[0];
var nestedRule = supportsRule.cssRules[0];
r.push(supportsRule.parentRule === null);
r.push(nestedRule.parentRule === supportsRule);
r.push(nestedRule.parentStyleSheet === sheet);
r.push(nestedRule.style.parentRule === nestedRule);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void CssSupportsRule_CssText_Rebuilds_From_Nested_Rules()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@supports (display: flex) { .flex { display: flex; } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var supportsRule = document.styleSheets[0].cssRules[0];
r.push(supportsRule.cssText.indexOf('@supports') >= 0);
r.push(supportsRule.cssText.indexOf('(display: flex)') >= 0);
supportsRule.cssRules[0].style.setProperty('display', 'inline-flex');
r.push(supportsRule.cssText.indexOf('display: inline-flex;') >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void CssSupportsRule_Preserves_Mixed_Rule_Order()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@import url(""base.css"");
@supports (display: flex) { .flex { display: flex; } }
@media screen { .test { color: red; } }
@keyframes pulse { from { opacity: 0; } to { opacity: 1; } }
.test { color: blue; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rules = document.styleSheets[0].cssRules;
r.push(rules.length === 5);
r.push(rules[0].type === 3);
r.push(rules[1].type === 11);
r.push(rules[2].type === 4);
r.push(rules[3].type === 7);
r.push(rules[4].type === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssLayerRule_Exposes_Type_Name_And_Nested_CssRules()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@layer utilities { .mt-1 { margin-top: 1px; } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rule = document.styleSheets[0].cssRules[0];
r.push(rule.type === 12);
r.push(rule.name === 'utilities');
r.push(rule.cssRules.length === 1);
r.push(rule.cssRules[0].type === 1);
r.push(rule.cssRules[0].selectorText === '.mt-1');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void CssLayerRule_Anonymous_Block_Exposes_Null_Name_And_Backreferences()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@layer { .mt-2 { margin-top: 2px; } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var layerRule = sheet.cssRules[0];
var nestedRule = layerRule.cssRules[0];
r.push(layerRule.name === null);
r.push(layerRule.parentRule === null);
r.push(nestedRule.parentRule === layerRule);
r.push(nestedRule.parentStyleSheet === sheet);
r.push(nestedRule.style.parentRule === nestedRule);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void CssLayerRule_Statement_Form_Is_Preserved_In_CssRules()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@layer base;
.test { color: blue; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rules = document.styleSheets[0].cssRules;
var rule = rules[0];
r.push(rules.length === 2);
r.push(rule.type === 12);
r.push(rule.name === 'base');
r.push(rule.cssRules.length === 0);
r.push(rule.cssText === '@layer base;');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void CssLayerRule_CssText_Rebuilds_From_Nested_Rules()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@layer theme { .button { color: red; } }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var layerRule = document.styleSheets[0].cssRules[0];
r.push(layerRule.cssText.indexOf('@layer theme') >= 0);
r.push(layerRule.cssText.indexOf('.button') >= 0);
layerRule.cssRules[0].style.setProperty('color', 'green');
r.push(layerRule.cssText.indexOf('color: green;') >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void CssLayerRule_Preserves_Mixed_Rule_Order()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@import url(""base.css"");
@layer reset;
@layer theme { .button { color: red; } }
@supports (display: flex) { .flex { display: flex; } }
@media screen { .test { color: blue; } }
.plain { color: black; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rules = document.styleSheets[0].cssRules;
r.push(rules.length === 6);
r.push(rules[0].type === 3);
r.push(rules[1].type === 12);
r.push(rules[2].type === 12);
r.push(rules[3].type === 11);
r.push(rules[4].type === 4);
r.push(rules[5].type === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssNamespaceRule_Exposes_Type_NamespaceUri_And_Prefix()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@namespace svg ""http://www.w3.org/2000/svg"";
@namespace ""http://www.w3.org/1999/xhtml"";
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rules = document.styleSheets[0].cssRules;
var prefixedRule = rules[0];
var defaultRule = rules[1];
r.push(prefixedRule.type === 9);
r.push(prefixedRule.prefix === 'svg');
r.push(prefixedRule.namespaceURI === 'http://www.w3.org/2000/svg');
r.push(defaultRule.type === 9);
r.push(defaultRule.prefix === undefined);
r.push(defaultRule.namespaceURI === 'http://www.w3.org/1999/xhtml');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssNamespaceRule_Supports_Url_Syntax_And_CssText()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@namespace mathml url(http://www.w3.org/1998/Math/MathML);
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rule = document.styleSheets[0].cssRules[0];
r.push(rule.type === 9);
r.push(rule.prefix === 'mathml');
r.push(rule.namespaceURI === 'http://www.w3.org/1998/Math/MathML');
r.push(rule.cssText.indexOf('@namespace mathml') >= 0);
r.push(rule.cssText.indexOf('http://www.w3.org/1998/Math/MathML') >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void CssNamespaceRule_Preserves_Mixed_Rule_Order()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@import url(""base.css"");
@namespace svg ""http://www.w3.org/2000/svg"";
@layer theme { .button { color: red; } }
@supports (display: flex) { .flex { display: flex; } }
.plain { color: black; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rules = document.styleSheets[0].cssRules;
r.push(rules.length === 5);
r.push(rules[0].type === 3);
r.push(rules[1].type === 9);
r.push(rules[2].type === 12);
r.push(rules[3].type === 11);
r.push(rules[4].type === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssPageRule_Exposes_Type_SelectorText_And_Style()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@page { margin-top: 1in; margin-bottom: 2in; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rule = sheet.cssRules[0];
r.push(rule.type === 6);
r.push(rule.selectorText === '');
r.push(rule.style.getPropertyValue('margin-top') === '1in');
r.push(rule.style.getPropertyValue('margin-bottom') === '2in');
r.push(rule.parentStyleSheet === sheet);
r.push(rule.parentRule === null);
r.push(rule.style.parentRule === rule);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssPageRule_Supports_Pseudo_Selectors_And_CssText_Rebuild()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@page :first { margin-top: 3cm; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rule = document.styleSheets[0].cssRules[0];
r.push(rule.type === 6);
r.push(rule.selectorText === ':first');
r.push(rule.cssText.indexOf('@page :first') >= 0);
r.push(rule.cssText.indexOf('margin-top: 3cm;') >= 0);
rule.style.setProperty('margin-left', '4cm');
r.push(rule.cssText.indexOf('margin-left: 4cm;') >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void CssPageRule_Preserves_Mixed_Rule_Order()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@import url(""base.css"");
@page :left { margin-top: 2cm; }
@namespace svg ""http://www.w3.org/2000/svg"";
@layer theme { .button { color: red; } }
.plain { color: black; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rules = document.styleSheets[0].cssRules;
r.push(rules.length === 5);
r.push(rules[0].type === 3);
r.push(rules[1].type === 6);
r.push(rules[2].type === 9);
r.push(rules[3].type === 12);
r.push(rules[4].type === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void CssCharsetRule_Exposes_Type_Encoding_And_CssText()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@charset ""UTF-8"";
body { color: black; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rule = sheet.cssRules[0];
r.push(rule.type === 2);
r.push(rule.encoding === 'UTF-8');
r.push(rule.cssText === '@charset ""UTF-8"";');
r.push(rule.parentStyleSheet === sheet);
r.push(rule.parentRule === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void CssCharsetRule_Preserves_Mixed_Rule_Order()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@charset ""windows-1252"";
@import url(""base.css"");
@page :left { margin-top: 2cm; }
@namespace svg ""http://www.w3.org/2000/svg"";
@layer theme { .button { color: red; } }
.plain { color: black; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var rules = document.styleSheets[0].cssRules;
r.push(rules.length === 6);
r.push(rules[0].type === 2);
r.push(rules[1].type === 3);
r.push(rules[2].type === 6);
r.push(rules[3].type === 9);
r.push(rules[4].type === 12);
r.push(rules[5].type === 1);
r.push(rules[0].encoding === 'windows-1252');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true,true", result);
    }

    // ────────────────────── Acid3-specific CSS patterns ──────────────────────

    [Fact]
    public void Acid3_Style_Block_FontFace_And_Rules()
    {
        // Simulates the Acid3 CSS structure with @font-face + regular rules
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@font-face { font-family: ""AcidAhemTest""; src: url(font.ttf); }
:root { background: silver; color: black; }
body { background: white; }
object { position: fixed; left: 130.5px; top: 84.3px; }
h1:first-child { text-shadow: rgba(192, 192, 192, 1.0) 3px 3px; }
#slash { color: red; color: hsla(0, 0%, 0%, 1.0); }
</style>
</head><body>
<h1>Test</h1>
<div id=""slash"">slash</div>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rules = sheet.cssRules;
r.push(rules.length >= 5);

// Check @font-face rule
var hasFontFace = false;
for (var i = 0; i < rules.length; i++) {
    if (rules[i].type === 5) { hasFontFace = true; break; }
}
r.push(hasFontFace);

// Check getComputedStyle for #slash returns hsla color
var cs = window.getComputedStyle(document.getElementById('slash'));
r.push(cs.color === 'hsla(0, 0%, 0%, 1.0)');

// Check :root gets silver background
var rootCs = window.getComputedStyle(document.documentElement);
r.push(rootCs.background === 'silver');

document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void Acid3_Position_Fixed_Object_Element()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
object { position: fixed; left: 130.5px; top: 84.3px; background: transparent; }
</style>
</head><body>
<object id=""target"" data=""empty.html""></object>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.position === 'fixed');
r.push(cs.left === '130.5px');
r.push(cs.top === '84.3px');
r.push(cs.background === 'transparent');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void GetComputedStyle_Multiple_CamelCase_Properties()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { z-index: 1; background-color: red; border-width: 2px; font-size: 14px; text-shadow: 1px 1px black; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.zIndex === '1');
r.push(cs.backgroundColor === 'red');
r.push(cs.borderWidth === '2px');
r.push(cs.fontSize === '14px');
r.push(cs.textShadow === '1px 1px black');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void FontFace_Rule_Does_Not_Match_As_Selector()
    {
        // @font-face should not be applied as a selector rule
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@font-face { font-family: ""TestFont""; src: url(test.ttf); }
div { color: blue; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.color === 'blue');
// font-family from @font-face should NOT be applied to elements
var ff = cs.fontFamily || '';
r.push(ff.indexOf('TestFont') === -1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Multiple_StyleSheets_Both_Have_Rules()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@font-face { font-family: ""Font1""; src: url(a.ttf); }
.a { color: red; }
</style>
<style>
.b { color: blue; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
r.push(document.styleSheets.length >= 2);
var sheet1 = document.styleSheets[0];
var sheet2 = document.styleSheets[1];
r.push(sheet1.cssRules.length >= 2);
r.push(sheet2.cssRules.length >= 1);

// Sheet1 should have @font-face (type=5) and style rule (type=1)
var types1 = [];
for (var i = 0; i < sheet1.cssRules.length; i++) types1.push(sheet1.cssRules[i].type);
r.push(types1.indexOf(5) >= 0);
r.push(types1.indexOf(1) >= 0);

document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void DeleteRule_On_StyleSheet()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.a { color: red; }
.b { color: blue; }
</style>
</head><body>
<div id=""result""></div>
<script>
var sheet = document.styleSheets[0];
var initLen = sheet.cssRules.length;
sheet.deleteRule(0);
document.getElementById('result').textContent = initLen + ':' + sheet.cssRules.length;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("2:1", result);
    }

    [Fact]
    public void InsertRule_On_StyleSheet_Updates_Live_CssRules_And_Clears_Deleted_Indices()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.first { color: red; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
var rules = sheet.cssRules;
r.push(typeof rules.item === 'function');
r.push(rules.item(0).selectorText === '.first');
r.push(sheet.insertRule('@media screen { .second { color: blue; } }', 1) === 1);
r.push(rules.length === 2);
r.push(rules[1].type === 4);
r.push(rules.item(1).cssRules[0].selectorText === '.second');
sheet.deleteRule(0);
r.push(rules.length === 1);
r.push(rules[0].type === 4);
r.push(rules[1] === undefined);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void StyleSheet_InsertRule_Does_Not_Reappear_After_Owner_TextContent_Is_Replaced()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.first { color: red; }
</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var styleEl = document.querySelector('style');
var sheet = document.styleSheets[0];
sheet.insertRule('.second { color: blue; }', 1);
r.push(sheet.cssRules.length === 2);
r.push(sheet.cssRules[1].selectorText === '.second');
styleEl.textContent = '.third { color: green; }';
r.push(sheet.cssRules.length === 1);
r.push(sheet.cssRules[0].selectorText === '.third');
r.push(sheet.cssRules[1] === undefined);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void StyleSheet_InsertRule_Is_Observed_By_GetComputedStyle()
    {
        // Phase 6 store unification: a script CSSOM insertRule()/deleteRule() must be
        // observed by getComputedStyle(), not just the cssRules view. The inserted
        // rule comes after the base rule (same specificity) so it wins the cascade.
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { z-index: 1; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var r = [];
var target = document.getElementById('target');
r.push(window.getComputedStyle(target).zIndex === '1');
document.styleSheets[0].insertRule('#target { z-index: 99; }', 1);
r.push(window.getComputedStyle(target).zIndex === '99');
document.styleSheets[0].deleteRule(1);
r.push(window.getComputedStyle(target).zIndex === '1');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Cursor_Property_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { cursor: help; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.cursor;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("help", result);
    }

    [Fact]
    public void FontWeight_Bolder_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { font-weight: bolder; font-size: 5em; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.fontWeight === '700');
r.push(cs.fontSize === '5em');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void BorderWidth_Shorthand_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { border-width: 0 0.2em 0.2em 0; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.borderWidth !== undefined);
r.push(cs.borderWidth !== '');
r.push(cs.getPropertyValue('border-width') !== '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Margin_Negative_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { margin-bottom: -0.4em; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.marginBottom === '-0.4em');
r.push(cs.getPropertyValue('margin-bottom') === '-0.4em');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }
}
