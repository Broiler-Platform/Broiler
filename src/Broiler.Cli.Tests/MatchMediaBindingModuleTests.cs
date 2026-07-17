using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of
/// <c>window.matchMedia</c> (<see cref="MatchMediaBinding"/>). The callback — previously the bridge's
/// <c>JsRegistrationMatchMedia069Core</c> in the shared JsFunctionCallbacks/Registration.cs grab-bag,
/// with its evaluation in the now-removed <c>DomBridge.EvaluateMediaQuery</c> wrapper — is now a
/// co-located module whose only bridge coupling is the live viewport, via the narrow
/// <see cref="IMatchMediaHost"/> contract. The characterization exercises the extracted API
/// end-to-end through the bridge with no layout dependency.
/// </summary>
public sealed class MatchMediaBindingModuleTests
{
    [Fact]
    public void MatchMedia_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(MatchMediaBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        // The host contract is an internal seam, and the bridge implements it.
        Assert.False(typeof(IMatchMediaHost).IsPublic);
        Assert.True(typeof(IMatchMediaHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void MatchMedia_Callback_And_Evaluator_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        Assert.Null(bridge.GetMethod("JsRegistrationMatchMedia069Core", all));
        Assert.Null(bridge.GetMethod("EvaluateMediaQuery", all));
    }

    [Fact]
    public void MatchMedia_Evaluates_Against_The_Viewport_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var m1 = window.matchMedia('(min-width: 100px)');
var m2 = window.matchMedia('(min-width: 5000px)');
var el = document.createElement('div');
el.id = 'result';
el.textContent =
  'a=' + m1.matches +
  '|amedia=' + m1.media +
  '|b=' + m2.matches +
  '|addl=' + (typeof m1.addListener) +
  '|reml=' + (typeof m1.removeListener);
document.body.appendChild(el);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("a=true|amedia=(min-width: 100px)|b=false|addl=function|reml=function", result);
    }
}
