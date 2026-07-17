using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the residual
/// thin window/document singletons (<see cref="WindowDocumentMiscBinding"/>): <c>window.alert</c>,
/// <c>performance.now</c>, <c>window.visualViewport.scale</c> setter, <c>document.contentType</c>,
/// <c>document.cookie</c> setter. These were the last callbacks in the JsFunctionCallbacks/Registration.cs
/// grab-bag; moving them empties (and deletes) that file. The characterization exercises all five
/// end-to-end through the bridge.
/// </summary>
public sealed class WindowDocumentMiscBindingModuleTests
{
    [Fact]
    public void WindowDocumentMisc_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(WindowDocumentMiscBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        Assert.False(typeof(IWindowDocumentMiscHost).IsPublic);
        Assert.True(typeof(IWindowDocumentMiscHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void Residual_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsRegistrationAlert076Core", "JsRegistrationNow122Core", "JsRegistrationSetScale143Core",
                     "JsRegistrationGetContentType063Core", "JsRegistrationSetCookie149Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Alert_Now_Scale_ContentType_And_Cookie_Flow_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var alertOk = (typeof window.alert === 'function') && (window.alert('hi') === undefined);
var nowIsNum = (typeof window.performance.now() === 'number');
window.visualViewport.scale = 2;
var scale = window.visualViewport.scale;
var ct = document.contentType;
document.cookie = 'a=1';
document.cookie = 'b=2';
var cookie = document.cookie;
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'alert=' + alertOk + '|now=' + nowIsNum + '|scale=' + scale + '|ct=' + ct + '|cookie=' + cookie;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">alert=true|now=true|scale=2|ct=text/html|cookie=a=1; b=2<", result);
    }
}
