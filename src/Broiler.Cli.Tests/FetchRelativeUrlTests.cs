using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.HtmlBridge.Logging;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>fetch</c> parses its input against the document's base URL (Fetch §5.4), so a page may name a
/// target the way pages actually do — <c>/gen_204</c>, <c>./ping</c>, <c>//host/path</c>.
/// <para>
/// The binding used to hand the string to <c>HttpClient</c> untouched. A relative request URI with no
/// <c>BaseAddress</c> is not a request at all: <c>HttpClient.PrepareRequestMessage</c> throws
/// <c>InvalidOperationException("An invalid request URI was provided…")</c> before anything reaches
/// the wire. google.com hits this on every page view — it beacons timing to
/// <c>/gen_204?atyp=i&amp;…</c> through <c>navigator.sendBeacon</c>, which delegates to this
/// <c>fetch</c> — and so does any <c>XMLHttpRequest</c> opened on a path, because the XHR polyfill
/// calls <c>fetch(this._url)</c>.
/// </para>
/// <para>
/// What is asserted is the URL the binding resolved, not a response body. The page URLs use the
/// reserved <c>.invalid</c> TLD (RFC 2606), which cannot resolve anywhere, so these reach no host:
/// the request fails, and the resolved URL is still what the binding reports as
/// <c>response.url</c> — on the error path as much as the success path. A request that fails is the
/// expected outcome here; a request that was never made because the URI would not parse is the bug.
/// </para>
/// </summary>
public sealed class FetchRelativeUrlTests
{
    private const string PageUrl = "https://page.invalid/search?q=x";

    private static (JSContext Context, DomBridge Bridge) Attach(string url = PageUrl)
    {
        var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!doctype html><html><body></body></html>", url);
        return (context, bridge);
    }

    /// <summary>Collects the errors the bridge logs while <paramref name="action"/> runs.</summary>
    private static List<string> ErrorsDuring(Action action)
    {
        var errors = new List<string>();
        void Handler(RenderLogEntry entry)
        {
            if (entry.Level == LogLevel.Error)
                lock (errors) errors.Add($"{entry.Context}: {entry.Message}");
        }

        RenderLogger.EntryLogged += Handler;
        try
        {
            action();
        }
        finally
        {
            RenderLogger.EntryLogged -= Handler;
        }

        lock (errors) return [.. errors];
    }

    private static void AssertNoInvalidUriError(List<string> errors) =>
        Assert.DoesNotContain(errors, e => e.Contains("invalid request URI", StringComparison.OrdinalIgnoreCase));

    [Theory]
    [InlineData("/gen_204?atyp=i", "https://page.invalid/gen_204?atyp=i")]
    [InlineData("gen_204", "https://page.invalid/gen_204")]
    [InlineData("./ping", "https://page.invalid/ping")]
    [InlineData("//cdn.invalid/og/ping", "https://cdn.invalid/og/ping")]
    [InlineData("https://absolute.invalid/kept", "https://absolute.invalid/kept")]
    public void FetchResolvesItsInputAgainstThePageUrl(string requested, string expected)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var errors = ErrorsDuring(() => context.Eval(
            $"globalThis.__resolved = null; fetch('{requested}').then(function(r) {{ globalThis.__resolved = r.url; }});"));

        AssertNoInvalidUriError(errors);
        Assert.Equal(expected, context.Eval("String(globalThis.__resolved)").ToString());
    }

    /// <summary>
    /// The reported failure end to end: google.com's own beacon call. <c>sendBeacon</c> reports
    /// <see langword="true"/> when it queued the request, and the URI error must not appear.
    /// </summary>
    [Fact]
    public void SendBeacon_ToARootRelativeTarget_DoesNotRaiseTheInvalidUriError()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var errors = ErrorsDuring(() => context.Eval(
            "globalThis.__queued = navigator.sendBeacon('/gen_204?atyp=i&ei=abc', 'payload');"));

        AssertNoInvalidUriError(errors);
        Assert.Equal("true", context.Eval("String(globalThis.__queued)").ToString());
    }

    /// <summary>
    /// An <c>XMLHttpRequest</c> opened on a path reaches the same resolution — the polyfill's
    /// <c>send</c> calls <c>fetch(this._url)</c>, so this was the second way into the same throw.
    /// </summary>
    [Fact]
    public void Xhr_OpenedOnAPath_DoesNotRaiseTheInvalidUriError()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var errors = ErrorsDuring(() => context.Eval(
            "var x = new XMLHttpRequest(); x.open('GET', '/client_204?atyp=i'); x.send();"));

        AssertNoInvalidUriError(errors);
    }

    /// <summary>
    /// A target that resolves against nothing is a failed request, not a failed script: the binding
    /// reports an error <c>Response</c> the way it reports any other network failure. Throwing would
    /// abort the whole calling script over one beacon.
    /// </summary>
    [Fact]
    public void AnUnresolvableTarget_YieldsAnErrorResponse_RatherThanThrowing()
    {
        var (context, bridge) = Attach(url: string.Empty);
        using var _ = context;
        using var __ = bridge;

        var thrown = Record.Exception(() => context.Eval(
            "globalThis.__type = null; fetch('/gen_204').then(function(r) { globalThis.__type = r.type; });"));

        Assert.Null(thrown);
        Assert.Equal("error", context.Eval("String(globalThis.__type)").ToString());
    }
}
