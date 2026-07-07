using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Diagnostic #4: the DomBridge evaluates the <c>check-layout-th.js</c>
/// <c>data-offset-*</c> / <c>data-expected-*</c> assertions against its computed
/// box geometry so the WPT runner can report precise layout diffs.
/// </summary>
public sealed class CheckLayoutAssertionTests
{
    [Fact]
    public void Evaluates_One_Assertion_Per_Data_Attribute_With_Expected_Values()
    {
        const string html =
            "<!DOCTYPE html><html><body style=\"margin:0\">" +
            "<div id=\"box\" title=\"t\" style=\"position:absolute; left:10px; top:20px; width:100px; height:40px\" " +
            "data-offset-x=\"10\" data-offset-y=\"20\" data-expected-width=\"100\" data-expected-height=\"40\"></div>" +
            "</body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///check-layout.html");

        var assertions = bridge.EvaluateCheckLayoutAssertions();

        // One entry per declared data-* attribute, all on the same element.
        Assert.Equal(4, assertions.Count);
        Assert.All(assertions, a => Assert.Equal("div#box[title=t]", a.Element));

        var byProperty = assertions.ToDictionary(a => a.Property);
        Assert.Equal(10, byProperty["offset-x"].Expected);
        Assert.Equal(20, byProperty["offset-y"].Expected);
        Assert.Equal(100, byProperty["width"].Expected);
        Assert.Equal(40, byProperty["height"].Expected);

        // The computed actual is a finite number for every assertion (the bridge
        // produced geometry rather than NaN/throwing).
        Assert.All(assertions, a => Assert.False(double.IsNaN(a.Actual)));
    }

    [Fact]
    public void Computes_Border_Box_Size_For_Explicit_Width_And_Height()
    {
        // An element with explicit width/height and no border/padding has a
        // border-box size equal to those lengths — a deterministic offsetWidth/Height.
        const string html =
            "<!DOCTYPE html><html><body style=\"margin:0\">" +
            "<div style=\"width:120px; height:30px\" " +
            "data-expected-width=\"120\" data-expected-height=\"30\"></div>" +
            "</body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///check-layout-size.html");

        var byProperty = bridge.EvaluateCheckLayoutAssertions().ToDictionary(a => a.Property);

        Assert.True(Math.Abs(120 - byProperty["width"].Actual) <= 1.0,
            $"offsetWidth expected ~120, got {byProperty["width"].Actual}");
        Assert.True(Math.Abs(30 - byProperty["height"].Actual) <= 1.0,
            $"offsetHeight expected ~30, got {byProperty["height"].Actual}");
    }

    [Fact]
    public void Returns_Empty_When_No_Check_Layout_Attributes()
    {
        const string html = "<!DOCTYPE html><html><body><div>plain</div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///plain.html");

        Assert.Empty(bridge.EvaluateCheckLayoutAssertions());
    }
}
