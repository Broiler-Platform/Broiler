using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The experimental CSS Grid Level 3 <c>grid-lanes</c> &lt;display-inside&gt;
/// keyword is not shipped unflagged by any reference browser, so
/// <c>display: grid-lanes</c> (and the two-value <c>inline grid-lanes</c>) is an
/// invalid declaration: reference browsers drop it and the element keeps its
/// default display. These tests lock in that fallback — a <c>&lt;div&gt;</c>
/// grid-lanes container lays its children out as a block, not a grid — together
/// with the CSS Sizing 4 <c>aspect-ratio</c> the block fallback still honours
/// (the grid-lanes container derives its auto block size from its used width, so
/// it renders as an aspect-ratio square) and the definite-height percentage
/// resolution the fallback depends on (a block child's <c>height:%</c> against a
/// fixed- or aspect-ratio-height block parent).
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
            .Where(a => Math.Abs(a.Expected - a.Actual) > 0.5)
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
    /// CSS Sizing 4 aspect-ratio on the dropped grid-lanes container: reference
    /// browsers drop <c>display: grid-lanes</c> to <c>block</c> (issue #1218) but
    /// still honour <c>aspect-ratio</c>. A block with an explicit inline size and a
    /// preferred aspect ratio derives its auto block size from that width, so a
    /// grid-lanes box is sized to the ratio — <c>grid-template-*</c> is inert on the
    /// block and does not enter the calculation. (The css-grid/grid-lanes/
    /// track-sizing/auto-repeat WPT cluster renders exactly this square; Broiler
    /// previously ignored aspect-ratio and rendered a min-height-tall bar, matching
    /// the reference by only ~8%.)
    /// </summary>
    [Theory]
    [InlineData("1/1", 200, 200, 200)]
    [InlineData("2/1", 200, 200, 100)]
    [InlineData("1/2", 100, 100, 200)]
    public void GridLanesContainer_AspectRatio_DerivesHeightFromWidth(
        string ratio, int width, int expectedWidth, int expectedHeight)
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + $".gl{{display:inline grid-lanes;aspect-ratio:{ratio};width:{width}px;"
            + "grid-template-columns:repeat(auto-fill,50px);position:relative}}"
            + "</style></head><body style=\"margin:0\">"
            + "<div class=\"gl\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + $"data-expected-width=\"{expectedWidth}\" data-expected-height=\"{expectedHeight}\"></div>"
            + "</body></html>";
        AssertCheckLayout(html);
    }

    /// <summary>
    /// The real grid-lanes auto-repeat scenario: the container's inline size is
    /// <c>auto</c>, so as an in-flow block it fills its containing block's width,
    /// then the 1/1 aspect-ratio makes it a square that tall. A 180px-wide parent
    /// yields a 180×180 grid-lanes square — the fill-then-square path the reference
    /// browser takes, and the reason the auto-repeat references are viewport-wide
    /// squares rather than the small track-count squares #1230 assumed.
    /// </summary>
    [Fact]
    public void GridLanesContainer_AspectRatio_AutoWidthFillsThenSquares()
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".wrap{width:180px;position:relative}"
            + ".gl{display:inline grid-lanes;aspect-ratio:1/1;min-height:60px;"
            + "grid-template-columns:repeat(auto-fill,50px)}"
            + "</style></head><body style=\"margin:0\"><div class=\"wrap\">"
            + "<div class=\"gl\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + "data-expected-width=\"180\" data-expected-height=\"180\"></div>"
            + "</div></body></html>";
        AssertCheckLayout(html);
    }

    /// <summary>
    /// CSS2.1 §10.7: a definite <c>min-height</c> floors the transferred
    /// aspect-ratio height. A 50px-wide 1/1 box would be 50px tall, but a 200px
    /// min-height wins, so the box is 50×200 (an intentionally non-square result
    /// that pins the clamp order — aspect-ratio first, then min-height).
    /// </summary>
    [Fact]
    public void GridLanesContainer_AspectRatio_MinHeightFloorsSquare()
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".gl{display:grid-lanes;aspect-ratio:1/1;width:50px;min-height:200px;position:relative}"
            + "</style></head><body style=\"margin:0\">"
            + "<div class=\"gl\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + "data-expected-width=\"50\" data-expected-height=\"200\"></div>"
            + "</body></html>";
        AssertCheckLayout(html);
    }

    /// <summary>
    /// A percentage-height child resolves against the container's transferred
    /// aspect-ratio height, which is definite even though the container's own
    /// <c>height</c> is <c>auto</c> — the reference browser sizes a filling
    /// <c>height:100%</c> child to the aspect-ratio square. Here a 120px-wide 1/1
    /// grid-lanes container is 120×120, so its <c>height:100%</c> child is 120 tall.
    /// </summary>
    [Fact]
    public void GridLanesContainer_AspectRatio_PercentageHeightChildFillsSquare()
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".gl{display:inline grid-lanes;aspect-ratio:1/1;width:120px;position:relative}"
            + "</style></head><body style=\"margin:0\">"
            + "<div class=\"gl\">"
            + "<div id=\"c\" style=\"height:100%\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + "data-expected-width=\"120\" data-expected-height=\"120\"></div>"
            + "</div></body></html>";
        AssertCheckLayout(html);
    }

    /// <summary>
    /// An explicit block <c>height</c> suppresses the aspect-ratio transfer (CSS
    /// Sizing 4 §4 only derives an <em>auto</em> block size from the ratio). Here
    /// the author <c>width:120px</c>/<c>height:80px</c> both win, so the container
    /// is exactly 120×80 — not the 120×120 aspect-ratio square — proving the ratio
    /// never overrides an author-specified height.
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
