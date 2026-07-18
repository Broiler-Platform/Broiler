using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>&lt;iframe&gt;</c>-element browsing-context IDL accessors (<see cref="IframeElementBinding"/>) —
/// <c>contentDocument</c> / <c>contentWindow</c> / <c>getSVGDocument()</c> (same-origin sub-document or
/// sub-window, else <c>null</c>), the <c>src</c> / <c>srcdoc</c> read/write pair (whose setters reload the
/// frame) and the read-only <c>sandbox</c> reflection. The browsing-context machinery is reached through the
/// <see cref="IIframeElementHost"/> contract; the content-attribute reads/writes use the bridge's neutral
/// <c>internal static</c> helpers. Sibling of the P3.52 <c>&lt;object&gt;</c> <c>ObjectElementBinding</c>.
/// Was the bridge's <c>JsJsObjectsGetContentDocument135Core</c>..<c>SetSrcdoc141Core</c>.
/// </summary>
public sealed class IframeElementBindingModuleTests
{
    [Fact]
    public void Iframe_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(IframeElementBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.True(moduleType.IsAbstract && moduleType.IsSealed); // C# static class

        Assert.False(typeof(IIframeElementHost).IsPublic);
        Assert.True(typeof(IIframeElementHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void Iframe_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsGetContentDocument135Core", "JsJsObjectsGetContentWindow136Core",
                     "JsJsObjectsGetSVGDocument137Core", "JsJsObjectsSetSrc139Core", "JsJsObjectsSetSrcdoc141Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Iframe_ContentDocument_ContentWindow_And_GetSVGDocument_Resolve_Same_Origin()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
document.body.appendChild(iframe);
var r = [];
r.push(iframe.contentDocument !== null && iframe.contentDocument !== undefined);
r.push(iframe.contentDocument.nodeType === 9);
r.push(iframe.contentWindow !== null && iframe.contentWindow !== undefined);
r.push(typeof iframe.getSVGDocument === 'function');
r.push(iframe.getSVGDocument() === iframe.contentDocument);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true,true<", result);
    }

    [Fact]
    public void Iframe_Src_And_Srcdoc_Round_Trip_Through_The_Content_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
document.body.appendChild(iframe);
iframe.src = 'child.html';
iframe.srcdoc = '<p>hi</p>';
var r = [];
r.push(iframe.src === 'child.html');
r.push(iframe.getAttribute('src') === 'child.html');
r.push(iframe.srcdoc === '<p>hi</p>');
r.push(iframe.getAttribute('srcdoc') === '<p>hi</p>');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true<", result);
    }
}
