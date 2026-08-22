using System;
using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// CSS Transforms 1 §6/§8 — <c>transform-box</c> and <c>transform-origin</c> on an SVG element.
///
/// <para>
/// An SVG <c>transform</c> was applied about the user-space origin and nothing else. That is what
/// SVG 1.1 did and is still right under the initial <c>transform-box: view-box</c>, but
/// <c>fill-box</c> makes the element's own bounding box the reference box, and
/// <c>transform-origin</c> then names a point inside it — the centre when it is not written. The
/// element's own transform has to be conjugated by that point, <c>T(o) · M · T(-o)</c>, so a
/// <c>rotate(90)</c> meant about a box centre turned about the viewport corner instead and swung
/// the element off the canvas.
/// </para>
///
/// <para>
/// A second gap kept the first invisible: <c>SvgRenderer</c> reads the serialized markup and sees
/// no stylesheet, and the projection that writes cascaded SVG presentation properties back as
/// attributes (<c>DomBridge.Serialization.SvgZoom</c>) carried a fixed list that did not include
/// these two. Every one of the WPT cases declares <c>transform-box</c> in a <c>&lt;style&gt;</c>
/// block, so the rule reached nothing at all.
/// </para>
///
/// <para>
/// Measured in pixels rather than off the display list because a rotation does not leave a rect
/// item behind to inspect — <c>SvgItemTransformer</c> maps it to another primitive.
/// </para>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class SvgTransformOriginTests
{
    private const int Canvas = 300;

    private static BBitmap Render(string rectAttributes)
    {
        string html =
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<svg width=\"200\" height=\"200\" xmlns=\"http://www.w3.org/2000/svg\">"
            + $"<rect width=\"100\" height=\"100\" fill=\"blue\" {rectAttributes}/>"
            + "</svg></body></html>";

        string dir = Path.Combine(Path.GetTempPath(), "broiler-torigin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);
            return new WptTestRunner(Canvas, Canvas).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static bool IsBlue(BBitmap bmp, int x, int y)
    {
        var p = bmp.GetPixel(x, y);
        return p.R < 80 && p.G < 80 && p.B > 180;
    }

    /// <summary>
    /// Whether the square is still where it started — sampled at its centre, (50,50). A rotate that
    /// turns about the box centre maps the square onto itself; one that turns about the viewport
    /// origin takes it to negative x, off the canvas entirely.
    /// </summary>
    private static bool SquareStaysPut(string rectAttributes) => IsBlue(Render(rectAttributes), 50, 50);

    /// <summary>The baseline: no transform at all, so the square is simply there.</summary>
    [Fact(Timeout = 600000)]
    public void NoTransform_LeavesTheSquareInPlace()
    {
        Assert.True(SquareStaysPut(string.Empty));
    }

    /// <summary>
    /// The case that must NOT move: with no <c>transform-box</c> the initial <c>view-box</c>
    /// applies and a quarter turn is about the user-space origin, taking the square off the canvas
    /// exactly as it did before this change.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void NoTransformBox_KeepsTheViewBoxMeaning()
    {
        Assert.False(SquareStaysPut("transform=\"rotate(90)\""),
            "without transform-box the rotate is about the viewport origin, so the square leaves the canvas");
    }

    /// <summary>
    /// CSS Transforms 1 §8: an SVG element has no CSS layout box, and for one of those the used
    /// value of an unspecified <c>transform-origin</c> is <c>0 0</c> — the reference box's own
    /// corner, not its centre. For a rect already at the origin that is the user-space origin too,
    /// so the quarter turn still carries the square off the canvas.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void FillBox_WithNoOriginWritten_UsesTheBoxCorner()
    {
        Assert.False(SquareStaysPut("transform=\"rotate(90)\" style=\"transform-box: fill-box\""));
    }

    /// <summary>
    /// And the corner is the <em>box's</em> corner, which is what tells <c>fill-box</c> apart from
    /// <c>view-box</c>: a rect away from the origin turns about itself, so it holds its place.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void FillBoxCorner_IsTheElementsOwnCorner()
    {
        string html =
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<svg width=\"200\" height=\"200\" xmlns=\"http://www.w3.org/2000/svg\">"
            + "<rect x=\"50\" y=\"50\" width=\"100\" height=\"100\" fill=\"blue\""
            + " transform=\"rotate(90)\" style=\"transform-box: fill-box\"/>"
            + "</svg></body></html>";

        string dir = Path.Combine(Path.GetTempPath(), "broiler-torigin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);
            var bmp = new WptTestRunner(Canvas, Canvas).RenderHtmlFileBitmapPublic(file, dir);

            // rotate(90) about the box corner (50,50) takes (50,50)..(150,150) to
            // (-50,50)..(50,150), so the square's right edge lands just past x = 50 at y = 100.
            Assert.True(IsBlue(bmp, 25, 100),
                "fill-box measures 0 0 from the element's own corner, not the viewport's");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>
    /// The cascade route: <c>transform-box</c> from a stylesheet rule rather than an attribute,
    /// which is how every WPT case in this family writes it and what reached nothing before.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TransformBoxFromAStylesheetRule_Applies()
    {
        string html =
            "<!DOCTYPE html><html><head><style>rect { transform-box: fill-box; }</style></head>"
            + "<body style=\"margin:0\">"
            + "<svg width=\"200\" height=\"200\" xmlns=\"http://www.w3.org/2000/svg\">"
            + "<rect x=\"50\" y=\"50\" width=\"100\" height=\"100\" fill=\"blue\" transform=\"rotate(90)\"/>"
            + "</svg></body></html>";

        string dir = Path.Combine(Path.GetTempPath(), "broiler-torigin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);
            var bmp = new WptTestRunner(Canvas, Canvas).RenderHtmlFileBitmapPublic(file, dir);

            // The rule reaching the renderer is what is under test, so the rect is placed away
            // from the origin: under view-box the quarter turn would take it off the canvas, and
            // under fill-box it turns about its own corner and stays partly on it.
            Assert.True(IsBlue(bmp, 25, 100),
                "a `rect { transform-box: fill-box }` rule must reach the SVG renderer");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>An origin at the box's own corner turns about that corner, so the square leaves.</summary>
    [Fact(Timeout = 600000)]
    public void OriginAtTheCorner_TurnsAboutThatCorner()
    {
        Assert.False(SquareStaysPut(
            "transform=\"rotate(90)\" transform-origin=\"0 0\" style=\"transform-box: fill-box\""));
    }

    /// <summary>A single value gives x and centres y; 50 is the centre of a 100-wide box.</summary>
    [Fact(Timeout = 600000)]
    public void SingleOriginValue_CentresTheOtherAxis()
    {
        Assert.True(SquareStaysPut(
            "transform=\"rotate(90)\" transform-origin=\"50\" style=\"transform-box: fill-box\""));
    }

    /// <summary>Percentages resolve against the reference box.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("50% 50%", true)]
    [InlineData("0% 0%", false)]
    public void PercentageOrigin_ResolvesAgainstTheBox(string origin, bool staysPut)
    {
        Assert.Equal(staysPut, SquareStaysPut(
            $"transform=\"rotate(90)\" transform-origin=\"{origin}\" style=\"transform-box: fill-box\""));
    }

    /// <summary>
    /// The keyword forms, including the pair written the other way round — which CSS allows only
    /// when both halves are keywords.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("center center", true)]
    [InlineData("left top", false)]
    [InlineData("top left", false)]
    public void OriginKeywords_Resolve(string origin, bool staysPut)
    {
        Assert.Equal(staysPut, SquareStaysPut(
            $"transform=\"rotate(90)\" transform-origin=\"{origin}\" style=\"transform-box: fill-box\""));
    }

    /// <summary>
    /// A length-percentage may not follow a vertical keyword, so `top 100%` is not a form at all.
    /// The declaration is dropped whole and the initial value stands — which for an SVG element is
    /// `0 0`, the reference box's corner. Two separate errors here kept the twelve WPT
    /// <c>svg-origin-relative-length-invalid-*</c> cases red: first reading `top 100%` as
    /// `100% top`, then falling back to the box centre rather than to `0 0`.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("top 100%")]
    [InlineData("bottom 50px")]
    [InlineData("50% left")]
    public void InvalidOrigin_FallsBackToTheInitialCorner(string origin)
    {
        Assert.False(SquareStaysPut(
            $"transform=\"rotate(90)\" transform-origin=\"{origin}\" style=\"transform-box: fill-box\""),
            $"transform-origin: {origin} is invalid, so the initial 0 0 must stand — not the centre");
    }

    /// <summary><c>transform-box</c> with no transform must not invent one.</summary>
    [Fact(Timeout = 600000)]
    public void TransformBoxWithoutATransform_ChangesNothing()
    {
        Assert.True(SquareStaysPut("style=\"transform-box: fill-box\" transform-origin=\"0 0\""));
    }

    /// <summary>
    /// The other box-relative keywords. A shape here carries no box decorations to tell them apart
    /// from <c>fill-box</c>, so they resolve to the same rectangle rather than to the viewport.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("stroke-box")]
    [InlineData("content-box")]
    [InlineData("border-box")]
    public void OtherBoxRelativeValues_AlsoUseTheElementsBox(string box)
    {
        // `center` is what distinguishes them from view-box: resolved against the element's own
        // 100x100 box it is (50,50) and the quarter turn maps the square onto itself, where
        // view-box would not reach this path at all and carry the square off the canvas.
        Assert.True(SquareStaysPut(
            $"transform=\"rotate(90)\" transform-origin=\"center\" style=\"transform-box: {box}\""));
    }
}
