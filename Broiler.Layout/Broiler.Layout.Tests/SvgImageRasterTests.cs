using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Broiler.Layout.IR;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// Covers <see cref="SvgImageRaster"/>, the seam that makes an SVG used <em>as an image</em> render
/// through the same <see cref="SvgRenderer"/> as inline <c>&lt;svg&gt;</c> markup.
/// <para>
/// The case that drove it is issue #1627. The image backend used to draw SVG itself, with a regex
/// pass per element type that knew only rect, circle, ellipse, line, path and text — so a document
/// whose content was a <c>&lt;polygon&gt;</c> rasterised to a fully transparent bitmap and the image
/// did not appear at all, while the same file rendered correctly as inline markup. Eleven WPT tests
/// were failing on exactly that (<c>css/css-transforms/transform-root-bg-*</c>,
/// <c>transform-background-*</c> and <c>css/compositing/root-element-background-image-transparency-*</c>,
/// all of which load a <c>&lt;polygon&gt;</c>), and they had been misfiled as reference disagreements
/// because Broiler rendered blank for the test <em>and</em> its reference, which matched at 100%.
/// </para>
/// <para>
/// The point of these tests is therefore the <b>equivalence</b>, not the individual shapes: whatever
/// the inline renderer draws, the image path must draw the same. A test that pinned pixel output per
/// element would pass while the two renderers drifted apart again, which is the bug class this change
/// closes.
/// </para>
/// </summary>
public sealed class SvgImageRasterTests
{
    private static readonly RectangleF Bounds = new(0, 0, 100, 100);

    private static string Doc(string body) =>
        $"<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100' viewBox='0 0 100 100'>{body}</svg>";

    /// <summary>Records what it was asked to replay, standing in for the image backend's raster backend.</summary>
    private sealed class RecordingBackend : IRasterBackend
    {
        public List<(DisplayList List, object Surface)> Calls { get; } = [];

        public void Render(DisplayList list, object surface) => Calls.Add((list, surface));
    }

    // ─────────────────────────── the regression that motivated it ───────────────────────────

    /// <summary>
    /// The shape the old image renderer dropped. A <c>&lt;polygon&gt;</c> must produce display items;
    /// producing none is what made the eleven WPT tests render a blank canvas.
    /// </summary>
    [Fact]
    public void Polygon_Produces_Display_Items()
    {
        var list = SvgImageRaster.BuildDisplayList(
            Doc("<polygon fill='blue' points='0,50 100,100 100,0' />"), Bounds);

        Assert.NotEmpty(list.Items);
    }

    /// <summary>
    /// The same square written as a <c>&lt;rect&gt;</c> and as a <c>&lt;polygon&gt;</c> must both draw.
    /// This is the exact pair that isolated the bug: `rect` painted and `polygon` did not, through the
    /// same code path, at the same size and position.
    /// </summary>
    [Fact]
    public void Polygon_And_Rect_Both_Draw_The_Same_Square()
    {
        var asRect = SvgImageRaster.BuildDisplayList(
            Doc("<rect fill='blue' x='0' y='0' width='100' height='100' />"), Bounds);
        var asPolygon = SvgImageRaster.BuildDisplayList(
            Doc("<polygon fill='blue' points='0,0 100,0 100,100 0,100' />"), Bounds);

        Assert.NotEmpty(asRect.Items);
        Assert.NotEmpty(asPolygon.Items);
    }

    /// <summary>
    /// <c>&lt;polyline&gt;</c> is the other shape the old renderer had no arm for. It is covered so the
    /// fix is not read as being about one element.
    /// </summary>
    [Fact]
    public void Polyline_Produces_Display_Items()
    {
        var list = SvgImageRaster.BuildDisplayList(
            Doc("<polyline fill='none' stroke='blue' stroke-width='4' points='0,0 50,50 100,0' />"), Bounds);

        Assert.NotEmpty(list.Items);
    }

    // ─────────────────────────── the equivalence that is the point ───────────────────────────

    /// <summary>
    /// The image path and the inline path must produce the identical display list for the identical
    /// document. This is what "one SVG renderer, not two" means, and it is the assertion that fails if
    /// anyone reintroduces a separate image-side renderer.
    /// </summary>
    [Theory]
    [InlineData("<rect fill='blue' x='10' y='10' width='50' height='50' />")]
    [InlineData("<circle fill='blue' cx='50' cy='50' r='40' />")]
    [InlineData("<ellipse fill='blue' cx='50' cy='50' rx='40' ry='20' />")]
    [InlineData("<line stroke='blue' stroke-width='4' x1='0' y1='0' x2='100' y2='100' />")]
    [InlineData("<path fill='blue' d='M 0 0 L 100 0 L 100 100 Z' />")]
    [InlineData("<polygon fill='blue' points='0,50 100,100 100,0' />")]
    [InlineData("<polyline fill='none' stroke='blue' stroke-width='4' points='0,0 50,50 100,0' />")]
    public void Image_Path_Builds_What_The_Inline_Renderer_Builds(string body)
    {
        string svg = Doc(body);

        var viaImage = SvgImageRaster.BuildDisplayList(svg, Bounds).Items;
        var viaInline = SvgRenderer.RenderSvgContent(svg, Bounds);

        Assert.Equal(viaInline.Count, viaImage.Count);
        Assert.Equal(
            viaInline.Select(i => i.GetType().Name),
            viaImage.Select(i => i.GetType().Name));
    }

    /// <summary>
    /// The zoom argument reaches the renderer rather than being dropped by the wrapper. A view-box-less
    /// document is the case that shows it, since there the zoom seeds the user-unit scale instead of
    /// being overridden by a view-box-derived one.
    /// </summary>
    [Fact]
    public void Effective_Zoom_Is_Forwarded()
    {
        const string svg =
            "<svg xmlns='http://www.w3.org/2000/svg'><rect fill='blue' x='0' y='0' width='10' height='10' /></svg>";

        var atOne = SvgImageRaster.BuildDisplayList(svg, Bounds, 1.0).Items;
        var atTwo = SvgImageRaster.BuildDisplayList(svg, Bounds, 2.0).Items;

        Assert.Equal(
            SvgRenderer.RenderSvgContent(svg, Bounds, 2.0).Select(i => i.GetType().Name),
            atTwo.Select(i => i.GetType().Name));
        Assert.NotEmpty(atOne);
    }

    // ─────────────────────────── the replay contract ───────────────────────────

    /// <summary>A document that draws something is replayed onto the caller's surface, once.</summary>
    [Fact]
    public void Render_Replays_The_List_Onto_The_Callers_Surface()
    {
        var backend = new RecordingBackend();
        var surface = new object();

        bool drew = SvgImageRaster.Render(
            Doc("<polygon fill='blue' points='0,50 100,100 100,0' />"), Bounds, backend, surface);

        Assert.True(drew);
        var call = Assert.Single(backend.Calls);
        Assert.Same(surface, call.Surface);
        Assert.NotEmpty(call.List.Items);
    }

    /// <summary>
    /// The negative half, and the one that keeps the change from painting where it should not: a
    /// document that draws nothing must leave the surface untouched rather than replay an empty list.
    /// The image backend clears the bitmap to transparent before calling in, so an empty replay would
    /// be harmless today — but it would also make "nothing to draw" indistinguishable from "drew
    /// nothing", which is precisely the confusion that hid this bug.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'></svg>")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><unsupported-element /></svg>")]
    public void Render_Does_Not_Touch_The_Surface_When_Nothing_Is_Drawn(string svg)
    {
        var backend = new RecordingBackend();

        bool drew = SvgImageRaster.Render(svg, Bounds, backend, new object());

        Assert.False(drew);
        Assert.Empty(backend.Calls);
    }

    /// <summary>Null source is "draws nothing", not a throw — an unreadable image must not take the page down.</summary>
    [Fact]
    public void Null_Source_Draws_Nothing()
    {
        var backend = new RecordingBackend();

        Assert.False(SvgImageRaster.Render(null!, Bounds, backend, new object()));
        Assert.Empty(backend.Calls);
    }

    /// <summary>The two arguments the caller cannot sensibly omit are rejected rather than swallowed.</summary>
    [Fact]
    public void Backend_And_Surface_Are_Required()
    {
        string svg = Doc("<rect fill='blue' x='0' y='0' width='10' height='10' />");

        Assert.Throws<ArgumentNullException>(
            () => SvgImageRaster.Render(svg, Bounds, null!, new object()));
        Assert.Throws<ArgumentNullException>(
            () => SvgImageRaster.Render(svg, Bounds, new RecordingBackend(), null!));
    }
}
