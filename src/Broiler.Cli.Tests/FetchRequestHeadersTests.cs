using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Every request header the page sets reaches the wire, including the <c>Content-*</c> ones.
/// <para>
/// <c>HttpRequestMessage</c> splits headers in two: the request-level ones live on
/// <c>request.Headers</c> and the entity ones — <c>Content-Language</c>,
/// <c>Content-Disposition</c>, <c>Content-Encoding</c> and the rest — belong to the body.
/// <c>TryAddWithoutValidation</c> reports a header it will not take by returning
/// <see langword="false"/>, and the binding's loop discarded that return without a word: a page
/// could set <c>Content-Language</c>, see no error, and have it silently never sent.
/// </para>
/// <para>
/// Asserted against a loopback origin that records every header it received, so what is checked is
/// the request as it actually arrived.
/// </para>
/// </summary>
public sealed class FetchRequestHeadersTests
{
    private static void Run(RecordingOrigin origin, string script)
    {
        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, "<!doctype html><html><body></body></html>", origin.PageUrl);
        context.Eval(script);
    }

    private static string PostWith(RecordingOrigin origin, string headersLiteral, string body = "payload")
    {
        Run(origin, $$"""
            fetch('/post', { method: 'POST', headers: {{headersLiteral}}, body: '{{body}}' });
            """);
        return body;
    }

    /// <summary>The entity headers the old loop dropped on the floor.</summary>
    [Theory]
    [InlineData("Content-Language", "en-GB")]
    [InlineData("Content-Disposition", "form-data; name=\\\"upload\\\"")]
    [InlineData("Content-Location", "/canonical/here")]
    public void ContentHeaders_ReachTheOrigin(string name, string value)
    {
        using var origin = new RecordingOrigin();

        PostWith(origin, $"{{ '{name}': '{value}' }}");

        var request = Assert.Single(origin.Requests);
        Assert.Equal(value.Replace("\\\"", "\""), request.Header(name));
    }

    /// <summary>Request-level headers keep working — the fallback must not have moved them.</summary>
    [Theory]
    [InlineData("X-Requested-With", "XMLHttpRequest")]
    [InlineData("Accept", "application/json")]
    [InlineData("Accept-Language", "en-GB,en;q=0.9")]
    [InlineData("X-Client-Data", "abc123")]
    public void RequestHeaders_StillReachTheOrigin(string name, string value)
    {
        using var origin = new RecordingOrigin();

        PostWith(origin, $"{{ '{name}': '{value}' }}");

        var request = Assert.Single(origin.Requests);
        Assert.Equal(value, request.Header(name));
    }

    /// <summary>A mixed set arrives whole, each half routed to the collection that accepts it.</summary>
    [Fact]
    public void RequestAndContentHeaders_TravelTogether()
    {
        using var origin = new RecordingOrigin();

        PostWith(origin, """
            {
                'Content-Type': 'application/x-www-form-urlencoded;charset=utf-8',
                'Content-Language': 'de',
                'X-Requested-With': 'XMLHttpRequest',
                'Accept': 'application/json'
            }
            """);

        var request = Assert.Single(origin.Requests);
        Assert.Equal("application/x-www-form-urlencoded; charset=utf-8", request.ContentType);
        Assert.Equal("de", request.Header("Content-Language"));
        Assert.Equal("XMLHttpRequest", request.Header("X-Requested-With"));
        Assert.Equal("application/json", request.Header("Accept"));
        Assert.Equal("payload", request.Body);
    }

    /// <summary>
    /// <c>Content-Length</c> stays dropped, and deliberately so. .NET derives it from the body it is
    /// about to write, and an author value <em>replaces</em> that rather than being rejected — so
    /// forwarding one would let a page declare a length its body does not have and leave the server
    /// waiting for bytes that never arrive. Fetch forbids authors the header for the same reason.
    /// What must hold is that the length on the wire describes the body actually sent.
    /// </summary>
    [Fact]
    public void ContentLength_IsNotTakenFromThePage()
    {
        using var origin = new RecordingOrigin();

        PostWith(origin, "{ 'Content-Length': '9999' }", body: "twelve chars");

        var request = Assert.Single(origin.Requests);
        Assert.Equal("twelve chars", request.Body);
        Assert.Equal("12", request.Header("Content-Length"));
    }

    /// <summary>The XHR path sets headers the same way, so it gains the same reach.</summary>
    [Fact]
    public void Xhr_SetRequestHeader_SendsContentHeadersToo()
    {
        using var origin = new RecordingOrigin();

        Run(origin, """
            var x = new XMLHttpRequest();
            x.open('POST', '/xhr');
            x.setRequestHeader('Content-Language', 'fr');
            x.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
            x.send('body=1');
            """);

        var request = Assert.Single(origin.Requests);
        Assert.Equal("/xhr", request.Path);
        Assert.Equal("fr", request.Header("Content-Language"));
        Assert.Equal("XMLHttpRequest", request.Header("X-Requested-With"));
    }

    /// <summary>
    /// A bodyless request has no content to carry an entity header, so one set there is still not
    /// sent. Manufacturing an empty body to hold it would add a <c>Content-Length: 0</c> to a GET and
    /// change what the request means, which is a worse trade than the dropped header — this pins the
    /// boundary rather than endorsing it. Request-level headers are unaffected and must still arrive.
    /// </summary>
    [Fact]
    public void BodylessRequest_SendsRequestHeaders_ButHasNoContentToCarryEntityOnes()
    {
        using var origin = new RecordingOrigin();

        Run(origin, """
            fetch('/get', { headers: { 'Content-Language': 'es', 'X-Requested-With': 'probe' } });
            """);

        var request = Assert.Single(origin.Requests);
        Assert.Equal("GET", request.Method);
        Assert.Equal("probe", request.Header("X-Requested-With"));
        Assert.Null(request.Header("Content-Language"));
    }
}
