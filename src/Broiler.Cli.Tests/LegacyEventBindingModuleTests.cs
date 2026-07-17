using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of
/// <c>document.createEvent</c> (<see cref="LegacyEventBinding"/>): the legacy DOM Events Level 3
/// factory that returns a plain event object pre-populated with the union of UI/Mouse/Keyboard/Wheel/
/// Custom fields and the legacy <c>init*Event</c> / propagation-control methods. The callback —
/// previously the bridge's ~320-line <c>JsRegistrationCreateEvent033Core</c> in the shared
/// JsFunctionCallbacks/Registration.cs grab-bag — is now a co-located, stateless module with no host
/// contract (it builds a self-contained JS object with closures over its own state).
/// </summary>
public sealed class LegacyEventBindingModuleTests
{
    [Fact]
    public void LegacyEvent_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(LegacyEventBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.NotNull(moduleType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public void CreateEvent_Callback_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        Assert.Null(bridge.GetMethod("JsRegistrationCreateEvent033Core",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
    }

    [Fact]
    public void CreateEvent_Builds_An_Initializable_Event_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var e = document.createEvent('Event');
var typeBefore = e.type;             // '' before init
e.initEvent('click', true, true);    // type, bubbles, cancelable
e.preventDefault();                  // cancelable -> defaultPrevented true
var out = document.createElement('div');
out.id = 'result';
out.textContent =
  'before=' + (typeBefore === '') +
  '|type=' + e.type +
  '|bubbles=' + e.bubbles +
  '|prevented=' + e.defaultPrevented +
  '|stop=' + (typeof e.stopPropagation);
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">before=true|type=click|bubbles=true|prevented=true|stop=function<", result);
    }
}
