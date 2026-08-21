using System.Diagnostics;
using System.Net;
using System.Text;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The measurement behind multithreading roadmap item #17, expressed as an assertion rather than a
/// stopwatch: <em>when</em> a document's sub-resource requests reach the origin.
/// </summary>
/// <remarks>
/// <para>
/// A wall-clock comparison of "with the scan" against "without" is the obvious test and the wrong
/// one — it measures the host's scheduler as much as the change, and it fails on a loaded CI box for
/// reasons that have nothing to do with the code. What the item actually claims is structural and
/// can be checked exactly, by an origin that <b>holds every request open until released</b>:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Overlap with the parse.</b> With the scan, the stylesheet requests have arrived at the origin
/// while <c>Attach</c> — the parse — is still running, and <c>Attach</c> returns without waiting for
/// them. Without it, not one request has been made by the time the parse is over.
/// </description></item>
/// <item><description>
/// <b>Overlap with each other.</b> With the scan, a document's external scripts are in flight
/// together; without it the host fetches them one at a time, so the origin never sees two at once.
/// </description></item>
/// </list>
/// <para>
/// Both are true regardless of how fast the machine is, which is what makes them assertions. The
/// wall-clock figures that go in the roadmap come from the same shape with a fixed per-request
/// delay, and they are the arithmetic these assertions imply.
/// </para>
/// </remarks>
public sealed class PreloadScanOverlapTests : IDisposable
{
    private readonly LatencyOrigin _origin = new();

    public void Dispose() => _origin.Dispose();

    /// <summary>
    /// The claim in the roadmap's own words — "overlaps network with parse". With the scan the
    /// sheet requests are on the wire before the parse finishes; without it they do not exist yet.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Stylesheet_Requests_Are_In_Flight_While_The_Document_Is_Still_Parsing()
    {
        var html = Document(sheets: 3, scripts: 0);

        _origin.HoldRequests();
        try
        {
            using var context = new JSContext();
            var bridge = new DomBridge();
            WithScan(true, () => bridge.Attach(context, html, _origin.PageUrl));

            // Deterministic: the requests will arrive, because the worker has been handed them and
            // nothing can withdraw them. What is being asserted is that Attach did not have to wait
            // for them — the origin is still holding every one of them open.
            Assert.True(_origin.WaitForRequests(3, TimeSpan.FromSeconds(10)));
            Assert.Equal(0, _origin.CompletedCount);
        }
        finally
        {
            _origin.ReleaseRequests();
        }
    }

    [Fact(Timeout = 600000)]
    public void Without_The_Scan_No_Stylesheet_Request_Exists_When_The_Parse_Ends()
    {
        var html = Document(sheets: 3, scripts: 0);

        _origin.HoldRequests();
        try
        {
            using var context = new JSContext();
            var bridge = new DomBridge();
            WithScan(false, () => bridge.Attach(context, html, _origin.PageUrl));

            // No wait and no window to race against: with the scan off, the only code that could
            // have issued these requests is the post-parse sheet collection, which has not run.
            Assert.Equal(0, _origin.ReceivedCount);
        }
        finally
        {
            _origin.ReleaseRequests();
        }
    }

    /// <summary>
    /// The capture host walks the script tags itself and fetched each external one inline as it
    /// reached it, so its round trips were strictly serial — a call site item #2's split never
    /// reached. The scan supplies the URL set early enough to fix it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void External_Scripts_Are_Fetched_Concurrently_Rather_Than_One_At_A_Time()
    {
        var html = Document(sheets: 0, scripts: 4);

        _origin.Reset();
        var withScan = WithScan(true, () => CaptureService.ExecuteScriptsWithDom(html, _origin.PageUrl));
        var concurrentPeak = _origin.PeakConcurrency;

        _origin.Reset();
        var withoutScan = WithScan(false, () => CaptureService.ExecuteScriptsWithDom(html, _origin.PageUrl));
        var serialPeak = _origin.PeakConcurrency;

        Assert.Equal(1, serialPeak);
        Assert.True(concurrentPeak > 1, $"expected overlapping script fetches, peak was {concurrentPeak}");

        // The exit gate: the scripts still run, in document order, to the same result.
        Assert.Contains("0123", withScan);
        Assert.Equal(withoutScan, withScan);
    }

    /// <summary>
    /// The wall-clock figure the assertions above imply, printed rather than asserted: eight
    /// scripts behind a fixed per-request delay cost eight delays serially and about one
    /// concurrently.
    /// </summary>
    /// <remarks>
    /// Reported as the median of interleaved pairs, because the two settings are being compared on
    /// a shared machine: measuring all of one and then all of the other charges any drift in the
    /// host — another test suite starting, a GC, a frequency change — entirely to whichever ran
    /// second. A pair run back to back sees the same conditions, and the median of several pairs
    /// discards the one that did not.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void Serial_And_Overlapped_Wall_Clock_Are_Reported()
    {
        const int pairs = 5;
        var html = Document(sheets: 0, scripts: 8);

        // One untimed pass each way: the first run through this path JITs the bridge, the engine
        // and the HTTP stack, and timing that measures the runtime warming up.
        Run(html, scan: false);
        Run(html, scan: true);

        var ratios = new List<double>();
        var serials = new List<double>();
        var overlaps = new List<double>();
        var peak = 0;

        for (var i = 0; i < pairs; i++)
        {
            var serial = Measure(() => Run(html, scan: false));
            var overlapped = Measure(() => Run(html, scan: true));
            peak = Math.Max(peak, _origin.PeakConcurrency);

            serials.Add(serial.TotalMilliseconds);
            overlaps.Add(overlapped.TotalMilliseconds);
            ratios.Add(overlapped.TotalMilliseconds / serial.TotalMilliseconds);
        }

        Console.WriteLine(
            $"preload scan, 8 scripts at {LatencyOrigin.DelayMilliseconds} ms per request, {pairs} interleaved pairs: " +
            $"serial median {Median(serials):F1} ms, overlapped median {Median(overlaps):F1} ms, " +
            $"median paired ratio {Median(ratios):F3}, peak concurrency {peak}");

        // Deliberately not an assertion on the ratio. The claim this file makes is the one above,
        // about how many requests are in flight; the time that saves is arithmetic, and asserting
        // on it would make a scheduling hiccup look like a regression.
        Assert.NotEmpty(ratios);
    }

    private string Run(string html, bool scan)
    {
        _origin.Reset();
        return WithScan(scan, () => CaptureService.ExecuteScriptsWithDom(html, _origin.PageUrl));
    }

    private static double Median(List<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }

    private static TimeSpan Measure(Action body)
    {
        var stopwatch = Stopwatch.StartNew();
        body();
        return stopwatch.Elapsed;
    }

    private static T WithScan<T>(bool enabled, Func<T> body)
    {
        var wasEnabled = SpeculativePreloadScan.Enabled;
        try
        {
            SpeculativePreloadScan.Enabled = enabled;
            return body();
        }
        finally
        {
            SpeculativePreloadScan.Enabled = wasEnabled;
        }
    }

    private static void WithScan(bool enabled, Action body) => WithScan(enabled, () => { body(); return 0; });

    /// <summary>
    /// A document naming <paramref name="sheets"/> stylesheets and <paramref name="scripts"/>
    /// scripts on the local origin. Each script appends its index to <c>#result</c>, so the
    /// serialized output pins execution order as well as content.
    /// </summary>
    private string Document(int sheets, int scripts)
    {
        var html = new StringBuilder("<!DOCTYPE html><html><head>");
        for (var i = 0; i < sheets; i++)
            html.Append($"""<link rel="stylesheet" href="{_origin.Url($"s{i}.css")}">""");
        html.Append("""</head><body><div id="target">text</div><div id="result"></div>""");
        for (var i = 0; i < scripts; i++)
            html.Append($"""<script src="{_origin.Url($"j{i}.js")}"></script>""");
        return html.Append("</body></html>").ToString();
    }
}

/// <summary>
/// A loopback origin that serves the sub-resources these tests name, after a fixed delay, and
/// records how its requests overlapped.
/// </summary>
/// <remarks>
/// The delay is what makes concurrency observable at all: without one, a fetch completes before the
/// next begins whatever the caller intended, and a serial loop is indistinguishable from a parallel
/// one. <see cref="HoldRequests"/> replaces the delay with a gate the test opens, which is what
/// turns "the requests overlapped the parse" from a race into an assertion.
/// </remarks>
internal sealed class LatencyOrigin : IDisposable
{
    internal const int DelayMilliseconds = 40;

    private readonly HttpListener _listener = new();
    private readonly ManualResetEventSlim _gate = new(initialState: true);
    private readonly Lock _sync = new();
    private readonly string _prefix;
    private int _inFlight;

    public LatencyOrigin()
    {
        // Port 0 asks the OS for a free one, but HttpListener has no way to report it back, so the
        // port is picked by binding a socket, closing it, and taking the number. The window between
        // the two is the standard cost of this pattern and is not worth a retry loop in a test.
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        _prefix = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(_prefix);
        _listener.Start();
        _ = Task.Run(Serve);
    }

    /// <summary>The page URL documents under test are given, on this origin.</summary>
    public string PageUrl => _prefix + "page.html";

    /// <summary>Requests that have reached the origin.</summary>
    public int ReceivedCount { get; private set; }

    /// <summary>Requests the origin has finished answering.</summary>
    public int CompletedCount { get; private set; }

    /// <summary>The most requests that were ever open at the same time.</summary>
    public int PeakConcurrency { get; private set; }

    public string Url(string path) => _prefix + path;

    public void Reset()
    {
        lock (_sync)
        {
            ReceivedCount = 0;
            CompletedCount = 0;
            PeakConcurrency = 0;
        }
    }

    /// <summary>Makes every request block until <see cref="ReleaseRequests"/>.</summary>
    public void HoldRequests()
    {
        Reset();
        _gate.Reset();
    }

    public void ReleaseRequests() => _gate.Set();

    /// <summary>Waits for <paramref name="count"/> requests to arrive. False on timeout.</summary>
    public bool WaitForRequests(int count, TimeSpan timeout)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < timeout)
        {
            lock (_sync)
            {
                if (ReceivedCount >= count)
                    return true;
            }

            Thread.Sleep(5);
        }

        return false;
    }

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException)
            {
                return; // Disposed.
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => Answer(context));
        }
    }

    private void Answer(HttpListenerContext context)
    {
        lock (_sync)
        {
            ReceivedCount++;
            _inFlight++;
            PeakConcurrency = Math.Max(PeakConcurrency, _inFlight);
        }

        // The gate is open in the timed tests (so the delay is the whole cost) and closed in the
        // overlap ones (so nothing completes until the assertion has been made).
        _gate.Wait();
        Thread.Sleep(DelayMilliseconds);

        var path = context.Request.Url?.AbsolutePath ?? "/";
        var (body, contentType) = path.EndsWith(".css", StringComparison.Ordinal)
            ? ($"#target {{ color: rgb(1, 2, 3); }}", "text/css")
            : ($"document.getElementById('result').textContent += '{IndexIn(path)}';", "text/javascript");

        var bytes = Encoding.UTF8.GetBytes(body);
        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes);
        context.Response.Close();

        lock (_sync)
        {
            _inFlight--;
            CompletedCount++;
        }
    }

    /// <summary>The digits in <c>/j2.js</c>, so a script can report which one it was.</summary>
    private static string IndexIn(string path) => new([.. path.Where(char.IsDigit)]);

    public void Dispose()
    {
        _gate.Set();
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (ObjectDisposedException)
        {
            // Already closed.
        }

        _gate.Dispose();
    }
}
