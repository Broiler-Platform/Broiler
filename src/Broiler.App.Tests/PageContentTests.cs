using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class PageContentTests
{
    [Fact]
    public void Constructor_StoresHtmlAndScripts()
    {
        var scripts = new List<string> { "var x = 1;" };
        var content = new PageContent("<html></html>", scripts);

        Assert.Equal("<html></html>", content.Html);
        Assert.Single(content.Scripts);
        Assert.Equal("var x = 1;", content.Scripts[0]);
    }

    [Fact]
    public void Constructor_EmptyScripts_ReturnsEmptyList()
    {
        var content = new PageContent("<html></html>", Array.Empty<string>());

        Assert.Empty(content.Scripts);
    }
}
