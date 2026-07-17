using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>document</c> structural accessors (<see cref="DocumentStructureBinding"/>): <c>document.body</c>,
/// <c>document.head</c> and <c>document.title</c> (get/set). The callbacks — previously the bridge's
/// <c>JsRegistrationGetBody002Core</c>/<c>GetHead003Core</c>/<c>SetTitle005Core</c> (and the inline
/// title getter) in the shared JsFunctionCallbacks/Registration.cs grab-bag — are now co-located; the
/// document root, wrapper factory and title are reached through the <see cref="IDocumentStructureHost"/>
/// contract. The characterization exercises the accessors end-to-end through the bridge.
/// </summary>
public sealed class DocumentStructureBindingModuleTests
{
    [Fact]
    public void DocumentStructure_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(DocumentStructureBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IDocumentStructureHost).IsPublic);
        Assert.True(typeof(IDocumentStructureHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void Structural_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsRegistrationGetBody002Core", "JsRegistrationGetHead003Core",
                     "JsRegistrationSetTitle005Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Body_Head_And_Title_Flow_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><head><title>Original</title></head><body>
<script>
var bodyTag = document.body ? document.body.tagName : 'null';
var headTag = document.head ? document.head.tagName : 'null';
var titleBefore = document.title;
document.title = 'Changed';
var titleAfter = document.title;
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'body=' + bodyTag + '|head=' + headTag + '|titleBefore=' + titleBefore + '|titleAfter=' + titleAfter;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">body=BODY|head=HEAD|titleBefore=Original|titleAfter=Changed<", result);
    }
}
