namespace Broiler.Cli.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Cli")]
public class EngineTestServiceTests
{
    private readonly EngineTestService _service = new();

    [Fact]
    public void TestHtmlRenderer_ReturnsPass()
    {
        var result = _service.TestHtmlRenderer();
        Assert.True(result.Passed, result.Error);
        Assert.Equal("HTML-Renderer", result.EngineName);
        Assert.Null(result.Error);
    }

    [Fact]
    public void TestYantraJS_ReturnsPass()
    {
        var result = _service.TestYantraJS();
        Assert.True(result.Passed, result.Error);
        Assert.Equal("YantraJS", result.EngineName);
        Assert.Null(result.Error);
    }

    [Fact]
    public void RunAll_AllEnginesPass()
    {
        var results = _service.RunAll();
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Passed, $"{r.EngineName}: {r.Error}"));
    }
}
