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
    public void ContentSized_Modal_Is_Not_Stretched_To_The_Viewport()
    {
        // No explicit size → the UA centring inset default is suppressed on the auto axes, so the modal
        // keeps its natural (content) size rather than stretching to fill the viewport.
        var (_, _, width, height) = ShowModalRect("", "short");

        Assert.True(width < ViewportWidth, $"content-sized modal width {width} should not fill the viewport");
        Assert.True(height < ViewportHeight, $"content-sized modal height {height} should not fill the viewport");
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
    public void Definite_Width_Auto_Height_Centers_Only_Horizontally()
    {
        var (left, top, width, _) = ShowModalRect("dialog { width: 300px; }", "one line");

        // Horizontal axis has a definite size → centred.
        Assert.Equal((ViewportWidth - width) / 2, left, 1);
        // Vertical axis is content-sized → keeps its static top (near the top of the viewport), not centred.
        Assert.True(top < ViewportHeight / 4, $"auto-height modal top {top} should stay near the viewport top");
    }
}
