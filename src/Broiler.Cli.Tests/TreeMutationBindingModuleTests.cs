using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the DOM
/// <c>Node</c> child-mutation methods (<see cref="TreeMutationBinding"/>) — <c>insertBefore</c>,
/// <c>appendChild</c>, <c>append</c>, <c>prepend</c>, <c>removeChild</c> and <c>replaceChild</c>,
/// registered on every element wrapper. The callbacks — previously the bridge's
/// <c>JsJsObjectsInsertBefore080Core</c>, <c>AppendChild088Core</c>, <c>Append089Core</c>,
/// <c>Prepend090Core</c>, <c>RemoveChild091Core</c> and <c>ReplaceChild092Core</c> — are now
/// co-located; the JS-object→node resolver, the argument-node builder, the side-effecting insertion
/// primitive, style-scope invalidation and the node-iterator / mutation notifications are reached
/// through the <see cref="ITreeMutationHost"/> contract. The characterizations drive each method
/// end-to-end through the bridge.
/// </summary>
public sealed class TreeMutationBindingModuleTests
{
    [Fact]
    public void TreeMutation_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(TreeMutationBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.True(moduleType.IsAbstract && moduleType.IsSealed); // C# static class

        Assert.False(typeof(ITreeMutationHost).IsPublic);
        Assert.True(typeof(ITreeMutationHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void TreeMutation_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsInsertBefore080Core", "JsJsObjectsAppendChild088Core",
                     "JsJsObjectsAppend089Core", "JsJsObjectsPrepend090Core",
                     "JsJsObjectsRemoveChild091Core", "JsJsObjectsReplaceChild092Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void AppendChild_Append_Prepend_And_InsertBefore_Position_Children()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var box = document.createElement('div');
box.id = 'box';
document.body.appendChild(box);

function mk(t) { var e = document.createElement('span'); e.textContent = t; return e; }
var a = mk('A'), b = mk('B'), c = mk('C');
box.appendChild(a);             // A
box.append(c);                  // A C
box.insertBefore(b, c);         // A B C
box.prepend(mk('P'));           // P A B C

var seq = '';
for (var i = 0; i < box.childNodes.length; i++) seq += box.childNodes[i].textContent;

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'seq=' + seq + '|n=' + box.childNodes.length;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">seq=PABC|n=4<", result);
    }

    [Fact]
    public void RemoveChild_And_ReplaceChild_Mutate_The_Tree()
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

var removed = box.removeChild(b);        // A C   (returns b)
var z = mk('Z');
var old = box.replaceChild(z, c);        // A Z   (returns c)

var seq = '';
for (var i = 0; i < box.childNodes.length; i++) seq += box.childNodes[i].textContent;

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'seq=' + seq + '|rm=' + removed.textContent + '|old=' + old.textContent;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">seq=AZ|rm=B|old=C<", result);
    }

    [Fact]
    public void AppendChild_Rejects_A_Circular_Insertion()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var parent = document.createElement('div');
var child = document.createElement('div');
parent.appendChild(child);
document.body.appendChild(parent);

var threw = false;
try { child.appendChild(parent); } catch (e) { threw = true; }

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'threw=' + threw;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">threw=true<", result);
    }
}
