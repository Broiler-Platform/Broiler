using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// The <c>clip-path</c> basic shapes the paint walker models, and the shape's reach over the
/// canvas background.
///
/// It understood only <c>inset()</c>. Two failures were tangled together in WPT
/// <c>css/css-masking/clip-path/clip-path-document-element{,-will-change}.html</c> (issue #1544
/// problems 14 and 15, both at 1.0% match): the test's <c>polygon()</c> clip was dropped entirely,
/// and a <c>clip-path</c> on the document element was not applied to the background that element
/// propagates to the canvas (CSS2.1 §14.2). <c>circle()</c> and <c>ellipse()</c> were dropped the
/// same way, which is the whole <c>clip-path-circle-*</c> / <c>clip-path-ellipse-*</c> family.
/// These tests render those WPT cases and the references they point at and assert the pixels agree.
/// </summary>
public class ClipPathShapeTests
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
div { width: 100px; height: 100px; background: green; clip-path: path("M 0 0 L 100 0 L 100 100 Z"); }
</style>
<div></div>
""");

        Assert.Equal(Green, Rgb(bitmap, 50, 50));
        Assert.Equal(Green, Rgb(bitmap, 5, 5));
    }

    // ── circle() / ellipse() ────────────────────────────────────────────────────────────────
    //
    // WPT css-masking/clip-path/clip-path-circle-00{1..8} and -ellipse-00{1..8} all reduce to
    // "an ellipse of these radii at this centre of the border box", written six different ways.
    // These cases mirror the ones those tests pin.

    /// <summary>
    /// A pixel is inside the clip iff it is inside the ellipse — asserted on the axes (just inside
    /// and just outside each extreme) and on the diagonals, where a bounding-box approximation
    /// would wrongly keep the corners.
    /// </summary>
    private static void AssertEllipse(
        BBitmap bitmap, int boxWidth, int boxHeight, float centerX, float centerY, float radiusX, float radiusY)
    {
        for (int y = 0; y < 300; y++)
        {
            for (int x = 0; x < 300; x++)
            {
                float dx = (x + 0.5f - centerX) / radiusX;
                float dy = (y + 0.5f - centerY) / radiusY;
                float distance = (dx * dx) + (dy * dy);

                // Skip the boundary band: which side of the edge a pixel centre lands on is a
                // rounding question, not a correctness one.
                if (distance > 0.96f && distance < 1.04f)
                    continue;

                // The clip only ever removes paint. Where the shape reaches past the element's own
                // box — circle(farthest-side) on an oblong, say — there was nothing to keep.
                bool inBox = x < boxWidth && y < boxHeight;
                var expected = distance <= 1f && inBox ? Green : White;
                Assert.True(
                    expected == Rgb(bitmap, x, y),
                    $"({x},{y}) at r²={distance:0.000} expected {expected} but was {Rgb(bitmap, x, y)}");
            }
        }
    }

    /// <summary>
    /// <c>circle()</c> with no arguments: <c>closest-side</c> at the centre of the border box.
    /// On a non-square box that is the shorter half-extent, so the circle stays a circle
    /// (WPT clip-path-circle-001 and -007).
    /// </summary>
    [Theory]
    [InlineData("circle()", 200, 200, 100, 100, 100)]
    [InlineData("circle(50%)", 200, 200, 100, 100, 100)]
    [InlineData("circle(50% at 50% 50%)", 200, 200, 100, 100, 100)]
    [InlineData("circle(at 50%)", 200, 200, 100, 100, 100)]
    [InlineData("circle(closest-side)", 200, 200, 100, 100, 100)]
    [InlineData("circle(farthest-side)", 200, 200, 100, 100, 100)]
    [InlineData("circle(closest-side)", 400, 200, 200, 100, 100)]
    [InlineData("circle(closest-side)", 200, 400, 100, 200, 100)]
    [InlineData("circle(farthest-side)", 400, 200, 200, 100, 200)]
    [InlineData("circle(60px at 40px 30px)", 200, 200, 40, 30, 60)]
    public void CircleClipPath_ClipsToTheCircle(
        string clipPath, int width, int height, float centerX, float centerY, float radius)
    {
        using var bitmap = Render($$"""
<!DOCTYPE html>
<style>
body { margin: 0; }
div { width: {{width}}px; height: {{height}}px; background: green; clip-path: {{clipPath}}; }
</style>
<div></div>
""");

        AssertEllipse(bitmap, width, height, centerX, centerY, radius, radius);
    }

    /// <summary>
    /// <c>ellipse()</c> in its keyword, percentage and length forms — all of which WPT
    /// clip-path-ellipse-006/-007/-008 write as the same shape.
    /// </summary>
    [Theory]
    [InlineData("ellipse()", 150, 100, 75, 50, 75, 50)]
    [InlineData("ellipse(farthest-side closest-side)", 150, 100, 75, 50, 75, 50)]
    [InlineData("ellipse(50% 50%)", 150, 100, 75, 50, 75, 50)]
    [InlineData("ellipse(75px 50px at 125px 100px)", 250, 200, 125, 100, 75, 50)]
    [InlineData("ellipse(30% 25% at 50% 50%)", 250, 200, 125, 100, 75, 50)]
    [InlineData("ellipse(50% 50% at 50% 50%)", 200, 200, 100, 100, 100, 100)]
    public void EllipseClipPath_ClipsToTheEllipse(
        string clipPath, int width, int height, float centerX, float centerY, float radiusX, float radiusY)
    {
        using var bitmap = Render($$"""
<!DOCTYPE html>
<style>
body { margin: 0; }
div { width: {{width}}px; height: {{height}}px; background: green; clip-path: {{clipPath}}; }
</style>
<div></div>
""");

        AssertEllipse(bitmap, width, height, centerX, centerY, radiusX, radiusY);
    }

    /// <summary>
    /// The reference box is the border box, not the content box: WPT clip-path-ellipse-001 puts a
    /// 50px red border around a 150x100 box and centres the ellipse at 125px 100px — the middle of
    /// the 250x200 border box — so a correct clip shows the green content and none of the border.
    /// </summary>
    [Fact]
    public void EllipseClipPath_ResolvesAgainstTheBorderBox()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
body { margin: 0; }
div {
  width: 150px; height: 100px; border: solid red 50px; background: green;
  clip-path: ellipse(75px 50px at 125px 100px);
}
</style>
<div></div>
""");

        AssertEllipse(bitmap, 250, 200, 125, 100, 75, 50);
    }

    /// <summary>
    /// A circle percentage resolves against sqrt(w² + h²) / sqrt(2), not against either side, so
    /// on a non-square box it is neither half the width nor half the height (CSS Shapes §3.1).
    /// </summary>
    [Fact]
    public void CircleClipPath_ResolvesPercentageRadiusAgainstTheDiagonal()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
body { margin: 0; }
div { width: 240px; height: 100px; background: green; clip-path: circle(25%); }
</style>
<div></div>
""");

        // sqrt(240² + 100²) / sqrt(2) = 183.85…, so 25% is 45.96…
        AssertEllipse(bitmap, 240, 100, 120, 50, 45.961f, 45.961f);
    }

    /// <summary>
    /// A zero radius is a valid, empty shape — and an empty shape clips everything away. (A
    /// <em>negative</em> radius is invalid instead, so it drops the declaration and clips nothing.)
    /// </summary>
    [Fact]
    public void CircleClipPath_WithAZeroRadius_ClipsEverythingAway()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
body { margin: 0; }
div { width: 100px; height: 100px; background: green; clip-path: circle(0px); }
</style>
<div></div>
""");

        Assert.Equal(White, Rgb(bitmap, 50, 50));
        Assert.Equal(White, Rgb(bitmap, 5, 5));
    }

    /// <summary>
    /// The four-value edge-offset position form is not modelled; like any other unhandled syntax it
    /// leaves the element unclipped rather than clipping it in the wrong place.
    /// </summary>
    [Fact]
    public void CircleClipPath_WithAnUnmodelledPosition_LeavesTheElementUnclipped()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>
body { margin: 0; }
div { width: 100px; height: 100px; background: green; clip-path: circle(20px at left 10px top 20px); }
</style>
<div></div>
""");

        Assert.Equal(Green, Rgb(bitmap, 5, 5));
        Assert.Equal(Green, Rgb(bitmap, 95, 95));
    }
}
