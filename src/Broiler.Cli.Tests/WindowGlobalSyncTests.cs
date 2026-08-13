using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// A member a script adds to <c>window</c> is reachable unqualified — the behaviour
/// <c>DomBridge.SyncWindowMembersOntoGlobal</c> used to have to manufacture.
/// <para>
/// In a browser <c>window</c> <em>is</em> the global object, so a library's
/// <c>window.foo = …</c> makes the unqualified <c>foo</c> work for free. The bridge used to keep the
/// two apart, which made that reference a <c>ReferenceError</c> — and that aborts the whole
/// <c>&lt;script&gt;</c> which made it, not just the one statement. That is the shape of every WPT
/// support library, <c>/css/support/interpolation-testcommon.js</c> most of all: it exports
/// <c>test_interpolation</c> and five siblings, and each of the ~100 <c>*-interpolation</c> tests
/// calls them unqualified from its own inline script (issue #1552 problems 4, 18 and 22). The sync
/// closed that gap after the fact, once per script, and every host had to remember to call it.
/// </para>
/// <para>
/// The bridge now makes <c>window</c> the global object outright
/// (<see cref="WindowIsTheGlobalObjectTests"/>), so these hold with no sync at all — asserted here
/// <em>before</em> the sync runs, because that is the property the pages depend on. The sync itself
/// stays exercised: it is public API a host may still call, and it must remain harmless.
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
    /// The bug this file was written for: a member a script adds to <c>window</c> used to be
    /// unreachable unqualified until the mirror was re-run. It is reachable immediately now, so the
    /// assertion is made before the sync — the sync is then run to prove it changes nothing.
    /// </summary>
    [Fact]
    public void RuntimeWindowMember_IsReachableUnqualified_WithoutSyncing()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        context.Eval("window.test_interpolation = function() { return 'called'; };");
        Assert.Equal("function", TypeOf(context, "test_interpolation"));
        Assert.Equal("called", ValueOf(context, "test_interpolation()"));

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
    /// Publishing the DOM surface leaves the engine's own builtins intact — <c>window.document</c>,
    /// <c>window.location</c> and the rest land on the global object itself now, so a name collision
    /// would overwrite a builtin outright rather than being skipped by a gap-filling sweep.
    /// <para>
    /// A <em>page</em> that assigns <c>window.JSON = …</c> does replace the global <c>JSON</c>, and
    /// that is correct: those are writable, configurable properties of the global object, and a
    /// browser lets a page clobber them exactly this way. The guarantee is about registration, not
    /// about defending the realm from the page.
    /// </para>
    /// </summary>
    [Fact]
    public void EngineBuiltins_SurviveRegistration()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        bridge.SyncWindowMembersOntoGlobal();

        Assert.Equal("function", TypeOf(context, "JSON.stringify"));
        Assert.Equal("function", TypeOf(context, "Object"));
        Assert.Equal("function", TypeOf(context, "Array"));
        Assert.Equal("function", TypeOf(context, "Promise"));
        Assert.Equal("object", TypeOf(context, "Math"));
        Assert.Equal("function", TypeOf(context, "setTimeout"));
        Assert.Equal("object", TypeOf(context, "document"));
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
