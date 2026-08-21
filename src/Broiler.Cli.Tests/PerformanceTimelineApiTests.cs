namespace Broiler.Cli.Tests;

/// <summary>
/// The <c>performance</c> object's Performance Timeline surface
/// (see <c>DomBridge.RegisterPerformanceObject</c>).
/// </summary>
/// <remarks>
/// A capture records no performance entries, so the getters answer with an empty list — the answer
/// a browser gives before anything has been recorded, and not the same thing as the method being
/// absent. <c>getEntriesByName</c> was missing, and a page does not reach it behind a feature test:
/// <c>performance</c> and <c>PerformanceObserver</c> both existing is the guard it writes, and the
/// call comes after it. <c>duckduckgo.com</c>'s first-contentful-paint pixel opens with
/// <c>performance.getEntriesByName('first-contentful-paint')</c> on exactly that guard, and threw
/// <c>TypeError: undefined is not a function</c> — taking the <c>PerformanceObserver</c> fallback
/// in the same <c>try</c> block with it, so the page never observed the paint it was asking about
/// either.
/// </remarks>
public class PerformanceTimelineApiTests
{
    private static string Run(string script)
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
" + script + @"
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
    }

    /// <summary>
    /// What the script actually wrote, rather than the whole serialized document — which still
    /// carries the <c>&lt;script&gt;</c> source, so a marker word searched for across it would match
    /// the code that pushes the marker as readily as the marker itself.
    /// </summary>
    private static string ResultText(string document) =>
        System.Text.RegularExpressions.Regex.Match(
            document,
            @"id=""result""[^>]*>(?<text>.*?)</div>",
            System.Text.RegularExpressions.RegexOptions.Singleline)
            .Groups["text"].Value;

    [Fact(Timeout = 600000)]
    public void Performance_Timeline_Members_Are_Callable()
    {
        var result = Run(@"
var names = ['now','getEntries','getEntriesByType','getEntriesByName','mark','measure',
             'clearMarks','clearMeasures','clearResourceTimings','setResourceTimingBufferSize',
             'toJSON'];
var allCallable = true;
for (var i = 0; i < names.length; i++) {
  if (typeof performance[names[i]] !== 'function') allCallable = false;
}
r.push(allCallable);
r.push(typeof performance.timeOrigin === 'number');
");
        Assert.Contains("true,true", result);
    }

    /// <summary>
    /// The three getters return a real (empty) entry list, so the indexing and length reads a
    /// caller does next work rather than throwing on <c>undefined</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Entry_Getters_Return_An_Empty_List()
    {
        var result = Run(@"
var byName = performance.getEntriesByName('first-contentful-paint');
var byType = performance.getEntriesByType('navigation');
var all = performance.getEntries();
r.push(Array.isArray(byName) && byName.length === 0);
r.push(Array.isArray(byType) && byType.length === 0);
r.push(Array.isArray(all) && all.length === 0);
r.push(byName[0] === undefined);
");
        Assert.Contains("true,true,true,true", result);
    }

    /// <summary>
    /// <c>duckduckgo.com</c>'s first-contentful-paint pixel, in the shape the page ships it: the
    /// <c>getEntriesByName</c> call and, when it reports nothing, the <c>PerformanceObserver</c>
    /// that waits for the paint instead. Both live in one <c>try</c>, so the throw cost the page
    /// the observer as well as the reading.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Fcp_Pixel_Reaches_Its_PerformanceObserver_Fallback()
    {
        var result = Run(@"
if (typeof PerformanceObserver === 'undefined' || typeof performance === 'undefined') {
  r.push('guard-returned-early');
} else {
  try {
    var entries = performance.getEntriesByName('first-contentful-paint');
    if (entries[0]) {
      r.push('reported-directly');
    } else {
      new PerformanceObserver(function () {}).observe({ type: 'paint', buffered: true });
      r.push('observer-installed');
    }
  } catch (e) {
    r.push('threw:' + e.message);
  }
}
");
        Assert.Equal("observer-installed", ResultText(result));
    }

    /// <summary>
    /// Telemetry that ships timings reaches <c>toJSON</c> through <c>JSON.stringify(performance)</c>
    /// as often as by name.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Performance_Serialises_Through_JSON_Stringify()
    {
        var result = Run(@"
var text = JSON.stringify(performance);
r.push(typeof text === 'string');
r.push(text.indexOf('timeOrigin') !== -1);
");
        Assert.Contains("true,true", result);
    }
}
