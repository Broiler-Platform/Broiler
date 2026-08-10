using System;
using System.IO;
using System.Threading;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>Worker</c> — multithreading item #18. A worker script runs on its own thread in its own
/// <see cref="JSContext"/>, and messages cross by structured clone in both directions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every case drives the real bridge, not the binding in isolation.</b> A worker is only
/// interesting where it meets the page's event loop: the reply arrives as a frame action queued from
/// another thread, and the page sees it when its own drain runs. Testing the binding directly would
/// skip exactly the seam most likely to be wrong.
/// </para>
/// <para>
/// <b>Waiting is bounded and explicit.</b> Each case drains the bridge until the expected global
/// appears or a deadline passes, then asserts. A fixed sleep would either be flaky or slow, and a
/// drain with no deadline would hang the suite on a regression rather than failing it.
/// </para>
/// </remarks>
public sealed class WorkerBindingTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("broiler-worker-tests").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string WriteWorker(string name, string source)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, source);
        return path;
    }

    /// <summary>
    /// Runs <paramref name="pageScript"/> in a bridge, then drains until <paramref name="probe"/>
    /// evaluates truthy or the deadline passes. Returns whether it became truthy.
    /// </summary>
    private static bool RunAndDrain(DomBridge bridge, JSContext context, string pageScript, string probe,
        int timeoutMs = 15000)
    {
        context.Eval(pageScript);

        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (elapsed.ElapsedMilliseconds < timeoutMs)
        {
            if (context.Eval(probe).BooleanValue)
                return true;

            // Runs whatever the worker thread has queued onto the page's loop.
            bridge.FlushTimerStep();
            Thread.Sleep(5);
        }

        return context.Eval(probe).BooleanValue;
    }

    [Fact]
    public void Worker_receives_a_message_and_replies()
    {
        var script = WriteWorker("echo.js", @"
            onmessage = function (e) { postMessage({ echoed: e.data.value, from: 'worker' }); };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var reply = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ reply = e.data; }};
            w.postMessage({{ value: 41 }});",
            "reply !== null");

        Assert.True(ok, "the worker never replied");
        Assert.Equal(41, context.Eval("reply.echoed").DoubleValue);
        Assert.Equal("worker", context.Eval("reply.from").ToString());
    }

    /// <summary>
    /// The property the whole design turns on: the page and the worker must not share object
    /// identity. If the reply aliased the worker's graph — or the sent object aliased the page's —
    /// a mutation on one side would show up on the other.
    /// </summary>
    [Fact]
    public void Messages_are_cloned_in_both_directions()
    {
        var script = WriteWorker("mutate.js", @"
            var held = null;
            onmessage = function (e) {
                if (held === null) {
                    held = e.data;             // keep the first message
                    e.data.tag = 'worker-touched';
                    postMessage({ phase: 'first', seen: e.data.tag });
                } else {
                    // The page mutated its copy after sending; the worker's copy must be unchanged.
                    postMessage({ phase: 'second', heldTag: held.tag });
                }
            };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var first = null, second = null;
            var payload = {{ tag: 'page' }};
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{
                if (e.data.phase === 'first') {{ first = e.data; payload.tag = 'page-mutated-after-send'; w.postMessage({{}}); }}
                else {{ second = e.data; }}
            }};
            w.postMessage(payload);",
            "second !== null");

        Assert.True(ok, "the worker never completed the exchange");

        // The worker's own mutation of its copy is visible to the worker...
        Assert.Equal("worker-touched", context.Eval("first.seen").ToString());
        // ...and never reached the page's object.
        Assert.Equal("page-mutated-after-send", context.Eval("payload.tag").ToString());
        // The page's later mutation never reached the worker's retained copy.
        Assert.Equal("worker-touched", context.Eval("second.heldTag").ToString());
    }

    /// <summary>Richer clone types survive the crossing, not just plain objects.</summary>
    [Fact]
    public void Structured_clone_types_survive_the_crossing()
    {
        var script = WriteWorker("types.js", @"
            onmessage = function (e) {
                var d = e.data;
                postMessage({
                    when: d.when.getTime(),
                    pattern: d.re.source,
                    items: d.list.join('-'),
                    nested: d.deep.inner.n,
                    cyclic: d.self === d
                });
            };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var msg = {{ when: new Date(86400000), re: /a+b/g, list: [1,2,3], deep: {{ inner: {{ n: 7 }} }} }};
            msg.self = msg;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage(msg);",
            "got !== null");

        Assert.True(ok, "the worker never replied");
        Assert.Equal(86400000, context.Eval("got.when").DoubleValue);
        Assert.Equal("a+b", context.Eval("got.pattern").ToString());
        Assert.Equal("1-2-3", context.Eval("got.items").ToString());
        Assert.Equal(7, context.Eval("got.nested").DoubleValue);
        Assert.True(context.Eval("got.cyclic").BooleanValue, "the cycle did not survive the crossing");
    }

    /// <summary>
    /// The worker has its own realm — page globals are not visible to it, and its globals are not
    /// visible to the page — and delivery is asynchronous rather than an inline call.
    /// </summary>
    /// <remarks>
    /// Realm separation is what this asserts, and asynchrony is asserted separately below by
    /// observing that <c>postMessage</c> returns before any reply exists. Neither proves a
    /// <em>thread</em> on its own; that is what <c>JSContextIsolationTests</c> and
    /// <c>--js-context-scaling</c> are for, and this file does not restate their claim.
    /// </remarks>
    [Fact]
    public void Worker_runs_on_its_own_thread_and_its_own_realm()
    {
        var script = WriteWorker("identity.js", @"
            var mine = 'worker-only';
            onmessage = function () {
                postMessage({ hasPageGlobal: typeof pageOnly !== 'undefined', hasOwn: mine === 'worker-only' });
            };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var pageOnly = 'page';
            var got = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage(null);
            var replyWasSynchronous = (got !== null);",
            "got !== null");

        Assert.True(ok, "the worker never replied");
        Assert.False(context.Eval("replyWasSynchronous").BooleanValue,
            "the reply was already present when postMessage returned — delivery was inline, not queued");
        Assert.False(context.Eval("got.hasPageGlobal").BooleanValue,
            "the worker could see a page global — the realms are not separate");
        Assert.True(context.Eval("got.hasOwn").BooleanValue, "the worker lost its own global");
        Assert.True(context.Eval("typeof mine === 'undefined'").BooleanValue,
            "a worker global leaked into the page");
    }

    /// <summary>A worker whose script cannot be found fires <c>error</c> rather than throwing.</summary>
    [Fact]
    public void Missing_worker_script_fires_error_without_throwing()
    {
        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, @"
            var err = null;
            var w = new Worker('definitely-not-here.js');
            w.onerror = function (e) { err = e.message; };",
            "err !== null");

        Assert.True(ok, "no error event was delivered for a missing worker script");
        Assert.Contains("definitely-not-here.js", context.Eval("err").ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>terminate()</c> stops delivery, and — the part that matters for a headless host — bridge
    /// disposal joins worker threads instead of leaving them running past the document.
    /// </summary>
    [Fact]
    public void Terminate_stops_delivery_and_disposal_joins_the_thread()
    {
        var script = WriteWorker("chatty.js", @"
            onmessage = function (e) { postMessage({ n: e.data.n }); };");

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage({{ n: 1 }});",
            "got !== null");
        Assert.True(ok, "the worker never replied before terminate");

        context.Eval("w.terminate(); got = null; w.postMessage({ n: 2 });");
        for (var i = 0; i < 40; i++)
        {
            bridge.FlushTimerStep();
            Thread.Sleep(5);
        }

        Assert.True(context.Eval("got === null").BooleanValue, "a terminated worker still delivered a message");

        // Disposal must return promptly; a worker thread left running would hang this.
        var disposed = System.Diagnostics.Stopwatch.StartNew();
        bridge.Dispose();
        disposed.Stop();
        Assert.True(disposed.Elapsed < TimeSpan.FromSeconds(10), "bridge disposal did not join worker threads promptly");
    }

    private static string Quote(string path) => "'" + path.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
}
