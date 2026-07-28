using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// WPT css/filter-effects/svg-filter-primitive-units-user-space: an SVG shape that references a
/// <c>&lt;filter&gt;</c> whose sole primitive is an <c>&lt;feFlood&gt;</c> is replaced by a solid
/// fill of the flood colour over the filter region (the shape's bounding box expanded by ±10%).
/// These cover the SVG-shape path (main-repo <c>SvgRenderer</c> + the document-wide
/// <c>SvgFilterTable</c>); the CSS <c>filter: url()</c> path lives in the paint walker and is
/// exercised by the WPT reftest itself.
/// </summary>
public class SvgFloodFilterTests
{
    // The feFlood filter region is the rect bbox (10,10,100,100) grown by -10%/+10% → (0,0,120,120)
    // in SVG user space, so green fills the top-left of the svg and (10,10) reads green.
    [Fact]
    public void SvgRect_WithFeFloodFilter_InSameSvg_PaintsFloodColorOverFilterRegion()
    {
        const string html = """
<!DOCTYPE html><body style="margin:0">
<svg width="150" height="150">
  <defs><filter id="f"><feFlood flood-color="green" width="100%" height="100%"/></filter></defs>
  <rect x="10" y="10" width="100" height="100" fill="red" filter="url(#f)"/>
</svg>
</body>
""";
        using var b = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);

        var inRegion = b.GetPixel(50, 50);   // inside the (0,0,120,120) flood region
        Assert.True((inRegion.R, inRegion.G, inRegion.B) == (0, 128, 0),
            $"expected green flood but got ({inRegion.R},{inRegion.G},{inRegion.B})");

        // The original red rect must be gone — feFlood ignores (replaces) its source.
        for (int y = 10; y < 110; y += 20)
            for (int x = 10; x < 110; x += 20)
            {
                var p = b.GetPixel(x, y);
                Assert.False(p.R > 200 && p.G < 60 && p.B < 60,
                    $"red source leaked through the flood at ({x},{y})");
            }
    }

    // The filter can be defined in a *different* <svg> subtree than the referencing shape — resolution
    // uses the document-wide filter table, not the per-<svg> string the renderer sees.
    [Fact]
    public void SvgRect_ResolvesFeFloodFilter_DefinedInAnotherSvg()
    {
        const string html = """
<!DOCTYPE html><body style="margin:0">
<svg width="0" height="0" style="position:absolute">
  <defs><filter id="shared"><feFlood flood-color="green" width="100%" height="100%"/></filter></defs>
</svg>
<svg width="150" height="150">
  <rect x="10" y="10" width="100" height="100" fill="red" filter="url(#shared)"/>
</svg>
</body>
""";
        using var b = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);

        var inRegion = b.GetPixel(50, 50);
        Assert.True((inRegion.R, inRegion.G, inRegion.B) == (0, 128, 0),
            $"cross-svg filter reference did not resolve — got ({inRegion.R},{inRegion.G},{inRegion.B})");
    }

    // A rect referencing an unknown / unmodelled filter renders normally (unfiltered), not blanked.
    [Fact]
    public void SvgRect_WithUnknownFilterReference_RendersUnfiltered()
    {
        const string html = """
<!DOCTYPE html><body style="margin:0">
<svg width="150" height="150"><rect x="10" y="10" width="100" height="100" fill="red" filter="url(#missing)"/></svg>
</body>
""";
        using var b = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);

        var p = b.GetPixel(50, 50);
        Assert.True(p.R > 200 && p.G < 60 && p.B < 60,
            $"expected the unfiltered red rect but got ({p.R},{p.G},{p.B})");
    }
}
