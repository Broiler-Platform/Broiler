using Broiler.Dom;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for the P2.2 JS wrapper-identity authority (<see cref="JsObjectRegistry"/>): direct unit
/// tests of the registry surface, plus characterization that a DOM node keeps one stable JS wrapper
/// across the bridge and that re-parse/dispose drops wrapper identity.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class JsObjectRegistryTests
{
    private const string BodyHtml = "<!DOCTYPE html><html><head></head><body></body></html>";

    // ------------------------------------------------------------------
    //  Registry unit tests
    // ------------------------------------------------------------------

    [Fact]
    public void Set_Then_TryGet_Returns_Same_Wrapper()
    {
        var doc = new DomDocument();
        var node = doc.CreateElement("div");
        var wrapper = new JSObject();
        var registry = new JsObjectRegistry();

        registry.Set(node, wrapper);

        Assert.True(registry.TryGet(node, out var got));
        Assert.Same(wrapper, got);
    }

    [Fact]
    public void Wrappers_Are_Keyed_By_Reference_Not_Value()
    {
        var doc = new DomDocument();
        var a = doc.CreateElement("div");
        var b = doc.CreateElement("div"); // structurally identical, distinct node
        var registry = new JsObjectRegistry();
        var wrapperA = new JSObject();

        registry.Set(a, wrapperA);

        Assert.True(registry.TryGet(a, out _));
        Assert.False(registry.TryGet(b, out _));
    }

    [Fact]
    public void Remove_Drops_The_Wrapper()
    {
        var doc = new DomDocument();
        var node = doc.CreateElement("div");
        var registry = new JsObjectRegistry();
        registry.Set(node, new JSObject());

        Assert.True(registry.Remove(node));
        Assert.False(registry.TryGet(node, out _));
        Assert.False(registry.Remove(node)); // idempotent
    }

    [Fact]
    public void TryGetNode_Reverse_Lookup_By_Wrapper_Identity()
    {
        var doc = new DomDocument();
        var node = doc.CreateElement("div");
        var wrapper = new JSObject();
        var registry = new JsObjectRegistry();
        registry.Set(node, wrapper);

        Assert.True(registry.TryGetNode(wrapper, out var found));
        Assert.Same(node, found);
        Assert.False(registry.TryGetNode(new JSObject(), out _));
    }

    [Fact]
    public void Document_Wrappers_Are_A_Separate_Map()
    {
        var doc = new DomDocument();
        var root = doc.CreateElement("div");
        var documentWrapper = new JSObject();
        var registry = new JsObjectRegistry();

        registry.SetDocument(root, documentWrapper);

        Assert.True(registry.TryGetDocument(root, out var got));
        Assert.Same(documentWrapper, got);
        // The document map is independent of the node map.
        Assert.False(registry.TryGet(root, out _));
    }

    [Fact]
    public void Clear_Drops_Node_And_Document_Wrappers()
    {
        var doc = new DomDocument();
        var node = doc.CreateElement("div");
        var root = doc.CreateElement("div");
        var registry = new JsObjectRegistry();
        registry.Set(node, new JSObject());
        registry.SetDocument(root, new JSObject());

        registry.Clear();

        Assert.False(registry.TryGet(node, out _));
        Assert.False(registry.TryGetDocument(root, out _));
    }

    // ------------------------------------------------------------------
    //  Wrapper identity through the bridge
    // ------------------------------------------------------------------

    [Fact]
    public void Same_Node_Yields_Stable_Wrapper_Identity()
    {
        using var ctx = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(ctx, BodyHtml, "file:///id.html");

        Assert.Equal("true", ctx.Eval("document.body === document.body").ToString());
    }

    [Fact]
    public void Node_Reached_By_Different_Paths_Is_The_Same_Wrapper()
    {
        using var ctx = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(ctx, BodyHtml, "file:///id.html");

        // The wrapper handed back for a freshly appended node equals the one reached via the tree.
        Assert.Equal("true", ctx.Eval(
            "(function(){ var d = document.createElement('div'); document.body.appendChild(d);" +
            " return d === document.body.lastChild; })()").ToString());
    }

    [Fact]
    public void ReAttach_Resets_Wrapper_Identity()
    {
        using var bridge = new DomBridge();
        using (var ctx1 = new JSContext())
        {
            bridge.Attach(ctx1, BodyHtml, "file:///1.html");
            ctx1.Eval("document.body.__tag = 'first';");
            Assert.Equal("first", ctx1.Eval("document.body.__tag").ToString());
        }

        using var ctx2 = new JSContext();
        bridge.Attach(ctx2, BodyHtml, "file:///2.html");
        // A fresh document mints fresh wrappers — the ad-hoc property from the prior document's
        // body wrapper is gone.
        Assert.Equal("undefined", ctx2.Eval("typeof document.body.__tag").ToString());
    }
}
