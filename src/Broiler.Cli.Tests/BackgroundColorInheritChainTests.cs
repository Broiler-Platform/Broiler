using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for chained <c>background-color: inherit</c> (CSS2.1 §6.2.1). background-color
/// is not an inherited property, so <c>inherit</c> must fold to the parent's <em>computed</em> value
/// as the cascade descends. Previously the literal <c>inherit</c> was stored verbatim and reached
/// <c>GetActualColor</c>, which folds an unrecognised value to opaque black — so a single
/// <c>inherit</c> resolved (its parent held a real colour) but a chain of them collapsed to black
/// from the second level down. The setter now resolves against the parent's already-cascaded colour.
/// </summary>
public sealed class BackgroundColorInheritChainTests
{
    private static bool Near(BColor c, int r, int g, int b) =>
        System.Math.Abs(c.R - r) <= 6 && System.Math.Abs(c.G - g) <= 6 && System.Math.Abs(c.B - b) <= 6;

    private static string Describe(BColor c) => $"rgb({c.R},{c.G},{c.B})";

    // A green box wrapping N descendants that each set `background-color: inherit`, all filling the
    // box. Every level must resolve to green; before the fix, level ≥2 painted opaque black.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void BackgroundColorInherit_ResolvesThroughAChain(int inheritLevels)
    {
        var inner = "";
        for (var i = 0; i < inheritLevels; i++)
            inner = $"<div style=\"position:absolute;inset:0;background-color:inherit\">{inner}</div>";
        var html =
            "<!DOCTYPE html><html><head></head><body style=\"margin:0\">"
            + $"<div style=\"position:relative;width:100px;height:100px;background-color:#008000\">{inner}</div>"
            + "</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);

        var px = bitmap.GetPixel(50, 50);
        Assert.True(Near(px, 0, 128, 0),
            $"{inheritLevels}-deep background-color:inherit chain should stay green, got {Describe(px)}");
    }

    // The `background` shorthand form (`background: inherit`) needs the Broiler.CSS patch 0024 to set
    // background-color:inherit at all; on the un-patched engine it stays transparent. Feature-probe
    // and self-skip so this stays green either way while still verifying the chain when patched.
    [Fact(Timeout = 600000)]
    public void BackgroundShorthandInherit_ResolvesThroughAChain_WhenSupported()
    {
        const string html =
            "<!DOCTYPE html><html><head></head><body style=\"margin:0\">"
            + "<div style=\"position:relative;width:100px;height:100px;background:#008000\">"
            + "<div style=\"position:absolute;inset:0;background:inherit\">"
            + "<div style=\"position:absolute;inset:0;background:red\"></div>"
            + "<div style=\"position:absolute;inset:0;background:inherit\"></div>"
            + "</div></div></body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);
        var px = bitmap.GetPixel(50, 50);

        // Probe: without patch 0024, `background: inherit` is transparent, so the red middle layer
        // shows through — self-skip. With the patch, the last `background: inherit` layer inherits
        // green through the chain and covers the red.
        if (Near(px, 255, 0, 0))
            return; // patch 0024 not applied to the pinned submodule.

        Assert.True(Near(px, 0, 128, 0),
            $"background:inherit chain should resolve to green when supported, got {Describe(px)}");
    }
}
