using System.Linq;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// The experimental CSS Grid Level 3 <c>grid-lanes</c> &lt;display-inside&gt;
/// keyword is not shipped unflagged by any reference browser, so
/// <c>display: grid-lanes</c> (and the two-value <c>inline grid-lanes</c>) is an
/// invalid declaration: reference browsers drop it and the element keeps its
/// default display. These tests lock in that fallback — a <c>&lt;div&gt;</c>
/// grid-lanes container lays its children out as a block, not a grid — and the
/// definite-height percentage resolution the fallback depends on (a block child's
/// <c>height:%</c> against a fixed-height block parent).
///
/// Companion to <see cref="GridTrackLayoutTests"/>; both drive the check-layout
/// <c>data-offset-*</c>/<c>data-expected-*</c> geometry through the real engine.
/// </summary>
public sealed class GridLanesFallbackTests
{
    private static void AssertCheckLayout(string html)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///grid-lanes-fallback.html");

        var assertions = bridge.EvaluateCheckLayoutAssertions();
        var failures = assertions
            .Where(a => System.Math.Abs(a.Expected - a.Actual) > 0.5)
            .Select(a => $"{a.Element} {a.Property}: expected {a.Expected}, got {a.Actual:0.##}")
            .ToList();

        Assert.True(assertions.Count > 0, "no check-layout assertions were evaluated");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{assertions.Count} assertions failed:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// <c>display: grid-lanes</c> and <c>display: inline grid-lanes</c> on a
    /// <c>&lt;div&gt;</c> are both invalid, so the div keeps its default
    /// <c>block</c> display: its children fill the container's 200px width and
    /// stack. Were grid-lanes instead honoured as a grid, the
    /// <c>grid-template-columns: repeat(3, 80px)</c> would place the two children
    /// side-by-side in 80px columns — so the width/offset assertions distinguish
    /// the block fallback from the grid interpretation.
    /// </summary>
    [Theory]
    [InlineData("grid-lanes")]
    [InlineData("inline grid-lanes")]
    public void GridLanesContainer_FallsBackToBlock(string display)
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".c{display:" + display + ";grid-template-columns:repeat(3,80px);width:200px;position:relative}"
            + "</style></head><body style=\"margin:0\"><div class=\"c\">"
            + "<div id=\"a\" style=\"height:40px\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + "data-expected-width=\"200\" data-expected-height=\"40\"></div>"
            + "<div id=\"b\" style=\"height:30px\" data-offset-x=\"0\" data-offset-y=\"40\" "
            + "data-expected-width=\"200\" data-expected-height=\"30\"></div>"
            + "</div></body></html>";
        AssertCheckLayout(html);
    }

    /// <summary>
    /// A block child's percentage <c>height</c> resolves against a definite-height
    /// block containing block. Block heights are applied bottom-up (the parent
    /// sizes itself after its children lay out), so the containing block's height
    /// must be derived from its specification for the child percentage to resolve;
    /// otherwise the children collapse to zero and overlap. This is what a
    /// grid-lanes container relying on <c>height:100%</c> children (the WPT
    /// row-auto-repeat cluster) exercises once it falls back to block.
    /// </summary>
    [Fact]
    public void PercentageHeight_ResolvesAgainstFixedHeightBlockParent()
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".p{width:100px;height:300px;position:relative}"
            + "</style></head><body style=\"margin:0\"><div class=\"p\">"
            + "<div id=\"a\" style=\"width:50px;height:50%\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + "data-expected-width=\"50\" data-expected-height=\"150\"></div>"
            + "<div id=\"b\" style=\"width:50px;height:25%\" data-offset-x=\"0\" data-offset-y=\"150\" "
            + "data-expected-width=\"50\" data-expected-height=\"75\"></div>"
            + "</div></body></html>";
        AssertCheckLayout(html);
    }
}
