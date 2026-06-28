using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for WPT #1113: the check-layout assertion evaluator's
/// box-geometry estimators recursed without memoization, so deep css-align /
/// css-anchor-position trees took exponential time and tripped the runner's
/// per-test timeout (these <c>columns:3</c> multicol tests went from a wrong
/// bitmap to a hard hang between runs #1105 and #1113). With the geometry caches
/// in <c>DomBridge.WithLayoutGeometryCache</c> they render in ~1s; this test
/// fails fast if the exponential blow-up returns.
/// </summary>
public class MulticolCheckLayoutTimeoutTests
{
    [Theory]
    [InlineData("align-content-block-002.html")]
    [InlineData("align-content-block-004.html")]
    [InlineData("align-content-block-006.html")]
    public async Task MulticolCheckLayoutTest_RendersWellWithinRunnerTimeout(string name)
    {
        var wptRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", "tests", "wpt"));
        var file = Path.Combine(wptRoot, "css", "css-align", "blocks", name);
        Assert.True(File.Exists(file), $"missing fixture {file}");

        var runner = new WptTestRunner(800, 800);
        var sw = Stopwatch.StartNew();
        var render = Task.Run(() => { using var b = runner.RenderHtmlFileBitmapPublic(file, wptRoot); });
        // Pre-fix these never completed; ~1s after. A 20s ceiling (well under the
        // runner's 30s per-test timeout) catches any reintroduced super-linearity.
        var completed = await Task.WhenAny(render, Task.Delay(System.TimeSpan.FromSeconds(20))) == render;
        sw.Stop();

        Assert.True(completed,
            $"{name}: render did not complete within 20s ({sw.ElapsedMilliseconds}ms) — " +
            "the check-layout geometry recursion is exponential again.");
    }
}
