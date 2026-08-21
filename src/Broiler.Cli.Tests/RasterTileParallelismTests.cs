using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Broiler.HTML.Image;
using Broiler.Layout.IR;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

/// <summary>
/// The exit gate for multithreading item #5: tile-parallel display-list replay produces the same
/// pixels the sequential replay does, at every tile setting.
/// </summary>
/// <remarks>
/// <para>
/// <b>Whole documents, not canvas calls.</b> Item #4's tests drive <c>BCanvas</c> primitives
/// directly, which is right for a change that lives inside one primitive. A tile is not a primitive
/// — it is a view of the surface that an entire display list is replayed into, with its own clip
/// stack, its own layer stack and its own compositing — so the unit under test is a rendered
/// document. The documents below are chosen for the features that make a tile view more than a
/// clip: layers that composite, clips that nest, gradients whose tile bitmap is built per replay,
/// and text, whose runs are rejected by row band rather than by bounds.
/// </para>
/// <para>
/// <b>Byte comparison, not tolerance.</b> The roadmap's global exit gate is that a parallel path has
/// a single-threaded equivalent reproducing its output <em>exactly</em>. A tolerance would forgive
/// precisely the failure this design has to rule out: a seam of wrong pixels along a tile boundary,
/// which is a handful of pixels in a 1.3-megapixel image.
/// </para>
/// <para>
/// <b>Both budgets are swept.</b> Bands (item #4) and tiles (item #5) divide the same work
/// differently and a tile view runs its bands inline, so a test that moved only one of them would
/// leave the interaction between them unchecked.
/// </para>
/// </remarks>
public class RasterTileParallelismTests
{
    private const int Width = 400;
    private const int Height = 600;

    /// <summary>
    /// Documents under test, one per feature that makes a tile view more than an extra clip. Named
    /// so a failure says which feature diverged rather than which index did.
    /// </summary>
    private static IReadOnlyDictionary<string, string> Documents => new Dictionary<string, string>
    {
        // Overlapping translucent fills: every pixel is blended against what a previous item left,
        // which is what makes "one tile owns each pixel" load-bearing rather than decorative.
        ["overlapping alpha"] = Boxes(
            count: 120,
            style: (i, hue) => $"background:hsla({hue},70%,55%,.45);border:2px solid rgba(0,0,0,.3)"),

        // Gradients: their tile bitmap is built inside the replay, so a tile that failed to cull
        // would build it again — and any difference in how it is built shows up as a colour ramp.
        ["gradients"] = Boxes(
            count: 90,
            style: (i, hue) =>
                $"background:linear-gradient({i % 180}deg,hsla({hue},70%,60%,.6),hsla({(hue + 120) % 360},70%,40%,.6))"),

        // Border radii clip to a rounded shape, which the clip bounding box can only approximate —
        // the per-pixel test still has to decide, and it has to decide the same way in every tile.
        ["rounded clips"] = Boxes(
            count: 90,
            style: (i, hue) => $"background:hsl({hue},60%,50%);border-radius:{4 + (i % 20)}px;overflow:hidden"),

        // Opacity layers: each tile pushes its own layer buffer and composites it back, so this is
        // where a composite that ignored the tile would overwrite a neighbour's pixels.
        ["opacity layers"] = Boxes(
            count: 60,
            style: (i, hue) => $"background:hsl({hue},65%,55%);opacity:0.{3 + (i % 6)}"),

        // Blend modes, for the same reason as opacity, with a compositing function that is not
        // source-over — a layer composited twice would be visibly wrong rather than subtly so.
        ["blend modes"] = Boxes(
            count: 60,
            style: (i, hue) =>
                $"background:hsl({hue},65%,55%);mix-blend-mode:{(i % 2 == 0 ? "multiply" : "screen")}"),

        // Text: the run is culled by row band from the font's metrics rather than by item bounds,
        // and a document taller than its viewport is where that path decides something.
        ["text past the viewport"] = Text(paragraphs: 120),

        // A background image scaled by background-size and clipped, which is the case that caught
        // the tile clip leaking into the clip stack: DrawClippedImage re-derives the image's SOURCE
        // rectangle from the intersection of its destination with GetClip(), so a view whose clip
        // had been narrowed to its tile resampled the image and drew one row of different pixels.
        // Obeying the clip is not the only thing a caller does with it.
        ["scaled background image"] = ScaledBackgroundImage(),

        // Nested scrolling boxes: a clip stack several deep, so the running clip intersection a tile
        // inherits has to survive being pushed and popped inside the replay.
        ["nested clips"] = NestedClips(depth: 12),
    };

    /// <summary>Tile and band settings every document is checked at.</summary>
    public static TheoryData<string, int, int> Cases
    {
        get
        {
            var data = new TheoryData<string, int, int>();
            foreach (var name in Documents.Keys)
            {
                foreach (var tiles in new[] { 2, 3, 4, 8 })
                {
                    foreach (var bands in new[] { 1, 4 })
                        data.Add(name, tiles, bands);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Tile_Parallel_Replay_Matches_The_Sequential_Replay(string document, int tiles, int bands)
    {
        var sequential = Render(Documents[document], tiles: 1, bands: 1);
        var parallel = Render(Documents[document], tiles, bands);

        Assert.Equal(sequential, parallel);
    }

    /// <summary>
    /// Guards the guard: every document above must actually be replayed in tiles, or the equality
    /// assertions are comparing the sequential replay with itself.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void These_Documents_Are_Actually_Replayed_In_Tiles()
    {
        foreach ((string name, string html) in Documents)
        {
            var counts = RenderCounting(html, tiles: 4);

            Assert.True(
                counts.TiledReplays > 0,
                $"'{name}' was never tiled ({counts.SurfaceRefusals} surface refusal(s), " +
                $"{counts.ContentRefusals} content refusal(s)), so its equality test proves nothing.");
            Assert.True(counts.Tiles >= counts.TiledReplays * 2);
        }
    }

    /// <summary>
    /// A tile budget of one is the sequential replay, not an approximation of it: the surface is
    /// never split, so no tile view is created at all.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Budget_Of_One_Creates_No_Tiles()
    {
        var counts = RenderCounting(Documents["overlapping alpha"], tiles: 1);

        Assert.Equal(0, counts.TiledReplays);
        Assert.Equal(0, counts.Tiles);
        Assert.True(counts.SurfaceRefusals > 0);
    }

    /// <summary>
    /// A display list holding an item the raster canvas cannot draw on its own is refused before
    /// any tile exists — the compat backend the tiles would share has not agreed to two threads.
    /// </summary>
    /// <remarks>
    /// A CSS rotation produces a <c>TransformItem</c>, which the eligibility test answers
    /// <c>false</c> for, and the refusal has to be counted as a <em>content</em> refusal rather than
    /// a surface one: the surface here is perfectly tileable and the test would pass vacuously if it
    /// were not distinguished.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void A_List_The_Raster_Canvas_Cannot_Draw_Alone_Is_Not_Tiled()
    {
        var counts = RenderCounting(
            Boxes(count: 40, style: (i, hue) => $"background:hsl({hue},60%,50%);transform:rotate({i % 40}deg)"),
            tiles: 4);

        Assert.Equal(0, counts.TiledReplays);
        Assert.True(counts.ContentRefusals > 0);
    }

    /// <summary>
    /// A tile view reports the same clip its parent does. The tile is a division of the work, not a
    /// change to the picture, so nothing above the rasterizer may be able to tell which tile it is
    /// drawing into.
    /// </summary>
    /// <remarks>
    /// <b>This is the assertion, and the pixel comparisons are not.</b> A caller may read the clip
    /// to <em>derive geometry</em> rather than only to obey it — the display-list backend recomputes
    /// a scaled image's source rectangle from the intersection of its destination with
    /// <c>GetClip()</c> — and a narrowed clip then resamples the image. Algebraically the two
    /// mappings agree, so the damage is a float rounding away and surfaced on two WPT tests out of
    /// 147 and on no synthetic document tried here. A property nothing above the rasterizer can
    /// observe the tile is checkable directly; waiting for it to show up as a wrong pixel is not.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void A_Tile_View_Reports_Its_Parents_Clip_Rather_Than_Its_Tile()
    {
        using var bitmap = new BBitmap(Width, Height);
        var surfaceClip = new RectangleF(0, 0, Width, Height);
        using var surface = bitmap.OpenGraphics(surfaceClip);

        var viewport = new RectangleF(3, 7, Width - 11, Height - 23);
        surface.PushClip(viewport);

        var tile = new Rectangle(0, Height / 4, Width, Height / 4);
        using var view = ((ITileParallelSurface)surface).CreateTileView(tile);

        Assert.Equal(viewport, view.GetClip());
        Assert.Equal(surface.GetClip(), view.GetClip());
    }

    /// <summary>
    /// No tile view may reach the compat backend. The eligibility test is per item while a few of
    /// the surface's fallbacks are decided per call, so this is the assertion that the gap between
    /// the two is empty for the documents above rather than merely believed to be.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void No_Tile_View_Falls_Back_To_The_Compat_Backend()
    {
        TileParallelReplay.ResetDiagnostics();
        foreach (var html in Documents.Values)
            Render(html, tiles: 4, bands: 1);

        Assert.Equal(0, TileParallelReplay.CompatFallbacks);
    }

    private static byte[] Render(string html, int tiles, int bands)
    {
        var previousTiles = TileParallelReplay.MaxDegreeOfParallelism;
        var previousBands = BRasterParallelism.MaxDegreeOfParallelism;
        TileParallelReplay.MaxDegreeOfParallelism = tiles;
        BRasterParallelism.MaxDegreeOfParallelism = bands;
        try
        {
            using var bitmap = HtmlRender.RenderToImageWithStyleSet(
                html, Width, Height, backgroundColor: new BColor(255, 255, 255, 255));
            return bitmap.Encode();
        }
        finally
        {
            TileParallelReplay.MaxDegreeOfParallelism = previousTiles;
            BRasterParallelism.MaxDegreeOfParallelism = previousBands;
        }
    }

    private static (long SurfaceRefusals, long ContentRefusals, long TiledReplays, long Tiles) RenderCounting(
        string html, int tiles)
    {
        TileParallelReplay.ResetDiagnostics();
        TileParallelReplay.CollectDiagnostics = true;
        try
        {
            Render(html, tiles, bands: 1);
            return TileParallelReplay.Diagnostics;
        }
        finally
        {
            TileParallelReplay.CollectDiagnostics = false;
        }
    }

    /// <summary>
    /// Absolutely positioned boxes swept across the viewport on strides coprime with its dimensions,
    /// so they overlap heavily and every tile boundary falls inside several of them.
    /// </summary>
    private static string Boxes(int count, Func<int, int, string> style)
    {
        var sb = new StringBuilder(count * 128);
        sb.Append("<html><head><style>body{margin:0;font:13px sans-serif}")
          .Append(".b{position:absolute}</style></head><body>");
        for (var i = 0; i < count; i++)
        {
            var x = (i * 37) % (Width - 80);
            var y = (i * 53) % (Height - 60);
            sb.Append("<div class=\"b\" style=\"left:").Append(x).Append("px;top:").Append(y)
              .Append("px;width:").Append(50 + (i % 40)).Append("px;height:").Append(30 + (i % 35))
              .Append("px;").Append(style(i, (i * 11) % 360)).Append("\">Aa</div>");
        }

        return sb.Append("</body></html>").ToString();
    }

    /// <summary>A document several times taller than the viewport, so most of its runs are clipped away.</summary>
    private static string Text(int paragraphs)
    {
        var sb = new StringBuilder(paragraphs * 96);
        sb.Append("<html><head><style>body{margin:0;font:14px/1.5 serif}p{margin:0 0 6px}</style></head><body>");
        for (var i = 0; i < paragraphs; i++)
        {
            sb.Append("<p>lorem ipsum dolor sit amet ").Append(i)
              .Append(" consectetur adipiscing elit sed do eiusmod tempor</p>");
        }

        return sb.Append("</body></html>").ToString();
    }

    /// <summary>
    /// Boxes whose background is a generated image drawn at half size and clipped to the padding
    /// box — <c>background-size</c> plus <c>background-clip</c>, the pair that makes the renderer
    /// compute a source rectangle from the clip rather than only test pixels against it.
    /// </summary>
    private static string ScaledBackgroundImage()
    {
        // A gradient with structure in both axes, so a resample of half a pixel is visible rather
        // than lost in a flat fill.
        using var source = new BBitmap(64, 64);
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
                source.SetPixel(x, y, new BColor((byte)(x * 4), (byte)(y * 4), (byte)((x + y) * 2), 255));
        }

        var uri = "data:image/png;base64," + Convert.ToBase64String(source.Encode());
        var sb = new StringBuilder(4096);
        sb.Append("<html><head><style>body{margin:0;font:14px sans-serif}")
          .Append(".v{border:20px solid rgba(60,150,255,.4);width:160px;height:120px;padding:16px;")
          .Append("margin:8px;background-size:50%;background-clip:padding-box;background-image:url(")
          .Append(uri).Append(")}")
          .Append(".n{background-repeat:no-repeat}</style></head><body>");
        for (var i = 0; i < 6; i++)
            sb.Append("<div class=\"v").Append(i % 2 == 0 ? " n" : "").Append("\">clip</div>");

        return sb.Append("</body></html>").ToString();
    }

    /// <summary>Boxes nested <paramref name="depth"/> deep, each clipping its children.</summary>
    private static string NestedClips(int depth)
    {
        var sb = new StringBuilder(depth * 96);
        sb.Append("<html><head><style>body{margin:0;font:12px sans-serif}")
          .Append(".n{overflow:hidden;border:1px solid #679;padding:6px;background:rgba(120,160,200,.25)}")
          .Append("</style></head><body>");
        for (var i = 0; i < depth; i++)
            sb.Append("<div class=\"n\">");
        sb.Append("lorem ipsum dolor sit amet consectetur adipiscing elit");
        for (var i = 0; i < depth; i++)
            sb.Append("</div>");

        return sb.Append("</body></html>").ToString();
    }
}
