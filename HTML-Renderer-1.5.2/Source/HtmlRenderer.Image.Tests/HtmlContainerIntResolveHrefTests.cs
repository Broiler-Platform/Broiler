using TheArtOfDev.HtmlRenderer.Core;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Image.Adapters;

namespace HtmlRenderer.Image.Tests;

public class HtmlContainerIntResolveHrefTests
{
    private static HtmlContainerInt CreateContainer(string? baseUrl = null)
    {
        var container = new HtmlContainerInt(SkiaImageAdapter.Instance, HandlerFactory.Instance);
        if (baseUrl != null)
            container.BaseUrl = baseUrl;
        return container;
    }

    [Fact]
    public void ResolveHref_NoBaseUrl_ReturnsOriginal()
    {
        var container = CreateContainer();
        Assert.Equal("./page.html", container.ResolveHref("./page.html"));
    }

    [Fact]
    public void ResolveHref_AbsoluteHref_ReturnsOriginal()
    {
        var container = CreateContainer("https://example.com/docs/");
        Assert.Equal("https://other.com/page.html", container.ResolveHref("https://other.com/page.html"));
    }

    [Fact]
    public void ResolveHref_RelativeHref_ResolvesAgainstBaseUrl()
    {
        var container = CreateContainer("https://example.com/docs/index.html");
        var result = container.ResolveHref("./other-page.html");
        Assert.Equal("https://example.com/docs/other-page.html", result);
    }

    [Fact]
    public void ResolveHref_ParentRelativeHref_ResolvesCorrectly()
    {
        var container = CreateContainer("https://example.com/docs/sub/index.html");
        var result = container.ResolveHref("../section/page.html");
        Assert.Equal("https://example.com/docs/section/page.html", result);
    }

    [Fact]
    public void ResolveHref_SimpleFilename_ResolvesAgainstBaseUrl()
    {
        var container = CreateContainer("https://example.com/docs/index.html");
        var result = container.ResolveHref("page.html");
        Assert.Equal("https://example.com/docs/page.html", result);
    }

    [Fact]
    public void ResolveHref_FileBaseUrl_ResolvesRelativePath()
    {
        var container = CreateContainer("file:///home/user/docs/index.html");
        var result = container.ResolveHref("./other.html");
        Assert.Equal("file:///home/user/docs/other.html", result);
    }

    [Fact]
    public void ResolveHref_EmptyBaseUrl_ReturnsOriginal()
    {
        var container = CreateContainer("");
        Assert.Equal("./page.html", container.ResolveHref("./page.html"));
    }
}
