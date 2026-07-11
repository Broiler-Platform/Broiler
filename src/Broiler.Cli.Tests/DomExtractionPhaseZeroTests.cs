using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Characterization tests for the extraction end state. The RF-BRIDGE-1c effort
/// removed the <c>DomElement</c> compatibility facade and the <c>HtmlTreeBuilder</c>
/// materializer at the <c>htmlbridge-public-surface/v2</c> boundary (Phase F4); these
/// checks assert the facade types are gone and the bridge holds only canonical
/// <c>Broiler.Dom</c> nodes.
/// </summary>
public sealed class DomExtractionPhaseZeroTests
{
    [Fact]
    public void Facade_DomElement_And_HtmlTreeBuilder_Types_Are_Removed_At_V2()
    {
        // RF-BRIDGE-1c Phase F4 (final cutover): the Core-owned compatibility facade
        // Broiler.HtmlBridge.DomElement and the Dom-owned materializer
        // Broiler.HtmlBridge.HtmlTreeBuilder no longer exist. The bridge builds its
        // whole tree from canonical Broiler.Dom nodes via the shared HtmlDocumentParser.
        var coreAssembly = typeof(Broiler.HtmlBridge.Dom.IDomBridgeRuntime).Assembly;
        var domAssembly = typeof(DomBridge).Assembly;

        Assert.Null(coreAssembly.GetType("Broiler.HtmlBridge.DomElement"));
        Assert.Null(domAssembly.GetType("Broiler.HtmlBridge.DomElement"));
        Assert.Null(domAssembly.GetType("Broiler.HtmlBridge.HtmlTreeBuilder"));
    }

    [Fact]
    public void DomBridge_Exposes_Canonical_Dom_Elements_Only()
    {
        // The public element seams surface canonical Broiler.Dom.DomElement — no facade
        // subtype leaks through.
        Assert.Equal(
            typeof(Broiler.Dom.DomElement),
            typeof(DomBridge).GetProperty(nameof(DomBridge.DocumentElement))!.PropertyType);

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<main id='host'><span id='child'></span></main>");

        Assert.IsType<Broiler.Dom.DomElement>(bridge.DocumentElement);
        Assert.All(bridge.Elements, element => Assert.IsType<Broiler.Dom.DomElement>(element));
    }

    [Fact]
    public void DomBridge_Parses_Implicit_Structure_With_Consistent_Parent_Links()
    {
        // Replaces the retired HtmlTreeBuilder characterization test: the bridge parses
        // via the shared HtmlDocumentParser and exposes the same implicit html/head/body
        // structure through its public canonical seams.
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<title>Phase Zero</title><main id='host'><span>one</span><span>two</span></main>");

        var documentElement = bridge.DocumentElement;
        Assert.Equal("html", documentElement.TagName);
        Assert.Equal("Phase Zero", bridge.Title);

        var body = Assert.Single(
            documentElement.ChildNodes.OfType<Broiler.Dom.DomElement>(),
            static child => child.TagName == "body");
        Assert.Same(documentElement, body.ParentNode);

        var host = Assert.Single(bridge.Elements, static element => element.Id == "host");
        Assert.Equal(["span", "span"], host.ChildNodes.OfType<Broiler.Dom.DomElement>().Select(static child => child.TagName));
        Assert.All(host.ChildNodes.OfType<Broiler.Dom.DomElement>(), child => Assert.Same(host, child.ParentNode));
    }

    [Fact]
    public void DomBridge_Owns_The_Canonical_Document_And_Exposes_Tree_Derived_Elements()
    {
        var bridge = new DomBridge();
        using var context = new JSContext();
        bridge.Attach(context, "<main id='host'><span id='child'></span></main>");

        Assert.Same(bridge.Document, bridge.DocumentElement.OwnerDocument);
        Assert.All(bridge.Elements, element => Assert.Same(bridge.Document, element.OwnerDocument));
        Assert.Same(
            bridge.Elements.Single(element => element.Id == "child"),
            bridge.Document.GetElementById("child"));
    }

    [Fact]
    public void DomBridge_Serialization_Is_Stable_Without_Intervening_Mutations()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="en">
            <head><title>Phase Zero</title></head>
            <body><main id="host" class="card"><span>ready</span></main></body>
            </html>
            """;

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.test/phase-zero");

        var first = bridge.SerializeToHtml();
        var second = bridge.SerializeToHtml();

        Assert.Equal(first, second);
        Assert.Contains("<html lang=\"en\">", first, StringComparison.Ordinal);
        Assert.Contains("<main id=\"host\" class=\"card\">", first, StringComparison.Ordinal);
        Assert.Contains("<span>ready</span>", first, StringComparison.Ordinal);
    }

    [Fact]
    public void DomBridge_Mutation_Preserves_Parent_And_Flat_Element_Views()
    {
        const string html = """
            <!DOCTYPE html>
            <html><body><main id="host"></main></body></html>
            """;

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.test/phase-zero");

        context.Eval("""
            var host = document.getElementById('host');
            var child = document.createElement('section');
            child.id = 'created';
            host.appendChild(child);
            """);

        var host = Assert.Single(bridge.Elements, static element => element.Id == "host");
        var created = Assert.Single(bridge.Elements, static element => element.Id == "created");

        Assert.Same(host, created.ParentNode);
        Assert.Contains(created, host.ChildNodes);
        Assert.Contains("<section id=\"created\"></section>", bridge.SerializeToHtml(), StringComparison.Ordinal);
    }
}
