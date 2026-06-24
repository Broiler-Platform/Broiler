using Broiler.HTML.Dom;
using Broiler.HtmlBridge;

namespace Broiler.DevConsole.Tests;

public class ConsoleServiceTests : IDisposable
{
    public ConsoleServiceTests()
    {
        RenderLogger.Clear();
    }

    public void Dispose()
    {
        RenderLogger.Clear();
    }

    [Fact]
    public void GetFilteredEntries_Returns_All_When_No_Filters()
    {
        using var service = new ConsoleService();

        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Info, "ctx", "msg1");
        RenderLogger.Log(LogCategory.HtmlRenderer, LogLevel.Warning, "ctx2", "msg2");

        var entries = service.GetFilteredEntries();
        Assert.True(entries.Count >= 2);
    }

    [Fact]
    public void GetFilteredEntries_Filters_By_Level()
    {
        using var service = new ConsoleService();

        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Debug, "ctx", "debug");
        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Warning, "ctx", "warn");
        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Error, "ctx", "err");

        var entries = service.GetFilteredEntries(minimumLevel: LogLevel.Warning);
        Assert.All(entries, e => Assert.True(e.Level >= LogLevel.Warning));
        Assert.True(entries.Count >= 2);
    }

    [Fact]
    public void GetFilteredEntries_Filters_By_Category()
    {
        using var service = new ConsoleService();

        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Info, "ctx", "js");
        RenderLogger.Log(LogCategory.HtmlRenderer, LogLevel.Info, "ctx", "html");

        var entries = service.GetFilteredEntries(category: LogCategory.JavaScript);
        Assert.All(entries, e => Assert.Equal(LogCategory.JavaScript, e.Category));
    }

    [Fact]
    public void GetFilteredEntries_Filters_By_SearchText()
    {
        using var service = new ConsoleService();

        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Info, "ctx", "hello world");
        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Info, "ctx", "goodbye");

        var entries = service.GetFilteredEntries(searchText: "hello");
        Assert.All(entries, e => Assert.Contains("hello", e.Message, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClearEntries_Removes_All()
    {
        using var service = new ConsoleService();

        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Info, "ctx", "msg");
        Assert.NotEmpty(service.GetFilteredEntries());

        service.ClearEntries();
        Assert.Empty(service.GetFilteredEntries());
    }

    [Fact]
    public void BuildBoxTree_Creates_Correct_Structure()
    {
        var root = new CssBox(null, new HtmlTag("div", false), new Uri("/"));
        var child = new CssBox(root, new HtmlTag("p", false), new Uri("/"));
        _ = new CssBox(child, new HtmlTag("span", false), new Uri("/"));

        var tree = ConsoleService.BuildBoxTree(root);

        Assert.Equal("div", tree.Tag);
        Assert.Equal(0, tree.Depth);
        Assert.Single(tree.Children);
        Assert.Equal("p", tree.Children[0].Tag);
        Assert.Equal(1, tree.Children[0].Depth);
        Assert.Single(tree.Children[0].Children);
        Assert.Equal("span", tree.Children[0].Children[0].Tag);
        Assert.Equal(2, tree.Children[0].Children[0].Depth);
    }

    [Fact]
    public void BuildBoxTree_Captures_Id_And_Class()
    {
        var tag = new HtmlTag("div", false, new Dictionary<string, string>
        {
            ["id"] = "main",
            ["class"] = "container wide",
        });
        var root = new CssBox(null, tag, new Uri("/"));

        var tree = ConsoleService.BuildBoxTree(root);

        Assert.Equal("main", tree.Id);
        Assert.Equal("container wide", tree.CssClass);
    }

    [Fact]
    public void BuildBoxTree_Handles_Anonymous_Box()
    {
        var root = new CssBox(null, null!, new Uri("/"));
        var tree = ConsoleService.BuildBoxTree(root);

        Assert.Equal("anon", tree.Tag);
        Assert.Null(tree.Id);
    }

    [Fact]
    public void GetComputedStyles_Returns_Grouped_Properties()
    {
        var root = new CssBox(null, new HtmlTag("div", false), new Uri("/"));

        var styles = ConsoleService.GetComputedStyles(root);

        Assert.NotEmpty(styles);
        Assert.Contains(styles, s => s.Name == "display");
        Assert.Contains(styles, s => s.Name == "font-size");
        Assert.Contains(styles, s => s.Name == "background-color");
        Assert.Contains(styles, s => s.Name == "margin-top");

        // Verify categories
        Assert.Contains(styles, s => s.Category == "Layout");
        Assert.Contains(styles, s => s.Category == "Text");
        Assert.Contains(styles, s => s.Category == "Visual");
        Assert.Contains(styles, s => s.Category == "Box Model");
    }

    [Fact]
    public void GetBoxModel_Returns_Zero_For_Uncomputed_Box()
    {
        var root = new CssBox(null, new HtmlTag("div", false), new Uri("/"));

        var model = ConsoleService.GetBoxModel(root);

        // Uncomputed boxes should have NaN mapped to 0
        Assert.Equal(0, model.Margin.Top);
        Assert.Equal(0, model.Border.Top);
        Assert.Equal(0, model.Padding.Top);
    }

    [Fact]
    public void EntryReceived_Event_Fires()
    {
        using var service = new ConsoleService();
        var received = new List<RenderLogEntry>();
        service.EntryReceived += received.Add;

        RenderLogger.Log(LogCategory.JavaScript, LogLevel.Info, "test", "event-test");

        Assert.Contains(received, e => e.Message == "event-test");
    }
}
