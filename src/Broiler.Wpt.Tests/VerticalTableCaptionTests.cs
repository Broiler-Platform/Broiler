using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guards for a <c>display:table-caption</c> box in a vertical
/// writing mode — the shape of WPT
/// <c>css-writing-modes/block-flow-direction-vlr-020</c> (vertical-lr) and
/// <c>-slr-060</c> (sideways-lr), which render "PASS" as a grid of Ahem cells
/// inside a <c>height:9em</c> caption. Both scored ~13% before these fixes.
///
/// <para>Three distinct bugs are covered:</para>
/// <list type="number">
///   <item><b>Containing block.</b> A <c>table-caption</c> was not recognised as
///   a block-container in <see cref="Broiler.Layout.Engine.CssBox.ContainingBlock"/>,
///   so its in-flow children resolved their width against the viewport instead of
///   the caption's (rotated) inline size. In the vertical-flow prototype the
///   children's viewport-width became the caption's physical height after
///   rotation, so the caption's blue box filled the whole viewport.</item>
///   <item><b>Border/padding axis.</b> A transposed vertical box read its
///   physical border-top/bottom as the logical-frame block-axis insets, so
///   <c>border-top</c>/<c>border-bottom</c> inflated the box's physical
///   <em>width</em> (block axis) instead of its height (inline axis) — the blue
///   box came out ~2.3× too wide.</item>
///   <item><b>sideways-lr inline direction.</b> Its inline axis runs bottom→top;
///   without the flip the content rendered vertically mirrored (PASS upside-down).</item>
/// </list>
/// These are integration guards (render + measure the painted region) rather than
/// pixel-exact reftests, so they hold regardless of whether Ahem or a fallback
/// face is used for the glyphs.
/// </summary>
public class VerticalTableCaptionTests : IDisposable
{
    private readonly string _tempDir;

    public VerticalTableCaptionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-vcap-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // Renders the body on a white canvas and returns the bounding box of the
    // painted (non-white) region as (left, top, width, height), or all-zero when
    // nothing painted.
    private (int Left, int Top, int Width, int Height) PaintedBounds(string body, int width, int height)
    {
        var html = $@"<!DOCTYPE html>
<style> html, body {{ margin: 0; background: white; }} </style>
<body>{body}</body>";
        var file = Path.Combine(_tempDir, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);

        var runner = new WptTestRunner(width, height);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _tempDir);

        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var p = bmp.GetPixel(x, y);
            if (p.R < 245 || p.G < 245 || p.B < 245)
            {
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }
        return maxX < 0 ? (0, 0, 0, 0) : (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // The vlr-020 caption: a height:9em (=180px at 20px/em) vertical-lr
    // table-caption whose block-level children carry 1em top+bottom borders.
    // Six columns amplify the border-axis bug (each wrongly-placed 2em border
    // adds ~40px to the block/width axis) so the width threshold catches it.
    private const string Col =
        "<div style=\"background:blue;border-top:blue solid 1em;border-bottom:blue solid 1em\">XXXXXXX</div>";
    private const string Caption =
        "<div style=\"display:table-caption;height:9em;writing-mode:{0};font:20px/1 monospace;color:yellow\">" +
          Col + Col + Col + Col + Col + Col +
        "</div>";

    [Fact]
    public void VerticalLrCaption_DoesNotFillViewport()
    {
        // Before the containing-block fix the caption's children took the viewport
        // width, which the rotation mapped onto the caption's physical height, so
        // the blue box filled the whole 300px-tall viewport. After it, the height
        // is bounded by the 9em (=180px) inline size plus the children's borders.
        var bounds = PaintedBounds(string.Format(Caption, "vertical-lr"), 640, 480);

        Assert.True(bounds.Height > 0, "caption painted nothing");
        Assert.True(bounds.Height <= 260,
            $"vertical-lr caption is {bounds.Height}px tall — expected ~180px (9em) + borders; " +
            "a viewport-tall box means its children escaped the caption's inline size " +
            "(table-caption not treated as their containing block).");

        // The border/padding-axis fix: six ~1-char-wide columns (block axis =
        // physical width). Physical width tracks the columns' block-size (the 1em
        // top/bottom borders belong on the inline/height axis, not here). Reading
        // those borders as width would add ~2em per column and blow the threshold.
        Assert.True(bounds.Width <= 180,
            $"vertical-lr caption is {bounds.Width}px wide — the child border-top/bottom " +
            "inflated the block (width) axis; they belong on the inline (height) axis.");
    }

    [Fact]
    public void SidewaysLrCaption_CollapsesToBoundedRegion()
    {
        // sideways-lr shares vertical-lr's block flow (left→right); it takes the
        // same containing-block and border/padding-axis code paths, so the painted
        // box must collapse to the same bounded shape. (The bottom→top inline
        // direction and 90° counter-clockwise glyph facing are covered by the WPT
        // reftest block-flow-direction-slr-060, which this shape mirrors.)
        var bounds = PaintedBounds(string.Format(Caption, "sideways-lr"), 640, 480);

        Assert.True(bounds.Height > 0, "sideways-lr caption painted nothing");
        Assert.True(bounds.Height <= 260,
            $"sideways-lr caption is {bounds.Height}px tall — expected ~180px (9em) + borders.");
        Assert.True(bounds.Width <= 180,
            $"sideways-lr caption is {bounds.Width}px wide — border axis regressed.");
    }
}
