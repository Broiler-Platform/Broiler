using Broiler.HTML.Dom.Core.Dom;

namespace Broiler.DevConsole.Tests;

public class BoxTreeVisitorTests
{
    [Fact]
    public void Walk_Visits_All_Boxes()
    {
        var root = new CssBox(null, new HtmlTag("div", false), new Uri("/"));
        var child1 = new CssBox(root, new HtmlTag("p", false), new Uri("/"));
        var child2 = new CssBox(root, new HtmlTag("span", false), new Uri("/"));
        var grandchild = new CssBox(child1, new HtmlTag("a", false), new Uri("/"));

        var visited = new List<(CssBox box, int depth)>();
        var visitor = new TestVisitor(visited);
        visitor.Walk(root);

        Assert.Equal(4, visited.Count);
        Assert.Equal((root, 0), visited[0]);
        Assert.Equal((child1, 1), visited[1]);
        Assert.Equal((grandchild, 2), visited[2]);
        Assert.Equal((child2, 1), visited[3]);
    }

    [Fact]
    public void Walk_Skips_Subtree_When_Visitor_Returns_False()
    {
        var root = new CssBox(null, new HtmlTag("div", false), new Uri("/"));
        var child1 = new CssBox(root, new HtmlTag("p", false), new Uri("/"));
        var grandchild = new CssBox(child1, new HtmlTag("a", false), new Uri("/"));
        var child2 = new CssBox(root, new HtmlTag("span", false), new Uri("/"));

        var visited = new List<(CssBox box, int depth)>();
        // Skip child1's subtree
        var visitor = new TestVisitor(visited, skipBox: child1);
        visitor.Walk(root);

        Assert.Equal(3, visited.Count);
        Assert.Equal((root, 0), visited[0]);
        Assert.Equal((child1, 1), visited[1]); // visited but children skipped
        Assert.Equal((child2, 1), visited[2]);
    }

    [Fact]
    public void Flatten_Returns_All_Boxes_Depth_First()
    {
        var root = new CssBox(null, new HtmlTag("div", false), new Uri("/"));
        var child1 = new CssBox(root, new HtmlTag("p", false), new Uri("/"));
        var grandchild = new CssBox(child1, new HtmlTag("a", false), new Uri("/"));
        var child2 = new CssBox(root, new HtmlTag("span", false), new Uri("/"));

        var flat = BoxTreeVisitor.Flatten(root);

        Assert.Equal(4, flat.Count);
        Assert.Same(root, flat[0]);
        Assert.Same(child1, flat[1]);
        Assert.Same(grandchild, flat[2]);
        Assert.Same(child2, flat[3]);
    }

    [Fact]
    public void Flatten_Single_Box_Returns_One()
    {
        var root = new CssBox(null, new HtmlTag("div", false), new Uri("/"));
        var flat = BoxTreeVisitor.Flatten(root);
        Assert.Single(flat);
    }

    private sealed class TestVisitor : BoxTreeVisitor
    {
        private readonly List<(CssBox box, int depth)> _visited;
        private readonly CssBox? _skipBox;

        public TestVisitor(List<(CssBox box, int depth)> visited, CssBox? skipBox = null)
        {
            _visited = visited;
            _skipBox = skipBox;
        }

        protected override bool VisitBox(CssBox box, int depth)
        {
            _visited.Add((box, depth));
            return box != _skipBox;
        }
    }
}
