using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// element-content IDL members (<see cref="ElementContentBinding"/>) — the HTML-serialization pair
/// <c>innerHTML</c> / <c>outerHTML</c> (read serializes, write reparses a fragment) and the text-content
/// trio <c>textContent</c> / <c>innerText</c> / <c>outerText</c> (read returns the node text; only
/// <c>textContent</c> writes). All route through the bridge's shared parser/serializer and canonical tree
/// mutation via <see cref="IElementContentHost"/>. Was the bridge's inline registration plus the
/// <c>JsJsObjectsSetInnerHTML016Core</c>/<c>SetOuterHTML018Core</c>/<c>SetTextContent021Core</c> callbacks.
/// </summary>
public sealed class ElementContentBindingModuleTests
{
    [Fact]
    public void ElementContent_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(ElementContentBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.True(moduleType.IsAbstract && moduleType.IsSealed); // C# static class

        Assert.False(typeof(IElementContentHost).IsPublic);
        Assert.True(typeof(IElementContentHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void ElementContent_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsSetInnerHTML016Core", "JsJsObjectsSetOuterHTML018Core", "JsJsObjectsSetTextContent021Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void InnerHtml_Reads_And_Writes_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""box""><span>a</span><span>b</span></div>
<div id=""result""></div>
<script>
var box = document.getElementById('box');
var before = box.innerHTML;
box.innerHTML = '<p id=""p"">hi</p>';
var r = [];
r.push(before.indexOf('<span>') !== -1);
r.push(box.children.length === 1);
r.push(box.querySelector('#p') !== null);
r.push(box.innerHTML.indexOf('<p') !== -1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true<", result);
    }

    [Fact]
    public void OuterHtml_Replaces_The_Element()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""host""><span id=""s"">x</span></div>
<div id=""result""></div>
<script>
var host = document.getElementById('host');
var s = document.getElementById('s');
s.outerHTML = '<b id=""b"">y</b>';
var r = [];
r.push(host.querySelector('#s') === null);   // old element replaced
r.push(host.querySelector('#b') !== null);   // new element in place
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true<", result);
    }

    [Fact]
    public void TextContent_And_InnerText_Read_And_Write()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""box""><span>a</span><span>b</span></div>
<div id=""result""></div>
<script>
var box = document.getElementById('box');
var read = box.textContent;
box.textContent = 'plain';
var r = [];
r.push(read === 'ab');
r.push(box.textContent === 'plain');
r.push(box.innerText === 'plain');       // innerText read shares GetNodeTextValue
r.push(box.children.length === 0);       // replaced children with a text node
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true<", result);
    }
}
