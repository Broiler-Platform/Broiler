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
/// probe confirms it.
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
            "cancel,constructor,getReader,locked,tee/" +
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

    /// <summary>
    /// Async iteration is deliberately absent, and the reason is the engine rather than the stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>for await</c> deadlocks the agent when the iterator's <c>next()</c> hands back a promise
    /// that is not <em>already</em> settled: the engine blocks the one thread allowed to run this
    /// context's JavaScript, so the job that would settle the promise can never run. An iterator over
    /// a stream returns exactly that — <c>reader.read().then(…)</c> — so installing the hook would
    /// turn the ordinary <c>for await (const chunk of response.body)</c> from a <c>TypeError</c> a
    /// page's script survives into a capture that never settles.
    /// </para>
    /// <para>
    /// The engine fix is written and verified and ships as
    /// <c>patches/0001-js-for-await-unsettled-step-result.patch</c>; with it applied, iteration over
    /// a page stream, a blob stream and a response body all answer what Chromium answers. This test
    /// pins the interim state so turning the hook on is a decision that comes with the patch rather
    /// than a drift.
    /// </para>
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void Async_Iteration_Is_Absent_Until_The_Engine_Can_Drive_It()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("undefined/undefined", Eval(context, """
            var stream = new Blob(['a']).stream();
            return (typeof stream[Symbol.asyncIterator]) + '/' + (typeof stream.values);
            """));
    }
}
