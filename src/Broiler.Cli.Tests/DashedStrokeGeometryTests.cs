using Broiler.Graphics;
using Broiler.Layout.IR;

namespace Broiler.Cli.Tests;

/// <summary>
/// The raster canvas strokes solid lines only, so a dashed or dotted edge has to be reduced to
/// a list of solid runs before it reaches the canvas. Before that reduction existed, those
/// strokes fell through to the graphics compatibility seam, which resolves to an inert stub on
/// a host with no OS backend — a <c>border-style: dashed</c> edge painted nothing at all while
/// <c>solid</c> painted normally.
/// </summary>
public sealed class DashedStrokeGeometryTests
{
    private static float Length(DashedStrokeGeometry.Segment s)
    {
        var dx = s.X2 - s.X1;
        var dy = s.Y2 - s.Y1;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static float PaintedLength(IReadOnlyList<DashedStrokeGeometry.Segment> segments)
    {
        var total = 0f;
        foreach (var s in segments)
            total += Length(s);
        return total;
    }

    [Fact(Timeout = 600000)]
    public void Solid_Style_Has_No_Pattern()
        => Assert.Empty(DashedStrokeGeometry.PatternFor(DashStyle.Solid, 3f));

    [Theory]
    [InlineData(DashStyle.Dash, 2)]
    [InlineData(DashStyle.Dot, 2)]
    [InlineData(DashStyle.DashDot, 4)]
    [InlineData(DashStyle.DashDotDot, 6)]
    public void Dashed_Styles_Produce_An_Even_On_Off_Pattern(DashStyle style, int expectedRuns)
    {
        var pattern = DashedStrokeGeometry.PatternFor(style, 2f);

        Assert.Equal(expectedRuns, pattern.Count);
        Assert.All(pattern, run => Assert.True(run > 0f));
    }

    [Fact(Timeout = 600000)]
    public void Pattern_Scales_With_Stroke_Width()
    {
        var thin = DashedStrokeGeometry.PatternFor(DashStyle.Dash, 1f);
        var thick = DashedStrokeGeometry.PatternFor(DashStyle.Dash, 4f);

        // A dash belongs to the border it draws, so it grows with it.
        Assert.Equal(thin[0] * 4f, thick[0]);
    }

    [Fact(Timeout = 600000)]
    public void Hairline_Widths_Do_Not_Collapse_The_Pattern()
    {
        // A sub-pixel border must still dash rather than degenerate to zero-length runs.
        var pattern = DashedStrokeGeometry.PatternFor(DashStyle.Dash, 0.25f);

        Assert.All(pattern, run => Assert.True(run >= 1f));
    }

    [Fact(Timeout = 600000)]
    public void An_Empty_Pattern_Paints_The_Whole_Stroke()
    {
        var segments = DashedStrokeGeometry.Segments(0f, 0f, 10f, 0f, []);

        var only = Assert.Single(segments);
        Assert.Equal(new DashedStrokeGeometry.Segment(0f, 0f, 10f, 0f), only);
    }

    [Fact(Timeout = 600000)]
    public void A_Dashed_Stroke_Paints_Less_Than_The_Whole_Line()
    {
        // The point of the whole exercise: gaps exist, and the line is not blank.
        var segments = DashedStrokeGeometry.Segments(0f, 0f, 60f, 0f, [6f, 6f]);

        Assert.NotEmpty(segments);
        var painted = PaintedLength(segments);
        Assert.True(painted > 0f, "a dashed stroke must paint something");
        Assert.True(painted < 60f, $"a dashed stroke must leave gaps, painted {painted} of 60");
    }

    [Fact(Timeout = 600000)]
    public void Runs_Alternate_On_And_Off_Along_The_Line()
    {
        var segments = DashedStrokeGeometry.Segments(0f, 0f, 24f, 0f, [6f, 6f]);

        Assert.Equal(2, segments.Count);
        Assert.Equal(new DashedStrokeGeometry.Segment(0f, 0f, 6f, 0f), segments[0]);
        Assert.Equal(new DashedStrokeGeometry.Segment(12f, 0f, 18f, 0f), segments[1]);
    }

    [Fact(Timeout = 600000)]
    public void The_Last_Run_Is_Clipped_To_The_Stroke()
    {
        // 20px of a 6/6 pattern ends mid-dash: 0-6, 12-18, then 24-30 clipped to 20.
        var segments = DashedStrokeGeometry.Segments(0f, 0f, 20f, 0f, [6f, 6f]);

        Assert.Equal(2, segments.Count);
        Assert.All(segments, s => Assert.True(s.X2 <= 20f));
        Assert.Equal(18f, segments[^1].X2);
    }

    [Fact(Timeout = 600000)]
    public void A_Stroke_Shorter_Than_One_Dash_Still_Paints()
    {
        // Short edges — a 4px-wide dashed border on a small box — must not vanish.
        var segments = DashedStrokeGeometry.Segments(0f, 0f, 3f, 0f, [12f, 12f]);

        var only = Assert.Single(segments);
        Assert.Equal(3f, Length(only), 3);
    }

    [Fact(Timeout = 600000)]
    public void Dashes_Follow_A_Diagonal_Stroke()
    {
        var segments = DashedStrokeGeometry.Segments(0f, 0f, 6f, 8f, [5f, 5f]);

        Assert.NotEmpty(segments);
        // Every run stays on the line: for this 3-4-5 direction, y is always 4/3 of x.
        Assert.All(segments, s =>
        {
            Assert.Equal(s.X1 * 4f / 3f, s.Y1, 3);
            Assert.Equal(s.X2 * 4f / 3f, s.Y2, 3);
        });
    }

    [Fact(Timeout = 600000)]
    public void A_Vertical_Stroke_Dashes_Along_Y()
    {
        var segments = DashedStrokeGeometry.Segments(5f, 0f, 5f, 24f, [6f, 6f]);

        Assert.Equal(2, segments.Count);
        Assert.All(segments, s =>
        {
            Assert.Equal(5f, s.X1);
            Assert.Equal(5f, s.X2);
        });
        Assert.Equal(new DashedStrokeGeometry.Segment(5f, 0f, 5f, 6f), segments[0]);
    }

    [Fact(Timeout = 600000)]
    public void A_Zero_Length_Stroke_Paints_Nothing()
        => Assert.Empty(DashedStrokeGeometry.Segments(4f, 4f, 4f, 4f, [6f, 6f]));

    [Fact(Timeout = 600000)]
    public void A_Degenerate_Pattern_Falls_Back_To_A_Solid_Run()
    {
        // All-zero and negative patterns must not spin or silently blank the stroke.
        Assert.Single(DashedStrokeGeometry.Segments(0f, 0f, 10f, 0f, [0f, 0f]));
        Assert.Single(DashedStrokeGeometry.Segments(0f, 0f, 10f, 0f, [-1f, 4f]));
        Assert.Single(DashedStrokeGeometry.Segments(0f, 0f, 10f, 0f, [float.NaN, 4f]));
    }

    [Fact(Timeout = 600000)]
    public void A_Pattern_With_A_Zero_Gap_Still_Advances()
    {
        // [w, 0] is a solid line written as a dash; it must terminate and cover the stroke.
        var segments = DashedStrokeGeometry.Segments(0f, 0f, 30f, 0f, [10f, 0f]);

        Assert.Equal(30f, PaintedLength(segments), 3);
    }
}
