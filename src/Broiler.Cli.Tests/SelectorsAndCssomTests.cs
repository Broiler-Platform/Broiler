namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 4 Acid3 compliance: CSS Selectors API and CSSOM —
/// additional pseudo-classes (:only-child, :empty, :root, :lang, :enabled,
/// :disabled, :checked, :last-of-type, :only-of-type, :nth-of-type,
/// :nth-last-of-type, :nth-last-child), attribute selectors ([attr|=val],
/// [attr~=val], etc.), getComputedStyle(), cssFloat, matchMedia(),
/// document.defaultView, document.createElementNS(), element.title,
/// and node.data.
/// </summary>
public class SelectorsAndCssomTests
{
    // ────────────────────── Pseudo-class: :only-child ──────────────────────

    [Fact]
    public void OnlyChild_Matches_Single_Element_Child()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><span id=""child"">only</span></div>
<div id=""result""></div>
<script>
var r = [];
r.push(document.querySelector('#child:only-child') !== null);
r.push(document.querySelector('#parent:only-child') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void OnlyChild_Fails_With_Multiple_Children()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div><span id=""a"">a</span><span id=""b"">b</span></div>
<div id=""result""></div>
<script>
var r = [];
r.push(document.querySelector('#a:only-child') === null);
r.push(document.querySelector('#b:only-child') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ──────────────────────── Pseudo-class: :empty ────────────────────────

    [Fact]
    public void Empty_Matches_Element_Without_Children()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""empty""></div>
<div id=""notempty"">text</div>
<div id=""result""></div>
<script>
var r = [];
r.push(document.querySelector('#empty:empty') !== null);
r.push(document.querySelector('#notempty:empty') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Empty_Dynamic_AddRemove_Children()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var p = document.createElement('p');
document.body.appendChild(p);
var r = [];
r.push(document.querySelector('p:empty') !== null);
var span = document.createElement('span');
p.appendChild(span);
r.push(document.querySelector('p:empty') === null);
p.removeChild(span);
r.push(document.querySelector('p:empty') !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ─────────────────────── Pseudo-class: :root ──────────────────────────

    [Fact]
    public void Root_Matches_DocumentElement_Only()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
r.push(document.querySelector(':root') !== null);
r.push(document.querySelector(':root').tagName);
r.push(document.querySelector('body:root') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,HTML,true", result);
    }

    [Fact]
    public void Not_Root_Matches_Non_Root_Elements()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<p id=""p1"">test</p>
<div id=""result""></div>
<script>
var r = [];
var matches = document.querySelectorAll(':not(:root)');
r.push(matches.length > 0);
var htmlMatched = false;
for (var i = 0; i < matches.length; i++) {
    if (matches[i].tagName === 'HTML') htmlMatched = true;
}
r.push(!htmlMatched);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── Pseudo-class: :lang() ─────────────────────────

    [Fact]
    public void Lang_Matches_Element_With_Lang_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""en"" lang=""en-GB"">English</div>
<div id=""fr"" lang=""french"">French</div>
<div id=""result""></div>
<script>
var r = [];
r.push(document.querySelector('#en:lang(en)') !== null);
r.push(document.querySelector('#fr:lang(en)') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Lang_Inherits_From_Parent()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div lang=""en-GB""><p id=""child"">test</p></div>
<div id=""result""></div>
<script>
var r = [];
r.push(document.querySelector('#child:lang(en)') !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    // ─────────── Pseudo-classes: :enabled, :disabled, :checked ────────────

    [Fact]
    public void Enabled_Disabled_Checkbox()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var input = document.createElement('input');
input.type = 'checkbox';
document.body.appendChild(input);
var r = [];
r.push(document.querySelector('input:enabled') !== null);
r.push(document.querySelector('input:disabled') === null);
input.disabled = true;
r.push(document.querySelector('input:enabled') === null);
r.push(document.querySelector('input:disabled') !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void Checked_Matches_Checked_Input()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var input = document.createElement('input');
input.type = 'checkbox';
document.body.appendChild(input);
var r = [];
r.push(document.querySelector('input:checked') === null);
input.checked = true;
r.push(document.querySelector('input:checked') !== null);
input.checked = false;
r.push(document.querySelector('input:checked') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ──────────── Pseudo-classes: :last-of-type, :only-of-type ────────────

    [Fact]
    public void LastOfType_Matches_Last_Sibling_Of_Same_Tag()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""container"">
<p id=""p1"">first</p>
<div id=""d1"">mid</div>
<p id=""p2"">last p</p>
</div>
<div id=""result""></div>
<script>
var r = [];
r.push(document.querySelector('#p1:last-of-type') === null);
r.push(document.querySelector('#p2:last-of-type') !== null);
r.push(document.querySelector('#d1:last-of-type') !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void OnlyOfType_Matches_Single_Tag_Type()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""container"">
<p id=""p1"">only p</p>
<div id=""d1"">div 1</div>
<div id=""d2"">div 2</div>
</div>
<div id=""result""></div>
<script>
var r = [];
r.push(document.querySelector('#p1:only-of-type') !== null);
r.push(document.querySelector('#d1:only-of-type') === null);
r.push(document.querySelector('#d2:only-of-type') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ─────── :nth-of-type, :nth-last-of-type, :nth-last-child ──────────

    [Fact]
    public void NthOfType_Matches_Correct_Position()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var container = document.createElement('div');
document.body.appendChild(container);
var p1 = document.createElement('p');
var d1 = document.createElement('div');
var d2 = document.createElement('div');
var p2 = document.createElement('p');
container.appendChild(p1); p1.id = 'p1';
container.appendChild(d1); d1.id = 'd1';
container.appendChild(d2); d2.id = 'd2';
container.appendChild(p2); p2.id = 'p2';
var r = [];
r.push(document.querySelector('#p1:nth-of-type(1)') !== null);
r.push(document.querySelector('#p2:nth-of-type(2)') !== null);
r.push(document.querySelector('#d1:nth-of-type(1)') !== null);
r.push(document.querySelector('#d2:nth-of-type(2)') !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void NthLastOfType_Matches_From_End()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var container = document.createElement('div');
document.body.appendChild(container);
var items = [];
for (var i = 0; i < 5; i++) {
    var p = document.createElement('p');
    p.id = 'p' + i;
    container.appendChild(p);
    items.push(p);
}
var r = [];
r.push(document.querySelector('#p4:nth-last-of-type(1)') !== null);
r.push(document.querySelector('#p3:nth-last-of-type(2)') !== null);
r.push(document.querySelector('#p0:nth-last-of-type(5)') !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void NthLastChild_Matches_From_End()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var container = document.createElement('div');
document.body.appendChild(container);
for (var i = 0; i < 4; i++) {
    var p = document.createElement('p');
    p.id = 'p' + i;
    container.appendChild(p);
}
var r = [];
r.push(document.querySelector('#p3:nth-last-child(1)') !== null);
r.push(document.querySelector('#p2:nth-last-child(2)') !== null);
r.push(document.querySelector('#p0:nth-last-child(4)') !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void NthChild_Odd_Even()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var container = document.createElement('div');
document.body.appendChild(container);
for (var i = 0; i < 6; i++) {
    var p = document.createElement('p');
    p.id = 'p' + i;
    container.appendChild(p);
}
var r = [];
r.push(document.querySelector('#p0:nth-child(odd)') !== null);
r.push(document.querySelector('#p1:nth-child(odd)') === null);
r.push(document.querySelector('#p1:nth-child(even)') !== null);
r.push(document.querySelector('#p0:nth-child(even)') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ──────── Attribute selectors: |=, ~=, ^=, $=, *= ────────────────────

    [Fact]
    public void AttributeSelector_DashMatch()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d1"" class=""widget-tree"">one</div>
<div id=""d2"" class=""WIDGET"">two</div>
<div id=""result""></div>
<script>
var r = [];
r.push(document.querySelector('[class|=widget]') !== null);
r.push(document.querySelector('[class|=widget]').id);
r.push(document.querySelector('[class|=WIDGET]') !== null);
r.push(document.querySelector('[class|=WIDGET]').id);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,d1,true,d2", result);
    }

    [Fact]
    public void AttributeSelector_WordMatch()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d1"" class=""foo bar baz"">one</div>
<div id=""result""></div>
<script>
var r = [];
r.push(document.querySelector('[class~=bar]') !== null);
r.push(document.querySelector('[class~=bar]').id);
r.push(document.querySelector('[class~=nope]') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,d1,true", result);
    }

    [Fact]
    public void AttributeSelector_StartsWith_EndsWith_Contains()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d1"" data-val=""hello world"">one</div>
<div id=""result""></div>
<script>
var r = [];
r.push(document.querySelector('[data-val^=hello]') !== null);
r.push(document.querySelector('[data-val$=world]') !== null);
r.push(document.querySelector('[data-val*=""o w""]') !== null);
r.push(document.querySelector('[data-val^=world]') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ─────────────────── document.defaultView ─────────────────────────────

    [Fact]
    public void DefaultView_Returns_Window()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
r.push(document.defaultView !== null);
r.push(document.defaultView !== undefined);
r.push(typeof document.defaultView.getComputedStyle);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,function", result);
    }

    // ─────────────────── getComputedStyle ──────────────────────────────────

    [Fact]
    public void GetComputedStyle_Returns_Inline_Style()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"" style=""color: red; font-size: 16px"">test</div>
<div id=""result""></div>
<script>
var el = document.getElementById('target');
var cs = window.getComputedStyle(el, '');
var r = [];
r.push(cs.color);
r.push(cs.fontSize);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("red,16px", result);
    }

    [Fact]
    public void GetComputedStyle_Matches_CSS_Rules()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { z-index: 0; }
.highlight { z-index: 5; }
</style>
</head><body>
<p id=""p1"" class=""highlight"">test</p>
<p id=""p2"">plain</p>
<div id=""result""></div>
<script>
var r = [];
var cs1 = window.getComputedStyle(document.getElementById('p1'), '');
var cs2 = window.getComputedStyle(document.getElementById('p2'), '');
r.push(cs1.zIndex);
r.push(cs2.zIndex);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("5,0", result);
    }

    [Fact]
    public void GetComputedStyle_Via_DefaultView()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { z-index: 42; }
</style>
</head><body>
<div id=""target"">test</div>
<div id=""result""></div>
<script>
var cs = document.defaultView.getComputedStyle(document.getElementById('target'), '');
document.getElementById('result').textContent = cs.zIndex;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("42", result);
    }

    [Fact]
    public void GetComputedStyle_PseudoClass_Selector_In_StyleSheet()
    {
        // Use dynamic element creation to ensure :first-child matches the first element.
        // The tree builder may move <style> from <head> into <body>, changing child order.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var container = document.createElement('div');
document.body.appendChild(container);
var style = document.createElement('style');
style.textContent = '* { z-index: 0; } :first-child { z-index: 10; }';
container.appendChild(style);
var p1 = document.createElement('p');
p1.id = 'first';
container.appendChild(p1);
var p2 = document.createElement('p');
p2.id = 'second';
container.appendChild(p2);
var r = [];
var cs1 = window.getComputedStyle(p1, '');
var cs2 = window.getComputedStyle(p2, '');
r.push(cs1.zIndex);
r.push(cs2.zIndex);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // The style element is first-child, so p1 is NOT first-child.
        // Both p1 and p2 should match * { z-index: 0; } only.
        Assert.Contains("0,0", result);
    }

    // ───────────────────────── cssFloat ────────────────────────────────────

    [Fact]
    public void CssFloat_Reads_Float_Property()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
r.push(!document.body.style.cssFloat);
document.body.setAttribute('style', 'float: right');
r.push(document.body.style.cssFloat);
document.body.setAttribute('style', 'float: none');
r.push(document.body.style.cssFloat);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,right,none", result);
    }

    // ────────────────────── matchMedia ─────────────────────────────────────

    [Fact]
    public void MatchMedia_Basic_All_Query()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
r.push(window.matchMedia('all').matches);
r.push(window.matchMedia('screen').matches);
r.push(window.matchMedia('(bogus)').matches);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,false", result);
    }

    // ──────────────────── document.createElementNS ────────────────────────

    [Fact]
    public void CreateElementNS_Creates_Element()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var el = document.createElementNS('http://example.com/', 'test');
var r = [];
r.push(el !== null);
r.push(el.tagName);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,test", result);
    }

    // ────────────────────── element.title ──────────────────────────────────

    [Fact]
    public void Title_Property_ReadWrite()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<p id=""p1"">test</p>
<div id=""result""></div>
<script>
var p = document.getElementById('p1');
var r = [];
r.push(p.title);
p.title = 'hello';
r.push(p.title);
r.push(p.getAttribute('title'));
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(",hello,hello", result);
    }

    // ───────────────────────── node.data ───────────────────────────────────

    [Fact]
    public void Data_Property_On_TextNodes()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var t = document.createTextNode('hello');
var r = [];
r.push(t.data);
t.data = 'world';
r.push(t.data);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("hello,world", result);
    }

    [Fact]
    public void Data_Property_On_CommentNodes()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var c = document.createComment('test comment');
var r = [];
r.push(c.data);
c.data = 'updated';
r.push(c.data);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("test comment,updated", result);
    }

    // ────────── setAttribute('style', ...) syncs to style object ──────────

    [Fact]
    public void SetAttribute_Style_Syncs_To_Style_Object()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"">test</div>
<div id=""result""></div>
<script>
var el = document.getElementById('target');
el.setAttribute('style', 'color: blue; font-size: 14px');
var r = [];
r.push(el.style.getPropertyValue('color'));
r.push(el.style.getPropertyValue('font-size'));
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("blue,14px", result);
    }

    // ─────────── getComputedStyle with @media rules ───────────────────────

    [Fact]
    public void GetComputedStyle_Media_All()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@media all and (min-color: 0) { #a { z-index: 1; } }
@media not all and (min-color: 0) { #b { z-index: 2; } }
@media (bogus) { #c { z-index: 3; } }
</style>
</head><body>
<p id=""a"">a</p>
<p id=""b"">b</p>
<p id=""c"">c</p>
<div id=""result""></div>
<script>
var r = [];
// CSS initial value for z-index is 'auto'; use explicit check for numeric values
var za = window.getComputedStyle(document.getElementById('a'), '').zIndex;
var zb = window.getComputedStyle(document.getElementById('b'), '').zIndex;
var zc = window.getComputedStyle(document.getElementById('c'), '').zIndex;
r.push(za === '1' || za === 1 ? '1' : '0');
r.push(zb === '2' || zb === 2 ? '2' : '0');
r.push(zc === '3' || zc === 3 ? '3' : '0');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("1,0,0", result);
    }

    // ────── Combined selectors with pseudo-classes in getComputedStyle ─────

    [Fact]
    public void GetComputedStyle_SelectorTest_Pattern()
    {
        // Mimics the Acid3 selectorTest pattern:
        // Add CSS rules, then check getComputedStyle().zIndex to verify matching
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { z-index: 0; position: absolute; }
.highlight { z-index: 1; }
:only-child { z-index: 2; }
</style>
</head><body>
<div id=""solo""><span id=""inner"" class=""highlight"">test</span></div>
<div id=""result""></div>
<script>
var inner = document.getElementById('inner');
var cs = document.defaultView.getComputedStyle(inner, '');
var r = [];
// :only-child has z-index 2 which overrides .highlight z-index 1
// because it comes later in the stylesheet (same specificity cascade)
r.push(cs.zIndex);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("2", result);
    }

    // ───────── Acid3 test 33 pattern: class & attribute selectors ──────────

    [Fact]
    public void Acid3_Test33_ClassSelector_CaseSensitive()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { z-index: 0; position: absolute; }
.selectorPingTest { z-index: 1; }
.SelectorPingTest { z-index: 2; }
.selectorpingtest { z-index: 3; }
</style>
</head><body>
<p id=""p1"" class=""selectorPingTest"">test</p>
<div id=""result""></div>
<script>
var cs = document.defaultView.getComputedStyle(document.getElementById('p1'), '');
document.getElementById('result').textContent = cs.zIndex;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // .selectorPingTest matches (z-index: 1), but not .SelectorPingTest or .selectorpingtest
        Assert.Contains("1", result);
    }

    // ─────────── Acid3 test 34 pattern: :lang and [|=] ─────────────────────

    [Fact]
    public void Acid3_Test34_Lang_And_DashMatch()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
* { z-index: 0; position: absolute; }
:lang(en) { z-index: 1; }
[class|=widget] { z-index: 2; }
</style>
</head><body>
<div id=""d1"" lang=""english"" class=""widget-tree"">one</div>
<div id=""d2"" lang=""en-GB"" class=""WIDGET"">two</div>
<p id=""child"">child</p>
<div id=""result""></div>
<script>
var r = [];
// lang=english should NOT be matched by :lang(en)
r.push(document.defaultView.getComputedStyle(document.getElementById('d1'), '').zIndex);
// lang=en-GB SHOULD be matched by :lang(en), giving z-index 1
// also class=widget-tree matched by [class|=widget] giving z-index 2
// CSS cascade: [class|=widget] is last so overrides :lang(en)
r.push(document.defaultView.getComputedStyle(document.getElementById('d2'), '').zIndex);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // d1: lang=english does NOT match :lang(en), but class=widget-tree matches [class|=widget] → z-index: 2
        // d2: lang=en-GB matches :lang(en) → z-index 1, class=WIDGET does NOT match [class|=widget] (case-sensitive)
        Assert.Contains("2,1", result);
    }
}
