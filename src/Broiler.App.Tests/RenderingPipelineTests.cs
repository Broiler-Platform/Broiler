using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class RenderingPipelineTests
{
    [Fact]
    public void ExecuteScripts_WithValidScripts_ReturnsTrue()
    {
        var pipeline = new RenderingPipeline(
            new PageLoader(new HttpClient()),
            new ScriptExtractor(),
            new ScriptEngine());

        var content = new PageContent(
            "<html><script>var x = 1;</script></html>",
            ["var x = 1;"]);

        Assert.True(pipeline.ExecuteScripts(content));
        pipeline.Dispose();
    }

    [Fact]
    public void ExecuteScripts_WithNoScripts_ReturnsTrue()
    {
        var pipeline = new RenderingPipeline(
            new PageLoader(new HttpClient()),
            new ScriptExtractor(),
            new ScriptEngine());

        var content = new PageContent("<html></html>", Array.Empty<string>());

        Assert.True(pipeline.ExecuteScripts(content));
        pipeline.Dispose();
    }

    [Fact]
    public void ExecuteScripts_WithInvalidScript_ReturnsFalse()
    {
        var pipeline = new RenderingPipeline(
            new PageLoader(new HttpClient()),
            new ScriptExtractor(),
            new ScriptEngine());

        var content = new PageContent(
            "<html></html>",
            ["invalid js @@"]);

        Assert.False(pipeline.ExecuteScripts(content));
        pipeline.Dispose();
    }
}
