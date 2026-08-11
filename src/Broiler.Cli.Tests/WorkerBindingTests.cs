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

    // ---------------------------------------------------------------- timers

    /// <summary>A worker's <c>setTimeout</c> fires, and its callback can post back.</summary>
    [Fact]
    public void Worker_setTimeout_fires_and_can_post()
    {
        var script = WriteWorker("timeout.js", @"
            onmessage = function () {
                setTimeout(function () { postMessage({ late: true }); }, 20);
            };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage(null);",
            "got !== null");

        Assert.True(ok, "the worker's setTimeout never fired");
        Assert.True(context.Eval("got.late").BooleanValue);
    }

    /// <summary>
    /// Deadline ordering, which is the property inherited from the page's loop: a later-registered
    /// shorter timeout runs before an earlier-registered longer one.
    /// </summary>
    [Fact]
    public void Worker_timers_fire_in_deadline_order()
    {
        var script = WriteWorker("order.js", @"
            onmessage = function () {
                var seen = [];
                setTimeout(function () { seen.push('slow'); postMessage({ order: seen.join(',') }); }, 60);
                setTimeout(function () { seen.push('fast'); }, 5);
            };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage(null);",
            "got !== null");

        Assert.True(ok, "the worker never reported timer order");
        Assert.Equal("fast,slow", context.Eval("got.order").ToString());
    }

    /// <summary><c>setInterval</c> repeats, and <c>clearInterval</c> stops it.</summary>
    [Fact]
    public void Worker_setInterval_repeats_until_cleared()
    {
        var script = WriteWorker("interval.js", @"
            onmessage = function () {
                var n = 0;
                var id = setInterval(function () {
                    n++;
                    if (n === 3) { clearInterval(id); postMessage({ ticks: n }); }
                }, 5);
            };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage(null);",
            "got !== null");

        Assert.True(ok, "the worker's interval never reported");
        Assert.Equal(3, context.Eval("got.ticks").DoubleValue);
    }

    /// <summary>
    /// A pending timer must not starve the inbox: a worker with a live <c>setInterval</c> still
    /// answers messages. This is the case a naive pump — one that drains timers until none are due
    /// before looking at the queue — gets wrong.
    /// </summary>
    [Fact]
    public void A_running_interval_does_not_starve_incoming_messages()
    {
        var script = WriteWorker("busy.js", @"
            var ticks = 0;
            setInterval(function () { ticks++; }, 1);
            onmessage = function () { postMessage({ answered: true, ticks: ticks }); };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage(null);",
            "got !== null");

        Assert.True(ok, "a worker with a running interval did not answer a message");
        Assert.True(context.Eval("got.answered").BooleanValue);
    }

    /// <summary>
    /// <c>clearTimeout</c> before the deadline cancels the callback, and the ids are interchangeable
    /// with <c>clearInterval</c> as the HTML spec requires.
    /// </summary>
    [Fact]
    public void Worker_clearTimeout_cancels_and_ids_are_interchangeable()
    {
        var script = WriteWorker("cancel.js", @"
            onmessage = function () {
                var fired = false;
                var a = setTimeout(function () { fired = true; }, 10);
                clearInterval(a);                       // interchangeable id space
                var b = setInterval(function () { fired = true; }, 10);
                clearTimeout(b);                        // and the other way round
                setTimeout(function () { postMessage({ fired: fired }); }, 40);
            };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage(null);",
            "got !== null");

        Assert.True(ok, "the worker never reported");
        Assert.False(context.Eval("got.fired").BooleanValue, "a cleared timer still fired");
    }

    /// <summary>
    /// A worker sitting on a repeating timer must still terminate promptly — the timer keeps the pump
    /// awake, so termination has to win over it rather than wait for it to go quiet.
    /// </summary>
    [Fact]
    public void Terminate_stops_a_worker_with_a_live_interval()
    {
        var script = WriteWorker("forever.js", @"
            setInterval(function () { }, 1);
            onmessage = function () { postMessage({ up: true }); };");

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage(null);",
            "got !== null");
        Assert.True(ok, "the worker never started");

        var stopped = System.Diagnostics.Stopwatch.StartNew();
        bridge.Dispose();
        stopped.Stop();

        Assert.True(stopped.Elapsed < TimeSpan.FromSeconds(10),
            $"disposal took {stopped.Elapsed} with a live interval — termination did not win over the timer");
    }

    // ---------------------------------------------------------- importScripts

    /// <summary>Imported scripts run in the worker's own global, in order, before the call returns.</summary>
    [Fact]
    public void ImportScripts_runs_scripts_in_order_in_the_worker_global()
    {
        WriteWorker("lib-a.js", "var trail = 'a'; function fromA() { return 'A'; }");
        WriteWorker("lib-b.js", "trail += 'b';");
        var script = WriteWorker("importer.js", @"
            importScripts('lib-a.js', 'lib-b.js');
            var afterImport = trail;               // synchronous: already 'ab' here
            onmessage = function () { postMessage({ trail: afterImport, call: fromA() }); };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage(null);",
            "got !== null");

        Assert.True(ok, "the importing worker never replied");
        Assert.Equal("ab", context.Eval("got.trail").ToString());
        Assert.Equal("A", context.Eval("got.call").ToString());
    }

    /// <summary>
    /// Specifiers resolve against the <em>worker's</em> directory, not the page's — which is what the
    /// spec means by "relative to the worker's script URL".
    /// </summary>
    /// <remarks>
    /// The check is built so it cannot pass by accident: the worker lives in a subdirectory, and a
    /// <em>different</em> file of the same name sits next to the page. Resolving against the page's
    /// base path would find the decoy and report the wrong marker rather than failing to load.
    /// </remarks>
    [Fact]
    public void ImportScripts_resolves_against_the_workers_own_directory()
    {
        var sub = Directory.CreateDirectory(Path.Combine(_dir, "nested")).FullName;
        File.WriteAllText(Path.Combine(_dir, "shared.js"), "var marker = 'page-level-decoy';");
        File.WriteAllText(Path.Combine(sub, "shared.js"), "var marker = 'worker-level';");
        var script = Path.Combine(sub, "w.js");
        File.WriteAllText(script, @"
            importScripts('shared.js');
            onmessage = function () { postMessage({ marker: marker }); };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");
        bridge.SetLocalBasePath(_dir);   // the page's base path points at the decoy

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage(null);",
            "got !== null");

        Assert.True(ok, "the worker never replied");
        Assert.Equal("worker-level", context.Eval("got.marker").ToString());
    }

    /// <summary>
    /// A specifier that cannot be loaded is a <c>NetworkError</c>, and it aborts the call — later
    /// specifiers in the same <c>importScripts</c> do not run.
    /// </summary>
    [Fact]
    public void ImportScripts_throws_NetworkError_and_aborts_the_rest_of_the_call()
    {
        WriteWorker("after.js", "loadedAfter = true;");
        var script = WriteWorker("bad-import.js", @"
            var loadedAfter = false, threw = '';
            try { importScripts('no-such-file.js', 'after.js'); }
            catch (e) { threw = String(e && e.name ? e.name : e); }
            onmessage = function () { postMessage({ threw: threw, loadedAfter: loadedAfter }); };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage(null);",
            "got !== null");

        Assert.True(ok, "the worker never replied");
        Assert.Equal("NetworkError", context.Eval("got.threw").ToString());
        Assert.False(context.Eval("got.loadedAfter").BooleanValue,
            "a specifier after the failing one still ran — the call did not abort");
    }

    /// <summary>An imported script that throws propagates to the importer rather than being swallowed.</summary>
    [Fact]
    public void A_throwing_imported_script_propagates()
    {
        WriteWorker("boom.js", "throw new Error('from-import');");
        var script = WriteWorker("catches.js", @"
            var caught = '';
            try { importScripts('boom.js'); } catch (e) { caught = String(e.message || e); }
            onmessage = function () { postMessage({ caught: caught }); };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage(null);",
            "got !== null");

        Assert.True(ok, "the worker never replied");
        Assert.Contains("from-import", context.Eval("got.caught").ToString(), StringComparison.Ordinal);
    }

    /// <summary><c>importScripts</c> is re-entrant: an imported script may import further scripts.</summary>
    [Fact]
    public void Imported_scripts_may_import_further_scripts()
    {
        WriteWorker("leaf.js", "var depth = 'leaf';");
        WriteWorker("middle.js", "importScripts('leaf.js'); depth += '<middle';");
        var script = WriteWorker("root.js", @"
            importScripts('middle.js');
            onmessage = function () { postMessage({ depth: depth }); };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage(null);",
            "got !== null");

        Assert.True(ok, "the worker never replied");
        Assert.Equal("leaf<middle", context.Eval("got.depth").ToString());
    }

    // --------------------------------------------------------- transferables

    /// <summary>
    /// A transferred <c>ArrayBuffer</c> arrives with its contents and leaves the sender detached.
    /// Both halves matter: the contents prove it was not lost, the detachment proves it was
    /// transferred rather than copied.
    /// </summary>
    [Fact]
    public void Transferred_buffer_arrives_and_detaches_the_sender()
    {
        var script = WriteWorker("xfer.js", @"
            onmessage = function (e) {
                var view = new Uint8Array(e.data.buf);
                postMessage({ first: view[0], last: view[view.length - 1], size: e.data.buf.byteLength });
            };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var buf = new ArrayBuffer(4);
            var fill = new Uint8Array(buf); fill[0] = 7; fill[3] = 9;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage({{ buf: buf }}, [buf]);
            var lengthAfterSend = buf.byteLength;",
            "got !== null");

        Assert.True(ok, "the worker never replied");
        Assert.Equal(7, context.Eval("got.first").DoubleValue);
        Assert.Equal(9, context.Eval("got.last").DoubleValue);
        Assert.Equal(4, context.Eval("got.size").DoubleValue);

        // Detached on the sender: a transferred buffer is 0 bytes afterwards.
        Assert.Equal(0, context.Eval("lengthAfterSend").DoubleValue);
        Assert.Equal(0, context.Eval("buf.byteLength").DoubleValue);
    }

    /// <summary>
    /// The control for the case above: <b>without</b> a transfer list the same send copies, and the
    /// sender's buffer stays usable. Without this, "detached" could just be what postMessage always
    /// does to a buffer.
    /// </summary>
    [Fact]
    public void Without_a_transfer_list_the_buffer_is_copied_and_stays_usable()
    {
        var script = WriteWorker("copy.js", @"
            onmessage = function (e) {
                var view = new Uint8Array(e.data.buf);
                postMessage({ first: view[0], size: e.data.buf.byteLength });
            };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var got = null;
            var buf = new ArrayBuffer(4);
            new Uint8Array(buf)[0] = 5;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{ got = e.data; }};
            w.postMessage({{ buf: buf }});",
            "got !== null");

        Assert.True(ok, "the worker never replied");
        Assert.Equal(5, context.Eval("got.first").DoubleValue);
        Assert.Equal(4, context.Eval("got.size").DoubleValue);
        Assert.Equal(4, context.Eval("buf.byteLength").DoubleValue);
    }

    /// <summary>Transfer works from the worker back to the page too, detaching the worker's buffer.</summary>
    [Fact]
    public void Worker_can_transfer_a_buffer_back_to_the_page()
    {
        var script = WriteWorker("xfer-back.js", @"
            onmessage = function () {
                var buf = new ArrayBuffer(3);
                var v = new Uint8Array(buf); v[0] = 1; v[2] = 3;
                postMessage({ buf: buf }, [buf]);
                // Detached on this side immediately after the send.
                postMessage({ detachedHere: buf.byteLength === 0 });
            };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        var ok = RunAndDrain(bridge, context, $@"
            var payload = null, note = null;
            var w = new Worker({Quote(script)});
            w.onmessage = function (e) {{
                if (e.data.buf) {{ payload = e.data.buf; }} else {{ note = e.data; }}
            }};
            w.postMessage(null);",
            "payload !== null && note !== null");

        Assert.True(ok, "the worker never sent both messages");
        Assert.Equal(3, context.Eval("payload.byteLength").DoubleValue);
        Assert.Equal(3, context.Eval("new Uint8Array(payload)[2]").DoubleValue);
        Assert.True(context.Eval("note.detachedHere").BooleanValue,
            "the worker's own buffer was not detached by the transfer");
    }

    /// <summary>An invalid transfer list is a <c>DataCloneError</c>, per the messaging model.</summary>
    [Theory]
    [InlineData("w.postMessage({}, [{ notATransferable: true }]);", "non-transferable")]
    [InlineData("var b = new ArrayBuffer(1); w.postMessage({}, [b, b]);", "duplicate")]
    public void An_invalid_transfer_list_is_a_DataCloneError(string send, string _)
    {
        var script = WriteWorker("idle.js", "onmessage = function () { };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        context.Eval($"var w = new Worker({Quote(script)}); var name = '';");
        context.Eval($"try {{ {send} }} catch (e) {{ name = e.name || String(e); }}");

        Assert.Equal("DataCloneError", context.Eval("name").ToString());
    }

    /// <summary>
    /// Re-transferring an already-detached buffer is a <c>DataCloneError</c> rather than a silent
    /// send of nothing.
    /// </summary>
    [Fact]
    public void Transferring_an_already_detached_buffer_is_a_DataCloneError()
    {
        var script = WriteWorker("idle2.js", "onmessage = function () { };");

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<html><body></body></html>", "file:///page.html");

        context.Eval($@"
            var w = new Worker({Quote(script)});
            var buf = new ArrayBuffer(2);
            w.postMessage({{ buf: buf }}, [buf]);   // detaches it
            var name = '';
            try {{ w.postMessage({{ buf: buf }}, [buf]); }} catch (e) {{ name = e.name || String(e); }}");

        Assert.Equal(0, context.Eval("buf.byteLength").DoubleValue);
        Assert.Equal("DataCloneError", context.Eval("name").ToString());
    }

    private static string Quote(string path) => "'" + path.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
}
