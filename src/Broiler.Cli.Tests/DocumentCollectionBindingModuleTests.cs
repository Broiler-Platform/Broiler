using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>document</c> live-collection accessors (<see cref="DocumentCollectionBinding"/>):
/// <c>document.forms</c>, <c>document.images</c>, <c>document.links</c>, <c>document.styleSheets</c>.
/// The callbacks — previously the bridge's <c>JsRegistrationGetForms050Core</c> etc. in the shared
/// JsFunctionCallbacks/Registration.cs grab-bag — are now co-located; the document root, element
/// list, wrapper factory, tree-order link collector and stylesheet-object builder are reached through
/// the <see cref="IDocumentCollectionHost"/> contract. The characterization exercises the collections
/// end-to-end through the bridge (including <c>forms</c> named access and the <c>links</c>
/// href-only rule).
/// </summary>
public sealed class DocumentCollectionBindingModuleTests
{
    [Fact]
    public void DocumentCollection_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(DocumentCollectionBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IDocumentCollectionHost).IsPublic);
        Assert.True(typeof(IDocumentCollectionHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void Collection_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsRegistrationGetForms050Core", "JsRegistrationGetImages053Core",
                     "JsRegistrationGetLinks054Core", "JsRegistrationGetStyleSheets055Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Collections_Enumerate_Elements_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form name=""loginForm""><input></form>
<form></form>
<img src=""a.png""><img src=""b.png""><img src=""c.png"">
<a href=""x"">link1</a>
<a href=""y"">link2</a>
<a>no-href</a>
<style>.x{color:red}</style>
<style>.y{color:blue}</style>
<script>
var forms = document.forms;
var images = document.images;
var links = document.links;
var sheets = document.styleSheets;
var out = document.createElement('div');
out.id = 'result';
out.textContent =
  'forms=' + forms.length +
  '|named=' + (forms.loginForm ? 'yes' : 'no') +
  '|images=' + images.length +
  '|links=' + links.length +
  '|sheets=' + sheets.length;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("forms=2|named=yes|images=3|links=2|sheets=2", result);
    }
}
