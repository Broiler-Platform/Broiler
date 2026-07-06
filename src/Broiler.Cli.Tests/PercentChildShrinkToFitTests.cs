using System.Linq;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS Sizing 3 §5.1: a child's percentage width resolves against the size being
/// computed, so it is treated as <c>auto</c> when computing a shrink-to-fit
/// container's intrinsic width. Without this a <c>width:100%</c> child resolves
/// against the containing block and balloons a float / inline-block / abspos
/// container (and an auto-fill grid) to the full available width instead of its
/// content. Verified via the WPT check-layout <c>data-offset-*</c> geometry so no
/// pixel reference is needed. (Regression guard for the shrink-to-fit fix in
/// CssBoxHelper.GetMinMaxSumWords + CssBox.ComputeShrinkToFitWidth.)
/// </summary>
public sealed class PercentChildShrinkToFitTests
{
    private static void AssertCheckLayout(string html, string url)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, url);
        var assertions = bridge.EvaluateCheckLayoutAssertions();
        var failures = assertions
            .Where(a => System.Math.Abs(a.Expected - a.Actual) > 0.5)
            .Select(a => $"{a.Element} {a.Property}: expected {a.Expected}, got {a.Actual:0.##}")
            .ToList();
        Assert.True(assertions.Count > 0, "no check-layout assertions were evaluated");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{assertions.Count} assertions failed:\n" + string.Join("\n", failures));
    }

    // A shrink-to-fit container ('mode') wrapping a 120px fixed block and a
    // width:100% block must shrink to the fixed block's 120px, not the viewport:
    // the % child contributes as auto (its content, here 0) to the intrinsic width.
    private static string Doc(string mode) =>
        "<!DOCTYPE html><html><head><style>"
        + "#f{" + mode + "}.fixed{width:120px;height:20px;}.pct{width:100%;height:20px;}"
        + "</style></head><body style=\"margin:0\"><div style=\"position:relative\">"
        + "<div id=\"f\" data-offset-x=\"0\" data-offset-y=\"0\" "
        + "data-expected-width=\"120\" data-expected-height=\"40\">"
        + "<div class=\"fixed\"></div><div class=\"pct\"></div></div></div></body></html>";

    [Fact]
    public void Float_WithPercentChild_ShrinksToContent() =>
        AssertCheckLayout(Doc("float:left;"), "file:///stf-float-pct.html");

    [Fact]
    public void InlineBlock_WithPercentChild_ShrinksToContent() =>
        AssertCheckLayout(Doc("display:inline-block;"), "file:///stf-inline-block-pct.html");

    [Fact]
    public void Abspos_WithPercentChild_ShrinksToContent() =>
        AssertCheckLayout(Doc("position:absolute;"), "file:///stf-abspos-pct.html");

    /// <summary>
    /// A floated auto-fill grid with a definite <c>min-width</c> and a grid item
    /// sized <c>width:100%</c>: the % item must not inflate the grid's shrink-to-fit
    /// width, so the grid resolves to <c>min-width</c> (300 → three 100px columns)
    /// and the item at <c>grid-column:-2</c> (negative line = second-to-last of the
    /// explicit grid) lands in the last column at x=200.
    /// </summary>
    [Fact]
    public void AutoFillGrid_WithPercentItem_ResolvesMinWidthColumns()
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + "#g{display:grid;float:left;min-width:300px;"
            + "grid-template-columns:repeat(auto-fill,100px);grid-template-rows:50px;}"
            + ".i{width:100%;height:100%;}"
            + "</style></head><body style=\"margin:0\">"
            + "<div id=\"g\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + "data-expected-width=\"300\" data-expected-height=\"50\">"
            + "<div id=\"it\" class=\"i\" style=\"grid-column:-2;grid-row:1;\" "
            + "data-offset-x=\"200\" data-offset-y=\"0\" "
            + "data-expected-width=\"100\" data-expected-height=\"50\"></div>"
            + "</div></body></html>";
        AssertCheckLayout(html, "file:///stf-autofill-grid-pct.html");
    }
}
