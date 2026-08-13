using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>window</c> is the global object, as it is in a browser.
/// <para>
/// The bridge used to publish <c>window</c> as a <em>separate</em> <c>JSObject</c> and copy its
/// members onto the global afterwards (<c>MirrorWindowMembersOntoGlobal</c>). That covers the
/// members the bridge itself installs, and — via <c>SyncWindowMembersOntoGlobal</c>, if the host
/// remembers to call it between scripts — the ones a page adds. It cannot cover a page that writes
/// <c>window.x</c> and reads the unqualified <c>x</c> <em>inside one script</em>: there is no moment
/// between the two at which a host could run.
/// </para>
/// <para>
/// That is not a hypothetical. It is how google.com bootstraps, in its first real inline script:
/// <c>window.google=_g</c> in one IIFE and <c>google.sn='webhp'</c> in the next. The unqualified read
/// raised <c>ReferenceError: google is not defined</c>, which aborts the whole
/// <c>&lt;script&gt;</c> — so the <c>google</c> namespace was never finished, and every later script
/// on the page died on the same name. The rendered page had none of its script-driven content.
/// </para>
/// </summary>
public sealed class WindowIsTheGlobalObjectTests
{
    private static (JSContext Context, DomBridge Bridge) Attach(string url = "https://www.google.com/")
    {
        var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!doctype html><html><body></body></html>", url);
        return (context, bridge);
    }

    private static string ValueOf(JSContext context, string expression) =>
        context.Eval($"String({expression})").ToString();

    /// <summary>
    /// The identity itself, plus the three self-references a browser exposes alongside it. A
    /// top-level window is its own <c>parent</c>; it used to be the bare global object instead,
    /// which is what let <c>GetWindowOrigin</c>'s parent walk terminate by accident.
    /// </summary>
    [Fact]
    public void Window_IsTheGlobalObject()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("true", ValueOf(context, "window === globalThis"));
        Assert.Equal("true", ValueOf(context, "window.self === window"));
        Assert.Equal("true", ValueOf(context, "window.parent === window"));
        Assert.Equal("true", ValueOf(context, "document.defaultView === window"));
    }

    /// <summary>
    /// The reported failure, reduced to the shape google.com actually ships: the namespace is
    /// published through <c>window</c> and consumed unqualified in the same script. No host runs
    /// between the two, so no amount of window→global syncing could have made this work.
    /// </summary>
    [Fact]
    public void GoogleBootstrapShape_WithinOneScript_DoesNotThrow()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var thrown = Record.Exception(() => context.Eval(
            "(function(){var _g={kEI:'trp9ar'};" +
            "(function(){var a;((a=window.google)==null?0:a.stvsc)?google.kEI=_g.kEI:window.google=_g;}).call(this);})();" +
            "(function(){google.sn='webhp';google.kHL='en';google.usb=true;})();"));

        Assert.Null(thrown);
        Assert.Equal("webhp", ValueOf(context, "window.google.sn"));
        Assert.Equal("trp9ar", ValueOf(context, "google.kEI"));
    }

    /// <summary>
    /// The same namespace across two <c>&lt;script&gt;</c> elements — google.com's later scripts
    /// open with a bare <c>google.kEXPI=…</c>. This half a between-scripts sync could reach, but
    /// only if every host remembered to call one; sharing the object removes the requirement.
    /// </summary>
    [Fact]
    public void GoogleBootstrapShape_AcrossScripts_DoesNotThrow()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        context.Eval("window.google = {kEI:'trp9ar'};");
        var thrown = Record.Exception(() => context.Eval("google.kEXPI='0,4254156';google.sn='webhp';"));

        Assert.Null(thrown);
        Assert.Equal("0,4254156", ValueOf(context, "google.kEXPI"));
    }

    /// <summary>Both spellings name one property, in both directions, with no sync in between.</summary>
    [Fact]
    public void WindowAndGlobal_AreOneNamespace()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        context.Eval("window.viaWindow = 1; globalThis.viaGlobal = 2; viaBare = 3;");

        Assert.Equal("1", ValueOf(context, "viaWindow"));
        Assert.Equal("2", ValueOf(context, "window.viaGlobal"));
        Assert.Equal("3", ValueOf(context, "window.viaBare"));

        context.Eval("viaWindow = 11;");
        Assert.Equal("11", ValueOf(context, "window.viaWindow"));
    }

    /// <summary>
    /// A top-level <c>about:blank</c> document can post a message. <c>GetWindowOrigin</c> follows
    /// <c>parent</c> to inherit an origin an <c>about:blank</c> document does not have of its own,
    /// and a top-level window is its own parent — so without a base case the walk never terminates.
    /// It used to terminate only because the top window's <c>parent</c> was the bare global object,
    /// which had no <c>location</c> to recurse on. A regression here is a stack overflow, which
    /// takes the test host down rather than failing this assertion.
    /// </summary>
    [Fact]
    public void TopLevelAboutBlank_CanPostMessage_WithoutRecursingForever()
    {
        var (context, bridge) = Attach("about:blank");
        using var _ = context;
        using var __ = bridge;

        var thrown = Record.Exception(() => context.Eval(
            "window.addEventListener('message', function(e) { window.__got = e.data; });" +
            "window.postMessage('hello', '*');"));

        Assert.Null(thrown);
    }
}
