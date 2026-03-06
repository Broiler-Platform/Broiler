using TheArtOfDev.HtmlRenderer.Core.Dom;

namespace Broiler.DevConsole.Tests;

public class DomQueryHelperTests
{
    [Fact]
    public void FindByTag_Returns_Matching_Boxes()
    {
        var root = new CssBox(null, new HtmlTag("div", false));
        var p1 = new CssBox(root, new HtmlTag("p", false));
        var span = new CssBox(root, new HtmlTag("span", false));
        var p2 = new CssBox(span, new HtmlTag("p", false));

        var results = DomQueryHelper.FindByTag(root, "p").ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(p1, results);
        Assert.Contains(p2, results);
    }

    [Fact]
    public void FindByTag_Is_Case_Insensitive()
    {
        var root = new CssBox(null, new HtmlTag("DIV", false));
        var results = DomQueryHelper.FindByTag(root, "div").ToList();
        Assert.Single(results);
    }

    [Fact]
    public void FindById_Returns_Matching_Box()
    {
        var attrs = new Dictionary<string, string> { { "id", "main" } };
        var root = new CssBox(null, new HtmlTag("div", false));
        var target = new CssBox(root, new HtmlTag("section", false, attrs));
        var other = new CssBox(root, new HtmlTag("p", false));

        var result = DomQueryHelper.FindById(root, "main");

        Assert.Same(target, result);
    }

    [Fact]
    public void FindById_Returns_Null_When_Not_Found()
    {
        var root = new CssBox(null, new HtmlTag("div", false));
        var child = new CssBox(root, new HtmlTag("p", false));

        Assert.Null(DomQueryHelper.FindById(root, "nonexistent"));
    }

    [Fact]
    public void CountBoxes_Returns_Total_Count()
    {
        var root = new CssBox(null, new HtmlTag("div", false));
        var child1 = new CssBox(root, new HtmlTag("p", false));
        var child2 = new CssBox(root, new HtmlTag("span", false));
        var grandchild = new CssBox(child1, new HtmlTag("a", false));

        Assert.Equal(4, DomQueryHelper.CountBoxes(root));
    }

    [Fact]
    public void CountBoxes_Single_Box_Returns_One()
    {
        var root = new CssBox(null, new HtmlTag("div", false));
        Assert.Equal(1, DomQueryHelper.CountBoxes(root));
    }
}
