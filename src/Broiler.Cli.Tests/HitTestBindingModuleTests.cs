using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>document</c> point hit-testing methods (<see cref="HitTestBinding"/>):
/// <c>document.elementFromPoint</c>, <c>document.elementsFromPoint</c>. The callbacks — previously the
/// bridge's <c>JsRegistrationElementFromPoint011Core</c>/<c>ElementsFromPoint012Core</c> in the shared
/// JsFunctionCallbacks/Registration.cs grab-bag — are now co-located; the document root, wrapper factory
/// and the point hit-test are reached through the <see cref="IHitTestHost"/> contract, with coordinate
/// parsing via the bridge's neutral internal static helper.
/// </summary>
public sealed class HitTestBindingModuleTests
{
    [Fact]
    public void HitTest_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(HitTestBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IHitTestHost).IsPublic);
        Assert.True(typeof(IHitTestHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void HitTest_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        Assert.Null(bridge.GetMethod("JsRegistrationElementFromPoint011Core", all));
        Assert.Null(bridge.GetMethod("JsRegistrationElementsFromPoint012Core", all));
    }

    [Fact]
    public void ElementFromPoint_And_ElementsFromPoint_Hit_The_Box_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body style=""margin:0"">
<div id=""box"" style=""position:absolute; left:0; top:0; width:100px; height:100px""></div>
<script>
var hit = document.elementFromPoint(50, 50);
var stack = document.elementsFromPoint(50, 50);
var stackHasBox = false;
for (var i = 0; i < stack.length; i++) { if (stack[i].id === 'box') stackHasBox = true; }
var out = document.createElement('div');
out.id = 'result';
out.textContent =
  'hit=' + (hit ? hit.id : 'null') +
  '|stackHasBox=' + stackHasBox +
  '|stackLen=' + (stack.length >= 1);
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">hit=box|stackHasBox=true|stackLen=true<", result);
    }
}
