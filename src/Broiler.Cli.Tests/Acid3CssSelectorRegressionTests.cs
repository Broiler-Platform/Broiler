using Broiler.Cli;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase G: Acid3 Bucket 3 — CSS Selectors (Tests 33–48) explicit regression tests.
/// Promotes indirect (🔶) coverage to explicit (✅) for CSS selector patterns.
/// </summary>
public class Acid3CssSelectorRegressionTests
{
    [Fact]
    public void Acid3_Test33_Class_Selector_Matches_Element()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>.highlight { z-index: 33; } .a.b { z-index: 330; }</style>
<div id=""single"" class=""highlight"">one</div>
<div id=""multi"" class=""a b"">two</div>
<div id=""none"">three</div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('single'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('multi'), '').zIndex);
r.push(document.querySelectorAll('.highlight').length);
r.push(document.querySelectorAll('.a.b').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("33|330|1|1", result);
    }

    [Fact]
    public void Acid3_Test34_Attribute_Selectors_Match()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>
[data-x] { z-index: 1; }
[data-x=""alpha""] { z-index: 2; }
[data-words~=""beta""] { z-index: 3; }
[data-lang|=""en""] { z-index: 4; }
</style>
<div id=""a"" data-x=""alpha"">a</div>
<div id=""b"" data-x=""alpha"">b</div>
<div id=""c"" data-words=""alpha beta gamma"">c</div>
<div id=""d"" data-lang=""en-US"">d</div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('a'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('c'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('d'), '').zIndex);
r.push(document.querySelectorAll('[data-x]').length);
r.push(document.querySelectorAll('[data-x=""alpha""]').length);
r.push(document.querySelectorAll('[data-words~=""beta""]').length);
r.push(document.querySelectorAll('[data-lang|=""en""]').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("2|3|4|2|2|1|1", result);
    }

    [Fact]
    public void Acid3_Test35_FirstChild_Pseudo_Class()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>#container > p:first-child { z-index: 35; }</style>
<div id=""container""><p id=""first"">first</p><p id=""second"">second</p></div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('first'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('second'), '').zIndex);
r.push(document.querySelectorAll('#container > p:first-child').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("35|", result);
        Assert.Contains("|1", result);
    }

    [Fact]
    public void Acid3_Test36_LastChild_Pseudo_Class()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>#box > span:last-child { z-index: 36; }</style>
<div id=""box""><span id=""s1"">a</span><span id=""s2"">b</span></div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('s1'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('s2'), '').zIndex);
r.push(document.querySelectorAll('#box > span:last-child').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("36", result);
        Assert.Contains("|1", result);
    }

    [Fact]
    public void Acid3_Test37_NthChild_Pseudo_Class()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>#list > li:nth-child(2) { z-index: 37; }</style>
<ul id=""list""><li id=""l1"">a</li><li id=""l2"">b</li><li id=""l3"">c</li></ul>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('l1'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('l2'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('l3'), '').zIndex);
r.push(document.querySelectorAll('#list > li:nth-child(2)').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("37", result);
        Assert.Contains("|1", result);
    }

    [Fact]
    public void Acid3_Test38_NthChild_Odd_Even()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>
#items > div:nth-child(odd) { z-index: 1; }
#items > div:nth-child(even) { z-index: 2; }
</style>
<div id=""items""><div id=""d1"">a</div><div id=""d2"">b</div><div id=""d3"">c</div><div id=""d4"">d</div></div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('d1'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('d2'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('d3'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('d4'), '').zIndex);
r.push(document.querySelectorAll('#items > div:nth-child(odd)').length);
r.push(document.querySelectorAll('#items > div:nth-child(even)').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("1|2|1|2|2|2", result);
    }

    [Fact]
    public void Acid3_Test39_OnlyChild_Pseudo_Class()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>p:only-child { z-index: 39; }</style>
<div id=""solo""><p id=""lonely"">only</p></div>
<div id=""pair""><p id=""sibling1"">a</p><p id=""sibling2"">b</p></div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('lonely'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('sibling1'), '').zIndex);
r.push(document.querySelectorAll('p:only-child').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("39|", result);
        Assert.Contains("|1", result);
    }

    [Fact]
    public void Acid3_Test40_Empty_Pseudo_Class()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>div.box:empty { z-index: 40; }</style>
<div id=""empty"" class=""box""></div>
<div id=""notempty"" class=""box"">content</div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('empty'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('notempty'), '').zIndex);
r.push(document.querySelectorAll('div.box:empty').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("40|", result);
        Assert.Contains("|1", result);
    }

    [Fact]
    public void Acid3_Test41_Not_Pseudo_Class()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>#group > p:not(.skip) { z-index: 41; }</style>
<div id=""group""><p id=""inc"" class=""keep"">a</p><p id=""exc"" class=""skip"">b</p><p id=""inc2"">c</p></div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('inc'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('exc'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('inc2'), '').zIndex);
r.push(document.querySelectorAll('#group > p:not(.skip)').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("41|", result);
        Assert.Contains("|2", result);
    }

    [Fact]
    public void Acid3_Test42_Child_Combinator()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>#outer > span { z-index: 42; }</style>
<div id=""outer""><span id=""direct"">a</span><div><span id=""nested"">b</span></div></div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('direct'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('nested'), '').zIndex);
r.push(document.querySelectorAll('#outer > span').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("42|", result);
        Assert.Contains("|1", result);
    }

    [Fact]
    public void Acid3_Test43_Adjacent_Sibling_Combinator()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>h2 + p { z-index: 43; }</style>
<div><h2>title</h2><p id=""adj"">adjacent</p><p id=""far"">further</p></div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('adj'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('far'), '').zIndex);
r.push(document.querySelectorAll('h2 + p').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("43|", result);
        Assert.Contains("|1", result);
    }

    [Fact]
    public void Acid3_Test44_General_Sibling_Combinator()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>h3 ~ p { z-index: 44; }</style>
<div><h3>heading</h3><span>not-p</span><p id=""sib1"">a</p><p id=""sib2"">b</p></div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('sib1'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('sib2'), '').zIndex);
r.push(document.querySelectorAll('h3 ~ p').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("44|44|2", result);
    }

    [Fact]
    public void Acid3_Test45_FirstOfType_Pseudo_Class()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>#mix > span:first-of-type { z-index: 45; }</style>
<div id=""mix""><p>para</p><span id=""fs"">first-span</span><span id=""ss"">second-span</span></div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('fs'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('ss'), '').zIndex);
r.push(document.querySelectorAll('#mix > span:first-of-type').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("45|", result);
        Assert.Contains("|1", result);
    }

    [Fact]
    public void Acid3_Test46_LastOfType_Pseudo_Class()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>#mix2 > span:last-of-type { z-index: 46; }</style>
<div id=""mix2""><span id=""s2a"">a</span><span id=""s2b"">b</span><p>para</p></div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('s2a'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('s2b'), '').zIndex);
r.push(document.querySelectorAll('#mix2 > span:last-of-type').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("46", result);
        Assert.Contains("|1", result);
    }

    [Fact]
    public void Acid3_Test47_NthOfType_Pseudo_Class()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>#typed > p:nth-of-type(2) { z-index: 47; }</style>
<div id=""typed""><span>x</span><p id=""p1"">a</p><p id=""p2"">b</p><p id=""p3"">c</p></div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('p1'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('p2'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('p3'), '').zIndex);
r.push(document.querySelectorAll('#typed > p:nth-of-type(2)').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("47", result);
        Assert.Contains("|1", result);
    }

    [Fact]
    public void Acid3_Test48_Universal_And_Descendant_Combinator()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>#wrap * { z-index: 48; }</style>
<div id=""wrap""><p id=""wp"">text</p><div><span id=""ws"">nested</span></div></div>
<div id=""outside"">out</div>
<div id=""result""></div>
<script>
var r = [];
r.push(window.getComputedStyle(document.getElementById('wp'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('ws'), '').zIndex);
r.push(window.getComputedStyle(document.getElementById('outside'), '').zIndex);
r.push(document.querySelectorAll('#wrap *').length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("48|48|", result);
    }
}
