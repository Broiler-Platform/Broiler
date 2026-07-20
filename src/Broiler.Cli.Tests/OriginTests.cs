using System;
using Broiler.HtmlBridge.Scripting;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 7 (item 4): tests for the shared <see cref="Origin"/> primitive that replaced the five
/// copy-pasted <c>scheme://host[:port]</c> constructions and two scheme/host/port comparisons across the
/// CSP matcher, cross-origin check, <c>postMessage</c> delivery, and the <c>Location</c> projections.
/// </summary>
public sealed class OriginTests
{
    [Fact]
    public void Of_Serializes_Scheme_Host_And_Omits_Default_Port()
    {
        Assert.Equal("https://a.test", Origin.Of(new Uri("https://a.test/page.html")));
        Assert.Equal("http://a.test", Origin.Of(new Uri("http://a.test:80/x")));      // default port omitted
        Assert.Equal("https://a.test:8443", Origin.Of(new Uri("https://a.test:8443/x"))); // non-default kept
    }

    [Fact]
    public void HostOf_Returns_Host_With_Optional_Port()
    {
        Assert.Equal("a.test", Origin.HostOf(new Uri("https://a.test/x")));
        Assert.Equal("a.test:8443", Origin.HostOf(new Uri("https://a.test:8443/x")));
    }

    [Fact]
    public void SchemeHostPortEquals_Compares_All_Three_Components()
    {
        var a = new Uri("https://a.test:443/x");
        Assert.True(Origin.SchemeHostPortEquals(a, new Uri("https://a.test/y")));    // default 443 == 443
        Assert.False(Origin.SchemeHostPortEquals(a, new Uri("http://a.test/y")));    // scheme
        Assert.False(Origin.SchemeHostPortEquals(a, new Uri("https://b.test/y")));   // host
        Assert.False(Origin.SchemeHostPortEquals(a, new Uri("https://a.test:8443/y"))); // port
    }

    [Fact]
    public void SchemeHostPortEquals_Is_Case_Insensitive_On_Scheme_And_Host()
    {
        Assert.True(Origin.SchemeHostPortEquals(
            new Uri("HTTPS://A.TEST/x"), new Uri("https://a.test/y")));
    }
}
