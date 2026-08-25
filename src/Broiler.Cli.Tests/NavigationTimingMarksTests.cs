using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;

namespace Broiler.Cli.Tests;

/// <summary>
/// The <c>PerformanceNavigationTiming</c> entry carries its timing attributes (Navigation Timing §4).
/// </summary>
/// <remarks>
/// The entry had none at all, and absent is the one thing they may not be: they are read inside
/// subtraction far more often than alone, so the RUM idioms built on them — <c>responseEnd -
/// requestStart</c>, <c>domComplete - domInteractive</c> — produced <c>NaN</c> rather than a
/// duration, silently.
/// <para>
/// The document-lifecycle marks are <b>measured</b> by the bridge's load sequence against the same
/// monotonic origin <c>performance.now()</c> uses. The network-phase marks are measured by whichever
/// host performed the fetch and handed its measurements across; <b>this fixture supplies none</b> —
/// it hands HTML to the bridge as a string, which is a document with no fetch to describe — so they
/// report <c>0</c>, the specification's "no information" value. That case is pinned here; the
/// measured one is pinned in <see cref="NavigationTimingNetworkPhasesTests"/>.
/// </para>
/// </remarks>
public class NavigationTimingMarksTests
{
    // The marks are only meaningful once the load sequence has stamped them, so every assertion runs
    // from a load listener.
    private static string ExecOnLoad(string jsCode)
    {
        var html = $@"<!doctype html>
<html><head><title>T</title></head><body><div id=""result""></div>
<script>
window.addEventListener('load', function () {{
{jsCode}
}});
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
    }

    [Fact(Timeout = 600000)]
    public void The_Lifecycle_Marks_Are_Numbers()
    {
        var result = ExecOnLoad(@"
            var e = performance.getEntriesByType('navigation')[0];
            var keys = ['domInteractive', 'domContentLoadedEventStart', 'domContentLoadedEventEnd',
                        'domComplete', 'loadEventStart', 'loadEventEnd'];
            document.getElementById('result').textContent =
                'V:' + keys.map(function (k) { return typeof e[k]; }).join(',');
        ");
        Assert.Contains("V:number,number,number,number,number,number", result);
    }

    // The defect this closes: the idioms that silently produced NaN.
    [Fact(Timeout = 600000)]
    public void The_Duration_Idioms_Produce_Numbers_Rather_Than_NaN()
    {
        var result = ExecOnLoad(@"
            var e = performance.getEntriesByType('navigation')[0];
            var durations = [
                e.responseEnd - e.requestStart,
                e.domComplete - e.domInteractive,
                e.domContentLoadedEventEnd - e.domContentLoadedEventStart,
                e.responseEnd - e.fetchStart
            ];
            document.getElementById('result').textContent =
                'V:anyNaN=' + durations.some(isNaN);
        ");
        Assert.Contains("V:anyNaN=false", result);
    }

    // Measured, not asserted: the marks advance in the order the load sequence reaches them.
    [Fact(Timeout = 600000)]
    public void The_Lifecycle_Marks_Are_Monotonically_Ordered()
    {
        var result = ExecOnLoad(@"
            var e = performance.getEntriesByType('navigation')[0];
            document.getElementById('result').textContent = 'V:' +
                (e.domInteractive <= e.domContentLoadedEventStart) + ',' +
                (e.domContentLoadedEventStart <= e.domContentLoadedEventEnd) + ',' +
                (e.domContentLoadedEventEnd <= e.domComplete) + ',' +
                (e.domComplete <= e.loadEventStart);
        ");
        Assert.Contains("V:true,true,true,true", result);
    }

    // Real measurements, so they are past the time origin rather than zero.
    [Fact(Timeout = 600000)]
    public void The_Lifecycle_Marks_Are_Actually_Measured()
    {
        var result = ExecOnLoad(@"
            var e = performance.getEntriesByType('navigation')[0];
            document.getElementById('result').textContent =
                'V:' + (e.domInteractive > 0) + ',' + (e.domComplete > 0);
        ");
        Assert.Contains("V:true,true", result);
    }

    // A mark and a performance.now() reading are two points on one timeline — they share an origin.
    [Fact(Timeout = 600000)]
    public void The_Marks_Share_The_Timeline_With_PerformanceNow()
    {
        var result = ExecOnLoad(@"
            var e = performance.getEntriesByType('navigation')[0];
            document.getElementById('result').textContent =
                'V:' + (e.domComplete <= performance.now());
        ");
        Assert.Contains("V:true", result);
    }

    // Zero because the phase genuinely did not occur, which is the specified value for each.
    [Fact(Timeout = 600000)]
    public void The_Phases_That_Did_Not_Happen_Report_Zero()
    {
        var result = ExecOnLoad(@"
            var e = performance.getEntriesByType('navigation')[0];
            var keys = ['redirectStart', 'redirectEnd', 'unloadEventStart', 'unloadEventEnd', 'workerStart'];
            document.getElementById('result').textContent =
                'V:' + keys.map(function (k) { return e[k]; }).join(',');
        ");
        Assert.Contains("V:0,0,0,0,0", result);
    }

    // The network phases are present as numbers so the arithmetic works, measured or not — this
    // fixture measures none, having no fetch.
    [Fact(Timeout = 600000)]
    public void The_Network_Phase_Marks_Are_Present_As_Numbers()
    {
        var result = ExecOnLoad(@"
            var e = performance.getEntriesByType('navigation')[0];
            var keys = ['fetchStart', 'domainLookupStart', 'domainLookupEnd', 'connectStart',
                        'connectEnd', 'secureConnectionStart', 'requestStart', 'responseStart',
                        'responseEnd', 'transferSize', 'encodedBodySize', 'decodedBodySize'];
            var allNumbers = keys.every(function (k) { return typeof e[k] === 'number'; });
            document.getElementById('result').textContent = 'V:' + allNumbers;
        ");
        Assert.Contains("V:true", result);
    }

    // The entry members that already answered must be untouched. duration is NOT among them: it was
    // a hardcoded 0 and is now loadEventEnd (see below), so it is asserted there rather than here.
    [Fact(Timeout = 600000)]
    public void The_Existing_Entry_Members_Are_Unchanged()
    {
        var result = ExecOnLoad(@"
            var e = performance.getEntriesByType('navigation')[0];
            document.getElementById('result').textContent =
                'V:' + [e.entryType, e.type, e.startTime, e.redirectCount].join('|');
        ");
        Assert.Contains("V:navigation|navigate|0|0", result);
    }

    /// <summary>
    /// <c>duration</c> is <c>loadEventEnd - startTime</c> (Navigation Timing §4), and a navigation
    /// entry's <c>startTime</c> is 0, so it is <c>loadEventEnd</c>. It was pinned at a hardcoded 0 —
    /// correct only <em>until</em> the load event ends, which is not when a page reads it.
    /// <c>entry.duration</c> is the shortest way to write "how long did this page take", so a 0 there
    /// is a plausible number rather than an absent one, and nothing distinguishes the two.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Duration_Is_LoadEventEnd()
    {
        // Read from a task scheduled BY the load handler, not from the handler itself: loadEventEnd
        // is stamped when the load dispatch returns, so inside it the mark has not happened and both
        // it and duration are still 0 — agreeing, but not yet saying anything. A browser answers the
        // same way, which is why analytics read this from a timeout rather than from `onload`.
        var result = ExecOnLoad(@"
            setTimeout(function () {
                var e = performance.getEntriesByType('navigation')[0];
                document.getElementById('result').textContent =
                    'V:' + (e.duration === e.loadEventEnd) + ',' + (e.duration > 0);
            }, 0);
        ");
        Assert.Contains("V:true,true", result);
    }

    /// <summary>
    /// Read before the load event, <c>duration</c> is 0 — the value the specification gives a moment
    /// that has not been reached. The old constant was right for exactly this case, which is what
    /// kept it looking correct.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Duration_Is_Zero_Before_The_Load_Event()
    {
        var html = @"<!doctype html>
<html><head><title>T</title></head><body><div id=""result""></div>
<script>
var e = performance.getEntriesByType('navigation')[0];
document.getElementById('result').textContent = 'V:' + e.duration;
</script>
</body></html>";
        Assert.Contains("V:0", CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page"));
    }
}
