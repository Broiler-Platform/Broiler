using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// A <c>display:none</c> box generates no boxes at all (CSS 2.1 §9.2.4), so it contributes nothing
/// to an intrinsic size.
/// <para>
/// REGRESSION GUARD: the min/max-content passes walked every child collecting words with no
/// <c>display:none</c> guard — while the shrink-to-fit *height* paths beside them had one. The
/// UA-hidden elements that carry text (<c>&lt;style&gt;</c>, <c>&lt;script&gt;</c>,
/// <c>&lt;title&gt;</c>) were therefore measured, and their source text set the width of any
/// shrink-to-fit ancestor: a <c>display:inline-block</c> div holding one short word and a
/// stylesheet measured 861px wide instead of 65px, and grew without limit as the stylesheet grew
/// (a 600-character comment took it past 6000px).
/// </para>
/// <para>
/// Found while chasing WPT issue #1491 problem 29, where every shadow host is a shrink-to-fit box
/// containing its component's <c>&lt;style&gt;</c> — but the bug is general and has nothing to do
/// with shadow DOM.
/// </para>
/// </summary>
public sealed class IntrinsicWidthSkipsDisplayNoneTests
{
    // An inline-block shrinks to fit, so its painted width is exactly the measurement under test.
    // The long stylesheet would drag that width across the whole canvas if it were measured.
    private const string LongStyle =
        "<style>li { color: rgb(1,2,3); } /*" +
        "PADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPAD" +
        "PADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPAD*/</style>";

    private static BBitmap Render(string body) =>
        HtmlRender.RenderToImageWithStyleSet(
            "<!DOCTYPE html><html><body style=\"margin:0\">" + body + "</body></html>", 600, 200);

    private static bool IsInk(BBitmap bitmap, int x, int y)
    {
        var pixel = bitmap.GetPixel(x, y);
        return pixel.R == 170 && pixel.G == 170 && pixel.B == 170;
    }

    /// <summary>The right edge of the painted box: scanned rather than sampled at a guessed
    /// coordinate, so baseline alignment or line-height cannot move the box out from under the
    /// assertion. 0 when nothing painted.</summary>
    private static int PaintedWidth(BBitmap bitmap)
    {
        int width = 0;
        for (int y = 0; y < 200; y++)
            for (int x = 0; x < 600; x++)
                if (IsInk(bitmap, x, y) && x + 1 > width)
                    width = x + 1;
        return width;
    }

    [Fact]
    public void A_Hidden_Stylesheet_Does_Not_Widen_A_Shrink_To_Fit_Box()
    {
        using var bitmap = Render(
            "<div style=\"display:inline-block;background:rgb(170,170,170)\">" + LongStyle + "Hi</div>");

        int width = PaintedWidth(bitmap);

        Assert.True(width > 0, "the box should paint");
        // "Hi" is far narrower than 300px; the stylesheet's text would carry the box past here.
        Assert.True(width < 300,
            $"the box painted {width}px wide — the hidden <style>'s text was measured into it");
    }

    [Fact]
    public void The_Same_Box_Without_The_Stylesheet_Is_The_Same_Width()
    {
        // The positive control: the guard must not have simply collapsed the box.
        using var withStyle = Render(
            "<div id=\"a\" style=\"display:inline-block;background:rgb(170,170,170)\">" + LongStyle + "Hi</div>");
        using var without = Render(
            "<div id=\"a\" style=\"display:inline-block;background:rgb(170,170,170)\">Hi</div>");

        Assert.True(PaintedWidth(without) > 0, "the control box should paint");
        Assert.Equal(PaintedWidth(without), PaintedWidth(withStyle));
    }

    [Fact]
    public void A_Hidden_Script_Is_Not_Measured_Either()
    {
        // <script> is UA display:none and carries text too, so it takes the same path.
        using var bitmap = Render(
            "<div style=\"display:inline-block;background:rgb(170,170,170)\">" +
            "<script>var padding = 'PADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPADPAD';</script>" +
            "Hi</div>");

        Assert.True(PaintedWidth(bitmap) < 300,
            "the box was widened by the hidden <script>'s source text");
    }
}
