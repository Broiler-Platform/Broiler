using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for WPT #1115: the box-geometry estimators behind the live
/// JS geometry getters (<c>offsetTop</c>/<c>offsetLeft</c>/<c>offset*</c>/
/// <c>client*</c>/<c>scroll*</c>/<c>getBoundingClientRect</c>) recurse up
/// (containing block), down (auto content extent) and across (preceding
/// siblings), so a single query re-derives the same sub-rects combinatorially —
/// exponential in DOM nesting depth. WPT #1113 memoized that recursion only for
/// the static check-layout assertion pass; <c>align-content-table-cell.html</c>
/// instead drives ~30 live <c>offsetTop</c> reads from a testharness script and
/// so kept timing out (it was one of the 2 survivors after #1113). The fix wraps
/// each live geometry getter in <c>DomBridge.WithLayoutGeometryCache</c> for the
/// duration of that one synchronous query (no JS runs mid-getter → the layout
/// snapshot is static → caching is sound). This test fails fast if the
/// exponential blow-up returns to the live-query path.
/// </summary>
public class LiveGeometryQueryTimeoutTests
{
    [Fact]
    public async Task AlignContentTableCell_OffsetTopReads_RenderWellWithinRunnerTimeout()
    {
        var wptRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", "tests", "wpt"));
        var file = Path.Combine(wptRoot, "css", "css-align", "blocks", "align-content-table-cell.html");
        Assert.True(File.Exists(file), $"missing fixture {file}");

        var runner = new WptTestRunner(800, 800);
        var sw = Stopwatch.StartNew();
        var render = Task.Run(() => { using var b = runner.RenderHtmlFileBitmapPublic(file, wptRoot); });
        // Pre-fix this never completed (hard hang past the runner's 30s per-test
        // timeout); ~2s after. A 20s ceiling catches any reintroduced
        // super-linearity in the live-query geometry path.
        var completed = await Task.WhenAny(render, Task.Delay(System.TimeSpan.FromSeconds(20))) == render;
        sw.Stop();

        Assert.True(completed,
            $"align-content-table-cell: render did not complete within 20s ({sw.ElapsedMilliseconds}ms) — " +
            "the live offsetTop geometry recursion is exponential again.");
    }
}
