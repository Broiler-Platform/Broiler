using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 second slice (P3.2): the whole
/// MutationObserver feature — the observer registry, the observe()/disconnect() registration and
/// the childList/attribute/characterData record delivery — is now a co-located binding module
/// (<see cref="MutationObserverBinding"/>) consumed through the narrow
/// <see cref="IMutationObserverHost"/> contract. The behavior characterizations exercise the
/// extracted delivery end-to-end through the bridge with no layout dependency.
/// </summary>
public sealed class MutationObserverBindingModuleTests
{
    [Fact]
    public void MutationObserver_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(MutationObserverBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.False(typeof(IMutationObserverHost).IsPublic);
    }

    [Fact]
    public void DomBridge_Consumes_MutationObserver_Through_The_Host_Contract()
    {
        Assert.True(typeof(IMutationObserverHost).IsAssignableFrom(typeof(DomBridge)));
        Assert.Contains(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(MutationObserverBinding));
    }

    [Fact]
    public void Observer_Registry_Moved_Off_The_Bridge_Into_The_Module()
    {
        // The MutationObserverHub state authority is now owned by the feature module, not the bridge.
        var hubType = typeof(DomBridge).Assembly.GetType("Broiler.HtmlBridge.Dom.Runtime.MutationObserverHub");
        Assert.NotNull(hubType);
        Assert.DoesNotContain(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            field => field.FieldType == hubType);
        Assert.Contains(
            typeof(MutationObserverBinding).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            field => field.FieldType == hubType);
    }

    [Fact]
    public void ChildList_Mutations_Are_Delivered_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<script>
var target = document.getElementById('target');
var log = [];
var obs = new MutationObserver(function(records) {
  for (var i = 0; i < records.length; i++) {
    log.push(records[i].type + ':' + records[i].addedNodes.length);
  }
});
obs.observe(target, { childList: true });
target.appendChild(document.createElement('span'));
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'log=' + log.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("log=childList:1", result);
    }

    [Fact]
    public void Attribute_Mutations_With_OldValue_Are_Delivered_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"" data-x=""one""></div>
<script>
var target = document.getElementById('target');
var log = [];
var obs = new MutationObserver(function(records) {
  for (var i = 0; i < records.length; i++) {
    log.push(records[i].type + ':' + records[i].attributeName + ':' + records[i].oldValue);
  }
});
obs.observe(target, { attributes: true, attributeOldValue: true });
target.setAttribute('data-x', 'two');
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'log=' + log.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("log=attributes:data-x:one", result);
    }

    [Fact]
    public void Disconnect_Stops_Delivery_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<script>
var target = document.getElementById('target');
var count = 0;
var obs = new MutationObserver(function(records) { count += records.length; });
obs.observe(target, { childList: true });
obs.disconnect();
target.appendChild(document.createElement('span'));
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'count=' + count;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("count=0", result);
    }
}
