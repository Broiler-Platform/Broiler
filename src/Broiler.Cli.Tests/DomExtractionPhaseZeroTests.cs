using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Characterization tests for the legacy bridge-owned tree. These tests freeze
/// the Phase 0 starting point so the canonical DOM extraction can proceed in
/// small, behavior-preserving steps.
/// </summary>
public sealed class DomExtractionPhaseZeroTests
{
    private static readonly string[] LegacyMutableCollectionProperties =
    [
        "JsSetStyleProps",
        "NsAttrMap",
        "Style",
    ];

    private static readonly string[] LegacySettableScalarProperties =
    [
        "ClassName",
        "Id",
        "InnerHtml",
        "NamespaceURI",
        "OwnerDocRoot",
        "Parent",
        "TextContent",
    ];

    [Fact]
    public void Legacy_DomElement_Compatibility_Surface_Is_Explicitly_Frozen()
    {
        var properties = typeof(DomElement)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var mutableCollections = properties
            .Where(static property =>
                property.PropertyType.IsGenericType &&
                property.PropertyType.GetGenericTypeDefinition() is var definition &&
                (definition == typeof(Dictionary<,>) ||
                 definition == typeof(HashSet<>) ||
                 definition == typeof(List<>)))
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        var settableScalars = properties
            .Where(static property => property.SetMethod?.IsPublic == true)
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(LegacyMutableCollectionProperties, mutableCollections);
        Assert.Equal(LegacySettableScalarProperties, settableScalars);
    }

    [Fact]
    public void Legacy_DomElement_Is_A_Canonical_Dom_Node()
    {
        var element = new DomElement("div", "host", null, string.Empty);

        Assert.IsAssignableFrom<Broiler.Dom.DomNode>(element);
        Assert.IsAssignableFrom<Broiler.Dom.DomElement>(element);
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
    public void HtmlTreeBuilder_Creates_Implicit_Structure_With_Consistent_Parent_Links()
    {
        var builder = new HtmlTreeBuilder();

        var (documentElement, elements, title) = builder.Build(
            "<title>Phase Zero</title><main id='host'><span>one</span><span>two</span></main>");

        Assert.Equal("html", documentElement.TagName);
        Assert.Equal("Phase Zero", title);

        var head = Assert.Single(documentElement.Children, static child => child.TagName == "head");
        var body = Assert.Single(documentElement.Children, static child => child.TagName == "body");
        Assert.Same(documentElement, head.Parent);
        Assert.Same(documentElement, body.Parent);

        var host = Assert.Single(elements, static element => element.Id == "host");
        Assert.Same(body, host.Parent);
        Assert.Equal(["span", "span"], host.Children.Select(static child => child.TagName));
        Assert.All(host.Children, child => Assert.Same(host, child.Parent));
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

        Assert.Same(host, created.Parent);
        Assert.Contains(created, host.Children);
        Assert.Contains("<section id=\"created\"></section>", bridge.SerializeToHtml(), StringComparison.Ordinal);
    }
}
