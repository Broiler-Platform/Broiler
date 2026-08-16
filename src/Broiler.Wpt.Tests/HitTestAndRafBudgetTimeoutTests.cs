using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guards for two more timeout causes from the 2026-08-16 WPT run, both of the
/// "one full document layout per iteration" shape that WPT #1113 and #1115 already fixed
/// elsewhere (see <see cref="MulticolCheckLayoutTimeoutTests"/> and
/// <see cref="LiveGeometryQueryTimeoutTests"/>).
/// </summary>
/// <remarks>
/// The fixtures are written here rather than pointed at a WPT path so the guards run without a
/// WPT checkout. Each is the offending shape reduced to its essentials, and each ceiling is set
/// far above the post-fix time but far below the pre-fix one, so a shared CI box does not make
/// them flaky while a reintroduced O(N) factor still trips them.
/// </remarks>
public class HitTestAndRafBudgetTimeoutTests : IDisposable
{
    private readonly string _root;

    public HitTestAndRafBudgetTimeoutTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "broiler-hittest-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private async Task<bool> RendersWithin(string html, int seconds)
    {
        var file = Path.Combine(_root, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);

        var runner = new WptTestRunner(600, 600);
        var sw = Stopwatch.StartNew();
        var render = Task.Run(() => { using var b = runner.RenderHtmlFileBitmapPublic(file, _root); });
        var completed = await Task.WhenAny(render, Task.Delay(TimeSpan.FromSeconds(seconds))) == render;
        sw.Stop();
        return completed;
    }

    /// <summary>
    /// <c>elementFromPoint</c> walks the whole element tree asking each element for its border
    /// box. Every one of those reads used to open — and tear down — its own shared-geometry
    /// snapshot, which is a deep document clone plus a full cascade plus a full layout, and the
    /// teardown also dropped the computed-props memo the next element's ancestor walk needed. So
    /// one hit test cost O(elements) document layouts. On WPT's
    /// css-break/table/repeated-section/special-elements-crash.html — 900 elementFromPoint calls
    /// over a fragmented multicol table — that was >300s and 4.4 GB against a 30s budget; it is
    /// 15s now. The walk opens one pass for the whole query instead.
    /// </summary>
    [Fact]
    public async Task ElementFromPoint_OverManyElements_DoesNotLayOutOncePerElement()
    {
        var boxes = string.Concat(Enumerable.Range(0, 120).Select(i =>
            $"<div class='b' style='position:absolute;left:{i % 20 * 30}px;top:{i / 20 * 30}px;width:28px;height:28px'></div>"));

        var html = $@"<!DOCTYPE html>
<html><body style=""margin:0"">
{boxes}
<div id=""out"" style=""position:absolute;left:0;top:400px"">pending</div>
<script>
  var n = 0;
  for (var i = 0; i < 150; i++) {{
    if (document.elementFromPoint(10 + (i % 20) * 30, 10 + (i % 6) * 30)) n++;
  }}
  document.getElementById('out').textContent = 'hits:' + n;
</script>
</body></html>";

        // ~150 queries over ~120 elements. Pre-fix that is ~18,000 full document layouts; post-fix
        // it is 150. A 40s ceiling is generous for the fixed path and unreachable for the old one.
        Assert.True(await RendersWithin(html, 40),
            "elementFromPoint is building a geometry snapshot per element again — " +
            "the hit-test walk must run inside a single WithLayoutGeometryCache pass.");
    }

    /// <summary>
    /// The runner replaces rAF with a synchronous stub, so a self-rescheduling loop recurses
    /// natively instead of yielding to a queue. The real rAF it replaces is capped
    /// (BrowserEventLoop.DrainAll's maxIterations); the stub had no cap, so a loop whose exit
    /// condition this engine never satisfies ran until the CLR stack gave out — two full document
    /// layouts per level on the way. That is what made
    /// css-fonts/variations/font-opentype-collections.html an 88s timeout; it is 4s now.
    /// </summary>
    /// <remarks>
    /// Asserts the frame budget itself rather than a wall-clock ceiling. A timing guard does not
    /// work here: without the cap this shape reaches CLR stack exhaustion and raises RangeError
    /// quickly enough to stay well inside any ceiling loose enough not to be flaky — the real
    /// test is slow because every level also re-reads layout, which a reduced fixture does not
    /// reproduce. Counting frames pins the invariant directly and deterministically.
    /// </remarks>
    [Fact]
    public void NonConvergingRequestAnimationFrameLoop_StopsAtTheFrameBudget()
    {
        // The exit condition is never true, so the loop only ends when the stub refuses to
        // recurse further. try/catch so an unbounded stub's RangeError still reaches the paint.
        var html = @"<!DOCTYPE html>
<html><body style=""margin:0"">
<div id=""box"" style=""width:200px;height:200px;background:rgb(200,0,0)""></div>
<script>
  var spins = 0;
  function frame() { spins++; if (spins < 0) return; window.requestAnimationFrame(frame); }
  try { window.requestAnimationFrame(frame); } catch (e) {}
  if (spins <= 256) document.getElementById('box').style.background = 'rgb(0,180,0)';
</script>
</body></html>";

        var file = Path.Combine(_root, "raf-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);
        var runner = new WptTestRunner(200, 200);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _root);
        var pixel = bmp.GetPixel(100, 100);

        Assert.True(pixel.G > 100 && pixel.R < 100,
            $"expected green (frames capped at the stub's budget), got rgb({pixel.R},{pixel.G},{pixel.B}). " +
            "The synchronous rAF stub is recursing without a cap again — a non-converging loop " +
            "will run to CLR stack exhaustion, laying the document out twice per level.");
    }
}
