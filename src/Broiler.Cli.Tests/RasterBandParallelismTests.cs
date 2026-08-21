using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

/// <summary>
/// The exit gate for multithreading item #4: band-parallel scanline raster produces the same
/// pixels the sequential rasterizer does, at every thread setting.
/// </summary>
/// <remarks>
/// <para>
/// <b>Byte comparison, not tolerance.</b> The roadmap's global exit gate is that a parallel path
/// has a single-threaded equivalent reproducing its output <em>exactly</em>; a pixel-difference
/// budget would let a genuine race hide inside it, because a race between two bands shows up as a
/// handful of wrong pixels, which is precisely what a tolerance forgives.
/// </para>
/// <para>
/// <b>Each case draws past the split threshold on purpose.</b> Below
/// <c>BRasterParallelism.MinimumParallelArea</c> every setting runs inline and the comparison is
/// trivially true — a concurrency test that never starts a thread. The fills here are sized so the
/// partitioner splits them, and <see cref="Band_Parallelism_Actually_Splits_These_Fills"/> asserts
/// that it did rather than trusting the arithmetic.
/// </para>
/// </remarks>
public class RasterBandParallelismTests
{
    private const int Width = 320;
    private const int Height = 240;

    /// <summary>Thread settings every drawing case is checked at.</summary>
    public static TheoryData<int> ThreadSettings => new() { 2, 3, 4, 8 };

    /// <summary>
    /// The drawing operations under test, one per rasterizer primitive that got a band split. Named
    /// so a failure says which primitive diverged rather than which index did.
    /// </summary>
    private static IReadOnlyDictionary<string, Action<BCanvas>> Operations => new Dictionary<string, Action<BCanvas>>
    {
        ["FillRect"] = canvas =>
            canvas.FillRect(new RectangleF(10, 10, 300, 220), new BColor(200, 40, 60, 180)),

        ["DrawLine"] = canvas =>
            canvas.DrawLine(new PointF(5, 5), new PointF(315, 235), new BColor(20, 90, 220, 255), strokeWidth: 9f),

        ["FillPolygon"] = canvas =>
            canvas.FillPolygon(
                [new PointF(15, 5), new PointF(310, 60), new PointF(160, 235), new PointF(30, 120)],
                new BColor(40, 190, 90, 200)),

        ["FillGlyphContours"] = canvas =>
            canvas.FillGlyphContours(
                [
                    [new PointF(20, 20), new PointF(300, 30), new PointF(290, 220), new PointF(30, 200)],
                    [new PointF(90, 70), new PointF(230, 75), new PointF(225, 170), new PointF(95, 165)],
                ],
                new BColor(10, 10, 10, 255)),

        ["FillLinearGradientRect"] = canvas =>
            canvas.FillLinearGradientRect(
                new RectangleF(0, 0, Width, Height),
                [new BColor(255, 0, 0, 255), new BColor(0, 0, 255, 255), new BColor(0, 255, 0, 128)],
                positions: null,
                angle: 37f),

        ["FillRadialGradientRect"] = canvas =>
            canvas.FillRadialGradientRect(
                new RectangleF(0, 0, Width, Height),
                [new BColor(250, 250, 10, 255), new BColor(10, 10, 250, 220)],
                positions: null,
                centerX: 0.35f,
                centerY: 0.6f),

        ["FillConicGradientRect"] = canvas =>
            canvas.FillConicGradientRect(
                new RectangleF(0, 0, Width, Height),
                [new BColor(255, 128, 0, 255), new BColor(0, 128, 255, 255), new BColor(255, 0, 128, 200)],
                positions: null,
                centerX: 0.5f,
                centerY: 0.5f,
                fromAngleDeg: 20f),

        ["DrawBitmap"] = canvas =>
            canvas.DrawBitmap(Checkerboard(), new RectangleF(0, 0, Width, Height), new RectangleF(0, 0, 32, 32)),

        ["FillRectTiled"] = canvas =>
            canvas.FillRectTiled(
                Checkerboard(),
                new RectangleF(0, 0, Width, Height),
                new RectangleF(0, 0, 32, 32),
                new PointF(3, 7)),

        // A clipped fill, because the clip list is the one piece of canvas state every band reads
        // while it draws. If bands could see a half-applied clip, this is where it would show.
        ["FillRect under a clip"] = canvas =>
        {
            canvas.PushClip(new RectangleF(25, 15, 240, 180));
            canvas.PushClipExclude(new RectangleF(80, 60, 70, 50));
            canvas.FillRect(new RectangleF(0, 0, Width, Height), new BColor(90, 30, 150, 210));
            canvas.PopClip();
            canvas.PopClip();
        },
    };

    public static TheoryData<string, int> Cases
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var name in Operations.Keys)
            {
                foreach (var threads in new[] { 2, 3, 4, 8 })
                    data.Add(name, threads);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Band_Parallel_Raster_Matches_The_Sequential_Rasterizer(string operation, int threads)
    {
        var sequential = Render(Operations[operation], 1);
        var parallel = Render(Operations[operation], threads);

        Assert.Equal(sequential, parallel);
    }

    /// <summary>
    /// Guards the guard: every case above must actually reach the partitioner's split path, or the
    /// equality assertions are comparing the sequential rasterizer with itself.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Band_Parallelism_Actually_Splits_These_Fills()
    {
        foreach ((string name, Action<BCanvas> operation) in Operations)
        {
            var previous = BRasterParallelism.MaxDegreeOfParallelism;
            BRasterParallelism.MaxDegreeOfParallelism = 4;
            BRasterParallelism.ResetDiagnostics();
            BRasterParallelism.CollectDiagnostics = true;
            try
            {
                using var bitmap = new BBitmap(Width, Height);
                using var canvas = bitmap.OpenRasterCanvas();
                operation(canvas);

                Assert.True(
                    BRasterParallelism.Diagnostics.SplitFills > 0,
                    $"'{name}' never reached the split path, so its equality test proves nothing.");
            }
            finally
            {
                BRasterParallelism.CollectDiagnostics = false;
                BRasterParallelism.MaxDegreeOfParallelism = previous;
            }
        }
    }

    /// <summary>
    /// A thread budget of one is the sequential rasterizer, not an approximation of it: no fill is
    /// split however large it is.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Budget_Of_One_Splits_Nothing()
    {
        var previous = BRasterParallelism.MaxDegreeOfParallelism;
        BRasterParallelism.MaxDegreeOfParallelism = 1;
        BRasterParallelism.ResetDiagnostics();
        BRasterParallelism.CollectDiagnostics = true;
        try
        {
            using var bitmap = new BBitmap(Width, Height);
            using var canvas = bitmap.OpenRasterCanvas();
            canvas.FillRect(new RectangleF(0, 0, Width, Height), new BColor(1, 2, 3, 255));

            Assert.Equal(0, BRasterParallelism.Diagnostics.SplitFills);
            Assert.True(BRasterParallelism.Diagnostics.InlineFills > 0);
        }
        finally
        {
            BRasterParallelism.CollectDiagnostics = false;
            BRasterParallelism.MaxDegreeOfParallelism = previous;
        }
    }

    private static byte[] Render(Action<BCanvas> operation, int threads)
    {
        var previous = BRasterParallelism.MaxDegreeOfParallelism;
        BRasterParallelism.MaxDegreeOfParallelism = threads;
        try
        {
            using var bitmap = new BBitmap(Width, Height);
            bitmap.Clear(new BColor(255, 255, 255, 255));
            using (var canvas = bitmap.OpenRasterCanvas())
                operation(canvas);

            return bitmap.Encode();
        }
        finally
        {
            BRasterParallelism.MaxDegreeOfParallelism = previous;
        }
    }

    /// <summary>Deterministic source image for the bitmap-sampling primitives.</summary>
    private static BBitmap Checkerboard()
    {
        var bitmap = new BBitmap(32, 32);
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                var dark = ((x / 4) + (y / 4)) % 2 == 0;
                bitmap.SetPixel(x, y, dark
                    ? new BColor((byte)(x * 8), (byte)(y * 8), 60, 255)
                    : new BColor(240, (byte)(255 - y * 7), (byte)(255 - x * 7), 190));
            }
        }

        return bitmap;
    }
}
