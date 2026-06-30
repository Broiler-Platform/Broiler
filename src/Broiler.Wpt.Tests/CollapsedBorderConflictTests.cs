using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression tests for CSS2.1 §17.6.2.1 collapsed-border conflict resolution
/// (WPT issue #1143, css/CSS2/tables/border-conflict-*). In the
/// <c>border-collapse:collapse</c> model adjacent cells share one border;
/// Broiler resolves each internal edge to a single winner — <c>hidden</c>
/// suppresses the edge, otherwise the wider border wins, then the
/// higher-priority style. Before this, every cell painted its own borders, so
/// a losing red border still showed.
/// </summary>
public class CollapsedBorderConflictTests : IDisposable
{
    private readonly string _tempDir;

    public CollapsedBorderConflictTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-collapse-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private (int red, int green) RenderCounts(string html)
    {
        var file = Path.Combine(_tempDir, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);
        var runner = new WptTestRunner(300, 200);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _tempDir);
        int red = 0, green = 0;
        for (int y = 0; y < 200; y++)
            for (int x = 0; x < 300; x++)
            {
                var p = bmp.GetPixel(x, y);
                // `green` is (0,128,0) and `lime` is (0,255,0) — detect a
                // green-dominant pixel, not a fixed channel threshold.
                if (p.R > 150 && p.G < 100 && p.B < 100) red++;
                else if (p.G > p.R + 40 && p.G > p.B + 40 && p.G > 80) green++;
            }
        return (red, green);
    }

    [Fact]
    public void HiddenBorder_SuppressesAdjacentConflictingBorders()
    {
        // Mirrors border-conflict-w-001: a centre cell with a `hidden` border;
        // its neighbours carry red borders only on the edges they share with it.
        // `hidden` wins every shared edge, so no red must survive; the outer
        // green borders remain.
        var html = @"<!DOCTYPE html><html><head><style>
table { border-collapse: collapse; }
td { border: 5px solid green; height: 2em; width: 40px; }
.c5 { border: 10px hidden red; }
.c2 { border-bottom-color: red; }
.c6 { border-left-color: red; }
.c8 { border-top-color: red; }
.c4 { border-right-color: red; }
</style></head><body>
<table>
 <tr><td>a</td><td class='c2'>b</td><td>c</td></tr>
 <tr><td class='c4'>d</td><td class='c5'>e</td><td class='c6'>f</td></tr>
 <tr><td>g</td><td class='c8'>h</td><td>i</td></tr>
</table></body></html>";
        var (red, green) = RenderCounts(html);
        Assert.True(green > 0, "winning green collapsed borders should paint");
        Assert.Equal(0, red);
    }

    [Fact]
    public void WiderBorder_WinsAtSharedEdge()
    {
        // Two cells sharing one edge: the right cell's wider green border must
        // win over the left cell's thin red one at that edge. The left cell's
        // red is confined to its outer edges, so green must dominate.
        var html = @"<!DOCTYPE html><html><head><style>
table { border-collapse: collapse; }
td { height: 2em; width: 40px; }
#a { border: 2px solid red; }
#b { border: 12px solid green; }
</style></head><body>
<table><tr><td id='a'>a</td><td id='b'>b</td></tr></table></body></html>";
        var (red, green) = RenderCounts(html);
        // The shared edge (the thickest band between the cells) is green, and
        // cell b's thick green frame dominates over cell a's thin red outline.
        Assert.True(green > red, $"wider green border should dominate (green={green}, red={red})");
    }
}
