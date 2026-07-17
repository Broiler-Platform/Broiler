using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>document.implementation</c> document-level factories (<see cref="DocumentLevelFactoryBinding"/>):
/// <c>createDocumentType</c>, <c>createDocument</c>, <c>createHTMLDocument</c> — completing the factory
/// surface begun in <see cref="DocumentFactoryBinding"/> (P3.25). The callbacks — previously the
/// bridge's <c>JsRegistrationCreateDocumentType057Core</c>..<c>CreateHTMLDocument059Core</c> in the
/// shared JsFunctionCallbacks/Registration.cs grab-bag — are now co-located; the node/document
/// construction funnels, reverse lookup, browsing-context document-root factory and sub-document
/// builder are reached through the <see cref="IDocumentLevelFactoryHost"/> contract.
/// </summary>
public sealed class DocumentLevelFactoryBindingModuleTests
{
    [Fact]
    public void DocumentLevelFactory_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(DocumentLevelFactoryBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IDocumentLevelFactoryHost).IsPublic);
        Assert.True(typeof(IDocumentLevelFactoryHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void DocumentLevel_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsRegistrationCreateDocumentType057Core", "JsRegistrationCreateDocument058Core",
                     "JsRegistrationCreateHTMLDocument059Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Factories_Build_Documents_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var impl = document.implementation;

var htmlDoc = impl.createHTMLDocument('My Title');
var p = htmlDoc.createElement('p');
p.id = 'x';
p.textContent = 'hi';
htmlDoc.body.appendChild(p);

var xdoc = impl.createDocument('http://www.w3.org/1999/xhtml', 'html', null);
var dt = impl.createDocumentType('html', 'pub', 'sys');

var out = document.createElement('div');
out.id = 'result';
out.textContent =
  'title=' + htmlDoc.title +
  '|body=' + htmlDoc.body.tagName +
  '|found=' + (htmlDoc.getElementById('x') === p) +
  '|xroot=' + xdoc.documentElement.tagName +
  '|dtName=' + dt.name;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">title=My Title|body=BODY|found=true|xroot=HTML|dtName=html<", result);
    }
}
