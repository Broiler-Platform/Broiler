using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// The 3D spellings of transforms that are really 2D — <c>translate3d</c>, <c>scale3d</c>,
/// <c>rotate3d</c> about Z, a 2D <c>matrix3d</c> — were skipped as unknown functions, so an element
/// using any of them rendered untransformed. Authors write them constantly (<c>translate3d</c> to
/// force GPU promotion), and a WPT reference may spell in 3D exactly what its test spells in 2D:
/// <c>css-view-transitions/content-with-transform-ref</c> uses <c>scale3d(2,3,1)</c> where the test
/// uses <c>scale(2,3)</c>, so the reference rendered at the wrong size.
/// <para>
/// Genuine 3D stays unsupported on purpose — see
/// <see cref="A_Rotation_Needing_Depth_Is_Still_Skipped"/>.
/// </para>
/// </summary>
public class Transform3dFlatteningTests
{
    // 60x30 box at (100, 40); each case is compared against the 2D spelling it is equivalent to.
    private static string Page(string transform) => $$"""
<!DOCTYPE html>
<html>
<style>
  body { margin: 0; }
  #box { position: absolute; left: 100px; top: 40px; width: 60px; height: 30px;
         background: red; transform: {{transform}}; transform-origin: 0 0; }
</style>
<div id="box"></div>
</html>
""";

    private static BBitmap Render(string transform) =>
        HtmlRender.RenderToImageWithStyleSet(Page(transform), 400, 200);

    private static void AssertSameAs(string threeD, string twoD)
    {
        using var actual = Render(threeD);
        using var expected = Render(twoD);

        for (var y = 0; y < 200; y += 2)
        {
            for (var x = 0; x < 400; x += 2)
            {
                var a = actual.GetPixel(x, y);
                var e = expected.GetPixel(x, y);
                Assert.True(a.R == e.R && a.G == e.G && a.B == e.B,
                    $"`{threeD}` differs from `{twoD}` at ({x},{y}): " +
                    $"{a.R},{a.G},{a.B} vs {e.R},{e.G},{e.B}");
            }
        }
    }

    [Theory]
    [InlineData("translate3d(50px, 20px, 0)", "translate(50px, 20px)")]
    [InlineData("translate3d(50px, 20px, 90px)", "translate(50px, 20px)")]   // Z is inert without perspective
    [InlineData("translateZ(90px)", "none")]
    [InlineData("scale3d(2, 3, 1)", "scale(2, 3)")]
    [InlineData("scale3d(2, 3, 7)", "scale(2, 3)")]
    [InlineData("scaleZ(7)", "none")]
    [InlineData("matrix3d(2,0,0,0, 0,3,0,0, 0,0,1,0, 40,10,0,1)", "matrix(2,0,0,3,40,10)")]
    public void A_3D_Spelling_Renders_As_Its_2D_Equivalent(string threeD, string twoD)
        => AssertSameAs(threeD, twoD);

    [Fact]
    public void A_Rotation_About_Z_Is_An_In_Plane_Rotation()
    {
        // rotate3d's axis decides: only Z rotates within the page. A negative Z reverses it, which
        // is why the sign is taken from the axis rather than the angle alone.
        AssertSameAs("rotate3d(0, 0, 1, 30deg)", "rotate(30deg)");
        AssertSameAs("rotate3d(0, 0, 2, 30deg)", "rotate(30deg)");
        AssertSameAs("rotate3d(0, 0, -1, 30deg)", "rotate(-30deg)");
        AssertSameAs("rotateZ(30deg)", "rotate(30deg)");
    }

    [Fact]
    public void A_Rotation_Needing_Depth_Is_Still_Skipped()
    {
        // Projecting each 3D function to the page plane on its own is not the same as composing in
        // 3D and projecting once, so a `preserve-3d` chain that cancels in space would stop
        // cancelling — WPT css-anchor-position/transform-005 rotates about an axis and then by its
        // inverse and expects a plain square. Leaving these unsupported (as before) is the honest
        // answer; approximating them is worse than not doing them.
        AssertSameAs("rotate3d(1, 1, 1, 90deg)", "none");
        AssertSameAs("rotateX(60deg)", "none");
        AssertSameAs("rotateY(60deg)", "none");
        AssertSameAs("rotate3d(0, 0, 0, 90deg)", "none");   // degenerate axis is a no-op per spec
    }

    [Fact]
    public void A_Genuinely_3D_Matrix_Is_Still_Skipped()
    {
        // Only a matrix3d whose Z row and column are the identity is a 2D matrix.
        AssertSameAs("matrix3d(1,0,0,0, 0,1,0,0, 0,0,1,0.002, 0,0,0,1)", "none");
        AssertSameAs("matrix3d(1,0,0,0, 0,0,1,0, 0,1,0,0, 0,0,0,1)", "none");
    }
}
