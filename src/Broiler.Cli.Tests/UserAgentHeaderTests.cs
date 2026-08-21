using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Broiler.Layout.Net;

namespace Broiler.Cli.Tests;

/// <summary>
/// Every request Broiler makes says who is making it.
/// </summary>
/// <remarks>
/// <para>
/// <c>HttpClient</c> sends no <c>User-Agent</c> unless one is configured, and a missing header is
/// not a milder version of an unrecognised one — it is grounds for refusal. Wikimedia's User-Agent
/// policy answers an unidentified request <c>403 Forbidden</c> without looking further, so
/// <c>https://www.mediawiki.org/wiki/MediaWiki</c> failed instantly in both the CLI and the browser,
/// and went on failing per sub-resource once the document was fixed: the stylesheets, the
/// <c>load.php?modules=startup</c> bootstrap that pulls in every other module, and the photographs
/// on <c>upload.wikimedia.org</c> each refused separately, for the same missing header.
/// </para>
/// <para>
/// So the assertion is per resource kind rather than per call site: a header set on the document's
/// client and nowhere else looks fixed from the outside and still renders an unstyled, script-less
/// page. What it cannot cover is the loaders inside the <c>Broiler.HTML</c> submodule — the
/// <c>&lt;link&gt;</c>, <c>&lt;img&gt;</c> and web-font fetchers — whose identical fix ships as a
/// patch under <c>patches/</c> until a maintainer applies it; asserting on those here would fail
/// against the pinned pointer CI builds.
/// </para>
/// </remarks>
public sealed class UserAgentHeaderTests : IDisposable
{
    private readonly RecordingOrigin _origin = new();

    public void Dispose() => _origin.Dispose();

    /// <summary>
    /// The reported bug exactly: the top-level document. This is the request that answered 403
    /// before Broiler had parsed a single byte.
    /// </summary>
    [Fact(Timeout = 600000)]
    public async Task The_Document_Request_Is_Identified()
    {
        _origin.Serve("page.html", "text/html", "<!DOCTYPE html><html><body>hi</body></html>");

        var output = Path.Combine(Path.GetTempPath(), $"broiler-ua-{Guid.NewGuid():N}.html");
        try
        {
            await new CaptureService().CaptureAsync(new CaptureOptions
            {
                Url = _origin.Url("page.html"),
                OutputPath = output,
            });
        }
        finally
        {
            File.Delete(output);
        }

        AssertEveryRequestIdentified("/page.html");
    }

    /// <summary>
    /// The sub-resource half. mediawiki.org served the document once the header was on that one
    /// client and still refused these, which is what left the page unstyled and its modules unloaded.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Stylesheet_And_Script_Requests_Are_Identified()
    {
        _origin.Serve("sheet.css", "text/css", "body { color: red }");
        _origin.Serve("app.js", "text/javascript", "var loaded = 1;");

        var html = new StringBuilder("<!DOCTYPE html><html><head>")
            .Append($"""<link rel="stylesheet" href="{_origin.Url("sheet.css")}">""")
            .Append("</head><body>")
            .Append($"""<script src="{_origin.Url("app.js")}"></script>""")
            .Append("</body></html>")
            .ToString();

        using var context = new JSContext();
        new DomBridge().Attach(context, html, _origin.Url("page.html"));
        CaptureService.ExecuteScriptsWithDom(html, _origin.Url("page.html"));

        AssertEveryRequestIdentified("/sheet.css");
        AssertEveryRequestIdentified("/app.js");
    }

    /// <summary>
    /// Asserts <paramref name="path"/> was requested, and that <em>every</em> request for it carried
    /// the header.
    /// </summary>
    /// <remarks>
    /// Not "exactly one request": a sheet the speculative preload scan has already asked for is
    /// asked for again by the post-parse collection, and how many round trips a resource costs is a
    /// different question from whether they identify themselves. Asserting a count here would make
    /// this test fail for a change to the preload scan.
    /// </remarks>
    private void AssertEveryRequestIdentified(string path)
    {
        var seen = _origin.UserAgentsFor(path);

        Assert.NotEmpty(seen);
        Assert.All(seen, agent => Assert.Equal(BroilerUserAgent.Value, agent));
    }

    /// <summary>
    /// A page that reads <c>navigator.userAgent</c> and a server that reads the header are asking
    /// the same question, so they get the same answer. Two literals is how they drift apart.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void What_Script_Is_Told_Is_What_The_Network_Is_Told()
    {
        const string Html = """<!DOCTYPE html><html><body><div id="out"></div></body></html>""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, Html, _origin.Url("page.html"));

        var reported = context.Eval("String(navigator.userAgent)").ToString();

        Assert.Equal(BroilerUserAgent.Value, reported);
    }

    /// <summary>A loopback origin that answers a fixed set of paths and records each request's
    /// <c>User-Agent</c>.</summary>
    private sealed class RecordingOrigin : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly ConcurrentDictionary<string, (string ContentType, string Body)> _routes = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _userAgents = new();
        private readonly string _prefix;

        public RecordingOrigin()
        {
            // HttpListener cannot report an OS-assigned port, so one is taken by binding a socket
            // and closing it — the same pattern LatencyOrigin uses in this project.
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            _prefix = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(_prefix);
            _listener.Start();
            _ = Task.Run(Serve);
        }

        public string Url(string path) => _prefix + path;

        public void Serve(string path, string contentType, string body) =>
            _routes["/" + path] = (contentType, body);

        /// <summary>
        /// Every <c>User-Agent</c> seen on <paramref name="path"/>. A request that carried none
        /// contributes the empty string rather than nothing, so "the header was missing" fails the
        /// assertion instead of emptying the collection and passing a sloppier one.
        /// </summary>
        public string[] UserAgentsFor(string path) =>
            _userAgents.TryGetValue(path, out var seen) ? [.. seen] : [];

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
            var path = context.Request.Url?.AbsolutePath ?? "/";
            _userAgents.GetOrAdd(path, _ => new ConcurrentQueue<string>())
                .Enqueue(context.Request.Headers["User-Agent"] ?? string.Empty);

            try
            {
                if (_routes.TryGetValue(path, out var route))
                {
                    var bytes = Encoding.UTF8.GetBytes(route.Body);
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = route.ContentType;
                    context.Response.ContentLength64 = bytes.Length;
                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    context.Response.StatusCode = 404;
                }

                context.Response.Close();
            }
            catch (HttpListenerException)
            {
                // The client gave up on the response; the header it sent is already recorded.
            }
        }

        public void Dispose()
        {
            _listener.Close();
            ((IDisposable)_listener).Dispose();
        }
    }
}
