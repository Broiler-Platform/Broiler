using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The File API data surfaces — <c>Blob</c>, <c>File</c>, <c>FileList</c> and
/// <c>URL.createObjectURL</c> — and the file input's <c>files</c> that reaches the last of them.
/// </summary>
/// <remarks>
/// <para>
/// None of the interfaces existed, so the bare name was a <c>ReferenceError</c> — the kind that
/// aborts the script rather than the statement. <c>Blob</c> is reached by ordinary pages and not only
/// by upload code: it is how a page builds a downloadable payload, how it posts binary through
/// <c>fetch</c>, and what <c>response.blob()</c> is supposed to hand back.
/// </para>
/// <para>
/// That last one is why this replaces a stub as well as filling an absence. <c>response.blob()</c>
/// already answered, with a plain object carrying four members and nothing else, so
/// <c>constructor.name</c> was <c>"Object"</c> and there was no <c>slice</c>. It was invisible only
/// because the interface it imitated did not exist to be compared against.
/// </para>
/// <para>
/// Every expectation is Chromium's measured answer. Three are worth naming because reasoning gets
/// them wrong: the parts argument is a Web IDL sequence, which deliberately does not accept a string,
/// so <c>new Blob('abc')</c> throws rather than making a three-byte blob; a <c>type</c> carrying a
/// character outside printable ASCII is discarded entirely rather than kept or escaped; and
/// <c>slice</c> gives its result an <em>empty</em> type rather than inheriting the source's.
/// </para>
/// </remarks>
public sealed class BlobInterfaceTests
{
    private const string Markup =
        "<!DOCTYPE html><html><body><input type=file id=fi><input type=text id=ti></body></html>";

    private static DomBridge Attach(out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, Markup, "https://example.com/index.html");
        return bridge;
    }

    private static string Outcome(JSContext context, string body) =>
        context.Eval($$"""
            (() => {
                try { return String({{body}}); }
                catch (e) { return 'THREW ' + (e.name || '?'); }
            })()
            """).ToString();

    // ---------------- The interface ----------------

    [Fact(Timeout = 600000)]
    public void Blob_Is_A_Constructible_Interface()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("function", Outcome(context, "typeof Blob"));
        Assert.Equal("Blob", Outcome(context, "new Blob().constructor.name"));
        Assert.Equal("[object Blob]", Outcome(context, "Object.prototype.toString.call(new Blob())"));
        Assert.Equal("true", Outcome(context, "new Blob() instanceof Blob"));
        Assert.Equal("THREW TypeError", Outcome(context, "Blob()"));
        // Members on the prototype, nothing on the instance.
        Assert.Equal("", Outcome(context, "Object.getOwnPropertyNames(new Blob()).join(',')"));
        Assert.Equal("arrayBuffer,constructor,size,slice,text,type",
            Outcome(context, "Object.getOwnPropertyNames(Blob.prototype).sort().join(',')"));
        // Both constructor parameters are optional, so Web IDL gives it a length of 0.
        Assert.Equal("0", Outcome(context, "Blob.length"));
        Assert.Equal("THREW TypeError", Outcome(context, "Blob.prototype.slice.call({}, 0)"));
    }

    [Fact(Timeout = 600000)]
    public void An_Empty_Blob_Has_No_Bytes_And_No_Type()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("0,", Outcome(context, "[new Blob().size, new Blob().type].join(',')"));
    }

    /// <summary>
    /// Every part kind the constructor accepts. A number is not one of them — it is stringified, which
    /// is why <c>[123]</c> makes three bytes and not one.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("new Blob(['ab', 'cd']).size", "4")]
    // UTF-8, so a two-character string can be five bytes.
    [InlineData("new Blob(['\\u00e9\\u4e2d']).size", "5")]
    [InlineData("new Blob([123]).size", "3")]
    [InlineData("new Blob(['a', new Blob(['xy']), 'b']).size", "4")]
    [InlineData("new Blob([new ArrayBuffer(5)]).size", "5")]
    [InlineData("new Blob([new Uint8Array([1, 2, 3])]).size", "3")]
    [InlineData("new Blob([new DataView(new ArrayBuffer(4))]).size", "4")]
    public void The_Parts_Sequence_Accepts_Strings_Buffers_And_Blobs(string body, string expected)
    {
        using var bridge = Attach(out var context);

        Assert.Equal(expected, Outcome(context, body));
    }

    /// <summary>
    /// A Web IDL <c>sequence</c> deliberately does not accept a string, however iterable a string is —
    /// so the shorthand a reader of the signature would expect to work is the one case that throws.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Non_Sequence_Parts_Argument_Is_A_TypeError()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("THREW TypeError", Outcome(context, "new Blob('abc')"));
    }

    /// <summary>The type is lower-cased, and dropped outright — not escaped, not kept — when it
    /// carries anything outside printable ASCII.</summary>
    [Fact(Timeout = 600000)]
    public void The_Type_Is_Lowercased_Or_Discarded()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("text/plain", Outcome(context, "new Blob(['a'], {type: 'TEXT/Plain'}).type"));
        Assert.Equal("", Outcome(context, "new Blob(['a'], {type: 'te\\u00e9xt'}).type"));
    }

    // ---------------- Reading a blob ----------------

    [Fact(Timeout = 600000)]
    public void Text_And_ArrayBuffer_Are_Real_Promises()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true", Outcome(context, "new Blob(['hi']).text() instanceof Promise"));
        Assert.Equal("true", Outcome(context, "new Blob(['hi']).arrayBuffer() instanceof Promise"));

        // And what they deliver, read after the microtask checkpoint that ends the evaluation which
        // started them. `text()` decodes as UTF-8, so the byte count and the character count differ.
        context.Eval("""
            var text = 'pending', bytes = -1;
            new Blob(['hi ', 'é']).text().then(function (t) { text = t; });
            new Blob(['hi ', 'é']).arrayBuffer().then(function (b) { bytes = b.byteLength; });
            """);
        Assert.Equal("hi é", context.Eval("text").ToString());
        Assert.Equal("5", context.Eval("String(bytes)").ToString());
    }

    /// <summary>Slicing: the bounds clamp, a negative one counts back from the end, and the result is
    /// itself a blob.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("b.slice(1, 3).size", "2")]
    [InlineData("b.slice().size", "6")]
    [InlineData("b.slice(2).size", "4")]
    [InlineData("b.slice(-2).size", "2")]
    [InlineData("b.slice(0, -2).size", "4")]
    // An end before the start is an empty blob, not an error and not a reversed one.
    [InlineData("b.slice(4, 2).size", "0")]
    [InlineData("b.slice(0, 99).size", "6")]
    [InlineData("b.slice(0, 2) instanceof Blob", "true")]
    public void Slice_Clamps_Its_Bounds(string body, string expected)
    {
        using var bridge = Attach(out var context);

        Assert.Equal(expected, Outcome(context, $"(function () {{ var b = new Blob(['abcdef']); return {body}; }})()"));
    }

    /// <summary>A slice does <em>not</em> inherit the source's content type: it has the one the caller
    /// passes, or none.</summary>
    [Fact(Timeout = 600000)]
    public void Slice_Takes_Only_The_Content_Type_It_Is_Given()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(",text/html", Outcome(context,
            "(function () { var b = new Blob(['abcdef'], {type: 'text/plain'});" +
            " return [b.slice(0, 2).type, b.slice(0, 2, 'text/html').type].join(','); })()"));
    }

    /// <summary>
    /// <c>stream()</c> is deliberately absent: it returns a <c>ReadableStream</c>, and this engine has
    /// one partial stream already — the one <c>response.body</c> hands back — which a second copy
    /// should not be written against. A page feature-detecting it takes its <c>arrayBuffer()</c>
    /// fallback, which works. Pinned so that adding it is a decision rather than a drift.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Stream_Is_Absent()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("undefined", Outcome(context, "typeof Blob.prototype.stream"));
    }

    // ---------------- File ----------------

    [Fact(Timeout = 600000)]
    public void File_Is_A_Blob_With_A_Name()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("x.txt,2,text/plain", Outcome(context,
            "(function () { var f = new File(['ab'], 'x.txt', {type: 'text/plain'});" +
            " return [f.name, f.size, f.type].join(','); })()"));
        Assert.Equal("true", Outcome(context, "new File(['a'], 'n') instanceof Blob"));
        // A real prototype chain rather than a hook, so `instanceof Blob` answers through it.
        Assert.Equal("true", Outcome(context, "Object.getPrototypeOf(File.prototype) === Blob.prototype"));
        Assert.Equal("constructor,lastModified,lastModifiedDate,name,webkitRelativePath",
            Outcome(context, "Object.getOwnPropertyNames(File.prototype).sort().join(',')"));
        Assert.Equal("2", Outcome(context, "File.length"));
        // The name is required, which is the one thing that separates the two constructors.
        Assert.Equal("THREW TypeError", Outcome(context, "new File(['a'])"));
        Assert.Equal("number", Outcome(context, "typeof new File(['a'], 'n').lastModified"));
        Assert.Equal("1234", Outcome(context, "new File(['a'], 'n', {lastModified: 1234}).lastModified"));
    }

    // ---------------- FileList and input.files ----------------

    /// <summary>
    /// <c>input.files</c> read <c>undefined</c> on every input, so the standard guard
    /// <c>if (input.files &amp;&amp; input.files.length)</c> was a <c>TypeError</c> on the very input it
    /// was written for. It is a <c>FileList</c> on a file input and <c>null</c> on anything else — and
    /// the list is empty, which is what a browser reports for an input nobody has touched.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_File_Input_Reports_An_Empty_FileList()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("function", Outcome(context, "typeof FileList"));
        Assert.Equal("FileList,0", Outcome(context,
            "(function () { var f = document.getElementById('fi').files; return [f.constructor.name, f.length].join(','); })()"));
        Assert.Equal("true", Outcome(context, "document.getElementById('fi').files instanceof FileList"));
        Assert.Equal("null", Outcome(context, "String(document.getElementById('fi').files.item(0))"));
        // One object per element, as in a browser: a page that stashes the list keeps the same one.
        Assert.Equal("true", Outcome(context,
            "(function () { var i = document.getElementById('fi'); return i.files === i.files; })()"));
        // Every other control has no files at all — null, not an empty list.
        Assert.Equal("null", Outcome(context, "String(document.getElementById('ti').files)"));
    }

    /// <summary>
    /// <c>FileList.prototype</c> carries <c>item</c> but not <c>length</c>, where a browser has both.
    /// That is the shared indexed-collection machinery answering <c>length</c> from the host rather
    /// than from a prototype accessor, so it is identical for <c>NodeList</c> and the rest and is not
    /// this interface's to change. Pinned rather than left unstated.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void FileList_Shares_The_Collection_Machinery()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("constructor,item", Outcome(context,
            "Object.getOwnPropertyNames(FileList.prototype).sort().join(',')"));
        Assert.Equal("0", Outcome(context, "document.getElementById('fi').files.length"));
        Assert.Equal("THREW TypeError", Outcome(context, "new FileList()"));
    }

    // ---------------- Object URLs ----------------

    [Fact(Timeout = 600000)]
    public void CreateObjectURL_Mints_A_Blob_Url()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true", Outcome(context, "/^blob:/.test(URL.createObjectURL(new Blob(['a'])))"));
        // Two calls name two different blobs, even for equal content.
        Assert.Equal("false", Outcome(context,
            "URL.createObjectURL(new Blob(['a'])) === URL.createObjectURL(new Blob(['a']))"));
        Assert.Equal("undefined", Outcome(context,
            "URL.revokeObjectURL(URL.createObjectURL(new Blob(['a'])))"));
        // Only a blob can be given one.
        Assert.Equal("THREW TypeError", Outcome(context, "URL.createObjectURL({})"));
    }
}
