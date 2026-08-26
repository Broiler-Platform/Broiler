using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>ReadableStream</c>, its default reader and controller, and <c>Blob.prototype.stream()</c>.
/// </summary>
/// <remarks>
/// <para>
/// There was no <c>ReadableStream</c>. What stood in for one was a shape-only object that
/// <c>response.body</c> handed back: a <c>getReader</c> whose reader had <c>read</c>, <c>cancel</c>
/// and <c>releaseLock</c> and nothing else — no <c>closed</c>, no <c>tee</c>, no <c>cancel</c> on the
/// stream, and no constructor for a page to build one of its own, so <c>new ReadableStream(...)</c>
/// was a <c>ReferenceError</c>: the kind that aborts the script rather than the statement.
/// <c>Blob.prototype.stream()</c> was deliberately left out for exactly that reason, and that
/// decision is what this reverses.
/// </para>
/// <para>
/// Every expectation is Chromium's measured answer over the same probe run against both. The reads
/// are written as <c>then</c> chains rather than <c>await</c> because this test host has no
/// microtask synchronization context — a real capture drives <c>await</c> fine, and a capture-level
/// probe confirms it. The async-iteration tests at the end are the exception: <c>for await</c> is
/// what they are testing, and the loop runs to completion inside the microtask checkpoint that ends
/// the evaluation which started it.
/// </para>
/// </remarks>
public sealed class ReadableStreamTests
{
    private static DomBridge Attach(out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!DOCTYPE html><html><body></body></html>", "https://example.com/index.html");
        return bridge;
    }

    private static string Eval(JSContext context, string body) =>
        context.Eval($$"""
            (() => {
                {{body}}
            })()
            """).ToString();

    /// <summary>
    /// Starts the reads in one evaluation and reads the log in the next: a <c>then</c> callback runs
    /// at the microtask checkpoint that ends the evaluation which queued it.
    /// </summary>
    private static string EvalAfterMicrotasks(JSContext context, string start, string read)
    {
        context.Eval(start);
        return context.Eval(read).ToString();
    }

    /// <summary>The three interfaces exist, are not constructible except the stream itself, and
    /// carry their members on their prototypes.</summary>
    [Fact(Timeout = 600000)]
    public void The_Interfaces_Exist_With_Their_Members_On_Their_Prototypes()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "function/function/function/" +
            "cancel,constructor,getReader,locked,tee,values/" +
            "cancel,closed,constructor,read,releaseLock/" +
            "close,constructor,desiredSize,enqueue,error/TypeError",
            Eval(context, """
                function proto(c) { return Object.getOwnPropertyNames(c.prototype).sort().join(','); }
                var noNew;
                try { ReadableStream(); noNew = 'ok'; } catch (e) { noNew = e.name; }
                return (typeof ReadableStream) + '/' + (typeof ReadableStreamDefaultReader) + '/' +
                       (typeof ReadableStreamDefaultController) + '/' + proto(ReadableStream) + '/' +
                       proto(ReadableStreamDefaultReader) + '/' + proto(ReadableStreamDefaultController) +
                       '/' + noNew;
                """));
    }

    /// <summary><c>blob.stream()</c> hands back a real stream, and reading it yields the blob's bytes
    /// as one <c>Uint8Array</c> chunk followed by the close.</summary>
    [Fact(Timeout = 600000)]
    public void A_Blobs_Stream_Yields_Its_Bytes_Then_Closes()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("ReadableStream|Uint8Array(6)/false|undefined/true|closed", EvalAfterMicrotasks(context, """
            var log = [];
            var stream = new Blob(['abcdef']).stream();
            log.push(stream.constructor.name);
            var reader = stream.getReader();
            reader.read()
                .then(function (r) { log.push(r.value.constructor.name + '(' + r.value.length + ')/' + r.done); return reader.read(); })
                .then(function (r) { log.push(String(r.value) + '/' + r.done); return reader.closed; })
                .then(function () { log.push('closed'); });
            """, """
            log.join('|')
            """));
    }

    /// <summary>A stream with nothing in it reads as done immediately rather than waiting for a
    /// chunk that never comes.</summary>
    [Fact(Timeout = 600000)]
    public void An_Empty_Blobs_Stream_Reads_As_Done()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("{\"done\":true}", EvalAfterMicrotasks(context, """
            var seen = '';
            new Blob([]).stream().getReader().read().then(function (r) { seen = JSON.stringify(r); });
            """, "seen"));
    }

    /// <summary>
    /// A stream is locked by its reader, and only one reader may hold it — which is what makes
    /// <c>tee()</c> the way to read a body twice.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Reader_Locks_The_Stream_And_Only_One_May_Hold_It()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("false/true/TypeError/TypeError", Eval(context, """
            var stream = new Blob(['abc']).stream();
            var before = stream.locked;
            stream.getReader();
            var second, byob;
            try { stream.getReader(); second = 'ok'; } catch (e) { second = e.name; }
            try { new Blob(['a']).stream().getReader({ mode: 'byob' }); byob = 'ok'; } catch (e) { byob = e.name; }
            return before + '/' + stream.locked + '/' + second + '/' + byob;
            """));
    }

    /// <summary>
    /// A page's own stream: <c>start</c> fills it, <c>pull</c> refills it on demand, and the
    /// controller refuses to enqueue into a stream that has been closed.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Page_Can_Build_Its_Own_Stream()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("1/TypeError|{\"value\":\"p\",\"done\":false}|{\"done\":true}", EvalAfterMicrotasks(context, """
            var log = [];
            var desired, refused;
            new ReadableStream({
                start: function (controller) {
                    desired = controller.desiredSize;
                    controller.close();
                    try { controller.enqueue(1); } catch (e) { refused = e.name; }
                },
            });
            log.push(desired + '/' + refused);
            var pulled = new ReadableStream({ pull: function (controller) { controller.enqueue('p'); controller.close(); } });
            var reader = pulled.getReader();
            reader.read().then(function (r) { log.push(JSON.stringify(r)); return reader.read(); })
                         .then(function (r) { log.push(JSON.stringify(r)); });
            """, """
            log.join('|')
            """));
    }

    /// <summary>A stream its source errored rejects the read with the source's own error, rather
    /// than resolving as done.</summary>
    [Fact(Timeout = 600000)]
    public void An_Errored_Stream_Rejects_Its_Reads()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("TypeError:boom", EvalAfterMicrotasks(context, """
            var seen = '';
            new ReadableStream({ start: function (c) { c.error(new TypeError('boom')); } })
                .getReader().read().then(function () { seen = 'resolved'; }, function (e) { seen = e.name + ':' + e.message; });
            """, "seen"));
    }

    /// <summary>
    /// Cancelling tells the source and leaves the stream closed and unlocked, so a later read
    /// answers done rather than hanging.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Cancelling_Reaches_The_Source_And_Closes_The_Stream()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("false|why|{\"done\":true}", EvalAfterMicrotasks(context, """
            var log = [];
            var reason;
            var stream = new Blob(['ab']).stream();
            stream.cancel('why').then(function () { log.push(String(stream.locked)); return stream.getReader().read(); })
                                .then(function (r) { log.push(JSON.stringify(r)); });
            var own = new ReadableStream({ cancel: function (r) { reason = r; } });
            own.cancel('why').then(function () { log.push(reason); });
            """, """
            log.join('|')
            """));
    }

    /// <summary>
    /// <c>tee()</c> gives two independent streams over one source, and locks the original — which is
    /// the point of it: the source is read once and the chunks are handed to both.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Tee_Gives_Two_Streams_Over_One_Source()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("2/ReadableStream/true|6|6", EvalAfterMicrotasks(context, """
            var log = [];
            var stream = new Blob(['abcdef']).stream();
            var branches = stream.tee();
            log.push(branches.length + '/' + branches[0].constructor.name + '/' + stream.locked);
            branches[0].getReader().read().then(function (r) { log.push(r.value.length); });
            branches[1].getReader().read().then(function (r) { log.push(r.value.length); });
            """, """
            log.join('|')
            """));
    }

    /// <summary>
    /// A fetch body is one of these too, rather than the look-alike it used to be — the same
    /// interface a page's own <c>new ReadableStream</c> and <c>blob.stream()</c> produce.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Response_Body_Is_The_Same_Interface()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("ReadableStream/true/function", Eval(context, """
            var body = new Response('hello').body;
            return body.constructor.name + '/' + (body instanceof ReadableStream) + '/' + (typeof body.tee);
            """));
    }

    // ------------------------------------------------------------------ async iteration
    //
    // These were the one piece of the stream that could not be turned on when the rest landed.
    // `for await` used to deadlock the agent whenever the iterator's next() handed back a promise
    // that was not *already* settled — the engine blocked the one thread allowed to run this
    // context's JavaScript, so the job that would settle the promise could never run — and
    // `reader.read().then(…)` is exactly that shape. The engine fix ("Stop for-await deadlocking on
    // a step result that is not already settled") is upstream and the pinned Broiler.JS pointer
    // carries it, so the hook is installed and these regressions replace the test that pinned its
    // absence. Every expectation below is Chromium's measured answer to the same probe.
    //
    // Unlike the read tests above, these are written with `await` rather than a `then` chain,
    // because `for await` is the thing under test. The async function runs to completion inside the
    // microtask checkpoint that ends the evaluation which started it, so the log is complete by the
    // next evaluation — the same EvalAfterMicrotasks shape.

    /// <summary>Every chunk arrives in order, and running the loop to completion releases the
    /// reader's lock rather than leaving the stream locked forever.</summary>
    [Fact(Timeout = 600000)]
    public void Async_Iteration_Yields_Every_Chunk_And_Then_Unlocks_The_Stream()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("a|b|c|locked=false", EvalAfterMicrotasks(context, """
            var log = [];
            (async function () {
                var stream = new ReadableStream({
                    start: function (c) { c.enqueue('a'); c.enqueue('b'); c.enqueue('c'); c.close(); },
                });
                for await (const chunk of stream) log.push(chunk);
                log.push('locked=' + stream.locked);
            })();
            """, """
            log.join('|')
            """));
    }

    /// <summary>A blob's stream is async-iterable, and yields its bytes as one chunk.</summary>
    [Fact(Timeout = 600000)]
    public void A_Blobs_Stream_Is_Async_Iterable()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("chunk:6|locked=false", EvalAfterMicrotasks(context, """
            var log = [];
            (async function () {
                var stream = new Blob(['abcdef']).stream();
                for await (const chunk of stream) log.push('chunk:' + chunk.length);
                log.push('locked=' + stream.locked);
            })();
            """, """
            log.join('|')
            """));
    }

    /// <summary>A response body is async-iterable, which is the shape a page actually writes.</summary>
    [Fact(Timeout = 600000)]
    public void A_Response_Body_Is_Async_Iterable()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("bytes=5", EvalAfterMicrotasks(context, """
            var log = [];
            (async function () {
                var total = 0;
                for await (const chunk of new Response('hello').body) total += chunk.length;
                log.push('bytes=' + total);
            })();
            """, """
            log.join('|')
            """));
    }

    /// <summary>Leaving the loop early runs the iterator's <c>return</c>, which cancels the stream —
    /// with the loop's completion value as the reason — and releases the lock.</summary>
    [Fact(Timeout = 600000)]
    public void Leaving_An_Async_Iteration_Early_Cancels_The_Stream()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("a|cancelled=yes:undefined|locked=false", EvalAfterMicrotasks(context, """
            var log = [];
            var cancelled = 'no';
            (async function () {
                var stream = new ReadableStream({
                    start: function (c) { c.enqueue('a'); c.enqueue('b'); c.close(); },
                    cancel: function (reason) { cancelled = 'yes:' + reason; },
                });
                for await (const chunk of stream) { log.push(chunk); break; }
                log.push('cancelled=' + cancelled);
                log.push('locked=' + stream.locked);
            })();
            """, """
            log.join('|')
            """));
    }

    /// <summary><c>values({preventCancel: true})</c> still releases the lock on the way out but does
    /// not cancel the source, so the rest of the stream stays readable by someone else.</summary>
    [Fact(Timeout = 600000)]
    public void preventCancel_Releases_The_Lock_Without_Cancelling()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("a|cancelled=no|locked=false", EvalAfterMicrotasks(context, """
            var log = [];
            var cancelled = 'no';
            (async function () {
                var stream = new ReadableStream({
                    start: function (c) { c.enqueue('a'); c.enqueue('b'); c.close(); },
                    cancel: function () { cancelled = 'yes'; },
                });
                for await (const chunk of stream.values({ preventCancel: true })) { log.push(chunk); break; }
                log.push('cancelled=' + cancelled);
                log.push('locked=' + stream.locked);
            })();
            """, """
            log.join('|')
            """));
    }

    /// <summary>An errored stream throws into the loop rather than ending it quietly. The chunk
    /// queued before the error is discarded, because <c>error()</c> clears the queue.</summary>
    [Fact(Timeout = 600000)]
    public void An_Errored_Stream_Throws_Into_The_Async_Loop()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("caught:TypeError:boom", EvalAfterMicrotasks(context, """
            var log = [];
            (async function () {
                try {
                    var stream = new ReadableStream({
                        start: function (c) { c.enqueue('a'); c.error(new TypeError('boom')); },
                    });
                    for await (const chunk of stream) log.push(chunk);
                    log.push('no-throw');
                } catch (e) { log.push('caught:' + e.name + ':' + e.message); }
            })();
            """, """
            log.join('|')
            """));
    }

    /// <summary>Async-iterating a stream that is already locked throws, because acquiring the
    /// iterator acquires a reader.</summary>
    [Fact(Timeout = 600000)]
    public void Async_Iterating_A_Locked_Stream_Throws()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("caught:TypeError", EvalAfterMicrotasks(context, """
            var log = [];
            var stream = new Blob(['xy']).stream();
            stream.getReader();
            (async function () {
                try { for await (const chunk of stream) log.push(chunk); }
                catch (e) { log.push('caught:' + e.name); }
            })();
            """, """
            log.join('|')
            """));
    }

    /// <summary><c>@@asyncIterator</c> is the same function object as <c>values</c>, both on the
    /// prototype rather than on the instance.</summary>
    [Fact(Timeout = 600000)]
    public void AsyncIterator_Is_The_Same_Function_As_values()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("function/true/true", Eval(context, """
            var stream = new Blob(['a']).stream();
            return (typeof stream.values) + '/' +
                   (ReadableStream.prototype[Symbol.asyncIterator] === ReadableStream.prototype.values) + '/' +
                   (Object.getOwnPropertyNames(stream).length === 0);
            """));
    }
}
