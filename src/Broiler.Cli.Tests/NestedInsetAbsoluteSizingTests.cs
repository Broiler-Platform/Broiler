using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for the block size of an absolutely-positioned box whose containing block is
/// itself an inset-sized absolute (CSS2.1 §10.6.4 / CSS Position 3). When a
/// <c>position:absolute; inset:0</c> box nests inside another such box, the ancestor's block size
/// is not yet settled when the descendant is positioned (heights resolve bottom-up), so
/// <c>GetAbsoluteContainingBlockPaddingBox</c> collapsed the containing-block height to ~0 and the
/// descendant got zero height. The fix resolves an auto-height, top+bottom-anchored containing
/// block from <em>its</em> containing block, recursing up to a definite/viewport-sized ancestor.
/// </summary>
public sealed class NestedInsetAbsoluteSizingTests
{
    private static string InnerHeight(int levels)
    {
        var inner = "<div id=t style=\"position:absolute;inset:0\"></div>";
        for (var i = 0; i < levels; i++)
            inner = $"<div style=\"position:absolute;inset:0\">{inner}</div>";
        var html =
            "<!DOCTYPE html><html><head></head><body>"
            + $"<div style=\"position:relative;width:200px;height:200px\">{inner}</div>"
            + "</body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///t.html");
        return context.Eval(
            "var r=document.getElementById('t').getBoundingClientRect(); r.width+'x'+r.height").ToString();
    }

    // One inset-sized wrapper (the containing block is the sized relative box) already worked;
    // two and three levels of nesting are the regression — each inner box must still fill 200×200.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void InsetZeroAbsolute_FillsThroughNestedInsetSizedContainingBlocks(int wrapperLevels)
    {
        Assert.Equal("200x200", InnerHeight(wrapperLevels));
    }

    // A non-anchored (top only, bottom auto) absolute descendant must NOT be stretched by the fix —
    // it stays content-sized (0 height for an empty box), so the fix is scoped to top+bottom anchors.
    [Fact]
    public void TopOnlyAbsolute_InInsetSizedContainingBlock_IsNotStretched()
    {
        const string html =
            "<!DOCTYPE html><html><head></head><body>"
            + "<div style=\"position:relative;width:200px;height:200px\">"
            + "<div style=\"position:absolute;inset:0\">"
            + "<div id=t style=\"position:absolute;top:0;left:0\"></div>"
            + "</div></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///t.html");
        var height = context.Eval(
            "document.getElementById('t').getBoundingClientRect().height").ToString();

        Assert.Equal("0", height);
    }
}
