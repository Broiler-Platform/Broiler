using System;
using Broiler.HtmlBridge.Scripting;
using Broiler.HtmlBridge.Internal.Scripting;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 7 (item 1): CSP <b>URL/origin source-matching</b> tests, isolated from discovery
/// (<see cref="CspMetaDiscoveryTests"/>) and directive parse/evaluation
/// (<see cref="ContentSecurityPolicyTests"/>). These exercise only <see cref="CspSourceMatching"/> —
/// resolving a candidate URL, same-origin comparison, and matching scheme / absolute host-source tokens.
/// </summary>
public sealed class CspSourceMatchingTests
{
    [Fact]
    public void ResolveUri_Keeps_Absolute_And_Resolves_Relative_Against_Page()
    {
        Assert.Equal("https://a.test/x.js",
            CspSourceMatching.ResolveUri("https://a.test/x.js", null)?.AbsoluteUri);
        Assert.Equal("https://a.test/lib/x.js",
            CspSourceMatching.ResolveUri("lib/x.js", "https://a.test/page.html")?.AbsoluteUri);
        Assert.Null(CspSourceMatching.ResolveUri("lib/x.js", null)); // relative with no base
    }

    [Fact]
    public void IsSameOrigin_Compares_Scheme_Host_Port()
    {
        var candidate = new Uri("https://a.test:443/x.js");
        Assert.True(CspSourceMatching.IsSameOrigin(candidate, "https://a.test/page.html"));
        Assert.False(CspSourceMatching.IsSameOrigin(candidate, "https://b.test/page.html")); // host
        Assert.False(CspSourceMatching.IsSameOrigin(candidate, "https://a.test:8443/page")); // port
        Assert.False(CspSourceMatching.IsSameOrigin(candidate, null));                       // no page
    }

    [Fact]
    public void IsSameOrigin_Treats_Two_File_Urls_As_Same_Origin()
    {
        Assert.True(CspSourceMatching.IsSameOrigin(new Uri("file:///a/x.js"), "file:///b/page.html"));
    }

    [Fact]
    public void IsSchemeSource_Recognises_Bare_Scheme_Tokens_Only()
    {
        Assert.True(CspSourceMatching.IsSchemeSource("https:"));
        Assert.False(CspSourceMatching.IsSchemeSource("'self'"));
        Assert.False(CspSourceMatching.IsSchemeSource("https://host.test"));
    }

    [Fact]
    public void MatchesAbsoluteSource_Requires_Scheme_Host_Port_And_Path_Prefix()
    {
        var candidate = new Uri("https://cdn.test/lib/x.js");
        Assert.True(CspSourceMatching.MatchesAbsoluteSource("https://cdn.test", candidate));
        Assert.True(CspSourceMatching.MatchesAbsoluteSource("https://cdn.test/lib/", candidate));
        Assert.False(CspSourceMatching.MatchesAbsoluteSource("https://cdn.test/other/", candidate)); // path
        Assert.False(CspSourceMatching.MatchesAbsoluteSource("https://cdn.test:8443", candidate));   // port
        Assert.False(CspSourceMatching.MatchesAbsoluteSource("http://cdn.test", candidate));          // scheme
    }
}
