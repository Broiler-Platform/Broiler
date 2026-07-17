using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>document</c> element-query methods (<see cref="DocumentQueryBinding"/>): <c>getElementById</c>,
/// <c>getElementsByTagName</c>, <c>getElementsByClassName</c>, <c>querySelector</c>,
/// <c>querySelectorAll</c>. The callbacks — previously the bridge's
/// <c>JsRegistrationGetElementById006Core</c> etc. in the shared JsFunctionCallbacks/Registration.cs
/// grab-bag — are now co-located; the document root, element list and wrapper factory are reached
/// through the narrow <see cref="IDocumentQueryHost"/> contract, with sub-tree search and selector
/// matching called as the bridge's neutral internal static helpers. Hit-testing, the structural
/// accessors (body/head/title) and the live collections are not part of this slice.
/// </summary>
public sealed class DocumentQueryBindingModuleTests
{
    [Fact]
    public void DocumentQuery_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(DocumentQueryBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IDocumentQueryHost).IsPublic);
        Assert.True(typeof(IDocumentQueryHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void Query_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsRegistrationGetElementById006Core", "JsRegistrationGetElementsByTagName007Core",
                     "JsRegistrationGetElementsByClassName008Core", "JsRegistrationQuerySelector009Core",
                     "JsRegistrationQuerySelectorAll010Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Queries_Find_Elements_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"" class=""foo bar"">T</div>
<p class=""foo"">P1</p>
<p class=""foo"">P2</p>
<span>S</span>
<script>
var byId = document.getElementById('target');
var byTag = document.getElementsByTagName('p');
var byClass = document.getElementsByClassName('foo');
var qs = document.querySelector('p.foo');
var qsa = document.querySelectorAll('p.foo');
var out = document.createElement('div');
out.id = 'result';
out.textContent =
  'id=' + (byId ? byId.className : 'null') +
  '|tag=' + byTag.length +
  '|class=' + byClass.length +
  '|qs=' + (qs ? qs.textContent : 'null') +
  '|qsa=' + qsa.length;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("id=foo bar|tag=2|class=3|qs=P1|qsa=2", result);
    }
}
