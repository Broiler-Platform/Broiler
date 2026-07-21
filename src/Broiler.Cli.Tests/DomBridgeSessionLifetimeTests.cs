using System.Drawing;
using Broiler.Dom;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Broiler.Layout;

namespace Broiler.Cli.Tests;

/// <summary>
/// Characterization + disposal tests for the bridge's session lifetime (HtmlBridge
/// complexity-reduction roadmap Phase 2, P2.1). Pins the Phase-2 exit criterion that
/// re-attaching leaves no state from the prior document, and verifies that
/// <see cref="DomBridge.Dispose"/> releases per-session resources deterministically and
/// idempotently.
/// </summary>
/// <remarks>
/// The disposal cases override the process-static <see cref="DomBridge.LayoutViewFactory"/> in a
/// try/finally, so this class joins the <c>SharedGeometryStatics</c> collection to run
/// sequentially with the other geometry-static tests.
///
/// The Phase-2 "two *simultaneous* sessions are isolated" exit criterion is now met and exercised by
/// <see cref="Two_Simultaneous_Sessions_Do_Not_See_Each_Others_State"/>: the Broiler.JS engine isolates
/// two live <see cref="JSContext"/> instances (each <c>Eval</c> enters its own realm scope; the current
/// context is <c>[ThreadStatic]</c> + <c>AsyncLocal</c>-scoped, not a last-wins global), and every
/// per-element/per-document runtime table is now per-<see cref="DomBridge"/> instance state (Phase-2
/// de-globalization). An earlier revision of this note claimed simultaneous isolation was blocked at the
/// engine layer; that is no longer true (verified 2026-07-21).
/// </remarks>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class DomBridgeSessionLifetimeTests
{
    private const string BodyHtml = "<!DOCTYPE html><html><head></head><body></body></html>";

    // ------------------------------------------------------------------
    //  Re-attach leaves no state from the prior document (exit criterion b)
    // ------------------------------------------------------------------

    [Fact]
    public void ReAttach_Clears_Prior_Document_Nodes()
    {
        using var bridge = new DomBridge();
        using (var ctx1 = new JSContext())
        {
            bridge.Attach(ctx1, "<!DOCTYPE html><html><body><p id='one'>1</p></body></html>", "file:///1.html");
            Assert.Equal("true", ctx1.Eval("document.getElementById('one') !== null").ToString());
        }

        using var ctx2 = new JSContext();
        bridge.Attach(ctx2, "<!DOCTYPE html><html><body><p id='two'>2</p></body></html>", "file:///2.html");
        Assert.Equal("true", ctx2.Eval("document.getElementById('two') !== null").ToString());
        Assert.Equal("true", ctx2.Eval("document.getElementById('one') === null").ToString());
    }

    [Fact]
    public void ReAttach_Clears_Pending_Timers()
    {
        using var bridge = new DomBridge();
        using (var ctx1 = new JSContext())
        {
            bridge.Attach(ctx1, BodyHtml, "file:///1.html");
            ctx1.Eval("setTimeout(function(){}, 0);");
            Assert.True(bridge.HasPendingTimers);
        }

        using var ctx2 = new JSContext();
        bridge.Attach(ctx2, BodyHtml, "file:///2.html");
        Assert.False(bridge.HasPendingTimers);
        Assert.False(bridge.FlushTimerStep());
    }

    [Fact]
    public void ReAttach_Clears_Window_Listeners()
    {
        using var ctx = new JSContext();
        using var bridge = new DomBridge();

        bridge.Attach(ctx, BodyHtml, "file:///1.html");
        ctx.Eval("var __hits = 0; window.addEventListener('load', function(){ __hits++; });");

        // Re-attach a new document to the same context: the prior document's window listener must
        // not survive into the new session.
        bridge.Attach(ctx, BodyHtml, "file:///2.html");
        bridge.FireWindowLoadEvent();

        Assert.Equal("0", ctx.Eval("__hits").ToString());
    }

    // ------------------------------------------------------------------
    //  Two simultaneous sessions are isolated (exit criterion a)
    // ------------------------------------------------------------------

    [Fact]
    public void Two_Simultaneous_Sessions_Do_Not_See_Each_Others_State()
    {
        using var bridgeA = new DomBridge();
        using var ctxA = new JSContext();
        bridgeA.Attach(ctxA, "<!DOCTYPE html><html><body><p id='a'>A</p></body></html>", "file:///a.html");

        // A second live session created AFTER the first — its JSContext ctor makes it the "last-created"
        // current context — with both remaining attached and live at the same time.
        using var bridgeB = new DomBridge();
        using var ctxB = new JSContext();
        bridgeB.Attach(ctxB, "<!DOCTYPE html><html><body><p id='b'>B</p></body></html>", "file:///b.html");

        // Interleave DOM, global-variable and attribute mutations across the two live sessions.
        ctxA.Eval("globalThis.who = 'A'; document.body.setAttribute('data-mark', 'A');");
        ctxB.Eval("globalThis.who = 'B'; document.body.setAttribute('data-mark', 'B');");

        // Each session sees only its own document nodes …
        Assert.Equal("true", ctxA.Eval("document.getElementById('a') !== null && document.getElementById('b') === null").ToString());
        Assert.Equal("true", ctxB.Eval("document.getElementById('b') !== null && document.getElementById('a') === null").ToString());

        // … its own globals …
        Assert.Equal("A", ctxA.Eval("''+globalThis.who").ToString());
        Assert.Equal("B", ctxB.Eval("''+globalThis.who").ToString());

        // … and its own DOM mutations.
        Assert.Equal("A", ctxA.Eval("document.body.getAttribute('data-mark')").ToString());
        Assert.Equal("B", ctxB.Eval("document.body.getAttribute('data-mark')").ToString());
    }

    // ------------------------------------------------------------------
    //  Deterministic disposal
    // ------------------------------------------------------------------

    [Fact]
    public void Dispose_Releases_The_Layout_View()
    {
        var recording = new RecordingLayoutView();
        var saved = DomBridge.LayoutViewFactory;
        try
        {
            DomBridge.LayoutViewFactory = () => recording;
            using var ctx = new JSContext();
            var bridge = new DomBridge();
            bridge.Attach(ctx, BodyHtml, "file:///d.html");

            // Force a geometry query so the bridge materializes its ILayoutView from the factory.
            ctx.Eval("document.body.getBoundingClientRect();");
            Assert.True(recording.GetGeometryCount >= 1, "geometry read did not materialize the layout view");
            Assert.Equal(0, recording.DisposeCount);

            bridge.Dispose();
            Assert.Equal(1, recording.DisposeCount);
        }
        finally
        {
            DomBridge.LayoutViewFactory = saved;
        }
    }

    [Fact]
    public void Dispose_Is_Idempotent()
    {
        var recording = new RecordingLayoutView();
        var saved = DomBridge.LayoutViewFactory;
        try
        {
            DomBridge.LayoutViewFactory = () => recording;
            using var ctx = new JSContext();
            var bridge = new DomBridge();
            bridge.Attach(ctx, BodyHtml, "file:///d.html");
            ctx.Eval("document.body.getBoundingClientRect();");

            bridge.Dispose();
            bridge.Dispose();
            Assert.Equal(1, recording.DisposeCount);
        }
        finally
        {
            DomBridge.LayoutViewFactory = saved;
        }
    }

    [Fact]
    public void Dispose_Drops_Pending_Timers_Without_Running_Them()
    {
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, BodyHtml, "file:///d.html");
        // Use a global `var` (this engine's global object and `window` are distinct, so a bare
        // read sees the global binding, not a `window.` property).
        ctx.Eval("var __ran = false; setTimeout(function(){ __ran = true; }, 0);");
        Assert.True(bridge.HasPendingTimers);

        bridge.Dispose();

        // The queued callback was dropped, not executed.
        Assert.Equal("false", ctx.Eval("__ran").ToString());
    }

    [Fact]
    public void Dispose_Does_Not_Dispose_The_Borrowed_JsContext()
    {
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, BodyHtml, "file:///d.html");

        bridge.Dispose();

        // The bridge only drops its reference to the context; the caller's context stays usable.
        Assert.Equal("3", ctx.Eval("1 + 2").ToString());
    }

    // ------------------------------------------------------------------
    //  Use-after-dispose fails fast
    // ------------------------------------------------------------------

    [Fact]
    public void Attach_After_Dispose_Throws()
    {
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, BodyHtml, "file:///d.html");
        bridge.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bridge.Attach(ctx, BodyHtml, "file:///d2.html"));
    }

    [Fact]
    public void Timer_Entry_Points_After_Dispose_Throw()
    {
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, BodyHtml, "file:///d.html");
        bridge.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = bridge.HasPendingTimers);
        Assert.Throws<ObjectDisposedException>(() => bridge.FlushTimerStep());
        Assert.Throws<ObjectDisposedException>(() => bridge.FlushTimers());
        Assert.Throws<ObjectDisposedException>(() => bridge.FireWindowLoadEvent());
    }

    // ------------------------------------------------------------------
    //  Architecture guards
    // ------------------------------------------------------------------

    [Fact]
    public void DomBridge_Is_Disposable() =>
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(DomBridge)));

    [Fact]
    public void DomBridgeRuntime_Interface_Is_Not_Disposable() =>
        // Source-compatibility guard: IDomBridgeRuntime consumers must not be forced to dispose
        // the bridge (Phase 2 P2.1 keeps IDisposable on the concrete type only).
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(Broiler.HtmlBridge.Dom.IDomBridgeRuntime)));

    /// <summary>Test-local <see cref="ILayoutView"/> that records geometry queries and disposals.</summary>
    private sealed class RecordingLayoutView : ILayoutView
    {
        private static readonly IReadOnlyDictionary<DomElement, BoxGeometry> Empty =
            new Dictionary<DomElement, BoxGeometry>();

        public int GetGeometryCount { get; private set; }
        public int DisposeCount { get; private set; }

        public IReadOnlyDictionary<DomElement, BoxGeometry> GetGeometry(
            DomDocument document, SizeF viewport, string baseUrl,
            Func<DomElement, DomDocument?>? contentDocumentResolver = null)
        {
            GetGeometryCount++;
            return Empty;
        }

        public void Dispose() => DisposeCount++;
    }
}
