using Broiler.Dom;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for the P2.5 listener authority (<see cref="EventTargetRegistry"/>): direct unit tests of
/// the node/window/generic-target listener stores, owner-window map and visual-viewport scroll
/// listeners, plus characterization that element and window listeners still fire through the bridge.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class EventTargetRegistryTests
{
    private static DomElement NewElement() => new DomDocument().CreateElement("div");
    private static JSFunction NoopFn() => new((in Arguments _) => JSUndefined.Value, "cb", 0);

    // ------------------------------------------------------------------
    //  Node listeners
    // ------------------------------------------------------------------

    [Fact]
    public void NodeListeners_Returns_A_Stable_Store_Per_Node()
    {
        var reg = new EventTargetRegistry();
        var a = NewElement();
        var b = NewElement();

        Assert.Same(reg.NodeListeners(a), reg.NodeListeners(a)); // same node → same store
        Assert.NotSame(reg.NodeListeners(a), reg.NodeListeners(b)); // distinct nodes → distinct stores
    }

    [Fact]
    public void NodeListeners_Are_Keyed_Case_Insensitively_By_Type()
    {
        var reg = new EventTargetRegistry();
        var node = NewElement();
        reg.NodeListeners(node)["Click"] = [];

        Assert.True(reg.NodeListeners(node).ContainsKey("click"));
    }

    // ------------------------------------------------------------------
    //  Window listeners
    // ------------------------------------------------------------------

    [Fact]
    public void WindowListeners_Create_On_Add_And_Are_Found_Case_Insensitively()
    {
        var reg = new EventTargetRegistry();

        Assert.False(reg.TryGetWindowListeners("load", out _));
        var list = reg.WindowListenersForAdd("load");
        list.Add(default);

        Assert.True(reg.TryGetWindowListeners("LOAD", out var found));
        Assert.Same(list, found);
    }

    // ------------------------------------------------------------------
    //  Generic-target listeners & owner windows
    // ------------------------------------------------------------------

    [Fact]
    public void TargetListeners_Are_Reference_Keyed()
    {
        var reg = new EventTargetRegistry();
        var target = new JSObject();
        var other = new JSObject();
        reg.TargetListenersForAdd(target)["message"] = [];

        Assert.True(reg.TryGetTargetListeners(target, out var byType));
        Assert.True(byType.ContainsKey("message"));
        Assert.False(reg.TryGetTargetListeners(other, out _));
    }

    [Fact]
    public void OwnerWindow_Set_And_Get()
    {
        var reg = new EventTargetRegistry();
        var target = new JSObject();
        var window = new JSObject();

        Assert.False(reg.TryGetOwnerWindow(target, out _));
        reg.SetOwnerWindow(target, window);
        Assert.True(reg.TryGetOwnerWindow(target, out var got));
        Assert.Same(window, got);
    }

    // ------------------------------------------------------------------
    //  Visual-viewport scroll listeners
    // ------------------------------------------------------------------

    [Fact]
    public void VisualViewportScrollListeners_Add_Is_Deduped_And_Removable()
    {
        var reg = new EventTargetRegistry();
        var fn = NoopFn();

        reg.AddVisualViewportScrollListener(fn);
        reg.AddVisualViewportScrollListener(fn); // duplicate ignored
        Assert.Single(reg.VisualViewportScrollListeners);

        reg.RemoveVisualViewportScrollListener(fn);
        Assert.Empty(reg.VisualViewportScrollListeners);
    }

    // ------------------------------------------------------------------
    //  Clear
    // ------------------------------------------------------------------

    [Fact]
    public void Clear_Drops_Every_Store()
    {
        var reg = new EventTargetRegistry();
        var node = NewElement();
        var target = new JSObject();
        reg.NodeListeners(node)["click"] = [];
        reg.WindowListenersForAdd("load").Add(default);
        reg.TargetListenersForAdd(target)["message"] = [];
        reg.SetOwnerWindow(target, new JSObject());
        reg.AddVisualViewportScrollListener(NoopFn());

        reg.Clear();

        Assert.Empty(reg.NodeListeners(node)); // fresh store after clear
        Assert.False(reg.TryGetWindowListeners("load", out _));
        Assert.False(reg.TryGetTargetListeners(target, out _));
        Assert.False(reg.TryGetOwnerWindow(target, out _));
        Assert.Empty(reg.VisualViewportScrollListeners);
    }

    // ------------------------------------------------------------------
    //  Characterization through the bridge
    // ------------------------------------------------------------------

    [Fact]
    public void Element_Listener_Fires_On_Dispatch()
    {
        using var ctx = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(ctx, "<!DOCTYPE html><html><body><div id='t'></div></body></html>", "file:///e.html");

        var result = ctx.Eval(@"
            (function(){
              var hits = 0;
              var t = document.getElementById('t');
              t.addEventListener('ping', function(){ hits++; });
              var e = document.createEvent('Event'); e.initEvent('ping', true, true);
              t.dispatchEvent(e);
              return hits;
            })()").ToString();

        Assert.Equal("1", result);
    }

    [Fact]
    public void Window_Listener_Fires_On_Dispatch()
    {
        using var ctx = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(ctx, "<!DOCTYPE html><html><body></body></html>", "file:///w.html");

        var result = ctx.Eval(@"
            (function(){
              var hits = 0;
              window.addEventListener('ping', function(){ hits++; });
              var e = document.createEvent('Event'); e.initEvent('ping', false, false);
              window.dispatchEvent(e);
              return hits;
            })()").ToString();

        Assert.Equal("1", result);
    }
}
