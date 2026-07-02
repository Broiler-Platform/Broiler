using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for WPT #1173 (issue: <c>anchor-position-writing-modes-001.html</c>
/// reported as a hard <c>Timeout</c>). Enabling <c>UseSharedLayoutGeometry</c> (issue
/// #1170) routed every live <c>getBoundingClientRect</c>/<c>offset*</c> query through a
/// full renderer layout. This test drives 6^4 = 1296 synchronous <c>test()</c> bodies at
/// load, each mutating <c>classList</c> (bumping the document version) and reading
/// <c>target.getBoundingClientRect()</c> — so every read forces a fresh full re-cascade.
/// The cascade was quadratic per query (<c>CssStyleEngine.CollectCascadedDeclarations</c>
/// re-matched every UA + author selector for the same element ~25× per query, ~68k
/// selector scans), pushing the render past the runner's 30s per-test cap. Memoizing the
/// declared cascade collapses that to a linear pass. This test fails fast if the
/// per-query re-cascade blows up again.
/// </summary>
public class CascadeMemoizationTimeoutTests
{
    [Fact]
    public async Task AnchorPositionWritingModes_ManyGetBoundingClientRect_RenderWellWithinRunnerTimeout()
    {
        var wptRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", "tests", "wpt"));
        var file = Path.Combine(wptRoot, "css", "css-anchor-position", "anchor-position-writing-modes-001.html");
        Assert.True(File.Exists(file), $"missing fixture {file}");

        var runner = new WptTestRunner(800, 800);
        var sw = Stopwatch.StartNew();
        var render = Task.Run(() => { using var b = runner.RenderHtmlFileBitmapPublic(file, wptRoot); });
        // Pre-fix this ran ~43s (past the runner's 30s per-test cap → reported Timeout);
        // ~11s after. A 25s ceiling stays under the 30s cap the real runner enforces yet
        // leaves ample headroom for the fixed path, so it only trips if the per-query
        // cascade goes super-linear again.
        var completed = await Task.WhenAny(render, Task.Delay(System.TimeSpan.FromSeconds(25))) == render;
        sw.Stop();

        Assert.True(completed,
            $"anchor-position-writing-modes-001: render did not complete within 25s ({sw.ElapsedMilliseconds}ms) — " +
            "the per-query declared-cascade recomputation is super-linear again (WPT #1173).");
    }
}
