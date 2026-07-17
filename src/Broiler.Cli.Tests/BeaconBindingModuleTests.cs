using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of
/// <c>navigator.sendBeacon</c> (<see cref="BeaconBinding"/>). The callback — previously the bridge's
/// <c>JsRegistrationSendBeacon124Core</c> in the shared JsFunctionCallbacks/Registration.cs grab-bag
/// — is now a co-located module. It reads only the supplied <c>window</c> and delegates to its
/// <c>fetch</c>, touching no bridge instance state, so it has no host contract. The characterization
/// stubs <c>window.fetch</c> so the delegation contract is observed deterministically without network.
/// </summary>
public sealed class BeaconBindingModuleTests
{
    [Fact]
    public void Beacon_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(BeaconBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.NotNull(moduleType.GetMethod("Send", BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public void SendBeacon_Callback_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        Assert.Null(bridge.GetMethod("JsRegistrationSendBeacon124Core",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
    }

    [Fact]
    public void SendBeacon_Delegates_To_Fetch_As_KeepAlive_Post_And_Honours_The_No_Data_Contract()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
// Stub fetch so the delegation is observed without touching the network.
var seen = '';
window.fetch = function (url, opts) { seen = url + '|' + opts.method + '|' + opts.keepalive + '|' + opts.body; return {}; };
var ok = navigator.sendBeacon('https://example.com/collect', 'payload');
var none = navigator.sendBeacon();            // no data -> false, no fetch call
var el = document.createElement('div');
el.id = 'result';
el.textContent =
  'ok=' + ok +
  '|none=' + none +
  '|type=' + (typeof navigator.sendBeacon) +
  '|seen=' + seen;
document.body.appendChild(el);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("ok=true|none=false|type=function|seen=https://example.com/collect|POST|true|payload", result);
    }
}
