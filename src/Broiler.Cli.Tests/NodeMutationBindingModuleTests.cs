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

    /// <summary>
    /// <c>Document</c> includes the <c>ParentNode</c> mixin (DOM §4.2.6), so <c>append</c>,
    /// <c>prepend</c> and <c>replaceChildren</c> exist on the document node. Only the
    /// <c>Node</c>-level methods above were bound, so <c>document.append(x)</c> was
    /// <c>undefined</c> and threw — mid-script, after whatever the script had already done
    /// to the tree.
    /// </summary>
    [Fact]
    public void Document_Exposes_The_ParentNode_Mixin()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""probe"">P</div>
<script>
var kinds = typeof document.append + ',' + typeof document.prepend + ',' + typeof document.replaceChildren;

var docEl = document.documentElement;
document.removeChild(docEl);
document.append(docEl);            // the mixin, not appendChild
var afterAppend = document.childNodes.length;

document.replaceChildren(docEl);   // clear, then re-insert the same node
var afterReplace = document.childNodes.length;

var probeOk = document.getElementById('probe') !== null;

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'kinds=' + kinds + '|append=' + afterAppend +
                  '|replace=' + afterReplace + '|probe=' + probeOk;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(
            ">kinds=function,function,function|append=1|replace=1|probe=true<",
            result);
    }

    /// <summary>
    /// DOM §4.2.3: an element may not go before the document's doctype, so `prepend` on a
    /// document that has one is a HierarchyRequestError. The check earns its place by what
    /// happens without it — the tree accepts the element ahead of the doctype and the document
    /// then reads as having no root element at all, silently, which is a blank page rather
    /// than an error.
    /// </summary>
    [Fact]
    public void Document_Prepend_Rejects_An_Element_Before_The_Doctype()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""probe"">P</div>
<script>
var docEl = document.documentElement;
var outcome;
document.removeChild(docEl);
try { document.prepend(docEl); outcome = 'no-throw'; }
catch (e) { outcome = e.name; document.append(docEl); }

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'outcome=' + outcome + '|root=' + document.documentElement.tagName;
document.documentElement.querySelector('body').appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">outcome=HierarchyRequestError|root=HTML<", result);
    }

    /// <summary>
    /// With no doctype in the way, `prepend` inserts at the front like the mixin says.
    /// </summary>
    [Fact]
    public void Document_Prepend_Inserts_At_The_Front_When_No_Doctype_Blocks_It()
    {
        var html = @"<html><body>
<div id=""probe"">P</div>
<script>
var docEl = document.documentElement;
document.removeChild(docEl);
document.prepend(docEl);

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'n=' + document.childNodes.length + '|root=' + document.documentElement.tagName;
document.documentElement.querySelector('body').appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">n=1|root=HTML<", result);
    }

    /// <summary>
    /// The shape WPT's <c>quirks/tables-inherit-color-from-body-quirk-007</c> uses: drop the
    /// document element and put a clone of it back through <c>append</c>. The throw this used to
    /// raise left the document with no root element at all, so the page rendered blank — the
    /// failure mode worth naming, because nothing about it points at <c>append</c>.
    /// </summary>
    [Fact]
    public void Document_Append_Restores_A_Detached_Root_Clone()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""probe"">SURVIVED</div>
<script>
var root = document.documentElement;
document.documentElement.remove();
document.append(root.cloneNode(true));
</script>
</body></html>";

        // Asserted on the serialized document rather than through a marker the script writes:
        // the failure being pinned is that the *whole tree* went missing, so the evidence is the
        // tree. Before the fix this came back as the doctype and nothing else.
        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("SURVIVED", result);
        Assert.Contains("<html", result);
    }

    /// <summary>
    /// <c>replaceChildren()</c> with no arguments empties the node — the one call in the mixin
    /// whose whole effect is the removal half.
    /// </summary>
    [Fact]
    public void Document_ReplaceChildren_With_No_Arguments_Empties_The_Document()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var docEl = document.documentElement;
document.replaceChildren();
var emptied = document.childNodes.length;
document.append(docEl);
var restored = document.childNodes.length;

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'emptied=' + emptied + '|restored=' + restored;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">emptied=0|restored=1<", result);
    }
}
