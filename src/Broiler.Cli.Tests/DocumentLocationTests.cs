using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>document.location</c> is the same <c>Location</c> object as <c>window.location</c>
/// (HTML §3.1.5 — the getter returns the document's relevant global object's Location).
/// <para>
/// It was registered on the window alone. Reading it therefore did not yield a missing property but
/// <see langword="undefined"/>, and the first member access on that threw
/// <c>Cannot get property protocol of undefined</c> — which aborts the entire <c>&lt;script&gt;</c>,
/// not the one statement, taking every function it would have defined and every listener it would
/// have registered with it.
/// </para>
/// <para>
/// google.com's One-Google-bar bundle builds its own origin with
/// <c>dF=function(){var a=document.location;return a.protocol+"//"+a.host}</c>, so it died there.
/// That is the same bundle <c>window.top</c> was fixed for: this is the next thing it reached once
/// <c>top</c> resolved, which is why the assertions below run the spelling verbatim rather than only
/// checking that the property exists.
/// </para>
/// </summary>
public sealed class DocumentLocationTests
{
    private const string PageUrl = "https://www.google.com/search?q=broiler#frag";

    private static (JSContext Context, DomBridge Bridge) Attach(string url = PageUrl)
    {
        var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!doctype html><html><body></body></html>", url);
        return (context, bridge);
    }

    /// <summary>The One-Google-bar origin helper, run as the bundle spells it.</summary>
    [Fact(Timeout = 600000)]
    public void DocumentLocation_YieldsAnOrigin_TheWayTheOneGoogleBarBuildsIt()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var thrown = Record.Exception(() => context.Eval("""
            globalThis.__origin = (function() {
                var a = document.location;
                return a.protocol + '//' + a.host;
            })();
            """));

        Assert.Null(thrown);
        Assert.Equal("https://www.google.com", context.Eval("String(globalThis.__origin)").ToString());
    }

    /// <summary>
    /// Identity, not a copy: pages compare the two, and a second Location object would drift from
    /// the first the moment either grew a mutable member.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void DocumentLocation_IsTheSameObjectAsWindowLocation()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("true", context.Eval("String(document.location === window.location)").ToString());
        Assert.Equal("true", context.Eval("String(document.location === location)").ToString());
    }

    /// <summary>Every member the window's Location carries is reachable through the document's.</summary>
    [Theory]
    [InlineData("protocol", "https:")]
    [InlineData("host", "www.google.com")]
    [InlineData("hostname", "www.google.com")]
    [InlineData("pathname", "/search")]
    [InlineData("search", "?q=broiler")]
    [InlineData("hash", "#frag")]
    [InlineData("origin", "https://www.google.com")]
    [InlineData("href", PageUrl)]
    public void DocumentLocation_ExposesTheSameMembers(string member, string expected)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal(expected, context.Eval($"String(document.location.{member})").ToString());
    }

    /// <summary>
    /// A framed document shares its own window's Location, not the top-level one — a page inside a
    /// frame reads <c>document.location</c> exactly as a top-level page does, and reading it must
    /// not throw there either. A <c>srcdoc</c> frame is used because it is the frame that actually
    /// gets a browsing context with no network: its document URL is <c>about:srcdoc</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void SubDocumentLocation_IsTheFramesOwnLocation()
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
            globalThis.__same = String(w.document.location === w.location);
            globalThis.__href = String(w.document.location.href);
            globalThis.__isTop = String(w.document.location === document.location);
            """));

        Assert.Null(thrown);
        Assert.Equal("true", context.Eval("String(globalThis.__same)").ToString());
        Assert.Equal("about:srcdoc", context.Eval("String(globalThis.__href)").ToString());
        Assert.Equal("false", context.Eval("String(globalThis.__isTop)").ToString());
    }
}
