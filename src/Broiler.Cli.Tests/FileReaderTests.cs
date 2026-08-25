using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>FileReader</c> (File API §6) and the <c>ProgressEvent</c> its events are.
/// </summary>
/// <remarks>
/// <para>
/// Both were absent, so the bare name was a <c>ReferenceError</c> — the kind that aborts the script
/// rather than the statement. <c>FileReader</c> is the standard way a page turns a <c>Blob</c> into
/// something it can use: the text of a dropped file, a data URL for a preview, an
/// <c>ArrayBuffer</c> to post. <c>Blob</c> itself landed earlier and this is the other half of the
/// same slice.
/// </para>
/// <para>Every expectation is Chromium's measured answer over the same probe run against both.</para>
/// </remarks>
public sealed class FileReaderTests
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
    /// Starts the read in one evaluation and reads the result in the next. A <c>FileReader</c> is
    /// asynchronous by definition — a page attaches its handlers after calling <c>readAs*</c>, so
    /// doing the work synchronously would deliver <c>load</c> to nobody.
    /// </summary>
    private static string EvalAfterMicrotasks(JSContext context, string start, string read)
    {
        context.Eval(start);
        return context.Eval(read).ToString();
    }

    /// <summary>The interface, its three state constants, and the initial state of a reader that has
    /// been asked for nothing.</summary>
    [Fact(Timeout = 600000)]
    public void The_Interface_Exists_With_Its_Constants_And_Initial_State()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("function/0/1/2/0/null/null/TypeError", Eval(context, """
            var reader = new FileReader();
            var noNew;
            try { FileReader(); noNew = 'ok'; } catch (e) { noNew = e.name; }
            return (typeof FileReader) + '/' + FileReader.EMPTY + '/' + FileReader.LOADING + '/' +
                   FileReader.DONE + '/' + reader.readyState + '/' + String(reader.result) + '/' +
                   String(reader.error) + '/' + noNew;
            """));
    }

    /// <summary>
    /// The event sequence and what each event reports. The result is readable from <c>load</c>
    /// onwards and not before, and <c>readyState</c> is <c>LOADING</c> until then.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Events_Arrive_In_Order_With_The_Progress_They_Report()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "1/null/0|" +
            "loadstart:ProgressEvent:0/5/true:1|progress:ProgressEvent:5/5/true:1|" +
            "load:ProgressEvent:5/5/true:2|loadend:ProgressEvent:5/5/true:2|\"hello\"",
            EvalAfterMicrotasks(context, """
                var events = [];
                var reader = new FileReader();
                ['loadstart', 'progress', 'load', 'loadend', 'error', 'abort'].forEach(function (type) {
                    reader.addEventListener(type, function (e) {
                        events.push(type + ':' + e.constructor.name + ':' + e.loaded + '/' + e.total +
                                    '/' + e.lengthComputable + ':' + reader.readyState);
                    });
                });
                reader.readAsText(new Blob(['hello']));
                // Nothing has happened yet: the read is a task, not a call.
                var immediate = reader.readyState + '/' + String(reader.result) + '/' + events.length;
                """, """
                [immediate].concat(events).concat([JSON.stringify(reader.result)]).join('|')
                """));
    }

    /// <summary>The four ways of reading the same bytes.</summary>
    /// <remarks>
    /// The data-URL default is the one worth pinning: a blob with no type reads as
    /// <c>application/octet-stream</c>, which is the specified default and not the empty media type
    /// the blob itself reports.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void The_Four_Read_Methods_Convert_The_Same_Bytes()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "\"hi\"|data:application/octet-stream;base64,aGk=|data:text/plain;base64,aGk=|" +
            "ArrayBuffer/4|\"\\u0001ÿA\"",
            EvalAfterMicrotasks(context, """
                var results = {};
                function read(name, method, blob) {
                    var reader = new FileReader();
                    reader.onload = function () { results[name] = reader.result; };
                    reader[method](blob);
                }
                read('text', 'readAsText', new Blob(['hi']));
                read('url', 'readAsDataURL', new Blob(['hi']));
                read('typedUrl', 'readAsDataURL', new Blob(['hi'], { type: 'text/plain' }));
                read('binary', 'readAsBinaryString', new Blob([new Uint8Array([1, 255, 65])]));
                var buffer = new FileReader();
                buffer.onload = function () { results.buffer = buffer.result.constructor.name + '/' + buffer.result.byteLength; };
                buffer.readAsArrayBuffer(new Blob(['abcd']));
                """, """
                [JSON.stringify(results.text), results.url, results.typedUrl, results.buffer,
                 JSON.stringify(results.binary)].join('|')
                """));
    }

    /// <summary>An <c>on*</c> handler and an <c>addEventListener</c> registration both receive the
    /// event, and a removed listener does not.</summary>
    [Fact(Timeout = 600000)]
    public void Both_Handler_Styles_Receive_The_Event()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("handler,listener", EvalAfterMicrotasks(context, """
            var seen = [];
            var reader = new FileReader();
            function removed() { seen.push('removed'); }
            reader.onload = function () { seen.push('handler'); };
            reader.addEventListener('load', function () { seen.push('listener'); });
            reader.addEventListener('load', removed);
            reader.removeEventListener('load', removed);
            reader.readAsText(new Blob(['x']));
            """, "seen.join(',')"));
    }

    /// <summary>
    /// A reader busy with one blob refuses a second, and a read with no blob at all is a
    /// <c>TypeError</c> rather than a read of nothing.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Busy_Reader_Refuses_A_Second_Read()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("InvalidStateError/TypeError", Eval(context, """
            var reader = new FileReader();
            reader.readAsText(new Blob(['a']));
            var busy, missing;
            try { reader.readAsText(new Blob(['b'])); busy = 'ok'; } catch (e) { busy = e.name; }
            try { new FileReader().readAsText(); missing = 'ok'; } catch (e) { missing = e.name; }
            return busy + '/' + missing;
            """));
    }

    /// <summary>
    /// <c>abort()</c> ends the read with no result: the reader fires <c>abort</c> and
    /// <c>loadend</c> and nothing else — not <c>loadstart</c>, because the read had not begun.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Abort_Ends_The_Read_With_No_Result()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("abort,loadend/2/null", EvalAfterMicrotasks(context, """
            var events = [];
            var reader = new FileReader();
            ['loadstart', 'progress', 'load', 'abort', 'loadend', 'error'].forEach(function (type) {
                reader.addEventListener(type, function () { events.push(type); });
            });
            reader.readAsText(new Blob(['x']));
            reader.abort();
            """, """
            events.join(',') + '/' + reader.readyState + '/' + String(reader.result)
            """));
    }

    /// <summary>
    /// <c>ProgressEvent</c> is a real interface, because a handler reads <c>e.constructor.name</c>
    /// and <c>e instanceof</c> as much as it reads <c>e.loaded</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ProgressEvent_Is_A_Real_Interface()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("ProgressEvent/progress/1/2/true/false/false", Eval(context, """
            var event = new ProgressEvent('progress', { loaded: 1, total: 2, lengthComputable: true });
            return event.constructor.name + '/' + event.type + '/' + event.loaded + '/' + event.total +
                   '/' + event.lengthComputable + '/' + event.bubbles + '/' + event.cancelable;
            """));
    }
}
