using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>AbortSignal</c> (DOM §3.2) — the interface itself, not just the controller that hands one out.
/// <para>
/// The signal used to be an object literal built inside <c>AbortController</c>, with no
/// <c>AbortSignal</c> constructor anywhere. Everything reached *through the controller* worked, so
/// the gap was invisible from the controller's side — but the name did not exist, and a script that
/// merely mentions <c>AbortSignal</c> gets a <c>ReferenceError</c>, which aborts the whole script
/// rather than the one line. google.com's main bundle does exactly that, and it is where the bundle
/// stopped once the parser fix let it run at all.
/// </para>
/// </summary>
public sealed class AbortSignalTests
{
    private static (JSContext Context, DomBridge Bridge) Attach()
    {
        var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!doctype html><html><body></body></html>", "https://example.com/");
        return (context, bridge);
    }

    private static string Eval(JSContext context, string expression) =>
        context.Eval($"String({expression})").ToString();

    /// <summary>The reported failure: the name has to resolve at all.</summary>
    [Fact(Timeout = 600000)]
    public void TheInterfaceExists()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("function", Eval(context, "typeof AbortSignal"));
        Assert.Equal("function", Eval(context, "typeof AbortController"));
    }

    /// <summary>
    /// A controller's signal is a real <c>AbortSignal</c>, and an <c>EventTarget</c> — the checks a
    /// feature-detecting script makes, and the ones an object literal could never pass.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AControllersSignal_IsAnAbortSignal()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("true", Eval(context, "new AbortController().signal instanceof AbortSignal"));
        Assert.Equal("true", Eval(context, "new AbortController().signal instanceof EventTarget"));
    }

    /// <summary>The interface has no public constructor: a signal only ever comes from elsewhere.</summary>
    [Fact(Timeout = 600000)]
    public void ItCannotBeConstructedDirectly()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("TypeError", Eval(context,
            "(function(){ try { new AbortSignal(); return 'no throw'; } catch (e) { return e.constructor.name; } })()"));
    }

    /// <summary>Aborting sets the state the host reads off the signal, and the default reason is an AbortError.</summary>
    [Fact(Timeout = 600000)]
    public void AbortingSetsAbortedAndReason()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("false", Eval(context, "new AbortController().signal.aborted"));
        Assert.Equal("true", Eval(context, "(function(){ var c = new AbortController(); c.abort(); return c.signal.aborted; })()"));
        Assert.Equal("AbortError", Eval(context, "(function(){ var c = new AbortController(); c.abort(); return c.signal.reason.name; })()"));
        Assert.Equal("why", Eval(context, "(function(){ var c = new AbortController(); c.abort('why'); return c.signal.reason; })()"));
    }

    /// <summary>A listener fires once however many times abort is called — aborting twice is a no-op.</summary>
    [Fact(Timeout = 600000)]
    public void AbortFiresListenersExactlyOnce()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("1", Eval(context,
            "(function(){ var c = new AbortController(), n = 0; c.signal.addEventListener('abort', function(){ n++; }); c.abort(); c.abort(); return n; })()"));
        Assert.Equal("0", Eval(context,
            "(function(){ var c = new AbortController(), n = 0; var f = function(){ n++; }; c.signal.addEventListener('abort', f); c.signal.removeEventListener('abort', f); c.abort(); return n; })()"));
    }

    /// <summary><c>throwIfAborted</c> is silent before the abort and throws the reason after it.</summary>
    [Fact(Timeout = 600000)]
    public void ThrowIfAbortedThrowsOnlyAfterAborting()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("AbortError", Eval(context,
            "(function(){ var c = new AbortController();" +
            " try { c.signal.throwIfAborted(); } catch (e) { return 'threw early'; }" +
            " c.abort(); try { c.signal.throwIfAborted(); return 'no throw'; } catch (e) { return e.name; } })()"));
    }

    /// <summary><c>AbortSignal.abort()</c> — a signal that is already aborted when handed out.</summary>
    [Fact(Timeout = 600000)]
    public void StaticAbort_ReturnsAnAlreadyAbortedSignal()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("true", Eval(context, "AbortSignal.abort().aborted"));
        Assert.Equal("AbortError", Eval(context, "AbortSignal.abort().reason.name"));
        Assert.Equal("custom", Eval(context, "AbortSignal.abort('custom').reason"));
        Assert.Equal("true", Eval(context, "AbortSignal.abort() instanceof AbortSignal"));
    }

    /// <summary>
    /// <c>AbortSignal.any</c> is already aborted when one of its inputs is, so composing signals
    /// cannot miss an abort that happened first.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void StaticAny_FollowsAnAlreadyAbortedInput()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("true", Eval(context, "AbortSignal.any([AbortSignal.abort('early')]).aborted"));
        Assert.Equal("early", Eval(context, "AbortSignal.any([AbortSignal.abort('early')]).reason"));
        Assert.Equal("false", Eval(context, "AbortSignal.any([new AbortController().signal]).aborted"));
    }

    /// <summary>And it follows one that aborts later, taking that signal's reason.</summary>
    [Fact(Timeout = 600000)]
    public void StaticAny_FollowsALaterAbort()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("late", Eval(context,
            "(function(){ var c = new AbortController(); var any = AbortSignal.any([c.signal]); c.abort('late'); return any.reason; })()"));
    }

    /// <summary>
    /// <c>AbortSignal.timeout</c> exists and its abort is a <c>TimeoutError</c>, deliberately not an
    /// <c>AbortError</c> — code that tells "cancelled" from "took too long" reads <c>reason.name</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void StaticTimeout_ExistsAndIsNotAbortedYet()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("function", Eval(context, "typeof AbortSignal.timeout"));
        Assert.Equal("false", Eval(context, "AbortSignal.timeout(50000).aborted"));
        Assert.Equal("true", Eval(context, "AbortSignal.timeout(50000) instanceof AbortSignal"));
    }
}
