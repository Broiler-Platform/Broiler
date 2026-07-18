using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the HTMLElement
/// global content-attribute reflectors (<see cref="GlobalAttributeBinding"/>) — <c>id</c>, <c>className</c>
/// (↔ <c>class</c>), <c>title</c>, <c>lang</c>, <c>accessKey</c> (↔ <c>accesskey</c>), <c>dir</c>, and the
/// enumerated <c>draggable</c>. The selector-affecting three (<c>id</c>/<c>className</c>/<c>dir</c>)
/// invalidate the style scope on write through the one-member <see cref="IGlobalAttributeHost"/> contract;
/// everything else is a plain reflected read/write over the bridge's neutral <c>internal static</c> helpers.
/// Was the bridge's <c>JsJsObjectsSetId002Core</c>..<c>SetDraggable014Core</c>.
/// </summary>
public sealed class GlobalAttributeBindingModuleTests
{
    [Fact]
    public void GlobalAttribute_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(GlobalAttributeBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.True(moduleType.IsAbstract && moduleType.IsSealed); // C# static class

        Assert.False(typeof(IGlobalAttributeHost).IsPublic);
        Assert.True(typeof(IGlobalAttributeHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void GlobalAttribute_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsJsObjectsSetId002Core", "JsJsObjectsGetClassName003Core", "JsJsObjectsSetClassName004Core",
                     "JsJsObjectsSetTitle006Core", "JsJsObjectsSetLang008Core", "JsJsObjectsSetAccessKey010Core",
                     "JsJsObjectsSetDir012Core", "JsJsObjectsGetDraggable013Core", "JsJsObjectsSetDraggable014Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Reflected_Global_Attributes_Round_Trip_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var el = document.createElement('div');
el.id = 'box';
el.className = 'a b';
el.title = 'hi';
el.lang = 'en';
el.accessKey = 'k';
el.dir = 'rtl';
el.draggable = true;
var r = [];
r.push(el.id === 'box');
r.push(el.getAttribute('id') === 'box');
r.push(el.className === 'a b');
r.push(el.getAttribute('class') === 'a b');
r.push(el.title === 'hi');
r.push(el.lang === 'en');
r.push(el.accessKey === 'k' && el.getAttribute('accesskey') === 'k');
r.push(el.dir === 'rtl');
r.push(el.draggable === true);
r.push(el.getAttribute('draggable') === 'true');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true,true,true,true,true,true,true<", result);
    }

    [Fact]
    public void Draggable_Default_Is_False_And_Unset_Id_Reads_Empty_Class()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var el = document.createElement('span');
var r = [];
r.push(el.draggable === false);      // no attribute -> false
r.push(el.className === '');          // no class -> empty string
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true<", result);
    }

    [Fact]
    public void Setting_Id_Invalidates_Style_Scope_So_Selectors_Recompute()
    {
        // #target { color: red } — assigning the id after insertion must re-run the cascade
        // (the style-scope invalidation the host contract exists for).
        var html = @"<!DOCTYPE html>
<html><head><style>#target { color: rgb(1, 2, 3); }</style></head><body>
<div id=""result""></div>
<script>
var el = document.createElement('div');
document.body.appendChild(el);
var before = window.getComputedStyle(el).color;
el.id = 'target';
var after = window.getComputedStyle(el).color;
document.getElementById('result').textContent =
  'before=' + before + '|after=' + after;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("|after=rgb(1, 2, 3)<", result);
    }
}
