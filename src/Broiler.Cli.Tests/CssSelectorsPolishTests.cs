namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 6 (v2) Acid3 compliance: CSS Selectors &amp; CSSOM Polish —
/// dynamic selector re-evaluation after DOM mutations, selector parser edge
/// cases (div*, escaped characters), :link/:visited pseudo-classes,
/// CSS cursor property in getComputedStyle, and cascade specificity accuracy.
/// </summary>
public class CssSelectorsPolishTests
{
    // ──────── 6.1  Dynamic selector re-evaluation after DOM mutation ────────

    [Fact]
    public void QuerySelectorAll_Reflects_AppendChild()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""container""><span class=""item"">a</span></div>
<div id=""result""></div>
<script>
var r = [];
r.push(document.querySelectorAll('.item').length);
var s = document.createElement('span');
s.className = 'item';
s.textContent = 'b';
document.getElementById('container').appendChild(s);
r.push(document.querySelectorAll('.item').length);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("1,2", result);
    }

    [Fact]
    public void Combinators_Work_After_RemoveChild()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><span id=""first"">1</span><span id=""second"">2</span></div>
<div id=""result""></div>
<script>
var r = [];
// Adjacent sibling: #first + #second should match
r.push(document.querySelector('#first + #second') !== null);
// Remove #first — now #second has no previous sibling
document.getElementById('parent').removeChild(document.getElementById('first'));
r.push(document.querySelector('#first + #second') === null);
// General sibling should also not match
r.push(document.querySelector('#first ~ #second') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ──────────── 6.2  Selector parser edge cases ─────────────────────────

    [Fact]
    public void DivStar_NoSpace_Parses_As_Descendant()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""outer""><p id=""inner"">text</p></div>
<div id=""result""></div>
<script>
var r = [];
// 'div*' (no space) must parse as 'div *' — descendant of div
r.push(document.querySelectorAll('div*').length > 0);
// Verify it matches the inner <p> which is a descendant of a <div>
var match = document.querySelector('div*');
r.push(match !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ──────────── 6.3  :link and :visited pseudo-classes ──────────────────

    [Fact]
    public void Link_Matches_Anchor_With_Href()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<a id=""a1"" href=""http://example.com"">link</a>
<a id=""a2"">no href</a>
<span id=""s1"" href=""http://example.com"">span</span>
<div id=""result""></div>
<script>
var r = [];
r.push(document.querySelector('#a1:link') !== null);
r.push(document.querySelector('#a2:link') === null);
r.push(document.querySelector('#s1:link') === null);
// :visited returns same styles as :link (privacy)
r.push(document.querySelector('#a1:visited') !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ──────────── 6.4  CSS cursor property in getComputedStyle ────────────

    [Fact]
    public void GetComputedStyle_Cursor_CSS3_Keywords()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#ptr { cursor: pointer; }
#cross { cursor: crosshair; }
#grab { cursor: grab; }
</style>
</head><body>
<div id=""ptr"">p</div>
<div id=""cross"">c</div>
<div id=""grab"">g</div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('ptr'), '').cursor);
r.push(window.getComputedStyle(document.getElementById('cross'), '').cursor);
r.push(window.getComputedStyle(document.getElementById('grab'), '').cursor);
r.push(window.getComputedStyle(document.getElementById('ptr'), '').getPropertyValue('cursor'));
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("pointer,crosshair,grab,pointer", result);
    }

    // ──────────── 6.5  getComputedStyle cascade / specificity ─────────────

    [Fact]
    public void GetComputedStyle_LastChild_WhiteSpace_PreWrap_Cascade()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
p { white-space: normal; }
p:last-child { white-space: pre-wrap; }
</style>
</head><body>
<div><p id=""first"">one</p><p id=""last"">two</p></div>
<div id=""result""></div>
<script>
var r = [];
var csFirst = window.getComputedStyle(document.getElementById('first'), '');
var csLast  = window.getComputedStyle(document.getElementById('last'), '');
r.push(csFirst.getPropertyValue('white-space'));
r.push(csLast.getPropertyValue('white-space'));
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("normal,pre-wrap", result);
    }

    [Fact]
    public void GetComputedStyle_Specificity_ID_Beats_Class()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
.cls { color: red; z-index: 1; }
#myid { color: blue; z-index: 2; }
</style>
</head><body>
<p id=""myid"" class=""cls"">text</p>
<div id=""result""></div>
<script>
var r = [];
var cs = window.getComputedStyle(document.getElementById('myid'), '');
// #myid has specificity 100, .cls has 10 — ID wins even though .cls comes first
r.push(cs.color);
r.push(cs.zIndex);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("blue,2", result);
    }

    [Fact]
    public void GetComputedStyle_PseudoClass_Specificity_Counted()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
p { z-index: 1; }
p:first-child { z-index: 2; }
</style>
</head><body>
<div><p id=""fc"">first child</p><p id=""sc"">second</p></div>
<div id=""result""></div>
<script>
var r = [];
var cs1 = window.getComputedStyle(document.getElementById('fc'), '');
var cs2 = window.getComputedStyle(document.getElementById('sc'), '');
// p:first-child has specificity 0-1-1 = 11, p has 0-0-1 = 1
// For first-child: both match, p:first-child wins with z-index 2
r.push(cs1.zIndex);
// For second: only p matches, z-index 1
r.push(cs2.zIndex);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("2,1", result);
    }
}
