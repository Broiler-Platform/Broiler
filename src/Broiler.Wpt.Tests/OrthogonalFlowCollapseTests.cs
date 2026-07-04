using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression test for CSS Writing Modes 4 §7.3 (auto-sizing in orthogonal
/// flows): a box establishing an orthogonal flow — a vertical writing-mode box
/// inside a non-vertical containing block — with an auto inline size is sized to
/// <em>fit-content</em>, not stretched to the containing block's perpendicular
/// inline size.
///
/// <para>Before the fix, the vertical-flow prototype laid such a box out in a
/// logical horizontal frame where it filled the containing block's width; the
/// post-layout rotation then mapped that width onto the box's physical height, so
/// an empty <c>writing-mode: vertical-rl</c> box rotated into a viewport-tall
/// strip instead of collapsing. This surfaced in the WPT
/// <c>css-grid/grid-lanes/subgrid/…/row-subgrid-auto-fill-*</c> reftests: the
/// <c>grid-lanes</c> container drops to a block (no browser ships grid-lanes
/// unflagged) and its child is an empty <c>vertical-rl</c> grid, which Chromium
/// collapses to blank but Broiler painted as a full-viewport light-grey block — a
/// 0% pixel match. The orthogonal box must collapse to its content instead.</para>
/// </summary>
public class OrthogonalFlowCollapseTests : IDisposable
{
    private readonly string _tempDir;

    public OrthogonalFlowCollapseTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-ortho-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // Renders the body on a white canvas and returns the fraction of non-white
    // pixels — i.e. how much of the viewport the content covers.
    private double NonWhiteFraction(string body, int width, int height)
    {
        var html = $@"<!DOCTYPE html>
<style> html, body {{ margin: 0; background: white; }} </style>
<body>{body}</body>";
        var file = Path.Combine(_tempDir, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);

        var runner = new WptTestRunner(width, height);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _tempDir);

        int nonWhite = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var p = bmp.GetPixel(x, y);
            if (p.R < 245 || p.G < 245 || p.B < 245)
                nonWhite++;
        }
        return (double)nonWhite / (width * height);
    }

    [Fact]
    public void EmptyVerticalRlBox_InAutoHeightBlock_CollapsesInsteadOfFillingViewport()
    {
        // .g is an auto-height block (a light-grey background makes any spurious
        // fill visible); its child is an empty vertical-rl grid whose only item is
        // an empty 10px box — the row-subgrid-auto-fill-007 shape once grid-lanes
        // has dropped to a block. Chromium collapses this to blank.
        var body =
            "<div style=\"background:lightgrey\">" +
              "<div style=\"display:grid;writing-mode:vertical-rl;background:black\">" +
                "<div style=\"min-width:10px;min-height:0;background:grey\"></div>" +
              "</div>" +
            "</div>";

        double frac = NonWhiteFraction(body, 400, 300);

        // Before the fix the vertical box filled the container width and rotated
        // into a viewport-tall strip (~100% of the canvas non-white). After it
        // collapses to its content — at most a tiny sliver.
        Assert.True(frac < 0.05,
            $"empty vertical-rl box did not collapse: {frac:P1} of the canvas is non-white " +
            "(orthogonal-flow auto inline-size regressed to filling the containing block).");
    }

    [Fact]
    public void VerticalRlBox_WithDefiniteHeightContainingBlock_IsUnaffected()
    {
        // A definite containing-block block size (an explicit-height parent) keeps
        // the existing behaviour: the guard only engages for an indefinite (auto)
        // orthogonal available size, so this box still paints. Locks the fix's
        // scope so it cannot silently start collapsing definite-size vertical boxes.
        var body =
            "<div style=\"height:100px;background:lightgrey\">" +
              "<div style=\"display:grid;writing-mode:vertical-rl;background:black\">" +
                "<div style=\"width:20px;height:20px;background:grey\"></div>" +
              "</div>" +
            "</div>";

        double frac = NonWhiteFraction(body, 400, 300);

        Assert.True(frac > 0.01,
            $"definite-height vertical container unexpectedly collapsed: only {frac:P2} non-white.");
    }
}
