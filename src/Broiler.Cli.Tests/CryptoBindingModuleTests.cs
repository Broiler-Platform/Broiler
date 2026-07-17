using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// Web Crypto <c>crypto</c> subset (<see cref="CryptoBinding"/>). The <c>crypto</c> object and its
/// <c>getRandomValues</c> callback — previously built inline in the bridge's
/// <c>RegisterSecurityAndConstructorPolyfills</c> with the callback buried in the shared
/// JsFunctionCallbacks/Registration.cs grab-bag — are now co-located in one module. It touches no
/// bridge instance state, so it has no host contract. The behavior characterization exercises the
/// extracted API end-to-end through the bridge with no layout dependency.
/// </summary>
public sealed class CryptoBindingModuleTests
{
    [Fact]
    public void Crypto_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(CryptoBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.NotNull(moduleType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public void GetRandomValues_Callback_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        Assert.Null(bridge.GetMethod("JsRegistrationGetRandomValues150Core",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
    }

    [Fact]
    public void Crypto_GetRandomValues_And_RandomUUID_Work_Through_The_Bridge()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var arr = [0, 0, 0, 0, 0, 0];
var ret = crypto.getRandomValues(arr);
var allNums = true;
for (var i = 0; i < arr.length; i++) { if (typeof arr[i] !== 'number') allNums = false; }
var uuid = crypto.randomUUID();
var uuidOk = (typeof uuid === 'string') && uuid.length === 36 && uuid.split('-').length === 5;
var el = document.createElement('div');
el.id = 'result';
el.textContent =
  'same=' + (ret === arr) +
  '|len=' + arr.length +
  '|nums=' + allNums +
  '|uuid=' + uuidOk;
document.body.appendChild(el);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("same=true|len=6|nums=true|uuid=true", result);
    }
}
