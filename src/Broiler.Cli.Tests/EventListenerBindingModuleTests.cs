using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 fourth slice (P3.4): the
/// <c>addEventListener</c>/<c>removeEventListener</c> registration semantics — the listener half of
/// the Events feature — are now a single co-located helper (<see cref="EventListenerBinding"/>)
/// that the element/document/window/message-port callbacks share, replacing the block that was
/// copied across four feature files. The behavior characterizations exercise the shared registration
/// end-to-end through the bridge (dedup, capture-scoped removal, once/passive) with no layout
/// dependency.
/// </summary>
public sealed class EventListenerBindingModuleTests
{
    [Fact]
    public void EventListener_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(EventListenerBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
    }

    [Fact]
    public void Duplicate_Registrations_Are_Deduplicated_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""t""></div>
<script>
var t = document.getElementById('t');
var count = 0;
function h() { count++; }
t.addEventListener('x', h);
t.addEventListener('x', h);   // duplicate (same listener + capture) — must not double-register
var evt = document.createEvent('Event');
evt.initEvent('x', false, false);
t.dispatchEvent(evt);
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'count=' + count;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("count=1", result);
    }

    [Fact]
    public void RemoveEventListener_Is_Capture_Scoped_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""t""></div>
<script>
var t = document.getElementById('t');
var fired = [];
function h() { fired.push('h'); }
t.addEventListener('x', h, true);    // capture registration
t.addEventListener('x', h, false);   // bubble registration (distinct)
t.removeEventListener('x', h, false); // removes only the bubble one
var evt = document.createEvent('Event');
evt.initEvent('x', true, false);
t.dispatchEvent(evt);
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'fired=' + fired.length;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // The capture registration survives, so the handler still fires exactly once.
        Assert.Contains("fired=1", result);
    }

    [Fact]
    public void Once_Option_Fires_Handler_A_Single_Time_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""t""></div>
<script>
var t = document.getElementById('t');
var count = 0;
t.addEventListener('x', function() { count++; }, { once: true });
function fire() {
  var e = document.createEvent('Event');
  e.initEvent('x', false, false);
  t.dispatchEvent(e);
}
fire();
fire();
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'count=' + count;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("count=1", result);
    }
}
