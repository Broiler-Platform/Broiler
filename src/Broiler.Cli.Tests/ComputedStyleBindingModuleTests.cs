using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of
/// <c>window.getComputedStyle</c> (<see cref="ComputedStyleBinding"/>): the CSSOM entry point that
/// resolves an element's used-value style declaration. The callback — previously the bridge's
/// <c>JsRegistrationGetComputedStyle121Core</c> in the shared JsFunctionCallbacks/Registration.cs
/// grab-bag — is now co-located; the JS-wrapper reverse lookup and the computed-style object builder
/// are reached through the <see cref="IComputedStyleHost"/> contract. The characterization resolves an
/// element's computed style end-to-end through the bridge.
/// </summary>
public sealed class ComputedStyleBindingModuleTests
{
    [Fact]
    public void ComputedStyle_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(ComputedStyleBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IComputedStyleHost).IsPublic);
        Assert.True(typeof(IComputedStyleHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void GetComputedStyle_Callback_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        Assert.Null(bridge.GetMethod("JsRegistrationGetComputedStyle121Core",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
    }

    [Fact]
    public void GetComputedStyle_Resolves_Used_Values_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var el = document.createElement('div');
el.style.display = 'flex';
el.style.zIndex = '5';
document.body.appendChild(el);

var cs = window.getComputedStyle(el);
var noArg = 'ok';
try { window.getComputedStyle(); } catch (e) { noArg = 'threw'; }   // no target -> empty object, no throw

var out = document.createElement('div');
out.id = 'result';
out.textContent =
  'display=' + cs.display +
  '|z=' + cs.zIndex +
  '|type=' + (typeof window.getComputedStyle) +
  '|noArg=' + noArg;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">display=flex|z=5|type=function|noArg=ok<", result);
    }
}
