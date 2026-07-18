using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the reflected
/// content-attribute IDL accessors (<see cref="ElementReflectionBinding"/>) — the plain string reflectors
/// (<c>label.htmlFor</c>, <c>meta.httpEquiv</c>, <c>.type</c>, and the generic named string / dimension
/// setters) and the URL-typed getters (<c>&lt;object&gt;.data</c>, <c>&lt;a&gt;/&lt;area&gt;/&lt;base&gt;/
/// &lt;link&gt;.href</c>) that resolve their relative content attribute against the live page URL. The
/// content-attribute reads/writes use the bridge's neutral <c>internal static</c> helpers; only the page
/// URL comes through the one-member <see cref="IElementReflectionHost"/> contract. Was the bridge's
/// <c>JsElementInterfacesSetHtmlFor047Core</c>..<c>Callback063Core</c> (URL/reflection subset).
/// </summary>
public sealed class ElementReflectionBindingModuleTests
{
    [Fact]
    public void ElementReflection_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(ElementReflectionBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IElementReflectionHost).IsPublic);
        Assert.True(typeof(IElementReflectionHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void ElementReflection_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsElementInterfacesSetHtmlFor047Core", "JsElementInterfacesSetHttpEquiv049Core",
                     "JsElementInterfacesGetData050Core", "JsElementInterfacesSetType053Core",
                     "JsElementInterfacesGetHref056Core", "JsElementInterfacesSetHref057Core",
                     "JsElementInterfacesCallback059Core", "JsElementInterfacesGetHref060Core",
                     "JsElementInterfacesSetHref061Core", "JsElementInterfacesCallback063Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Href_Reflection_Resolves_And_Round_Trips_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var a = document.createElement('a');
a.setAttribute('href', 'page.html');
document.body.appendChild(a);
var resolved = a.href;              // resolved against file:///dir/test.html
a.href = 'other/deep.html';        // set href content attribute
var afterSet = a.href;             // resolved again
var attr = a.getAttribute('href'); // raw content attribute

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'resolved=' + resolved + '|afterSet=' + afterSet + '|attr=' + attr;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///dir/test.html");

        Assert.Contains(">resolved=file:///dir/page.html|afterSet=file:///dir/other/deep.html|attr=other/deep.html<", result);
    }
}
