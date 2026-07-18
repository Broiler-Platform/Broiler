using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// shadow-DOM JS-binding members (<see cref="ShadowDomBinding"/>) — the <c>element.shadowRoot</c> getter
/// and <c>element.attachShadow()</c> method. The callbacks — previously the bridge's
/// <c>JsJsObjectsGetShadowRoot019Core</c> / <c>AttachShadow087Core</c> — are now co-located; the
/// per-element shadow linkage stays on the bridge's Shadow runtime slot, reached only through the named
/// primitives of the <see cref="IShadowDomHost"/> contract. The characterizations drive open/closed
/// attachment end-to-end through the bridge.
/// </summary>
public sealed class ShadowDomBindingModuleTests
{
    [Fact]
    public void ShadowDom_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(ShadowDomBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.True(moduleType.IsAbstract && moduleType.IsSealed); // C# static class

        Assert.False(typeof(IShadowDomHost).IsPublic);
        Assert.True(typeof(IShadowDomHost).IsAssignableFrom(typeof(Broiler.HtmlBridge.DomBridge)));
    }

    [Fact]
    public void ShadowDom_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[] { "JsJsObjectsGetShadowRoot019Core", "JsJsObjectsAttachShadow087Core" })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void AttachShadow_Open_Exposes_The_Root_And_Rejects_A_Second_Attach()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""host""></div>
<div id=""result""></div>
<script>
var host = document.getElementById('host');
var r = [];
r.push(host.shadowRoot === null);           // no root before attach
var root = host.attachShadow({ mode: 'open' });
r.push(root !== null);
r.push(host.shadowRoot === root);           // open root is exposed and identity-stable
var threw = false;
try { host.attachShadow({ mode: 'open' }); } catch (e) { threw = true; }
r.push(threw === true);                      // second attach throws NotSupportedError
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true<", result);
    }

    [Fact]
    public void AttachShadow_Closed_Hides_The_Root_From_The_Getter()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""host""></div>
<div id=""result""></div>
<script>
var host = document.getElementById('host');
var root = host.attachShadow({ mode: 'closed' });
var r = [];
r.push(root !== null);                        // attachShadow returns the root
r.push(host.shadowRoot === null);             // closed root is not exposed via the getter
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true<", result);
    }
}
