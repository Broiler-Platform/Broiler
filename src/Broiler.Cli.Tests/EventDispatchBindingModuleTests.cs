using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 third slice (P3.3): the DOM event
/// dispatch engine — capture → target → bubble propagation, the event object's propagation-control
/// methods and composedPath() — is now a co-located binding module
/// (<see cref="EventDispatchBinding"/>) consumed through the narrow
/// <see cref="IEventDispatchHost"/> contract. The behavior characterizations exercise the extracted
/// dispatch end-to-end through the bridge with no layout dependency.
/// </summary>
public sealed class EventDispatchBindingModuleTests
{
    [Fact]
    public void EventDispatch_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(EventDispatchBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.False(typeof(IEventDispatchHost).IsPublic);
    }

    [Fact]
    public void DomBridge_Consumes_EventDispatch_Through_The_Host_Contract()
    {
        Assert.True(typeof(IEventDispatchHost).IsAssignableFrom(typeof(DomBridge)));
        Assert.Contains(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(EventDispatchBinding));
    }

    [Fact]
    public void Bubbling_Fires_Capture_Then_Target_Then_Bubble_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""outer""><div id=""inner""></div></div>
<script>
var outer = document.getElementById('outer');
var inner = document.getElementById('inner');
var order = [];
outer.addEventListener('x', function() { order.push('outer-capture'); }, true);
outer.addEventListener('x', function() { order.push('outer-bubble'); }, false);
inner.addEventListener('x', function() { order.push('inner-target'); });
var evt = document.createEvent('Event');
evt.initEvent('x', true, true);
inner.dispatchEvent(evt);
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'order=' + order.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("order=outer-capture,inner-target,outer-bubble", result);
    }

    [Fact]
    public void StopPropagation_Halts_Bubbling_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""outer""><div id=""inner""></div></div>
<script>
var outer = document.getElementById('outer');
var inner = document.getElementById('inner');
var reached = [];
inner.addEventListener('x', function(e) { reached.push('inner'); e.stopPropagation(); });
outer.addEventListener('x', function() { reached.push('outer'); });
var evt = document.createEvent('Event');
evt.initEvent('x', true, true);
inner.dispatchEvent(evt);
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'reached=' + reached.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("reached=inner", result);
        Assert.DoesNotContain("reached=inner,outer", result);
    }

    [Fact]
    public void PreventDefault_Sets_DefaultPrevented_And_Dispatch_Returns_False()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""t""></div>
<script>
var t = document.getElementById('t');
t.addEventListener('x', function(e) { e.preventDefault(); });
var evt = document.createEvent('Event');
evt.initEvent('x', false, true);
var ret = t.dispatchEvent(evt);
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'ret=' + ret + '|prevented=' + evt.defaultPrevented;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("ret=false|prevented=true", result);
    }
}
