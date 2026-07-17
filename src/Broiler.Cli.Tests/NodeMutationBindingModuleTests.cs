using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>document</c>-node mutation methods (<see cref="NodeMutationBinding"/>):
/// <c>document.childNodes</c>, <c>document.removeChild</c>, <c>document.appendChild</c>,
/// <c>document.insertBefore</c>. The callbacks — previously the bridge's
/// <c>JsRegistrationGetChildNodes046Core</c>..<c>InsertBefore049Core</c> in the shared
/// JsFunctionCallbacks/Registration.cs grab-bag — are now co-located; the document node, wrapper
/// factory, reverse lookup and mutation notifications are reached through the
/// <see cref="INodeMutationHost"/> contract. This slice's extraction dropped the grab-bag under the
/// 750-line guard, de-listing it. The characterization mutates the document node directly (moving the
/// documentElement out and back) end-to-end through the bridge.
/// </summary>
public sealed class NodeMutationBindingModuleTests
{
    [Fact]
    public void NodeMutation_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(NodeMutationBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(INodeMutationHost).IsPublic);
        Assert.True(typeof(INodeMutationHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void Mutation_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsRegistrationGetChildNodes046Core", "JsRegistrationRemoveChild047Core",
                     "JsRegistrationAppendChild048Core", "JsRegistrationInsertBefore049Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Document_Node_Mutation_Flows_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""probe"">P</div>
<script>
// document.childNodes exposes the document's element children (the <html> documentElement).
var before = document.childNodes.length;
var docEl = document.childNodes[0];

// removeChild / appendChild / insertBefore operate on the document node itself, moving the
// documentElement subtree out and back (a document may hold only one element child, so this is
// the valid mutation to exercise).
document.removeChild(docEl);
var afterRemove = document.childNodes.length;
document.appendChild(docEl);
var afterAppend = document.childNodes.length;
document.removeChild(docEl);
document.insertBefore(docEl, null);   // null ref -> append
var afterInsert = document.childNodes.length;

// The probe subtree survived the round-trip and is reachable again.
var probeOk = document.getElementById('probe') !== null;

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'before=' + before + '|remove=' + afterRemove + '|append=' + afterAppend + '|insert=' + afterInsert + '|probe=' + probeOk;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">before=1|remove=0|append=1|insert=1|probe=true<", result);
    }
}
