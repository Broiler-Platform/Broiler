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
/// monotonic origin <c>performance.now()</c> uses. The network-phase marks are <b>not</b> measured —
/// the document is fetched by the capture host before the bridge exists — and report <c>0</c>, the
/// specification's "no information" value, so the arithmetic yields a number rather than NaN without
/// describing a fetch that was never observed.
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

    // The unmeasured network phases are present as numbers so the arithmetic works.
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

    // The entry members that already answered must be untouched.
    [Fact(Timeout = 600000)]
    public void The_Existing_Entry_Members_Are_Unchanged()
    {
        var result = ExecOnLoad(@"
            var e = performance.getEntriesByType('navigation')[0];
            document.getElementById('result').textContent =
                'V:' + [e.entryType, e.type, e.startTime, e.duration, e.redirectCount].join('|');
        ");
        Assert.Contains("V:navigation|navigate|0|0|0", result);
    }
}
