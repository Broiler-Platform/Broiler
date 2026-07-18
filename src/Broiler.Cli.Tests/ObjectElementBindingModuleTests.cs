using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>&lt;object&gt;</c>-element sub-document IDL accessors (<see cref="ObjectElementBinding"/>) — the
/// <c>data</c> content-attribute setter (which invalidates the cached sub-document), the
/// <c>contentDocument</c> getter (same-origin sub-document, or <c>null</c> when cross-origin / load-failed),
/// and <c>getSVGDocument()</c>. The plain reflected <c>data</c> getter and the <c>type</c> get/set stay in
/// <see cref="ElementReflectionBinding"/> (P3.49); this module owns only the parts coupled to the
/// sub-document / browsing-context machinery, reached through the narrow <see cref="IObjectElementHost"/>
/// contract. Was the bridge's <c>JsElementInterfacesSetData051Core</c>/<c>GetContentDocument054Core</c>/
/// <c>GetSVGDocument055Core</c>.
/// </summary>
public sealed class ObjectElementBindingModuleTests
{
    [Fact]
    public void ObjectElement_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(ObjectElementBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.True(moduleType.IsAbstract && moduleType.IsSealed); // C# static class

        Assert.False(typeof(IObjectElementHost).IsPublic);
        Assert.True(typeof(IObjectElementHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void ObjectElement_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsElementInterfacesSetData051Core",
                     "JsElementInterfacesGetContentDocument054Core",
                     "JsElementInterfacesGetSVGDocument055Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Object_ContentDocument_And_GetSVGDocument_Resolve_Same_Origin()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var obj = document.createElement('object');
var r = [];
r.push(obj.contentDocument !== null);
r.push(obj.contentDocument !== undefined);
r.push(typeof obj.getSVGDocument === 'function');
r.push(obj.getSVGDocument() === obj.contentDocument);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true<", result);
    }

    [Fact]
    public void Object_Data_Setter_Writes_The_Content_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var obj = document.createElement('object');
obj.data = 'resource.svg';
var r = [];
r.push(obj.getAttribute('data') === 'resource.svg');   // content attribute written
r.push(obj.data.indexOf('resource.svg') !== -1);       // reflected getter resolves it
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true<", result);
    }

    [Fact]
    public void Object_ContentDocument_Is_Null_For_Cross_Origin_Data()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var obj = document.createElement('object');
obj.data = 'https://cross.example.com/resource.html';
document.getElementById('result').textContent = (obj.contentDocument === null);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///dir/test.html");
        Assert.Contains(">true<", result);
    }
}
