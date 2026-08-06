using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>DomBridge.SyncWindowMembersOntoGlobal</c>: the window → global mirror re-run over whatever
/// the scripts that have run since last time added to <c>window</c>.
/// <para>
/// In a browser <c>window</c> <em>is</em> the global object, so a library's
/// <c>window.foo = …</c> makes the unqualified <c>foo</c> work for free. The bridge keeps the two
/// apart, so that unqualified reference is a <c>ReferenceError</c> — which aborts the whole
/// <c>&lt;script&gt;</c> that made it, not just the one statement. That is the shape of every WPT
/// support library, <c>/css/support/interpolation-testcommon.js</c> most of all: it exports
/// <c>test_interpolation</c> and five siblings, and each of the ~100 <c>*-interpolation</c> tests
/// calls them unqualified from its own inline script (issue #1552 problems 4, 18 and 22).
/// </para>
/// </summary>
public sealed class WindowGlobalSyncTests
{
    /// <summary>Attaches a bridge to a bare document and hands back both halves.</summary>
    private static (JSContext Context, DomBridge Bridge) Attach()
    {
        var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!doctype html><html><body></body></html>", "file:///window-global-sync.html");
        return (context, bridge);
    }

    private static string TypeOf(JSContext context, string expression) =>
        context.Eval($"String(typeof ({expression}))").ToString();

    private static string ValueOf(JSContext context, string expression) =>
        context.Eval($"String({expression})").ToString();

    /// <summary>
    /// The bug itself: a member a script adds to <c>window</c> is not reachable unqualified until
    /// the mirror is re-run. Both halves are asserted, so a change that made the two objects one
    /// (the real fix, were the bridge ever to make <c>window</c> the global object) fails the
    /// first assertion loudly rather than silently making the test vacuous.
    /// </summary>
    [Fact]
    public void RuntimeWindowMember_IsUnreachableUnqualified_UntilSynced()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        context.Eval("window.test_interpolation = function() { return 'called'; };");
        Assert.Equal("undefined", TypeOf(context, "typeof test_interpolation === 'undefined' ? undefined : test_interpolation"));

        bridge.SyncWindowMembersOntoGlobal();

        Assert.Equal("function", TypeOf(context, "test_interpolation"));
        Assert.Equal("called", ValueOf(context, "test_interpolation()"));
    }

    /// <summary>
    /// The whole export surface of a support library arrives, not a hand-picked few — the previous
    /// mechanism promoted a fixed list of 13 testharness names and left every other library behind.
    /// These six are exactly what <c>interpolation-testcommon.js</c> closes by assigning.
    /// </summary>
    [Fact]
    public void EveryExportOfASupportLibrary_BecomesReachable()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        context.Eval(@"
(function() {
  window.test_interpolation = function() {};
  window.test_no_interpolation = function() {};
  window.test_not_animatable = function() {};
  window.test_composition = function() {};
  window.neutralKeyframe = {};
  window.roundNumbers = function() {};
})();");

        bridge.SyncWindowMembersOntoGlobal();

        Assert.Equal("function", TypeOf(context, "test_interpolation"));
        Assert.Equal("function", TypeOf(context, "test_no_interpolation"));
        Assert.Equal("function", TypeOf(context, "test_not_animatable"));
        Assert.Equal("function", TypeOf(context, "test_composition"));
        Assert.Equal("object", TypeOf(context, "neutralKeyframe"));
        Assert.Equal("function", TypeOf(context, "roundNumbers"));
    }

    /// <summary>
    /// A value member is mirrored by identity, not by copy, so the two spellings name the same
    /// object — an object registered through <c>window.x</c> is mutable through the unqualified
    /// <c>x</c> and vice versa.
    /// </summary>
    [Fact]
    public void MirroredMember_IsTheSameObject_NotACopy()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        context.Eval("window.sharedState = { count: 0 };");
        bridge.SyncWindowMembersOntoGlobal();

        Assert.Equal("true", ValueOf(context, "sharedState === window.sharedState"));

        context.Eval("sharedState.count = 7;");
        Assert.Equal("7", ValueOf(context, "window.sharedState.count"));
    }

    /// <summary>
    /// An accessor member stays a live getter rather than freezing to the value it happened to
    /// have when the mirror ran — the property that lets <c>innerWidth</c> and friends work
    /// unqualified after a resize.
    /// </summary>
    [Fact]
    public void AccessorMember_StaysLive()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        context.Eval(@"
window.__tick = 1;
Object.defineProperty(window, 'liveTick', { configurable: true, get: function() { return window.__tick; } });");
        bridge.SyncWindowMembersOntoGlobal();

        Assert.Equal("1", ValueOf(context, "liveTick"));
        context.Eval("window.__tick = 42;");
        Assert.Equal("42", ValueOf(context, "liveTick"));
    }

    /// <summary>
    /// A name the global already has wins: the sweep fills gaps and never overwrites, so an engine
    /// builtin cannot be shadowed by a same-named window member.
    /// </summary>
    [Fact]
    public void ExistingGlobal_IsNotOverwritten()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        context.Eval("window.JSON = 'not the real JSON';");
        bridge.SyncWindowMembersOntoGlobal();

        Assert.Equal("function", TypeOf(context, "JSON.stringify"));
    }

    /// <summary>
    /// Safe to call after every script: repeating it neither throws nor disturbs what it already
    /// mirrored, and it still picks up what the next script adds.
    /// </summary>
    [Fact]
    public void RepeatedSync_IsIdempotentAndPicksUpLaterMembers()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        context.Eval("window.first = 'one';");
        bridge.SyncWindowMembersOntoGlobal();
        bridge.SyncWindowMembersOntoGlobal();
        Assert.Equal("one", ValueOf(context, "first"));

        context.Eval("window.second = 'two';");
        bridge.SyncWindowMembersOntoGlobal();
        Assert.Equal("one", ValueOf(context, "first"));
        Assert.Equal("two", ValueOf(context, "second"));
    }

    /// <summary>
    /// The scratch binding the sweep uses to hand <c>window</c> to its JS half is cleaned up, so
    /// repeated syncing does not leave engine plumbing enumerable on the global for a page (or a
    /// <c>for…in</c> over <c>globalThis</c>) to trip over.
    /// </summary>
    [Fact]
    public void Sync_LeavesNoScratchBindingBehind()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        bridge.SyncWindowMembersOntoGlobal();

        Assert.Equal("false", ValueOf(context, "'__broilerWindowForGlobalMirror' in globalThis"));
    }
}
