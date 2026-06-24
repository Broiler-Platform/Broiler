
using Broiler.HTML.Dom;

namespace Broiler.DevConsole.Tests;

public class DomQueryHelperTests
{
    [Fact]
    public void FindByTag_Returns_Matching_Boxes()
    {
        var root = new CssBox(null, new HtmlTag("div", false), new Uri("/"));
        var p1 = new CssBox(root, new HtmlTag("p", false), new Uri("/"));
        var span = new CssBox(root, new HtmlTag("span", false), new Uri("/"));
        var p2 = new CssBox(span, new HtmlTag("p", false), new Uri("/"));

        var results = DomQueryHelper.FindByTag(root, "p").ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(p1, results);
        Assert.Contains(p2, results);
    }

    [Fact]
    public void FindByTag_Is_Case_Insensitive()
    {
        var root = new CssBox(null, new HtmlTag("DIV", false), new Uri("/"));
        var results = DomQueryHelper.FindByTag(root, "div").ToList();
        Assert.Single(results);
    }

    [Fact]
    public void FindById_Returns_Matching_Box()
    {
        var attrs = new Dictionary<string, string> { { "id", "main" } };
        var root = new CssBox(null, new HtmlTag("div", false), new Uri("/"));
        var target = new CssBox(root, new HtmlTag("section", false, attrs), new Uri("/"));
        var other = new CssBox(root, new HtmlTag("p", false), new Uri("/"));

        var result = DomQueryHelper.FindById(root, "main");

        Assert.Same(target, result);
    }

    [Fact]
    public void FindById_Returns_Null_When_Not_Found()
    {
        var root = new CssBox(null, new HtmlTag("div", false), new Uri("/"));
        var child = new CssBox(root, new HtmlTag("p", false), new Uri("/"));

        Assert.Null(DomQueryHelper.FindById(root, "nonexistent"));
    }

    [Fact]
    public void CountBoxes_Returns_Total_Count()
    {
        var root = new CssBox(null, new HtmlTag("div", false), new Uri("/"));
        var child1 = new CssBox(root, new HtmlTag("p", false), new Uri("/"));
        var child2 = new CssBox(root, new HtmlTag("span", false), new Uri("/"));
        var grandchild = new CssBox(child1, new HtmlTag("a", false), new Uri("/"));

        Assert.Equal(4, DomQueryHelper.CountBoxes(root));
    }

    [Fact]
    public void CountBoxes_Single_Box_Returns_One()
    {
        var root = new CssBox(null, new HtmlTag("div", false), new Uri("/"));
        Assert.Equal(1, DomQueryHelper.CountBoxes(root));
    }
}
