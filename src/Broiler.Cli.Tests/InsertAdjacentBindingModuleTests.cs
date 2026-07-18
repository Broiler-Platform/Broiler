using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the DOM
/// <c>insertAdjacentElement</c> / <c>insertAdjacentText</c> / <c>insertAdjacentHTML</c> methods
/// (<see cref="InsertAdjacentBinding"/>) — each resolving the <c>beforebegin</c>/<c>afterbegin</c>/
/// <c>beforeend</c>/<c>afterend</c> position to a (parent, index) target and inserting an element, a text
/// node, or the parsed fragment there. The position-normalisation / target-resolution helpers moved into the
/// module with the methods; the JS context, reverse lookup, insertion primitive, text-node factory, fragment
/// parser and computed-style reset come through <see cref="IInsertAdjacentHost"/>. Was the bridge's
/// <c>JsJsObjectsInsertAdjacentElement130Core</c>..<c>InsertAdjacentHTML132Core</c>.
/// </summary>
public sealed class InsertAdjacentBindingModuleTests
{
    [Fact]
    public void InsertAdjacent_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(InsertAdjacentBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.True(moduleType.IsAbstract && moduleType.IsSealed); // C# static class

        Assert.False(typeof(IInsertAdjacentHost).IsPublic);
        Assert.True(typeof(IInsertAdjacentHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void InsertAdjacent_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsInsertAdjacentElement130Core", "JsJsObjectsInsertAdjacentText131Core",
                     "JsJsObjectsInsertAdjacentHTML132Core",
                     // helpers moved with them
                     "NormalizeInsertAdjacentPosition", "GetInsertAdjacentTarget",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void InsertAdjacentElement_Places_Node_At_Each_Position()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""host""><span id=""anchor"">x</span></div>
<div id=""result""></div>
<script>
var host = document.getElementById('host');
var anchor = document.getElementById('anchor');
function mk(id){ var e = document.createElement('i'); e.id = id; return e; }
anchor.insertAdjacentElement('beforebegin', mk('bb'));
anchor.insertAdjacentElement('afterbegin', mk('ab'));   // into anchor
anchor.insertAdjacentElement('beforeend', mk('be'));    // into anchor
anchor.insertAdjacentElement('afterend', mk('ae'));
// host children order: bb, anchor, ae ; anchor children: ab, 'x', be
var hostIds = Array.prototype.map.call(host.children, function(c){ return c.id; }).join(',');
var anchorIds = Array.prototype.map.call(anchor.children, function(c){ return c.id; }).join(',');
document.getElementById('result').textContent = 'host=' + hostIds + '|anchor=' + anchorIds;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">host=bb,anchor,ae|anchor=ab,be<", result);
    }

    [Fact]
    public void InsertAdjacentText_And_Html_Insert_Content()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""box""></div>
<div id=""result""></div>
<script>
var box = document.getElementById('box');
box.insertAdjacentText('beforeend', 'hello');
box.insertAdjacentHTML('beforeend', '<b id=""bold"">hi</b>');
var r = [];
r.push(box.textContent.indexOf('hello') !== -1);
r.push(box.querySelector('#bold') !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true<", result);
    }

    [Fact]
    public void InsertAdjacent_Invalid_Position_Throws_SyntaxError()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""box""></div>
<div id=""result""></div>
<script>
var box = document.getElementById('box');
var threw = 'no';
try { box.insertAdjacentText('nope', 'x'); } catch (e) { threw = 'yes'; }
document.getElementById('result').textContent = threw;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">yes<", result);
    }
}
