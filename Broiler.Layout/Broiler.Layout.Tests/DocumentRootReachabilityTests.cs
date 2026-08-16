using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// <see cref="DocumentRoot.IsUnreachableAbsoluteUrl"/> — the rule that keeps a render pinned to a
/// local document root from fetching a sub-resource off the real network.
/// </summary>
/// <remarks>
/// The default case is the one worth stating first: a host that has pinned nothing gets no
/// restriction at all, because a browser with a network is the normal caller and the restriction
/// would be a regression for it. Everything else here is the pinned case.
/// </remarks>
public sealed class DocumentRootReachabilityTests
{
    private static readonly string[] ServedHosts =
        ["web-platform.test", "www.web-platform.test", "xn--lve-6lad.web-platform.test"];

    [Fact]
    public void UnpinnedRender_TreatsEveryUrlAsReachable()
    {
        // No Pin: this is the real browser, and it is allowed to fetch what a page names.
        Assert.False(DocumentRoot.IsUnreachableAbsoluteUrl("http://example.com/a.png"));
        Assert.False(DocumentRoot.IsUnreachableAbsoluteUrl("https://192.0.2.1/a.png"));
    }

    [Fact]
    public void PinnedWithoutHosts_TreatsEveryUrlAsReachable()
    {
        // A root with no host list says where `/` resolves, not that the render is offline, so it
        // must not start suppressing loads on its own.
        using var scope = DocumentRoot.Pin("/corpus", localOriginHosts: null);

        Assert.False(DocumentRoot.IsUnreachableAbsoluteUrl("http://example.com/a.png"));
    }

    [Theory]
    // Hosts a corpus does not serve: unreachable, and the ones that cost wall-clock rather than
    // failing fast are exactly these — an unrouted IP literal or a documentation address.
    [InlineData("http://example.com/a.png")]
    [InlineData("https://example.com/a.png")]
    [InlineData("http://192.0.2.1/")]
    [InlineData("http://[2001::1]")]
    [InlineData("http://192.168.0.257/")]
    [InlineData("http://user:pass@foo:21/bar;par?b#c")]
    // A host that merely *looks* like a served one is not one: exact matching, so a corpus's
    // deliberately-unresolvable subdomains stay unresolvable.
    [InlineData("http://nonexistent.web-platform.test/a.png")]
    [InlineData("http://web-platform.test.evil.example/a.png")]
    public void PinnedRender_TreatsOffCorpusHttpUrlAsUnreachable(string url)
    {
        using var scope = DocumentRoot.Pin("/corpus", ServedHosts);

        Assert.True(DocumentRoot.IsUnreachableAbsoluteUrl(url));
    }

    [Theory]
    // A served host is content of the pinned root, so it stays loadable — the host's own loader
    // maps it onto a file, and suppressing it here would break that.
    [InlineData("http://web-platform.test:8000/images/blue.png")]
    [InlineData("https://www.web-platform.test:8443/images/blue.png")]
    // Uri lower-cases and IDNA-encodes the host, so a served host written either way matches.
    [InlineData("http://WEB-PLATFORM.TEST:8000/images/blue.png")]
    [InlineData("http://élève.web-platform.test:8000/images/blue.png")]
    public void PinnedRender_TreatsServedHostAsReachable(string url)
    {
        using var scope = DocumentRoot.Pin("/corpus", ServedHosts);

        Assert.False(DocumentRoot.IsUnreachableAbsoluteUrl(url));
    }

    [Theory]
    // Not this rule's business: none of these reaches a network for it to forbid. They are resolved
    // against the document's base URL by the loader, and a bad one fails there as it always did.
    [InlineData("")]
    [InlineData(null)]
    [InlineData("/images/blue.png")]
    [InlineData("images/blue.png")]
    [InlineData("//foo/bar")]
    [InlineData("#fragment")]
    [InlineData("file:///corpus/images/blue.png")]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    [InlineData("mailto:example.com/")]
    [InlineData("javascript:/example.com/")]
    public void PinnedRender_LeavesNonHttpAndRelativeUrlsAlone(string? url)
    {
        using var scope = DocumentRoot.Pin("/corpus", ServedHosts);

        Assert.False(DocumentRoot.IsUnreachableAbsoluteUrl(url));
    }

    [Fact]
    public void Pin_RestoresThePreviousHostsOnDispose()
    {
        using (var outer = DocumentRoot.Pin("/corpus", ServedHosts))
        {
            Assert.True(DocumentRoot.IsUnreachableAbsoluteUrl("http://example.com/a.png"));

            using (var inner = DocumentRoot.Pin(root: null, localOriginHosts: null))
                Assert.False(DocumentRoot.IsUnreachableAbsoluteUrl("http://example.com/a.png"));

            Assert.True(DocumentRoot.IsUnreachableAbsoluteUrl("http://example.com/a.png"));
        }

        // Restored to the unpinned default, so a test — or a render — that follows is not left
        // offline by one that ran before it.
        Assert.False(DocumentRoot.IsUnreachableAbsoluteUrl("http://example.com/a.png"));
    }
}
