using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>form.submit()</c> action (<see cref="FormSubmitBinding"/>), registered on every element wrapper.
/// The callback — previously the bridge's <c>JsJsObjectsSubmit125Core</c> — is now co-located; the
/// form's registered listener store is read through the one-member <see cref="IFormSubmitHost"/>
/// contract, while the no-op function factory, listener invoker and render logger stay the bridge's
/// static helpers. The characterization drives the synthetic <c>submit</c> event end-to-end.
/// </summary>
public sealed class FormSubmitBindingModuleTests
{
    [Fact]
    public void FormSubmit_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(FormSubmitBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.True(moduleType.IsAbstract && moduleType.IsSealed); // C# static class

        Assert.False(typeof(IFormSubmitHost).IsPublic);
        Assert.True(typeof(IFormSubmitHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void FormSubmit_Callback_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        Assert.Null(bridge.GetMethod("JsJsObjectsSubmit125Core", all));
    }

    [Fact]
    public void Submit_Fires_The_Submit_Listener_And_Honors_PreventDefault()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form id=""f""></form>
<div id=""result""></div>
<script>
var f = document.getElementById('f');
var r = [];

var fired = 0;
var sawCancelable = false;
f.addEventListener('submit', function (e) {
  fired++;
  sawCancelable = (e.type === 'submit') && (e.cancelable === true);
  e.preventDefault();
  r.push(e.defaultPrevented === true);
});

f.submit();
r.push(fired === 1);
r.push(sawCancelable === true);

document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true<", result);
    }

    [Fact]
    public void Submit_On_A_Non_Form_Is_A_No_Op()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var ok = (typeof d.submit === 'function');
var threw = false;
try { d.submit(); } catch (e) { threw = true; }
document.getElementById('result').textContent = 'ok=' + ok + '|threw=' + threw;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">ok=true|threw=false<", result);
    }
}
