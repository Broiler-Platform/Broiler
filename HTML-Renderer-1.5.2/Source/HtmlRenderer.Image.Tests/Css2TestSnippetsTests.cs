namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Tests for <see cref="Css2TestSnippets"/> utility methods,
/// in particular the HTML5-style body wrapper injection used
/// to align differential test snippets with Chromium's implicit
/// <c>&lt;html&gt;&lt;body&gt;</c> wrapper behaviour.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Engine", "HtmlRenderer")]
public class Css2TestSnippetsTests
{
    [Fact]
    public void EnsureBodyWrapper_BareDiv_WrapsInHtmlBody()
    {
        const string html = "<div style='width:100px;height:50px;'></div>";
        var wrapped = Css2TestSnippets.EnsureBodyWrapper(html);
        Assert.StartsWith("<html><body>", wrapped);
        Assert.EndsWith("</body></html>", wrapped);
        Assert.Contains(html, wrapped);
    }

    [Fact]
    public void EnsureBodyWrapper_WithBodyTag_ReturnsUnchanged()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;'></div>
              </body>";
        var result = Css2TestSnippets.EnsureBodyWrapper(html);
        Assert.Equal(html, result);
    }

    [Fact]
    public void EnsureBodyWrapper_WithHtmlTag_ReturnsUnchanged()
    {
        const string html =
            @"<html><body style='margin:0;'>
                <div>Content</div>
              </body></html>";
        var result = Css2TestSnippets.EnsureBodyWrapper(html);
        Assert.Equal(html, result);
    }

    [Fact]
    public void EnsureBodyWrapper_CaseInsensitive_BodyTag()
    {
        const string html = "<BODY><div>Content</div></BODY>";
        var result = Css2TestSnippets.EnsureBodyWrapper(html);
        Assert.Equal(html, result);
    }

    [Fact]
    public void EnsureBodyWrapper_StyleBlockWithoutBodyTag_Wraps()
    {
        const string html =
            @"<style>div { color: red; }</style>
              <div style='width:100px;height:50px;'></div>";
        var wrapped = Css2TestSnippets.EnsureBodyWrapper(html);
        Assert.StartsWith("<html><body>", wrapped);
        Assert.EndsWith("</body></html>", wrapped);
    }

    [Fact]
    public void All_AllSnippetsContainBodyTag()
    {
        foreach (var (chapter, name, html) in Css2TestSnippets.All())
        {
            Assert.True(
                html.Contains("<body", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("<html", StringComparison.OrdinalIgnoreCase),
                $"{chapter}/{name}: snippet should contain <body> or <html> after wrapping");
        }
    }

    [Fact]
    public void All_BareSnippetIsActuallyWrapped()
    {
        // Chapter9[0] is a bare <div> snippet without body/html tags.
        var raw = Css2TestSnippets.Chapter9[0];
        Assert.DoesNotContain("<body", raw.Html, StringComparison.OrdinalIgnoreCase);

        var wrapped = Css2TestSnippets.All()
            .First(t => t.Name == raw.Name);
        Assert.StartsWith("<html><body>", wrapped.Html);
        Assert.EndsWith("</body></html>", wrapped.Html);
    }

    [Fact]
    public void EnsureBodyWrapper_TagInAttribute_StillWraps()
    {
        const string html = "<div title='<bodytext'>Content</div>";
        var wrapped = Css2TestSnippets.EnsureBodyWrapper(html);
        Assert.StartsWith("<html><body>", wrapped);
    }
}
