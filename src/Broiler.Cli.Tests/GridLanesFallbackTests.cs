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
    /// CSS Grid Level 3 (grid-lanes) track sizing: a <c>display: grid-lanes</c>
    /// container with an <c>aspect-ratio</c> and a definite <c>min-height</c>
    /// resolves to a square even though the display keyword is dropped to block.
    /// The inline axis is the grid axis: the block <c>min-height</c> (60px)
    /// transfers through the 1/1 aspect-ratio into a 60px minimum inline size,
    /// which drives <c>grid-template-columns: repeat(auto-fill, 50px)</c> to two
    /// tracks (100px), then the aspect-ratio makes the block size 100px → a
    /// 100×100 square. The mirror test declares the tracks on the block (masonry)
    /// axis via <c>grid-template-rows</c>, which does not multiply the block size,
    /// so only the 60px min-height applies → a 60×60 square. Matches the committed
    /// WPT references (<c>ref-filled-green-100px-square-only</c> /
    /// <c>row-auto-repeat-003-ref</c>). Without the sizing, the block fallback
    /// fills the viewport width and both assertions fail.
    /// </summary>
    [Theory]
    [InlineData("grid-template-columns: repeat(auto-fill, 50px)", 100, 100)]
    [InlineData("grid-template-rows: repeat(auto-fill, 50px)", 60, 60)]
    public void GridLanesContainer_AspectRatioAutoRepeat_SizesToSquare(
        string template, int expectedWidth, int expectedHeight)
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".gl{display:inline grid-lanes;aspect-ratio:1/1;min-height:60px;"
            + template + ";position:relative}"
            + "</style></head><body style=\"margin:0\">"
            + "<div class=\"gl\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + $"data-expected-width=\"{expectedWidth}\" data-expected-height=\"{expectedHeight}\"></div>"
            + "</body></html>";
        AssertCheckLayout(html);
    }

    /// <summary>
    /// The <c>intrinsic-auto-repeat</c> variant: an <c>auto</c>-sized track takes
    /// its size from the widest item, so a 50px-wide item still yields two 50px
    /// columns (100×100), while a row-axis (masonry) auto template collapses to
    /// the 60px min-height (60×60).
    /// </summary>
    [Theory]
    [InlineData("grid-template-columns: repeat(auto-fill, auto)",
        "<div style=\"width:50px;height:1px;visibility:hidden\">x</div>", 100, 100)]
    [InlineData("grid-template-rows: repeat(auto-fill, auto)",
        "<div style=\"width:1px;height:50px\"></div>", 60, 60)]
    public void GridLanesContainer_AspectRatioIntrinsicAutoRepeat_SizesToSquare(
        string template, string child, int expectedWidth, int expectedHeight)
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".gl{display:inline grid-lanes;aspect-ratio:1/1;min-height:60px;"
            + template + ";position:relative}"
            + "</style></head><body style=\"margin:0\">"
            + "<div class=\"gl\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + $"data-expected-width=\"{expectedWidth}\" data-expected-height=\"{expectedHeight}\">"
            + child + "</div>"
            + "</body></html>";
        AssertCheckLayout(html);
    }

    /// <summary>
    /// The aspect-ratio sizing is gated: a grid-lanes container with an explicit
    /// (definite) size keeps the plain block fallback. Here the explicit
    /// <c>width</c>/<c>height</c> win, so the container is exactly 120×80 — not an
    /// aspect-ratio square — proving the path never overrides an author size (were
    /// the grid-lanes sizing to fire, the aspect-ratio would force a square width,
    /// not 120px).
    /// </summary>
    [Fact]
    public void GridLanesContainer_ExplicitSize_KeepsAuthorDimensions()
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".gl{display:inline grid-lanes;aspect-ratio:1/1;min-height:60px;"
            + "grid-template-columns:repeat(auto-fill,50px);width:120px;height:80px;position:relative}"
            + "</style></head><body style=\"margin:0\">"
            + "<div class=\"gl\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + "data-expected-width=\"120\" data-expected-height=\"80\"></div>"
            + "</body></html>";
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
