using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the element
/// box-model / scrolling interface (<see cref="ElementGeometryBinding"/>) — the <c>client*</c>/
/// <c>offset*</c>/<c>scroll*</c> metrics, <c>offsetParent</c>, <c>getBoundingClientRect</c>/
/// <c>getClientRects</c>, and the imperative <c>scrollTop</c>/<c>scrollLeft</c>/<c>scroll</c>/<c>scrollTo</c>/
/// <c>scrollBy</c>/<c>scrollIntoView</c>/<c>scrollParent</c> API. This is the one Phase 3 family that reads
/// the live layout, so it depends on the bridge through the deliberately wide <see cref="IElementGeometryHost"/>
/// contract (the "wide-explicit-host" template). Was the bridge's box-model block plus the
/// <c>JsElementInterfacesGetScrollTop072Core</c>..<c>ScrollParent085Core</c> callbacks.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class ElementGeometryBindingModuleTests
{
    private static string Eval(string bodyHtml, string expr)
    {
        var html = "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head><body>" +
                   bodyHtml + "</body></html>";
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///geometry.html");
        return ctx.Eval(expr).ToString();
    }

    [Fact]
    public void Geometry_Feature_Module_And_Host_Contract_Are_Internal()
    {
        var moduleType = typeof(ElementGeometryBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.True(moduleType.IsAbstract && moduleType.IsSealed); // C# static class

        Assert.False(typeof(IElementGeometryHost).IsPublic);
        Assert.True(typeof(IElementGeometryHost).IsAssignableFrom(typeof(DomBridge)));
    }

    [Fact]
    public void Geometry_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(DomBridge);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var name in new[]
                 {
                     "JsElementInterfacesGetScrollTop072Core", "JsElementInterfacesSetScrollTop073Core",
                     "JsElementInterfacesGetScrollLeft074Core", "JsElementInterfacesSetScrollLeft075Core",
                     "JsElementInterfacesGetOffsetParent078Core", "JsElementInterfacesGetBoundingClientRect079Core",
                     "JsElementInterfacesGetClientRects080Core", "JsElementInterfacesScrollIntoView081Core",
                     "JsElementInterfacesScroll082Core", "JsElementInterfacesScrollTo083Core",
                     "JsElementInterfacesScrollBy084Core", "JsElementInterfacesScrollParent085Core",
                 })
        {
            Assert.Null(bridge.GetMethod(name, all));
        }
    }

    [Fact]
    public void Box_Model_Metrics_Report_Own_Css_Pixels()
    {
        var body = "<div id='a' style='width:100px;height:40px'></div>";
        Assert.Equal("100,40", Eval(body,
            "(function(){var a=document.getElementById('a');return a.offsetWidth+','+a.offsetHeight;})()"));
    }

    [Fact]
    public void GetBoundingClientRect_Returns_A_Full_DomRect()
    {
        var body = "<div id='a' style='width:100px;height:40px'></div>";
        // width/height mirror offset*, and right/bottom derive from left+width / top+height.
        Assert.Equal("100,40,true,true", Eval(body,
            "(function(){var r=document.getElementById('a').getBoundingClientRect();" +
            "return r.width+','+r.height+','+((r.right-r.left)===r.width)+','+((r.bottom-r.top)===r.height);})()"));
    }

    [Fact]
    public void ScrollTop_Round_Trips_Through_The_Binding()
    {
        // A scroll container with overflowing content; setting scrollTop then reading it back returns the value.
        var body = "<div id='s' style='width:50px;height:50px;overflow:auto'>" +
                   "<div style='width:200px;height:200px'></div></div>";
        Assert.Equal("0|30", Eval(body,
            "(function(){var s=document.getElementById('s');var before=s.scrollTop;s.scrollTop=30;" +
            "return before+'|'+s.scrollTop;})()"));
    }

    [Fact]
    public void GetClientRects_Returns_An_Array()
    {
        var body = "<div id='a' style='width:100px;height:40px'></div>";
        Assert.Equal("true,1", Eval(body,
            "(function(){var rects=document.getElementById('a').getClientRects();" +
            "return Array.isArray(rects)+','+rects.length;})()"));
    }
}
