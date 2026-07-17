using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the Web
/// Animations <c>Animation</c> object surface (<see cref="AnimationObjectBinding"/>): its
/// <c>currentTime</c> get/set and its <c>ready</c>-promise <c>then</c>. The callbacks — previously the
/// bridge's <c>JsRegistrationGetCurrentTime152Core</c>/<c>SetCurrentTime153Core</c>/<c>Then154Core</c>
/// in the shared JsFunctionCallbacks/Registration.cs grab-bag — are now co-located static callbacks
/// (no host contract). The characterization drives an animation obtained from <c>getAnimations()</c>.
/// </summary>
public sealed class AnimationObjectBindingModuleTests
{
    [Fact]
    public void AnimationObject_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(AnimationObjectBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
    }

    [Fact]
    public void Animation_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsRegistrationGetCurrentTime152Core", "JsRegistrationSetCurrentTime153Core",
                     "JsRegistrationThen154Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void CurrentTime_And_Ready_Then_Flow_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""a"" style=""animation: spin 2s; animation-delay: -0.5s;""></div>
<script>
var anims = document.getAnimations();
var anim = anims[0];
anim.currentTime = 750;               // setter
var ct = anim.currentTime;            // getter -> 750
var thenRan = false;
anim.ready.then(function () { thenRan = true; });   // synchronous ready -> runs immediately
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'count=' + anims.length + '|ct=' + ct + '|then=' + thenRan;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">count=1|ct=750|then=true<", result);
    }
}
