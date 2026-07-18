using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Native modal <c>&lt;dialog&gt;</c> centring: the bridge applies the HTML UA
/// <c>dialog:modal { inset:0; margin:auto }</c> default (per axis, only where the modal has a definite
/// specified size) so the layout engine's §10.3.7/§10.6.4 auto-margin resolution centres the modal in
/// the viewport — replacing the previous "position:fixed, uncentred" behaviour. A content-sized axis is
/// left untouched (no stretch), and any author inset/margin suppresses the UA default.
/// </summary>
public sealed class NativeModalCenteringTests
{
    private const double ViewportWidth = 1024;
    private const double ViewportHeight = 768;

    private static (double left, double top, double width, double height) ShowModalRect(string css, string dialogContent = "Hi")
    {
        var html = $$"""
<!DOCTYPE html>
<html><head><style>{{css}}</style></head>
<body><dialog id="d">{{dialogContent}}</dialog></body></html>
""";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///t.html");
        context.Eval("document.getElementById('d').showModal();");
        bridge.ResolveAnchorPositions();
        var raw = context.Eval(
            "var r=document.getElementById('d').getBoundingClientRect(); r.left+'|'+r.top+'|'+r.width+'|'+r.height")
            .ToString();
        var p = raw!.Split('|');
        return (double.Parse(p[0]), double.Parse(p[1]), double.Parse(p[2]), double.Parse(p[3]));
    }

    [Fact]
    public void DefiniteSize_Modal_Is_Centered_In_The_Viewport()
    {
        var (left, top, width, height) = ShowModalRect("dialog { width: 200px; height: 100px; }");

        // Centred: left = (viewport - borderBoxWidth)/2, top = (viewport - borderBoxHeight)/2.
        Assert.Equal((ViewportWidth - width) / 2, left, 1);
        Assert.Equal((ViewportHeight - height) / 2, top, 1);
        // The box is not stretched to the viewport.
        Assert.True(width < ViewportWidth);
        Assert.True(height < ViewportHeight);
    }

    [Fact]
    public void ContentSized_Modal_ShrinkWraps_And_Centers_Horizontally()
    {
        // No explicit width → the UA `width: fit-content` default shrink-wraps the modal to its content
        // width (not stretched to the viewport), and the auto margins centre it horizontally.
        var (left, _, width, height) = ShowModalRect("", "short");

        Assert.True(width < ViewportWidth, $"content-sized modal width {width} should shrink-wrap, not fill the viewport");
        Assert.True(height < ViewportHeight, $"content-sized modal height {height} should not fill the viewport");
        Assert.Equal((ViewportWidth - width) / 2, left, 1);
    }

    [Fact]
    public void Author_Positioning_Suppresses_The_Ua_Centering()
    {
        // Author places the modal explicitly → the UA centring default must not fight it.
        var (left, top, _, _) = ShowModalRect("dialog { width: 200px; height: 100px; left: 40px; top: 30px; }");

        Assert.Equal(40, left, 1);
        Assert.Equal(30, top, 1);
    }

    [Fact]
    public void ExplicitWidth_And_ExplicitHeight_Center_On_Both_Axes()
    {
        // Both axes definite → centred on both.
        var (left, top, width, height) = ShowModalRect("dialog { width: 300px; height: 120px; }", "one line");

        Assert.True(width >= 300, $"explicit 300px width plus chrome, got {width}"); // border box adds padding/border
        Assert.True(height >= 120, $"explicit 120px height plus chrome, got {height}");
        Assert.Equal((ViewportWidth - width) / 2, left, 1);
        Assert.Equal((ViewportHeight - height) / 2, top, 1);
    }

    [Fact]
    public void ContentHeight_Modal_Is_Not_Vertically_MisCentered()
    {
        // With an explicit width but content height, the modal centres horizontally and keeps its
        // natural block position (near the viewport top) — it is NOT mis-centred using a stale height.
        // (Full content-height vertical centring awaits the engine shrink-wrap of out-of-flow heights.)
        var (left, top, width, _) = ShowModalRect("dialog { width: 300px; }", "one line");

        Assert.Equal((ViewportWidth - width) / 2, left, 1);
        Assert.True(top < ViewportHeight / 4, $"content-height modal top {top} should stay near the viewport top");
    }
}
