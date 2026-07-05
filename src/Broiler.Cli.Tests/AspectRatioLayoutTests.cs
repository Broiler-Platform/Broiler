using System.Linq;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS Sizing 4 §4 <c>aspect-ratio</c> for ordinary (non-replaced) boxes: an
/// in-flow block-level box with a preferred aspect ratio and an <c>auto</c> block
/// size derives its used height from its used inline size. These tests are the
/// general-feature companion to <see cref="GridLanesFallbackTests"/> (which
/// exercises the same path via the dropped <c>display: grid-lanes</c> fallback);
/// they drive the check-layout <c>data-expected-*</c> geometry through the real
/// engine, independent of grid-lanes.
/// </summary>
public sealed class AspectRatioLayoutTests
{
    private static void AssertCheckLayout(string html)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///aspect-ratio.html");

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
    /// A plain block with an explicit width and a preferred aspect ratio derives
    /// its auto height from the width (height = width ÷ ratio). Covers a square,
    /// a wide (2:1) and a tall (1:2) ratio so the width→height transfer direction
    /// is pinned unambiguously.
    /// </summary>
    [Theory]
    [InlineData("1/1", 200, 200, 200)]
    [InlineData("2/1", 200, 200, 100)]
    [InlineData("1/2", 100, 100, 200)]
    [InlineData("3/2", 300, 300, 200)]
    public void Block_AspectRatio_DerivesHeightFromExplicitWidth(
        string ratio, int width, int expectedWidth, int expectedHeight)
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + $".b{{display:block;width:{width}px;aspect-ratio:{ratio};position:relative}}"
            + "</style></head><body style=\"margin:0\">"
            + "<div class=\"b\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + $"data-expected-width=\"{expectedWidth}\" data-expected-height=\"{expectedHeight}\"></div>"
            + "</body></html>";
        AssertCheckLayout(html);
    }

    /// <summary>
    /// An auto-width in-flow block first fills its containing block's content
    /// width, then the aspect ratio makes it that tall: a 240px-wide parent yields
    /// a 240×80 box at 3/1.
    /// </summary>
    [Fact]
    public void Block_AspectRatio_AutoWidthFillsContainingBlockThenTransfers()
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + ".wrap{width:240px;position:relative}"
            + ".b{display:block;aspect-ratio:3/1}"
            + "</style></head><body style=\"margin:0\"><div class=\"wrap\">"
            + "<div class=\"b\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + "data-expected-width=\"240\" data-expected-height=\"80\"></div>"
            + "</div></body></html>";
        AssertCheckLayout(html);
    }

    /// <summary>
    /// The aspect-ratio applies to the box named by <c>box-sizing</c>: under
    /// <c>border-box</c> the ratio squares the border box (200×200 including the
    /// 20px padding), while under the default <c>content-box</c> it squares the
    /// content box (100×100 content → 120×120 border box with 10px padding).
    /// </summary>
    [Theory]
    [InlineData("border-box", 200, 20, 200, 200)]
    [InlineData("content-box", 100, 10, 120, 120)]
    public void Block_AspectRatio_HonoursBoxSizing(
        string boxSizing, int width, int padding, int expectedWidth, int expectedHeight)
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + $".b{{display:block;box-sizing:{boxSizing};width:{width}px;padding:{padding}px;"
            + "aspect-ratio:1/1;position:relative}}"
            + "</style></head><body style=\"margin:0\">"
            + "<div class=\"b\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + $"data-expected-width=\"{expectedWidth}\" data-expected-height=\"{expectedHeight}\"></div>"
            + "</body></html>";
        AssertCheckLayout(html);
    }

    /// <summary>
    /// A definite <c>height</c> suppresses the transfer entirely (only an
    /// <em>auto</em> block size is derived), and <c>min-/max-height</c> clamp the
    /// transferred height afterwards.
    /// </summary>
    [Theory]
    // Explicit height wins outright over the 1/1 ratio.
    [InlineData("height:40px", 100, 100, 40)]
    // min-height floors the 100px square up to 160.
    [InlineData("min-height:160px", 100, 100, 160)]
    // max-height caps the 300px square down to 90.
    [InlineData("max-height:90px", 300, 300, 90)]
    public void Block_AspectRatio_ClampedBySizeConstraints(
        string constraint, int width, int expectedWidth, int expectedHeight)
    {
        string html =
            "<!DOCTYPE html><html><head><style>"
            + $".b{{display:block;width:{width}px;aspect-ratio:1/1;{constraint};position:relative}}"
            + "</style></head><body style=\"margin:0\">"
            + "<div class=\"b\" data-offset-x=\"0\" data-offset-y=\"0\" "
            + $"data-expected-width=\"{expectedWidth}\" data-expected-height=\"{expectedHeight}\"></div>"
            + "</body></html>";
        AssertCheckLayout(html);
    }
}
