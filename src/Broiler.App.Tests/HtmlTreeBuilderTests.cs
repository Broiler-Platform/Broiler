using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class HtmlTreeBuilderTests
{
    private readonly HtmlTreeBuilder _builder = new();

    [Fact]
    public void Build_SimpleHtml_CreatesProperParentChildRelationships()
    {
        var (root, allElements, _) = _builder.Build("<html><body><div><span>Hi</span></div></body></html>");

        Assert.Equal("html", root.TagName);
        var body = root.Children.First(c => c.TagName == "body");
        Assert.NotNull(body);
        var div = allElements.First(e => e.TagName == "div");
        Assert.Equal(body, div.Parent);
        var span = allElements.First(e => e.TagName == "span");
        Assert.Equal(div, span.Parent);
    }

    [Fact]
    public void Build_TitleElement_ExtractsTitleText()
    {
        var (_, _, title) = _builder.Build("<html><head><title>My Title</title></head><body></body></html>");

        Assert.Equal("My Title", title);
    }

    [Fact]
    public void Build_CharacterData_CreatesTextNodes()
    {
        var (_, allElements, _) = _builder.Build("<html><body><p>Hello World</p></body></html>");

        var textNode = allElements.FirstOrDefault(e => e.IsTextNode);
        Assert.NotNull(textNode);
        Assert.Equal("Hello World", textNode.TextContent);
    }

    [Fact]
    public void Build_VoidElements_DoNotHaveChildren()
    {
        var (_, allElements, _) = _builder.Build("<html><body><br><img><input></body></html>");

        var br = allElements.First(e => e.TagName == "br");
        Assert.Empty(br.Children);
        var img = allElements.First(e => e.TagName == "img");
        Assert.Empty(img.Children);
    }

    [Fact]
    public void Build_ParagraphAutoClose_WhenBlockElementEncountered()
    {
        var (_, allElements, _) = _builder.Build("<html><body><p>First</p><div>Block</div></body></html>");

        var p = allElements.First(e => e.TagName == "p");
        var div = allElements.First(e => e.TagName == "div");
        // Both p and div should be children of body, not nested
        Assert.Equal(p.Parent, div.Parent);
    }

    [Fact]
    public void Build_IdAttribute_SetOnElement()
    {
        var (_, allElements, _) = _builder.Build("<html><body><div id=\"main\"></div></body></html>");

        var div = allElements.First(e => e.TagName == "div");
        Assert.Equal("main", div.Id);
    }

    [Fact]
    public void Build_ClassAttribute_SetOnElement()
    {
        var (_, allElements, _) = _builder.Build("<html><body><div class=\"box large\"></div></body></html>");

        var div = allElements.First(e => e.TagName == "div");
        Assert.Equal("box large", div.ClassName);
    }

    [Fact]
    public void Build_StyleAttribute_ParsedIntoDictionary()
    {
        var (_, allElements, _) = _builder.Build("<html><body><div style=\"color:red;font-size:14px\"></div></body></html>");

        var div = allElements.First(e => e.TagName == "div");
        Assert.Equal("red", div.Style["color"]);
        Assert.Equal("14px", div.Style["font-size"]);
    }

    [Fact]
    public void Build_AllElements_ContainsAllNonStructuralElements()
    {
        var (_, allElements, _) = _builder.Build("<html><body><div><span>Text</span></div><p>More</p></body></html>");

        Assert.Contains(allElements, e => e.TagName == "div");
        Assert.Contains(allElements, e => e.TagName == "span");
        Assert.Contains(allElements, e => e.TagName == "p");
        // Text nodes are also in the flat list
        Assert.Contains(allElements, e => e.IsTextNode && e.TextContent == "Text");
        Assert.Contains(allElements, e => e.IsTextNode && e.TextContent == "More");
    }
}
