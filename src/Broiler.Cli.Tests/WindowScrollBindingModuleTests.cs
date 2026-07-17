using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>window</c> scroll methods (<see cref="WindowScrollBinding"/>): <c>window.scroll</c>,
/// <c>window.scrollTo</c>, <c>window.scrollBy</c>. The callbacks — previously the bridge's
/// <c>JsRegistrationScroll133Core</c>/<c>ScrollTo134Core</c>/<c>ScrollBy135Core</c> in the shared
/// JsFunctionCallbacks/Registration.cs grab-bag — are now co-located; the scrolling element, argument
/// parser and scroll primitive are reached through the <see cref="IWindowScrollHost"/> contract. The
/// characterization drives absolute (<c>scrollTo</c>/<c>scroll</c>) and relative (<c>scrollBy</c>)
/// scrolling end-to-end through the bridge.
/// </summary>
public sealed class WindowScrollBindingModuleTests
{
    [Fact]
    public void WindowScroll_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(WindowScrollBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IWindowScrollHost).IsPublic);
        Assert.True(typeof(IWindowScrollHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void Scroll_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsRegistrationScroll133Core", "JsRegistrationScrollTo134Core", "JsRegistrationScrollBy135Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void ScrollTo_ScrollBy_And_Scroll_Update_The_Offset_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div style=""height:3000px"">tall</div>
<script>
window.scrollTo(0, 100);
var afterTo = window.scrollY;       // absolute -> 100
window.scrollBy(0, 50);
var afterBy = window.scrollY;       // relative -> 150
window.scroll(0, 25);               // scroll() is an alias of scrollTo() -> 25
var afterScroll = window.scrollY;
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'to=' + afterTo + '|by=' + afterBy + '|scroll=' + afterScroll;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">to=100|by=150|scroll=25<", result);
    }
}
