using Broiler.HtmlBridge.Scripting;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 7 (item 4): the shared <see cref="UrlResolver"/> that CSP source-matching and external-script
/// fetching now both resolve URLs through — the seam toward "one URL resolution implementation shared by
/// script, CSS, fetch, XHR and frames".
/// </summary>
public sealed class UrlResolverTests
{
    [Fact]
    public void Keeps_Absolute_Urls()
    {
        Assert.Equal("https://a.test/x.js", UrlResolver.Resolve("https://a.test/x.js", "https://b.test/")?.AbsoluteUri);
        Assert.Equal("file:///a/x.js", UrlResolver.Resolve("file:///a/x.js", null)?.AbsoluteUri);
    }

    [Fact]
    public void Resolves_Relative_Against_Base()
    {
        Assert.Equal("https://a.test/lib/x.js", UrlResolver.Resolve("lib/x.js", "https://a.test/page.html")?.AbsoluteUri);
        Assert.Equal("https://a.test/deep/y.js", UrlResolver.Resolve("y.js", "https://a.test/deep/page.html")?.AbsoluteUri);
    }

    [Fact]
    public void Returns_Null_When_Relative_And_No_Usable_Base()
    {
        Assert.Null(UrlResolver.Resolve("lib/x.js", null));
        Assert.Null(UrlResolver.Resolve("lib/x.js", "not-an-absolute-base"));
    }
}
