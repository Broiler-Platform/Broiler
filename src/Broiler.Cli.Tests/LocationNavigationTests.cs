using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>Location</c>'s methods — <c>assign</c>, <c>replace</c>, <c>reload</c> and <c>toString</c>.
/// <para>
/// The bridge defined the URL components and none of the methods, so <c>location.replace(url)</c>
/// was <c>TypeError: undefined is not a function</c>. That is not a missing-property read a page can
/// feature-detect around: it is raised at the call and unwinds the rest of the calling function,
/// taking whatever it would have done next with it.
/// </para>
/// <para>
/// Google Search reaches it on the branch its bot-detection bootstrap takes when <c>window.prs</c>
/// is absent and the <c>SG_SS</c> cookie is already set — <c>W(a)</c> ends in
/// <c>b !== void 0 &amp;&amp; a.replace(b)</c> over <c>a = location</c> — which is why the first
/// assertion below runs that expression verbatim rather than only checking the method exists.
/// </para>
/// <para>
/// <b>They do not navigate.</b> A capture renders the document it was given, and
/// <c>--follow-first-link</c> is the explicit opt-in for going elsewhere; each method records the
/// request and returns, which is what a browser that blocks a navigation does. The URL is
/// deliberately left alone with it — see <c>LocationBinding</c>.
/// </para>
/// </summary>
public sealed class LocationNavigationTests
{
    private const string PageUrl = "https://www.google.com/search?q=broiler#frag";

    private static (JSContext Context, DomBridge Bridge) Attach(string url = PageUrl)
    {
        var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!doctype html><html><body></body></html>", url);
        return (context, bridge);
    }

    /// <summary>The reported failure, run as the bootstrap spells it.</summary>
    [Fact(Timeout = 600000)]
    public void TheReportedShape_DoesNotThrow()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var thrown = Record.Exception(() => context.Eval("""
            (function() {
                var a = location, b = '/sorry/index?continue=x';
                b !== void 0 && a.replace(b);
            })();
            """));

        Assert.Null(thrown);
    }

    [Theory]
    [InlineData("location.assign('/next')")]
    [InlineData("location.replace('/next')")]
    [InlineData("location.reload()")]
    [InlineData("location.assign('https://example.com/absolute')")]
    [InlineData("document.location.replace('/next')")]
    public void NavigationMethods_ReturnUndefined_RatherThanThrowing(string call)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("undefined", context.Eval($"String(typeof ({call}))").ToString());
    }

    /// <summary>
    /// The document did not change, so neither did its URL. A `pathname` answering for a document
    /// nobody loaded is harder to debug than one answering for the document in hand.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ANavigationRequest_LeavesTheUrlAlone()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        context.Eval("location.replace('/somewhere-else'); location.assign('/elsewhere');");

        Assert.Equal(PageUrl, context.Eval("String(location.href)").ToString());
        Assert.Equal("/search", context.Eval("String(location.pathname)").ToString());
    }

    /// <summary>
    /// `location.href = url` is `assign(url)` with different spelling (HTML §7.10.5 — the setter
    /// performs "location-object navigate"), and it is the spelling pages reach for most. It was a
    /// plain data property, so the write stuck and nothing else happened: the page believed it had
    /// left, the capture never knew it had been asked, and the URL then disagreed with the document
    /// still in hand.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AssigningHref_DoesNotThrow_AndLeavesTheUrlAlone()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var thrown = Record.Exception(() => context.Eval("location.href = 'https://example.com/next';"));

        Assert.Null(thrown);
        Assert.Equal(PageUrl, context.Eval("String(location.href)").ToString());
        Assert.Equal("/search", context.Eval("String(location.pathname)").ToString());
    }

    /// <summary>Through the document's spelling of the same object, and in a frame.</summary>
    [Fact(Timeout = 600000)]
    public void AssigningHref_BehavesTheSameThroughDocumentLocation()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var thrown = Record.Exception(() => context.Eval("document.location.href = '/relative-next';"));

        Assert.Null(thrown);
        Assert.Equal(PageUrl, context.Eval("String(document.location.href)").ToString());
    }

    /// <summary>
    /// A `location.href` read is still a plain string read — the accessor must not change what the
    /// property answers, only what writing to it does.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ReadingHref_IsUnchanged()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal(PageUrl, context.Eval("String(location.href)").ToString());
        Assert.Equal("string", context.Eval("String(typeof location.href)").ToString());
        Assert.Equal("true", context.Eval("String('href' in location)").ToString());
    }

    /// <summary>
    /// Location stringifies to its href. `[object Object]` is what pages got from `"" + location`,
    /// and it is wrong everywhere it appears — in a URL they build, in a log line, in a comparison.
    /// </summary>
    [Theory]
    [InlineData("String(location)")]
    [InlineData("'' + location")]
    [InlineData("location.toString()")]
    [InlineData("`${location}`")]
    public void Location_StringifiesToItsHref(string expression)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal(PageUrl, context.Eval($"String({expression})").ToString());
    }

    /// <summary>
    /// `port` is empty for a URL on its scheme's default port — that default is exactly what `host`
    /// leaves out — and the number when one was given. It was undefined for every URL.
    /// </summary>
    [Theory]
    [InlineData("https://www.google.com/search", "")]
    [InlineData("http://example.com/", "")]
    [InlineData("https://example.com:8443/x", "8443")]
    [InlineData("http://localhost:3000/x", "3000")]
    public void Port_IsEmptyOnADefaultPortAndTheNumberOtherwise(string url, string expected)
    {
        var (context, bridge) = Attach(url);
        using var _ = context;
        using var __ = bridge;

        Assert.Equal(expected, context.Eval("String(location.port)").ToString());
    }

    /// <summary>
    /// `document.location` is the same object, so it is the same functions — not a second set that
    /// could drift.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void DocumentLocation_CarriesTheSameMethods()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("true", context.Eval("String(document.location.replace === location.replace)").ToString());
        Assert.Equal("true", context.Eval("String(document.location.assign === location.assign)").ToString());
    }

    /// <summary>
    /// A framed page calls `location.replace()` as readily as a top-level one, and its Location is
    /// built separately — so it needs the methods separately too.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AFramesLocation_CarriesTheMethodsToo()
    {
        var context = new JSContext();
        var bridge = new DomBridge();
        using var _ = context;
        using var __ = bridge;
        bridge.Attach(
            context,
            "<!doctype html><html><body><iframe id='f' srcdoc='<p>inner</p>'></iframe></body></html>",
            PageUrl);
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        var thrown = Record.Exception(() => context.Eval("""
            var w = document.getElementById('f').contentWindow;
            globalThis.__kinds = [typeof w.location.assign, typeof w.location.replace,
                                  typeof w.location.reload, typeof w.location.toString].join(',');
            w.location.replace('/inner-next');
            w.location.href = '/inner-href';
            globalThis.__href = String(w.location.href);
            """));

        Assert.Null(thrown);
        Assert.Equal("function,function,function,function", context.Eval("String(globalThis.__kinds)").ToString());
        Assert.Equal("about:srcdoc", context.Eval("String(globalThis.__href)").ToString());
    }

    /// <summary>
    /// What must not change: the components the bridge already answered, through the same object.
    /// </summary>
    [Theory]
    [InlineData("protocol", "https:")]
    [InlineData("host", "www.google.com")]
    [InlineData("hostname", "www.google.com")]
    [InlineData("pathname", "/search")]
    [InlineData("search", "?q=broiler")]
    [InlineData("hash", "#frag")]
    [InlineData("origin", "https://www.google.com")]
    public void TheExistingComponents_AreUnchanged(string member, string expected)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal(expected, context.Eval($"String(location.{member})").ToString());
    }
}
