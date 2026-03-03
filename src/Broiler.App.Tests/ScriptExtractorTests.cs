using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class ScriptExtractorTests
{
    private readonly ScriptExtractor _extractor = new();

    [Fact]
    public void Extract_NoScripts_ReturnsEmpty()
    {
        var html = "<html><body><p>Hello</p></body></html>";
        var result = _extractor.Extract(html);
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_SingleInlineScript_ReturnsContent()
    {
        var html = "<html><body><script>var x = 1;</script></body></html>";
        var result = _extractor.Extract(html);
        Assert.Single(result);
        Assert.Equal("var x = 1;", result[0]);
    }

    [Fact]
    public void Extract_MultipleScripts_ReturnsAll()
    {
        var html = @"
            <html><body>
                <script>var a = 1;</script>
                <p>Text</p>
                <script>var b = 2;</script>
            </body></html>";
        var result = _extractor.Extract(html);
        Assert.Equal(2, result.Count);
        Assert.Equal("var a = 1;", result[0]);
        Assert.Equal("var b = 2;", result[1]);
    }

    [Fact]
    public void Extract_EmptyScriptTag_IsIgnored()
    {
        var html = "<html><body><script>  </script></body></html>";
        var result = _extractor.Extract(html);
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_ScriptWithAttributes_ExtractsContent()
    {
        var html = "<html><body><SCRIPT type=\"text/javascript\">alert('hi');</SCRIPT></body></html>";
        var result = _extractor.Extract(html);
        Assert.Single(result);
        Assert.Equal("alert('hi');", result[0]);
    }

    [Fact]
    public void Extract_ExternalScriptTag_IsIgnored()
    {
        var html = "<html><body><script src=\"app.js\"></script><script>var x = 1;</script></body></html>";
        var result = _extractor.Extract(html);
        Assert.Single(result);
        Assert.Equal("var x = 1;", result[0]);
    }
}
