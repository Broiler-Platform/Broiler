using System.Net;
using System.Text;
using System.Text.Json;
using Broiler.HtmlBridge.Net;

namespace Broiler.Cli.Tests;

/// <summary>
/// The <c>PerformanceNavigationTiming</c> entry's <b>network</b> half — <c>fetchStart</c> through
/// <c>responseEnd</c>, and Resource Timing's <c>transferSize</c>/<c>encodedBodySize</c>/
/// <c>decodedBodySize</c> trio — reports what the host measured while fetching the document.
/// </summary>
/// <remarks>
/// <para>
/// They existed but reported <c>0</c>, which in Navigation Timing means "not observed" rather than
/// "instantaneous": the RUM arithmetic built on them yielded a number instead of <c>NaN</c>, but not
/// a measurement. Nothing below the CLI could do better — the document is fetched before the bridge
/// exists, and the bridge's time origin was stamped when it built the <c>performance</c> object,
/// which is already after the fetch, so any real value would have been negative and clamped to the
/// specification's floor of <c>0</c>.
/// </para>
/// <para>
/// So the fix has two halves and both are pinned here: the host measures the fetch, and the host's
/// <em>navigation start</em> becomes the document's time origin. The second is what makes the first
/// expressible, which is why <see cref="The_Marks_Share_One_Timeline_With_PerformanceNow"/> matters
/// as much as the measurements themselves.
/// </para>
/// </remarks>
public class NavigationTimingNetworkPhasesTests
{
    private const string Page = """
<!doctype html><html><head><title>T</title></head><body><div id="result"></div></body></html>
""";

    // ─────────────────────────── the bridge reports what it is given ───────────────────────────

    /// <summary>
    /// The binding half on its own: a bridge handed a timing reports those marks, rather than the
    /// zeros it reports when there is nothing to report.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Supplied_Timing_Reaches_The_Entry()
    {
        var timing = DocumentFetchTiming.StartNavigation();
        timing.MarkFetchStart();
        timing.MarkDomainLookupStart();
        timing.MarkDomainLookupEnd();
        timing.MarkConnectStart();
        timing.MarkConnectEnd();
        timing.MarkRequestStart();
        timing.MarkResponseStart();
        timing.MarkResponseEnd();
        timing.RecordBodySizes(transferSize: 1234, encodedBodySize: 1000, decodedBodySize: 1000);

        var report = Report(
            """
            var e = performance.getEntriesByType('navigation')[0];
            document.getElementById('result').textContent = '[' +
              'measured=' + (e.fetchStart > 0 && e.responseEnd > 0) +
              ' ordered=' + (e.fetchStart <= e.requestStart && e.requestStart <= e.responseStart
                             && e.responseStart <= e.responseEnd) +
              ' transferSize=' + e.transferSize +
              ' encodedBodySize=' + e.encodedBodySize +
              ' decodedBodySize=' + e.decodedBodySize + ']';
            """,
            timing);

        Assert.Equal(
            "[measured=true ordered=true transferSize=1234 encodedBodySize=1000 decodedBodySize=1000]",
            report);
    }

    /// <summary>
    /// The behaviour every other caller keeps: HTML handed to the bridge as a string had no fetch,
    /// so the network marks stay at the specification's "not observed" <c>0</c> rather than being
    /// invented. This is the case the WPT runner and almost every test take.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Without_A_Timing_The_Network_Marks_Stay_Zero()
    {
        var report = Report(
            """
            var e = performance.getEntriesByType('navigation')[0];
            var keys = ['fetchStart', 'domainLookupStart', 'domainLookupEnd', 'connectStart',
                        'connectEnd', 'secureConnectionStart', 'requestStart', 'responseStart',
                        'responseEnd', 'transferSize', 'encodedBodySize', 'decodedBodySize'];
            document.getElementById('result').textContent =
              '[' + keys.map(function (k) { return e[k]; }).join(',') + ']';
            """,
            fetchTiming: null);

        Assert.Equal("[0,0,0,0,0,0,0,0,0,0,0,0]", report);
    }

    /// <summary>
    /// A fetch that performed no lookup and opened no connection — a <c>file:</c> document — reports
    /// those phases as <c>fetchStart</c> rather than <c>0</c>, which is what Navigation Timing asks
    /// for. <c>secureConnectionStart</c> is the documented exception and stays <c>0</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Phases_A_Fetch_Did_Not_Perform_Collapse_Onto_FetchStart()
    {
        var timing = DocumentFetchTiming.StartNavigation();
        timing.MarkFetchStart();
        timing.MarkRequestStart();
        timing.MarkResponseStart();
        timing.MarkResponseEnd();

        var report = Report(
            """
            var e = performance.getEntriesByType('navigation')[0];
            document.getElementById('result').textContent = '[' +
              'collapsed=' + (e.domainLookupStart === e.fetchStart &&
                              e.domainLookupEnd === e.fetchStart &&
                              e.connectStart === e.fetchStart &&
                              e.connectEnd === e.fetchStart) +
              ' notZero=' + (e.fetchStart > 0) +
              ' secure=' + e.secureConnectionStart + ']';
            """,
            timing);

        Assert.Equal("[collapsed=true notZero=true secure=0]", report);
    }

    /// <summary>
    /// The origin half. A network mark and a <c>performance.now()</c> reading are two points on one
    /// timeline, so every measured phase precedes the lifecycle marks that follow it and all of them
    /// precede <c>now()</c>. Reading the entry before this change gave <c>fetchStart = 0</c> against
    /// a <c>domInteractive</c> measured from a later origin — two clocks presented as one.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Marks_Share_One_Timeline_With_PerformanceNow()
    {
        var timing = DocumentFetchTiming.StartNavigation();
        timing.MarkFetchStart();
        timing.MarkRequestStart();
        timing.MarkResponseStart();
        timing.MarkResponseEnd();

        var report = ReportOnLoad(
            """
            var e = performance.getEntriesByType('navigation')[0];
            document.getElementById('result').textContent = '[' +
              (e.responseEnd <= e.domInteractive) + ',' +
              (e.domInteractive <= e.domComplete) + ',' +
              (e.domComplete <= performance.now()) + ']';
            """,
            timing);

        Assert.Equal("[true,true,true]", report);
    }

    // ─────────────────────────── the host really measures the fetch ───────────────────────────

    /// <summary>
    /// End to end through the real capture path, against a local origin: the CLI's own fetch is what
    /// produces the marks, so this covers the instrumentation the unit cases above stand in for —
    /// the connect callback, the headers-read boundary, and the byte counts.
    /// </summary>
    [Fact(Timeout = 600000)]
    public async Task A_Real_Capture_Measures_Its_Own_Fetch()
    {
        const string body = "<!doctype html><html><body><p>hello</p></body></html>";
        using var origin = new HtmlOrigin(body);
        var outputPath = Path.Combine(Path.GetTempPath(), "broiler-navtiming-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            await new CaptureService().EvaluatePageAsync(new PageEvaluationOptions
            {
                Url = origin.Prefix + "page.html",
                OutputPath = outputPath,
                Expressions =
                [
                    // One expression per fact, so a failure names which one.
                    "performance.getEntriesByType('navigation')[0].fetchStart > 0",
                    "performance.getEntriesByType('navigation')[0].responseEnd >= performance.getEntriesByType('navigation')[0].responseStart",
                    "performance.getEntriesByType('navigation')[0].responseStart >= performance.getEntriesByType('navigation')[0].requestStart",
                    "performance.getEntriesByType('navigation')[0].requestStart >= performance.getEntriesByType('navigation')[0].connectEnd",
                    "performance.getEntriesByType('navigation')[0].connectEnd >= performance.getEntriesByType('navigation')[0].domainLookupEnd",
                    "performance.getEntriesByType('navigation')[0].domainLookupEnd >= performance.getEntriesByType('navigation')[0].fetchStart",
                    $"performance.getEntriesByType('navigation')[0].encodedBodySize === {Encoding.UTF8.GetByteCount(body)}",
                    "performance.getEntriesByType('navigation')[0].transferSize > performance.getEntriesByType('navigation')[0].encodedBodySize",
                    // The connection really was measured rather than collapsed: this fetch opened one.
                    "performance.getEntriesByType('navigation')[0].connectStart > performance.getEntriesByType('navigation')[0].fetchStart",
                    // http, so no handshake happened and the specification's 0 stands.
                    "performance.getEntriesByType('navigation')[0].secureConnectionStart === 0",
                ],
            });

            var values = EvaluationValues(outputPath);
            var untrue = values.Where(v => v.Value != "true").Select(v => $"{v.Key} => {v.Value}").ToList();
            Assert.True(untrue.Count == 0, string.Join("\n", untrue));
        }
        finally
        {
            try { File.Delete(outputPath); } catch { /* best-effort */ }
        }
    }

    // ───────────────────────────────────── plumbing ─────────────────────────────────────

    /// <summary>Runs <paramref name="js"/> in the page and returns its bracketed report.</summary>
    private static string Report(string js, DocumentFetchTiming? fetchTiming)
        => Extract(CaptureService.ExecuteScriptsWithDom(
            Page.Replace("</body>", $"<script>{js}</script></body>"),
            "https://example.com/page",
            fetchTiming: fetchTiming));

    /// <summary>As <see cref="Report"/>, but from a <c>load</c> listener so the lifecycle marks have
    /// been stamped by the time the assertions read them.</summary>
    private static string ReportOnLoad(string js, DocumentFetchTiming? fetchTiming)
        => Report($"window.addEventListener('load', function () {{ {js} }});", fetchTiming);

    private static string Extract(string serialized)
    {
        var open = serialized.IndexOf('[');
        var close = serialized.IndexOf(']', open + 1);
        Assert.True(open >= 0 && close > open, $"probe did not run; document was:\n{serialized}");
        return serialized[open..(close + 1)];
    }

    /// <summary>The evaluation report's expression → value map.</summary>
    private static Dictionary<string, string> EvaluationValues(string reportPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(reportPath));
        var values = new Dictionary<string, string>();
        foreach (var evaluation in document.RootElement.GetProperty("evaluations").EnumerateArray())
        {
            var expression = evaluation.GetProperty("expression").GetString()!;
            var error = evaluation.GetProperty("error");
            Assert.True(error.ValueKind == JsonValueKind.Null, $"{expression} errored: {error}");
            values[expression] = evaluation.GetProperty("value").GetString() ?? "<null>";
        }

        return values;
    }

    /// <summary>A local origin serving one HTML body for every request.</summary>
    private sealed class HtmlOrigin : IDisposable
    {
        private readonly HttpListener _listener = new();

        public HtmlOrigin(string html)
        {
            // HttpListener will not bind port 0, so pick by trial; the loopback range is effectively
            // free and a collision just moves to the next candidate.
            for (var attempt = 0; ; attempt++)
            {
                Prefix = $"http://127.0.0.1:{18300 + Random.Shared.Next(2000)}/";
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add(Prefix);
                try
                {
                    _listener.Start();
                    break;
                }
                catch (HttpListenerException) when (attempt < 10)
                {
                }
            }

            var body = Encoding.UTF8.GetBytes(html);
            _ = Task.Run(() =>
            {
                try
                {
                    while (_listener.IsListening)
                    {
                        var context = _listener.GetContext();
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.ContentLength64 = body.Length;
                        context.Response.OutputStream.Write(body, 0, body.Length);
                        context.Response.Close();
                    }
                }
                catch
                {
                    // Shutdown races the accept loop; nothing to report.
                }
            });
        }

        public string Prefix { get; private set; }

        public void Dispose()
        {
            try { _listener.Stop(); } catch { /* already torn down */ }
            try { _listener.Close(); } catch { /* already torn down */ }
        }
    }
}
