using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression guard for WPT css-grid/nested-grid-item-block-size-001 (roadmap
/// cluster 37 / Workstream B). A replaced element (<c>&lt;img&gt;</c>) with a
/// definite block-size and an <c>aspect-ratio</c> nested inside a
/// <c>display:grid</c> item must keep that block-size — the grid's auto row is
/// sized from the item's definite height, not a stale zero measurement. Before
/// the fix the image collapsed to height 0 (rendering blank), because the real
/// §11 track pass declined for any replaced item and the fallback approximation
/// dropped the image's height.
///
/// Uses the in-process <c>data-offset-*</c>/<c>data-expected-*</c> check-layout
/// geometry (the browser-correct answer), so it reproduces the WPT condition
/// without a pixel reference. The image src is intentionally unresolved: the
/// definite block-size + aspect-ratio must size the box regardless of loading.
/// </summary>
public sealed class NestedGridItemBlockSizeTests
{
    // block-size + aspect-ratio; a px block-size keeps the expected geometry
    // viewport-independent (the WPT test uses 55vw, exercised end-to-end on CI).
    private const string ImgStyle = "img{block-size:200px;aspect-ratio:2/1;}";

    private static (double x, double y, double w, double h) ImgBox(string containerDisplay, string wrappedImg)
    {
        string style = ".container{" + containerDisplay
            + "list-style:none;padding:0px;margin:0px;}" + ImgStyle;
        string html = "<!DOCTYPE HTML><html><head><style>" + style
            + "</style></head><body style=\"margin:0\">"
            + "<ul class=\"container\"><li id=\"li\">" + wrappedImg + "</li></ul></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///nested-grid-item-block-size.html");
        var byProp = bridge.EvaluateCheckLayoutAssertions()
            .Where(a => a.Element.Contains("img"))
            .ToDictionary(a => a.Property, a => a.Actual);
        double v(string p) => byProp.TryGetValue(p, out var x) ? x : double.NaN;
        return (v("offset-x"), v("offset-y"), v("width"), v("height"));
    }

    private const string ImgTag =
        "<img id=\"img\" src=\"support/colors-8x16.png\" "
        + "data-offset-x=\"0\" data-offset-y=\"0\" data-expected-width=\"0\" data-expected-height=\"0\">";

    [Fact]
    public void ImgWithDefiniteBlockSize_NestedInGrid_KeepsHeight_LikeBlockFlow()
    {
        // Reference: the same image directly in a plain (block) list item.
        var reference = ImgBox("", ImgTag);
        // Test: the image wrapped in inline-block > display:grid inside a
        // grid-auto-flow:column list container — the WPT nesting.
        var test = ImgBox(
            "display:grid;grid-auto-flow:column;",
            "<div style=\"display:inline-block;\"><div style=\"display:grid;\">" + ImgTag + "</div></div>");

        // The nested image must not collapse: it keeps its definite block-size,
        // matching the block-flow reference in size and top-left placement.
        Assert.True(reference.h > 100, $"reference image height collapsed: {reference.h}");
        Assert.True(test.h > 100, $"nested-grid image height collapsed to {test.h} (expected ~{reference.h})");
        Assert.True(Math.Abs(test.h - reference.h) <= 1.0,
            $"nested-grid image height {test.h} != block-flow reference {reference.h}");
        Assert.True(Math.Abs(test.w - reference.w) <= 1.0,
            $"nested-grid image width {test.w} != block-flow reference {reference.w}");
        Assert.True(Math.Abs(test.x) <= 1.0, $"nested-grid image x-offset {test.x} (expected 0)");
        Assert.True(Math.Abs(test.y) <= 1.0, $"nested-grid image y-offset {test.y} (expected 0)");
    }
}
