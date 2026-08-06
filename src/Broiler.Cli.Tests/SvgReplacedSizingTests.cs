using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// SVG 2 §8.2 ("Establishing a new viewport"): the used size of an outer
/// <c>&lt;svg&gt;</c> in an HTML document. Its <c>width</c>/<c>height</c>
/// presentation attributes default to <c>auto</c> and a <c>viewBox</c> gives it an
/// intrinsic aspect ratio but no intrinsic size, so CSS sizes it as a replaced
/// element: an auto width fills the containing block and the ratio transfers it
/// into the auto height. Broiler used to size every such element to the 300×150
/// default object size, which shrank a viewport-filling drawing to a small
/// letterboxed square (WPT <c>inert/inert-svg-hittest</c>,
/// <c>accessibility/svg-mouse-listener</c>,
/// <c>svg/animations/svgrect-animation-invalid-value-1</c>).
/// <para>Expected geometry throughout is Chromium's, measured on the same markup.
/// </para>
/// </summary>
public sealed class SvgReplacedSizingTests
{
    private static void AssertCheckLayout(string html)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///svg-sizing.html");

        var assertions = bridge.EvaluateCheckLayoutAssertions();
        var failures = assertions
            .Where(a => Math.Abs(a.Expected - a.Actual) > 0.5)
            .Select(a => $"{a.Element} {a.Property}: expected {a.Expected}, got {a.Actual:0.##}")
            .ToList();

        Assert.True(assertions.Count > 0, "no check-layout assertions were evaluated");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{assertions.Count} assertions failed:\n" + string.Join("\n", failures));
    }

    /// <summary>Wraps one <c>&lt;svg&gt;</c> in a fixed-width containing block and
    /// asserts its used border-box size.</summary>
    private static void AssertSvgSize(string svgAttributes, int containerWidth, int expectedWidth, int expectedHeight)
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + $".wrap{{width:{containerWidth}px;position:relative}}"
            + "</style></head><body style=\"margin:0\"><div class=\"wrap\">"
            + $"<svg {svgAttributes} data-offset-x=\"0\" data-offset-y=\"0\" "
            + $"data-expected-width=\"{expectedWidth}\" data-expected-height=\"{expectedHeight}\"></svg>"
            + "</div></body></html>";
        AssertCheckLayout(html);
    }

    /// <summary>
    /// With both axes auto and a viewBox to give the ratio, the width fills the
    /// containing block and the height follows from the ratio — the case that made
    /// the WPT tests above render a 150px square instead of a full-width drawing.
    /// </summary>
    [Theory]
    [InlineData("0 0 500 500", 400, 400, 400)]   // square
    [InlineData("0 0 500 250", 400, 400, 200)]   // wide
    [InlineData("0 0 250 500", 400, 400, 800)]   // tall
    [InlineData("-10 -10 500 250", 400, 400, 200)]  // the origin does not shape the ratio
    public void AutoSize_WithViewBox_FillsContainingBlockAndTransfersRatio(
        string viewBox, int containerWidth, int expectedWidth, int expectedHeight)
    {
        AssertSvgSize($"viewBox=\"{viewBox}\"", containerWidth, expectedWidth, expectedHeight);
    }

    /// <summary>
    /// With no ratio to transfer, each auto axis independently falls back to the
    /// CSS default object size (300×150) — so a lone <c>width</c> attribute leaves
    /// the height at 150, and a lone <c>height</c> attribute leaves the width at
    /// 300.
    /// </summary>
    [Theory]
    [InlineData("", 300, 150)]
    [InlineData("width=\"100\"", 100, 150)]
    [InlineData("height=\"100\"", 300, 100)]
    [InlineData("width=\"100\" height=\"70\"", 100, 70)]
    public void NoViewBox_FallsBackToDefaultObjectSizePerAxis(
        string attributes, int expectedWidth, int expectedHeight)
    {
        AssertSvgSize(attributes, 400, expectedWidth, expectedHeight);
    }

    /// <summary>
    /// A <c>viewBox</c> only establishes a ratio when it is exactly four unitless
    /// numbers with a positive width and height; every other form leaves the
    /// element with no ratio and so at the default object size.
    /// </summary>
    [Theory]
    [InlineData("0 0 0 0")]          // degenerate
    [InlineData("0 0 -500 250")]     // negative extent
    [InlineData("0 0 500")]          // too few numbers
    [InlineData("0 0 500px 250px")]  // units are not valid viewBox syntax
    [InlineData("foo")]              // not numbers at all
    public void MalformedViewBox_EstablishesNoRatio(string viewBox)
    {
        AssertSvgSize($"viewBox=\"{viewBox}\"", 400, 300, 150);
    }

    /// <summary>Commas are viewBox separators just like whitespace.</summary>
    [Fact]
    public void ViewBox_AcceptsCommaSeparators()
    {
        AssertSvgSize("viewBox=\"0,0,500,250\"", 400, 400, 200);
    }

    /// <summary>
    /// One specified axis gives the other through the ratio, in both directions:
    /// a definite width yields the height, and a definite height yields the width.
    /// </summary>
    [Theory]
    [InlineData("width=\"100\" viewBox=\"0 0 500 250\"", 100, 50)]
    [InlineData("height=\"100\" viewBox=\"0 0 500 250\"", 200, 100)]
    [InlineData("style=\"width:100px\" viewBox=\"0 0 500 500\"", 100, 100)]
    [InlineData("style=\"height:100px\" viewBox=\"0 0 500 500\"", 100, 100)]
    public void OneSpecifiedAxis_TransfersThroughRatio(
        string attributes, int expectedWidth, int expectedHeight)
    {
        AssertSvgSize(attributes, 400, expectedWidth, expectedHeight);
    }

    /// <summary>
    /// A percentage width resolves against the containing block first; the ratio
    /// then transfers the <em>used</em> width, not the percentage.
    /// </summary>
    [Fact]
    public void PercentageWidth_TransfersUsedWidth()
    {
        AssertSvgSize("width=\"50%\" viewBox=\"0 0 500 250\"", 400, 200, 100);
    }

    /// <summary>
    /// <c>max-width</c> clamps the used width before the transfer, so the ratio is
    /// applied to the clamped width (CSS2.1 §10.4 precedes Sizing 4 §4).
    /// </summary>
    [Fact]
    public void MaxWidth_ClampsBeforeTheTransfer()
    {
        AssertSvgSize("style=\"max-width:100px\" viewBox=\"0 0 500 250\"", 400, 100, 50);
    }

    /// <summary>
    /// An author <c>aspect-ratio</c> is a preferred ratio that replaces the natural
    /// one the viewBox supplies (CSS Sizing 4 §4): a 1:1 viewBox drawn in a box
    /// declared 2/1 gives a 400×200 box, not a 400×400 one.
    /// </summary>
    [Fact]
    public void AuthorAspectRatio_OverridesTheViewBoxRatio()
    {
        AssertSvgSize("style=\"width:400px;aspect-ratio:2/1\" viewBox=\"0 0 500 500\"", 400, 400, 200);
    }

    /// <summary>
    /// <c>width="auto"</c> is the keyword, not a length: it leaves the axis auto
    /// rather than becoming the unparseable "autopx" that collapsed the box to zero
    /// width and painted nothing.
    /// </summary>
    [Fact]
    public void AutoKeywordAttribute_LeavesTheAxisAuto()
    {
        AssertSvgSize("width=\"auto\" viewBox=\"0 0 500 250\"", 400, 400, 200);
    }
}
