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
        Assert.Equal("true", ValueOf(context, "window.top === window"));
        Assert.Equal("true", ValueOf(context, "document.defaultView === window"));
    }

    /// <summary>
    /// <c>top</c> resolves unqualified. It was the one member of the
    /// <c>self</c>/<c>parent</c>/<c>top</c> trio never registered on the main window — only
    /// sub-documents got one (<c>SubWindowBinding</c>), and <c>WindowContextManager</c> saved
    /// and restored a global that had never been defined in the first place.
    /// <para>
    /// Because <c>window</c> is the global object, an unregistered member is not an undefined
    /// property but a <c>ReferenceError</c>, and that aborts the entire <c>&lt;script&gt;</c>.
    /// A framing check is the first thing a page's boilerplate runs, so the whole bundle went
    /// down with it: google.com's One-Google-bar script raised
    /// <c>ReferenceError: top is not defined</c> and registered none of its listeners.
    /// </para>
    /// </summary>
    [Fact]
    public void Top_ResolvesUnqualified_AndIsTheSameWindow()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("true", ValueOf(context, "top === window"));
        Assert.Equal("true", ValueOf(context, "top === self"));
        Assert.Equal("true", ValueOf(context, "top === parent"));

        // The framing check itself, in the shape pages actually write it: it must run to
        // completion, and a top-level document must not think it is framed.
        var thrown = Record.Exception(() => context.Eval(
            "if (top != self) { window.__framed = true; } window.__checked = true;"));

        Assert.Null(thrown);
        Assert.Equal("true", ValueOf(context, "window.__checked"));
        Assert.Equal("undefined", ValueOf(context, "window.__framed"));
    }

    /// <summary>
    /// <c>top</c> reaches globals the page defined, the same way <c>parent</c> does — the
    /// reason both are bound to the global object rather than to a bare stand-in window.
    /// </summary>
    [Fact]
    public void Top_ReachesPageGlobals()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        context.Eval("window.notify = function() { return 'notified'; };");

        Assert.Equal("notified", ValueOf(context, "top.notify()"));
        Assert.Equal("true", ValueOf(context, "typeof top.document === 'object'"));
    }

    /// <summary>
    /// A framed document's <c>top</c> is the main window, and the main document still has its
    /// own <c>top</c> afterwards. <c>WindowContextManager</c> swaps the global <c>top</c> while a
    /// sub-window's script runs and restores what was there before; the main window had none to
    /// begin with, so the restore put back <c>undefined</c>. From the first frame onwards the
    /// main document's <c>top</c> was therefore wrong in the quieter way — no longer a
    /// <c>ReferenceError</c>, just not the window.
    /// </summary>
    [Fact]
    public void Top_FromInsideAFrame_IsTheMainWindow_AndSurvivesTheContextSwitch()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
<iframe id="frame" srcdoc="<!DOCTYPE html><html><body><script>top.__markedByFrame = 'yes';</script></body></html>"></iframe>
</body></html>
""";

        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        // The frame reached the main window through `top`, so the write landed here.
        Assert.Equal("yes", ValueOf(context, "window.__markedByFrame"));

        // …and the main document's own `top` is intact, not the undefined the restore left.
        Assert.Equal("true", ValueOf(context, "top === window"));
        Assert.Equal("true", ValueOf(context, "document.getElementById('frame').contentWindow.top === window"));
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
