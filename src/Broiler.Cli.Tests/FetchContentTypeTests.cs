using System.Diagnostics;
using System.Net;
using System.Text;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.HtmlBridge.Logging;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// A request body carries the <c>Content-Type</c> the page asked for, parameters and all.
/// <para>
/// The binding used to build the body as
/// <c>new StringContent(body, Encoding.UTF8, contentTypeHeader)</c>. That third parameter is the
/// media type <em>alone</em> — it validates through <c>MediaTypeHeaderValue</c> — so any value with
/// a <c>;</c> in it threw <c>FormatException("The format of value '…' is invalid.")</c> before the
/// request was built. The <c>catch</c> in the fetch binding turns that into an error
/// <c>Response</c>, so the symptom was not a crash: it was a POST that never reached the network,
/// reported to the page as a failure.
/// </para>
/// <para>
/// The spellings that hit it are the ordinary ones. google.com's start page posts
/// <c>application/x-www-form-urlencoded;charset=utf-8</c> — its XHR sets that through
/// <c>setRequestHeader</c>, and the polyfill hands <c>_headers</c> to <c>fetch</c> as
/// <c>opts.headers</c> — and every <c>multipart/form-data; boundary=…</c> upload and
/// <c>text/plain; charset=UTF-8</c> post failed the same way.
/// </para>
/// <para>
/// These assert against a loopback origin that records what it received, so what is checked is the
/// header on the wire rather than the binding's own bookkeeping. A request that never arrives is the
/// bug, so every case here also proves the request was made at all.
/// </para>
/// </summary>
public sealed class FetchContentTypeTests
{
    /// <summary>Runs <paramref name="script"/> against a bridge attached to the origin's page URL.</summary>
    private static List<string> Run(RecordingOrigin origin, string script)
    {
        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<!doctype html><html><body></body></html>", origin.PageUrl);

        var errors = new List<string>();
        void Handler(RenderLogEntry entry)
        {
            if (entry.Level == LogLevel.Error)
                lock (errors) errors.Add($"{entry.Context}: {entry.Message}");
        }

        RenderLogger.EntryLogged += Handler;
        try
        {
            // The binding's network call is synchronous (SendAsync(...).GetAwaiter().GetResult()), so
            // the origin has recorded the request by the time Eval returns — nothing to wait for.
            context.Eval(script);
        }
        finally
        {
            RenderLogger.EntryLogged -= Handler;
        }

        lock (errors) return [.. errors];
    }

    private static void AssertNoInvalidFormatError(List<string> errors) =>
        Assert.DoesNotContain(errors, e => e.Contains("format of value", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The reported failure, and its neighbours. The expected value is the parsed header re-rendered,
    /// which inserts the conventional space after <c>;</c> — the media type and every parameter
    /// survive, which is what the page and the server care about.
    /// </summary>
    [Theory]
    [InlineData("application/x-www-form-urlencoded;charset=utf-8", "application/x-www-form-urlencoded; charset=utf-8")]
    [InlineData("application/x-www-form-urlencoded; charset=UTF-8", "application/x-www-form-urlencoded; charset=UTF-8")]
    [InlineData("text/plain; charset=UTF-8", "text/plain; charset=UTF-8")]
    [InlineData("multipart/form-data; boundary=----WebKitFormBoundaryhQ1", "multipart/form-data; boundary=----WebKitFormBoundaryhQ1")]
    [InlineData("application/json; charset=utf-8", "application/json; charset=utf-8")]
    [InlineData("application/json", "application/json")]
    public void Fetch_SendsTheContentTypeThePageAskedFor(string requested, string expected)
    {
        using var origin = new RecordingOrigin();

        var errors = Run(origin, $$"""
            fetch('/post', {
                method: 'POST',
                headers: { 'Content-Type': '{{requested}}' },
                body: 'q=broiler'
            });
            """);

        AssertNoInvalidFormatError(errors);
        var request = Assert.Single(origin.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal(expected, request.ContentType);
        Assert.Equal("q=broiler", request.Body);
    }

    /// <summary>
    /// google.com's own path: <c>setRequestHeader</c> on an <c>XMLHttpRequest</c>. The polyfill
    /// forwards <c>_headers</c> to <c>fetch</c>, so this is the second way into the same throw.
    /// </summary>
    [Fact]
    public void Xhr_SetRequestHeaderContentType_ReachesTheOrigin()
    {
        using var origin = new RecordingOrigin();

        var errors = Run(origin, """
            var x = new XMLHttpRequest();
            x.open('POST', '/gen_204?atyp=i');
            x.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded;charset=utf-8');
            x.send('ei=abc&s=web');
            """);

        AssertNoInvalidFormatError(errors);
        var request = Assert.Single(origin.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("/gen_204", request.Path);
        Assert.Equal("application/x-www-form-urlencoded; charset=utf-8", request.ContentType);
        Assert.Equal("ei=abc&s=web", request.Body);
    }

    /// <summary>
    /// With no <c>Content-Type</c> of its own a string body keeps the Fetch default,
    /// <c>text/plain;charset=UTF-8</c> — the behaviour before the fix, unchanged.
    /// </summary>
    [Fact]
    public void Fetch_WithoutAContentType_DefaultsToTextPlainUtf8()
    {
        using var origin = new RecordingOrigin();

        var errors = Run(origin, "fetch('/post', { method: 'POST', body: 'q=broiler' });");

        AssertNoInvalidFormatError(errors);
        var request = Assert.Single(origin.Requests);
        Assert.Equal("text/plain; charset=utf-8", request.ContentType);
    }

    /// <summary>
    /// The charset parameter travels in the header; it does not choose the encoding. Fetch encodes a
    /// string body as UTF-8 whatever the author wrote, so a non-ASCII body arrives intact.
    /// </summary>
    [Fact]
    public void Fetch_EncodesTheBodyAsUtf8_WhateverCharsetTheHeaderNames()
    {
        using var origin = new RecordingOrigin();

        var errors = Run(origin, """
            fetch('/post', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded;charset=iso-8859-1' },
                body: 'q=café—über'
            });
            """);

        AssertNoInvalidFormatError(errors);
        var request = Assert.Single(origin.Requests);
        Assert.Equal("application/x-www-form-urlencoded; charset=iso-8859-1", request.ContentType);
        Assert.Equal("q=café—über", request.Body);
    }

    /// <summary>
    /// A value too malformed to parse is still sent rather than costing the request — losing the
    /// whole POST over a header the page spelled badly is the failure this file exists to remove.
    /// </summary>
    [Fact]
    public void Fetch_WithAnUnparseableContentType_StillSendsTheRequest()
    {
        using var origin = new RecordingOrigin();

        var errors = Run(origin, """
            fetch('/post', {
                method: 'POST',
                headers: { 'Content-Type': 'not a media type' },
                body: 'q=broiler'
            });
            """);

        AssertNoInvalidFormatError(errors);
        var request = Assert.Single(origin.Requests);
        Assert.Equal("q=broiler", request.Body);
    }

    /// <summary>The page sees the origin's answer, not an error <c>Response</c> standing in for it.</summary>
    [Fact]
    public void Fetch_WithAParameterisedContentType_ResolvesWithTheOriginsResponse()
    {
        using var origin = new RecordingOrigin();

        var status = string.Empty;
        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<!doctype html><html><body></body></html>", origin.PageUrl);
        context.Eval("""
            globalThis.__status = null;
            fetch('/post', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded;charset=utf-8' },
                body: 'q=broiler'
            }).then(function(r) { globalThis.__status = r.status + '|' + r.type; });
            """);
        status = context.Eval("String(globalThis.__status)").ToString();

        Assert.Equal("200|basic", status);
    }
}

/// <summary>
/// A loopback origin that answers every request 200 and records the method, path, <c>Content-Type</c>
/// and body it received. Requests are what is asserted, so nothing here is shared between tests: each
/// one binds its own port and disposes it.
/// </summary>
internal sealed class RecordingOrigin : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Lock _sync = new();
    private readonly List<ReceivedRequest> _requests = [];
    private readonly string _prefix;

    public RecordingOrigin()
    {
        // Port 0 asks the OS for a free one, but HttpListener has no way to report it back, so the
        // port is picked by binding a socket, closing it, and taking the number — the same approach
        // LatencyOrigin uses in PreloadScanOverlapTests.
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

    public IReadOnlyList<ReceivedRequest> Requests
    {
        get { lock (_sync) return [.. _requests]; }
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

            Answer(context);
        }
    }

    private void Answer(HttpListenerContext context)
    {
        using (var buffer = new MemoryStream())
        {
            context.Request.InputStream.CopyTo(buffer);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in context.Request.Headers.AllKeys)
            {
                if (name != null)
                    headers[name] = context.Request.Headers[name] ?? string.Empty;
            }

            // Decoded as UTF-8 rather than by the request's declared charset: what the assertions are
            // about is the bytes the binding produced, not the origin's interpretation of them.
            var received = new ReceivedRequest(
                context.Request.HttpMethod,
                context.Request.Url?.AbsolutePath ?? "/",
                context.Request.ContentType,
                Encoding.UTF8.GetString(buffer.ToArray()),
                headers);
            lock (_sync) _requests.Add(received);
        }

        var bytes = "ok"u8.ToArray();
        context.Response.ContentType = "text/plain";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes);
        context.Response.Close();
    }

    public void Dispose()
    {
        _listener.Close();
    }
}

/// <summary>One request as the origin saw it. <paramref name="Headers"/> holds every header it carried.</summary>
internal readonly record struct ReceivedRequest(
    string Method,
    string Path,
    string? ContentType,
    string Body,
    Dictionary<string, string> Headers)
{
    /// <summary>The value the origin received for <paramref name="name"/>, or null if it never arrived.</summary>
    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}
