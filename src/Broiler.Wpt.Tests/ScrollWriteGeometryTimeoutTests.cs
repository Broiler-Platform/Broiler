using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for WPT issue #1682: a script that writes <c>scrollTop</c>/<c>scrollLeft</c> over
/// a collection paid a whole-document layout per write.
/// <para>
/// The shared geometry snapshot was scoped to one read pass, and a pass is one <em>query</em> — so
/// clamping a single scroll offset, which reads <c>scrollWidth</c>/<c>clientWidth</c>/
/// <c>scrollHeight</c>/<c>clientHeight</c>, laid the document out four times.
/// <c>css/css-overflow/overflow-alignment-block-002.html</c> scrolls 84 containers on both axes
/// against a 1 617-element document that lays out in ~0.9s, so the render did not finish in ten
/// minutes; it and its nineteen siblings were reported as timeouts rather than as pixel results (all
/// twenty <c>css/css-overflow</c> entries in that run). Retaining the snapshot across passes while
/// its inputs are unchanged makes the whole loop one layout, and the render finishes in ~6s.
/// </para>
/// <para>
/// The 20s ceiling is well under the runner's 30s per-test budget, so this fails fast if the
/// per-query layout comes back. <c>Broiler.Cli.Tests.RetainedLayoutSnapshotTests</c> holds the other
/// half — that the retained snapshot is given up when a layout input moves.
/// </para>
/// </summary>
public class ScrollWriteGeometryTimeoutTests
{
    [Fact]
    public async Task OverflowAlignment_ScrollWrites_RenderWellWithinRunnerTimeout()
    {
        var wptRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", "tests", "wpt"));
        var file = Path.Combine(wptRoot, "css", "css-overflow", "overflow-alignment-block-002.html");
        Assert.True(File.Exists(file), $"missing fixture {file}");

        var runner = new WptTestRunner(800, 800);
        var sw = Stopwatch.StartNew();
        var render = Task.Run(() => { using var b = runner.RenderHtmlFileBitmapPublic(file, wptRoot); });
        var completed = await Task.WhenAny(render, Task.Delay(System.TimeSpan.FromSeconds(20))) == render;
        sw.Stop();

        Assert.True(completed,
            $"overflow-alignment-block-002: render did not complete within 20s ({sw.ElapsedMilliseconds}ms) — " +
            "the geometry snapshot is being rebuilt per scroll write again.");
    }
}
