using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the DOM
/// <c>ChildNode</c> mixin (<see cref="ChildNodeBinding"/>) — <c>remove()</c>, <c>before()</c>,
/// <c>after()</c> and <c>replaceWith()</c>, registered on every node wrapper. The callbacks —
/// previously the bridge's <c>JsJsObjectsRemove093Core</c>..<c>ReplaceWith096Core</c> — are now
/// co-located; the argument-node builder, the side-effecting insertion primitive, style-scope
/// invalidation and the node-iterator / mutation notifications are reached through the
/// <see cref="IChildNodeHost"/> contract. The characterization drives the mixin end-to-end through
/// the bridge.
/// </summary>
public sealed class ChildNodeBindingModuleTests
{
    [Fact]
    public void ChildNode_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(ChildNodeBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IChildNodeHost).IsPublic);
        Assert.True(typeof(IChildNodeHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void ChildNode_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsRemove093Core", "JsJsObjectsBefore094Core",
                     "JsJsObjectsAfter095Core", "JsJsObjectsReplaceWith096Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void ChildNode_Mixin_Flows_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var box = document.createElement('div');
box.id = 'box';
document.body.appendChild(box);

function mk(t) { var e = document.createElement('span'); e.textContent = t; return e; }
var a = mk('A'), b = mk('B'), c = mk('C');
box.appendChild(a); box.appendChild(b); box.appendChild(c);   // A B C

b.before(mk('X'));       // A X B C
b.after(mk('Y'));        // A X B Y C
a.remove();              // X B Y C
a.remove();              // detached node -> no-op
c.replaceWith(mk('Z'));  // X B Y Z

var seq = '';
for (var i = 0; i < box.childNodes.length; i++) seq += box.childNodes[i].textContent;

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'seq=' + seq + '|n=' + box.childNodes.length;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">seq=XBYZ|n=4<", result);
    }
}
