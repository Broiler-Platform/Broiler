using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the SVG DOM
/// element interfaces (<see cref="SvgElementBinding"/>) — the <c>SVGAnimatedLength</c> stubs for the
/// dimensional presentation attributes, the <c>viewBox</c> <c>SVGAnimatedRect</c>, the
/// <c>SVGTextContentElement</c> text-metric methods, the <c>SVGSVGElement</c> animation timeline and the
/// SMIL animation-element no-ops. Every accessor is an attribute/font-size estimation stub (no layout
/// geometry), so the module is a pure <c>internal static</c> class with <b>no host contract</b> (like
/// <c>ClassListBinding</c>/<c>WebStorageBinding</c>). Was the bridge's
/// <c>JsElementInterfacesCallback086Core</c>..<c>SetCurrentTime095Core</c>.
/// </summary>
public sealed class SvgElementBindingModuleTests
{
    [Fact]
    public void Svg_Feature_Module_Is_Internal_Static_With_No_Host_Contract()
    {
        var moduleType = typeof(SvgElementBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.True(moduleType.IsAbstract && moduleType.IsSealed); // C# static class

        // No ISvg*Host contract exists — the module reaches the bridge only through neutral internal statics.
        Assert.Empty(moduleType.Assembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace == "Broiler.HtmlBridge.Dom.Features"
                        && t.Name.StartsWith("ISvg", StringComparison.Ordinal)));
    }

    [Fact]
    public void Svg_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsElementInterfacesCallback086Core", "JsElementInterfacesGetViewBox087Core",
                     "JsElementInterfacesGetNumberOfChars088Core", "JsElementInterfacesGetComputedTextLength089Core",
                     "JsElementInterfacesGetSubStringLength090Core", "JsElementInterfacesGetStartPositionOfChar091Core",
                     "JsElementInterfacesGetEndPositionOfChar092Core", "JsElementInterfacesGetRotationOfChar093Core",
                     "JsElementInterfacesSetCurrentTime095Core", "CreateSvgLengthValue",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Svg_AnimatedLength_Reflects_Dimensional_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var rect = document.createElementNS(svgns, 'rect');
rect.setAttribute('width', '42');
rect.setAttribute('height', '17');
var r = [];
r.push(rect.width.baseVal.value === 42);
r.push(rect.width.animVal.value === 42);
r.push(rect.height.baseVal.value === 17);
r.push(rect.width.baseVal.SVG_LENGTHTYPE_NUMBER === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true<", result);
    }

    [Fact]
    public void Svg_ViewBox_Parses_Into_AnimatedRect()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var svg = document.createElementNS(svgns, 'svg');
svg.setAttribute('viewBox', '10 20 300 400');
var vb = svg.viewBox;
var r = [];
r.push(vb.baseVal.x === 10);
r.push(vb.baseVal.y === 20);
r.push(vb.baseVal.width === 300);
r.push(vb.animVal.height === 400);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true<", result);
    }

    [Fact]
    public void Svg_Text_Metric_Stubs_Estimate_From_Content_And_FontSize()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var text = document.createElementNS(svgns, 'text');
text.setAttribute('font-size', '20');
text.textContent = 'abcde';           // 5 chars
var r = [];
r.push(text.getNumberOfChars() === 5);
r.push(text.getComputedTextLength() === 5 * 20 * 0.6);      // 60
r.push(text.getSubStringLength(0, 2) === 2 * 20 * 0.6);     // 24
r.push(text.getStartPositionOfChar(0).x === 0);
r.push(text.getEndPositionOfChar(0).x === 1 * 20 * 0.6);    // 12
r.push(text.getRotationOfChar(0) === 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true,true,true,true,true<", result);
    }

    [Fact]
    public void Svg_CurrentTime_Round_Trips_Through_The_Timeline()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var svg = document.createElementNS(svgns, 'svg');
var r = [];
r.push(svg.getCurrentTime() === 0);
svg.setCurrentTime(2.5);
r.push(svg.getCurrentTime() === 2.5);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">true,true<", result);
    }
}
