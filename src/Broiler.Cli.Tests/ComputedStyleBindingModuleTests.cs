using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// computed-style reads (<see cref="ComputedStyleBinding"/>): the CSSOM entry point
/// <c>window.getComputedStyle</c> (which resolves an element's used-value style declaration) and the
/// <c>&lt;img&gt;.width</c>/<c>&lt;img&gt;.height</c> used-dimension getters (P3.53), which read the rendered
/// dimension out of the same computed-style object with a content-attribute fallback. The callbacks —
/// previously the bridge's <c>JsRegistrationGetComputedStyle121Core</c> and
/// <c>JsElementInterfacesCallback062Core</c> — are now co-located; the JS-wrapper reverse lookup and the
/// computed-style object builder are reached through the <see cref="IComputedStyleHost"/> contract.
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
    public void ComputedStyle_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        Assert.Null(bridge.GetMethod("JsRegistrationGetComputedStyle121Core", all));
        Assert.Null(bridge.GetMethod("JsElementInterfacesCallback062Core", all));
    }

    [Fact]
    public void Img_Width_Height_Report_Used_Dimension_With_Attribute_Fallback()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
// No CSS width/height -> the used dimension falls back to the content attribute.
var img = document.createElement('img');
img.setAttribute('width', '120');
img.setAttribute('height', '90');
document.body.appendChild(img);

var out = document.createElement('div');
out.id = 'result';
out.textContent = 'w=' + img.width + '|h=' + img.height +
  '|wnum=' + (typeof img.width) + '|hnum=' + (typeof img.height);
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">w=120|h=90|wnum=number|hnum=number<", result);
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
