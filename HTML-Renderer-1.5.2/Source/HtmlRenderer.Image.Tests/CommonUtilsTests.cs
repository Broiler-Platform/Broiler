using TheArtOfDev.HtmlRenderer.Core.Utils;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Tests for the CommonUtils utility class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Engine", "HtmlRenderer")]
public class CommonUtilsTests
{
    [Theory]
    [InlineData("/style.css")]
    [InlineData("/images/logo.png")]
    [InlineData("/path/to/resource")]
    public void TryGetUri_RootRelativePath_ReturnsNonNullUri(string path)
    {
        var uri = CommonUtils.TryGetUri(path);

        Assert.NotNull(uri);
    }

    [Theory]
    [InlineData("/style.css")]
    [InlineData("/images/logo.png")]
    public void TryGetUri_RootRelativePath_ReturnsRelativeUri(string path)
    {
        var uri = CommonUtils.TryGetUri(path);

        Assert.NotNull(uri);
        Assert.False(uri.IsAbsoluteUri);
    }

    [Theory]
    [InlineData("http://example.com/style.css")]
    [InlineData("https://example.com/images/logo.png")]
    public void TryGetUri_AbsoluteHttpUri_ReturnsAbsoluteUri(string path)
    {
        var uri = CommonUtils.TryGetUri(path);

        Assert.NotNull(uri);
        Assert.True(uri.IsAbsoluteUri);
    }

    [Theory]
    [InlineData("http://example.com/style.css", "http")]
    [InlineData("https://example.com/style.css", "https")]
    public void TryGetUri_AbsoluteHttpUri_HasCorrectScheme(string path, string expectedScheme)
    {
        var uri = CommonUtils.TryGetUri(path);

        Assert.NotNull(uri);
        Assert.Equal(expectedScheme, uri.Scheme);
    }

    [Fact]
    public void TryGetUri_Null_ReturnsNull()
    {
        var uri = CommonUtils.TryGetUri(null!);

        Assert.Null(uri);
    }

    [Fact]
    public void TryGetUri_EmptyString_ReturnsUri()
    {
        var uri = CommonUtils.TryGetUri("");

        Assert.NotNull(uri);
        Assert.False(uri.IsAbsoluteUri);
    }

    [Fact]
    public void TryGetUri_RelativePath_ReturnsNonNullUri()
    {
        var uri = CommonUtils.TryGetUri("style.css");

        Assert.NotNull(uri);
        Assert.False(uri.IsAbsoluteUri);
    }

    [Theory]
    [InlineData("/style.css")]
    [InlineData("/path/to/deep/resource.js")]
    public void TryGetUri_RootRelativePath_PreservesOriginalString(string path)
    {
        var uri = CommonUtils.TryGetUri(path);

        Assert.NotNull(uri);
        Assert.Equal(path, uri.OriginalString);
    }
}
