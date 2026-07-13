using System.Linq;
using System.Reflection;
using Broiler.Dom;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 first vertical slice: the DOM
/// traversal / Range feature is now a co-located binding module (<see cref="TraversalBinding"/>)
/// consumed by <c>DomBridge</c> through the narrow <see cref="ITraversalHost"/> contract, rather
/// than a scatter of partial-class callbacks reaching bridge private fields. The behavior
/// characterizations exercise the extracted handlers end-to-end through the bridge, with no layout
/// dependency, to lock behavior across the move.
/// </summary>
public sealed class TraversalBindingModuleTests
{
    [Fact]
    public void Traversal_Feature_Module_Is_Co_Located_In_The_Bridge_Assembly()
    {
        var moduleType = typeof(TraversalBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        // The module is an internal implementation detail — it must not widen the public surface.
        Assert.False(moduleType.IsPublic);
    }

    [Fact]
    public void DomBridge_Consumes_Traversal_Through_The_Narrow_Host_Contract()
    {
        // The bridge implements the host seam explicitly (so it adds no public members) and holds
        // the module by reference — the composition seam the feature module depends on.
        Assert.True(typeof(ITraversalHost).IsAssignableFrom(typeof(DomBridge)));
        Assert.Contains(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(TraversalBinding));

        // ITraversalHost is an internal contract, not a public leak.
        Assert.False(typeof(ITraversalHost).IsPublic);
    }

    [Fact]
    public void Traversal_Scoped_State_Moved_Off_The_Bridge_Into_The_Module()
    {
        // The active-range and active-node-iterator registries are now owned by the feature module;
        // the bridge no longer declares those weak-reference lists.
        var bridgeFields = typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.DoesNotContain(bridgeFields, IsWeakReferenceListOf<DomRange>);
        Assert.DoesNotContain(bridgeFields, IsWeakReferenceListOf<DomNodeIterator>);
    }

    private static bool IsWeakReferenceListOf<T>(FieldInfo field)
    {
        if (!field.FieldType.IsGenericType || field.FieldType.GetGenericTypeDefinition() != typeof(List<>))
            return false;
        var element = field.FieldType.GetGenericArguments()[0];
        return element.IsGenericType &&
            element.GetGenericTypeDefinition() == typeof(WeakReference<>) &&
            element.GetGenericArguments()[0] == typeof(T);
    }

    [Fact]
    public void Range_SetStart_SetEnd_ToString_Round_Trips_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<p id=""p"">Hello brave new world</p>
<script>
var text = document.getElementById('p').firstChild;
var range = document.createRange();
range.setStart(text, 6);
range.setEnd(text, 11);
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'toString=' + range.toString() + '|collapsed=' + range.collapsed;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("toString=brave|collapsed=false", result);
    }

    [Fact]
    public void CreateTreeWalker_Walks_Element_Children_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root""><span>a</span><span>b</span><span>c</span></div>
<script>
var root = document.getElementById('root');
var walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, null);
var names = [];
var node;
while ((node = walker.nextNode())) { names.push(node.textContent); }
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'walked=' + names.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("walked=a,b,c", result);
    }

    [Fact]
    public void CreateComment_Creates_A_Comment_Node_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var comment = document.createComment('hi there');
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'type=' + comment.nodeType + '|data=' + comment.data;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // nodeType 8 = COMMENT_NODE
        Assert.Contains("type=8|data=hi there", result);
    }
}
