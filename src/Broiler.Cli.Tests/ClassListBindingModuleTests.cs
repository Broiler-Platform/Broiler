using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 sixth slice (P3.6): the
/// <c>Element.classList</c> / <c>DOMTokenList</c> API is now a co-located binding module
/// (<see cref="ClassListBinding"/>). It is pure logic over the canonical <c>DomTokenList</c> with an
/// injected style-invalidation callback, so it has no host contract. The behavior characterizations
/// exercise the extracted API end-to-end through the bridge with no layout dependency.
/// </summary>
public sealed class ClassListBindingModuleTests
{
    [Fact]
    public void ClassList_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(ClassListBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
    }

    [Fact]
    public void Add_Remove_Contains_Round_Trip_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""t"" class=""a b""></div>
<script>
var t = document.getElementById('t');
t.classList.add('c', 'd');
t.classList.remove('b');
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'has-a=' + t.classList.contains('a') + '|has-b=' + t.classList.contains('b') + '|class=' + t.className;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("has-a=true|has-b=false|class=a c d", result);
    }

    [Fact]
    public void Toggle_With_And_Without_Force_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""t"" class=""x""></div>
<script>
var t = document.getElementById('t');
var r1 = t.classList.toggle('x');        // present -> removed -> false
var r2 = t.classList.toggle('y');        // absent -> added -> true
var r3 = t.classList.toggle('z', true);  // force on -> true (added)
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'r=' + r1 + ',' + r2 + ',' + r3 + '|class=' + t.className;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("r=false,true,true|class=y z", result);
    }

    [Fact]
    public void Replace_Swaps_A_Token_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""t"" class=""old keep""></div>
<script>
var t = document.getElementById('t');
var did = t.classList.replace('old', 'new');
var noop = t.classList.replace('missing', 'nope');
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'did=' + did + '|noop=' + noop + '|class=' + t.className;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("did=true|noop=false|class=new keep", result);
    }
}
