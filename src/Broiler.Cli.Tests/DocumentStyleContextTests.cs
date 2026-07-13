using Broiler.CSS.Dom;
using Broiler.Dom;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for the P2.3 computed-style authority (<see cref="DocumentStyleContext"/>): direct unit
/// tests of the memo, engine-scope caching and invalidation-batch state, plus characterization that
/// the single invalidation route keeps <c>getComputedStyle</c> correct across a selector-affecting
/// mutation through the bridge.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class DocumentStyleContextTests
{
    private static DomElement NewElement() => new DomDocument().CreateElement("div");

    private static ComputedStyleEngineScope NewScope()
    {
        var engine = new CssStyleEngine();
        return new ComputedStyleEngineScope(new CssStyleScopeBuilder(engine), engine);
    }

    // ------------------------------------------------------------------
    //  GetComputedProps memo
    // ------------------------------------------------------------------

    [Fact]
    public void ComputedProps_Set_Then_TryGet_Returns_Same_Map()
    {
        var ctx = new DocumentStyleContext();
        var el = NewElement();
        var props = new Dictionary<string, string> { ["color"] = "red" };

        ctx.SetComputedProps(el, props);

        Assert.True(ctx.TryGetComputedProps(el, out var got));
        Assert.Same(props, got);
    }

    [Fact]
    public void InvalidateComputedStyle_Clears_The_Memo()
    {
        var ctx = new DocumentStyleContext();
        var el = NewElement();
        ctx.SetComputedProps(el, new Dictionary<string, string>());

        ctx.InvalidateComputedStyle();

        Assert.False(ctx.TryGetComputedProps(el, out _));
    }

    [Fact]
    public void InProgress_Map_Set_Get_Remove()
    {
        var ctx = new DocumentStyleContext();
        var el = NewElement();
        var props = new Dictionary<string, string>();

        Assert.False(ctx.TryGetComputedPropsInProgress(el, out _));
        ctx.SetComputedPropsInProgress(el, props);
        Assert.True(ctx.TryGetComputedPropsInProgress(el, out var got));
        Assert.Same(props, got);
        ctx.RemoveComputedPropsInProgress(el);
        Assert.False(ctx.TryGetComputedPropsInProgress(el, out _));
    }

    // ------------------------------------------------------------------
    //  Engine scopes
    // ------------------------------------------------------------------

    [Fact]
    public void GetOrCreateEngineScope_Creates_Once_And_Caches()
    {
        var ctx = new DocumentStyleContext();
        var root = NewElement();
        var factoryCalls = 0;

        var first = ctx.GetOrCreateEngineScope(root, () => { factoryCalls++; return NewScope(); });
        var second = ctx.GetOrCreateEngineScope(root, () => { factoryCalls++; return NewScope(); });

        Assert.Equal(1, factoryCalls);
        Assert.Same(first, second);
    }

    [Fact]
    public void ResetEngines_Drops_Cached_Scopes()
    {
        var ctx = new DocumentStyleContext();
        var root = NewElement();
        var factoryCalls = 0;

        ctx.GetOrCreateEngineScope(root, () => { factoryCalls++; return NewScope(); });
        ctx.ResetEngines();
        ctx.GetOrCreateEngineScope(root, () => { factoryCalls++; return NewScope(); });

        Assert.Equal(2, factoryCalls); // rebuilt after reset
    }

    // ------------------------------------------------------------------
    //  Invalidation batching
    // ------------------------------------------------------------------

    [Fact]
    public void TryDeferRoot_Defers_Only_While_Batching()
    {
        var ctx = new DocumentStyleContext();
        var root = NewElement();

        Assert.False(ctx.TryDeferRoot(root)); // no batch open → caller invalidates immediately

        ctx.BeginBatch();
        Assert.True(ctx.TryDeferRoot(root)); // batch open → deferred
    }

    [Fact]
    public void EndBatch_Flushes_Only_When_Outermost_Closes()
    {
        var ctx = new DocumentStyleContext();

        Assert.False(ctx.EndBatchShouldFlush()); // none open

        ctx.BeginBatch();
        ctx.BeginBatch();
        Assert.False(ctx.EndBatchShouldFlush()); // inner closes, outer still open
        Assert.True(ctx.EndBatchShouldFlush());  // outer closes → flush
    }

    [Fact]
    public void DrainPendingRoots_Returns_Deferred_Roots_Then_Clears()
    {
        var ctx = new DocumentStyleContext();
        var a = NewElement();
        var b = NewElement();
        ctx.BeginBatch();
        ctx.TryDeferRoot(a);
        ctx.TryDeferRoot(b);
        ctx.TryDeferRoot(a); // duplicate collapses (HashSet semantics)

        var drained = ctx.DrainPendingRoots();

        Assert.Equal(2, drained.Count);
        Assert.Contains(a, drained);
        Assert.Contains(b, drained);
        Assert.Empty(ctx.DrainPendingRoots()); // cleared after draining
    }

    // ------------------------------------------------------------------
    //  Invalidation route through the bridge (end-to-end)
    // ------------------------------------------------------------------

    [Fact]
    public void ClassChange_Invalidates_ComputedStyle_Through_The_Bridge()
    {
        const string html = "<!DOCTYPE html><html><head><style>.h{display:none}</style></head>" +
                            "<body><p id='t'>x</p></body></html>";
        using var ctx = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///style.html");

        // Before the class is added, no `.h` rule matches, so display is not none.
        Assert.NotEqual("none", ctx.Eval(
            "window.getComputedStyle(document.getElementById('t')).display").ToString());

        // A selector-affecting mutation must invalidate the memo + engine caches so the next read
        // reflects the newly-matched rule.
        ctx.Eval("document.getElementById('t').className = 'h';");
        Assert.Equal("none", ctx.Eval(
            "window.getComputedStyle(document.getElementById('t')).display").ToString());
    }
}
