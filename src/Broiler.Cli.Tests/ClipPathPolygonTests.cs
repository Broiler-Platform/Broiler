using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>clip-path: polygon()</c> and its application to the canvas background.
///
/// Two failures were tangled together in WPT
/// <c>css/css-masking/clip-path/clip-path-document-element{,-will-change}.html</c> (issue #1544
/// problems 14 and 15, both at 1.0% match): the paint walker only understood <c>inset()</c>, so a
/// <c>polygon()</c> clip was dropped entirely, and a <c>clip-path</c> on the document element was
/// not applied to the background that element propagates to the canvas (CSS2.1 §14.2). The tests
/// render the WPT cases and their reference and assert the pixels agree.
/// </summary>
public class ClipPathPolygonTests
{
    // The WPT test's "L" shape: 50..150 square with the top-right 50x50 quadrant cut out.
    private const string LShape =
        "polygon(50px 50px, 100px 50px, 100px 100px, 150px 100px, 150px 150px, 50px 150px)";

    private static BBitmap Render(string html) =>
        HtmlRender.RenderToImageWithStyleSet(html, 300, 300);

    private static (int R, int G, int B) Rgb(BBitmap bitmap, int x, int y)
    {
        var pixel = bitmap.GetPixel(x, y);
        return (pixel.R, pixel.G, pixel.B);
    }

    private static readonly (int R, int G, int B) Green = (0, 128, 0);
    private static readonly (int R, int G, int B) White = (255, 255, 255);

    /// <summary>
    /// The document element's clip-path clips both its subtree and the background it propagated to
    /// the canvas, so the red root background never reaches the screen: a green "L" on white.
    /// </summary>
    [Fact]
    public void ClipPathOnDocumentElement_ClipsThePropagatedCanvasBackground()
    {
        using var bitmap = Render($$"""
<!DOCTYPE html>
<style>
html { background: red; clip-path: {{LShape}}; }
div { width: 500px; height: 500px; background: green; }
</style>
<div></div>
""");

        // Inside the "L": the upper arm, the foot, and the corner joining them.
        Assert.Equal(Green, Rgb(bitmap, 75, 75));
        Assert.Equal(Green, Rgb(bitmap, 75, 125));
        Assert.Equal(Green, Rgb(bitmap, 125, 125));

        // The cut-out quadrant and everything beyond the shape: the canvas, not the red background.
        Assert.Equal(White, Rgb(bitmap, 125, 75));
        Assert.Equal(White, Rgb(bitmap, 25, 25));
        Assert.Equal(White, Rgb(bitmap, 200, 200));
    }

    /// <summary>
    /// The <c>will-change: transform</c> variant of the same WPT test — promoting the root to its
    /// own compositing layer must not change what it paints.
    /// </summary>
    [Fact]
    public void ClipPathOnDocumentElement_WithWillChange_ClipsIdentically()
    {
        using var bitmap = Render($$"""
<!DOCTYPE html>
<style>
html { background: red; clip-path: {{LShape}}; will-change: transform; }
div { width: 500px; height: 500px; background: green; }
</style>
<div></div>
""");

        Assert.Equal(Green, Rgb(bitmap, 75, 75));
        Assert.Equal(Green, Rgb(bitmap, 75, 125));
        Assert.Equal(Green, Rgb(bitmap, 125, 125));
        Assert.Equal(White, Rgb(bitmap, 125, 75));
        Assert.Equal(White, Rgb(bitmap, 200, 200));
    }

    /// <summary>The WPT reference: three 50x50 squares laid out as the same "L".</summary>
    [Fact]
    public void ClipPathOnDocumentElement_MatchesTheWptReferenceRendering()
    {
        using var test = Render($$"""
<!DOCTYPE html>
<style>
html { background: red; clip-path: {{LShape}}; }
div { width: 500px; height: 500px; background: green; }
</style>
<div></div>
""");
        using var reference = Render("""
<!DOCTYPE html>
<style>
div { position: absolute; width: 50px; height: 50px; background: green; }
</style>
<div style="top: 50px; left: 50px"></div>
<div style="top: 100px; left: 50px"></div>
<div style="top: 100px; left: 100px"></div>
""");

        for (int y = 0; y < 300; y += 5)
        {
            for (int x = 0; x < 300; x += 5)
                Assert.Equal(Rgb(reference, x, y), Rgb(test, x, y));
        }
    }

    /// <summary>A polygon on an ordinary element clips that element's own painting.</summary>
    [Fact]
    public void PolygonClipPath_OnNonRootElement_ClipsToTheShape()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
body { margin: 0; }
/* A right triangle filling the lower-left half of the box. */
div { width: 100px; height: 100px; background: green; clip-path: polygon(0px 0px, 0px 100px, 100px 100px); }
</style>
<div></div>
""");

        Assert.Equal(Green, Rgb(bitmap, 20, 80));   // well inside the triangle
        Assert.Equal(White, Rgb(bitmap, 80, 20));   // the clipped-away upper-right half
    }

    /// <summary>Percentage vertices resolve against the element's own box, per axis.</summary>
    [Fact]
    public void PolygonClipPath_ResolvesPercentagesAgainstTheReferenceBox()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
body { margin: 0; }
/* The right half of a 200x100 box: x from 50% (100px) to 100%. */
div { width: 200px; height: 100px; background: green; clip-path: polygon(50% 0%, 100% 0%, 100% 100%, 50% 100%); }
</style>
<div></div>
""");

        Assert.Equal(White, Rgb(bitmap, 50, 50));
        Assert.Equal(Green, Rgb(bitmap, 150, 50));
    }

    /// <summary>A leading <c>&lt;fill-rule&gt;</c> is accepted and does not shift the vertex list.</summary>
    [Fact]
    public void PolygonClipPath_AcceptsALeadingFillRule()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
body { margin: 0; }
div { width: 100px; height: 100px; background: green; clip-path: polygon(evenodd, 0px 0px, 0px 100px, 100px 100px); }
</style>
<div></div>
""");

        Assert.Equal(Green, Rgb(bitmap, 20, 80));
        Assert.Equal(White, Rgb(bitmap, 80, 20));
    }

    /// <summary>
    /// <c>inset()</c> — the shape the paint walker already handled — still clips rectangularly.
    /// </summary>
    [Fact]
    public void InsetClipPath_StillClips()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
body { margin: 0; }
div { width: 100px; height: 100px; background: green; clip-path: inset(25px); }
</style>
<div></div>
""");

        Assert.Equal(Green, Rgb(bitmap, 50, 50));
        Assert.Equal(White, Rgb(bitmap, 10, 50));
        Assert.Equal(White, Rgb(bitmap, 90, 50));
    }

    /// <summary>
    /// A shape the rasterizer does not model leaves the element unclipped rather than guessing a
    /// clip — showing too much beats erasing content the page meant to show.
    /// </summary>
    [Fact]
    public void UnsupportedClipPathShape_LeavesTheElementUnclipped()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
body { margin: 0; }
div { width: 100px; height: 100px; background: green; clip-path: circle(25px); }
</style>
<div></div>
""");

        Assert.Equal(Green, Rgb(bitmap, 50, 50));
        Assert.Equal(Green, Rgb(bitmap, 5, 5));
    }
}
