using System;
using System.Collections.Generic;
using Broiler.Dom;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.HtmlBridge.Scripting;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 8 item 2: every <see cref="InteractiveSession"/> owns a private event-loop/context lifetime.
/// Disposing the session must tear down its bridge (the browser event loop — timers, listeners,
/// observers, layout view), and a <see cref="ScriptEngine.ExecuteInteractive(System.Collections.Generic.IReadOnlyList{string}, System.Collections.Generic.IReadOnlyList{string}, string, string?)"/>
/// whose setup throws must not leak the bridge/context it had already created.
/// </summary>
public class InteractiveSessionLifetimeTests
{
    /// <summary>A minimal bridge runtime that records disposal and can fail on attach.</summary>
    private sealed class FakeBridge : IDomBridgeRuntime, IDisposable
    {
        public bool Disposed { get; private set; }
        public bool ThrowOnAttach { get; set; }

        public ContentSecurityPolicy? Csp { get; set; }
        public Action? TaskCheckpointCallback { get; set; }
        public IReadOnlyList<DomElement> Elements { get; } = Array.Empty<DomElement>();
        public int CurrentScriptIndex { get; set; }
        public bool HasPendingTimers => false;

        public void Attach(JSContext context, string html)
        {
            if (ThrowOnAttach) throw new InvalidOperationException("attach failed");
        }

        public void Attach(JSContext context, string html, string url)
        {
            if (ThrowOnAttach) throw new InvalidOperationException("attach failed");
        }

        public void FireWindowLoadEvent() { }
        public bool FlushTimerStep() => false;
        public void FlushTimers() { }
        public string SerializeToHtml() => "<html></html>";
        public DomDocument GetRenderDocument() => throw new NotSupportedException("not exercised");

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeFactory : IDomBridgeRuntimeFactory
    {
        public FakeBridge Bridge { get; } = new();
        public IDomBridgeRuntime Create() => Bridge;
    }

    private static readonly string[] OneScript = { "1 + 1;" };

    [Fact]
    public void DisposingSession_DisposesBridge()
    {
        var factory = new FakeFactory();
        var engine = new ScriptEngine(factory);

        var session = engine.ExecuteInteractive(OneScript, Array.Empty<string>(), "<html></html>", "file:///t.html");
        Assert.NotNull(session);
        Assert.False(factory.Bridge.Disposed, "bridge must stay live while the session is in use.");

        session!.Dispose();
        Assert.True(factory.Bridge.Disposed, "disposing the session must dispose its bridge (the event loop).");

        // Idempotent: a second dispose is a no-op and does not throw.
        session.Dispose();
    }

    [Fact]
    public void FailedConstruction_DisposesBridgeAndContext()
    {
        var factory = new FakeFactory();
        factory.Bridge.ThrowOnAttach = true;
        var engine = new ScriptEngine(factory);

        Assert.Throws<InvalidOperationException>(() =>
            engine.ExecuteInteractive(OneScript, Array.Empty<string>(), "<html></html>", "file:///t.html"));

        Assert.True(factory.Bridge.Disposed,
            "a failed ExecuteInteractive must dispose the bridge it created instead of leaking it.");
    }
}
