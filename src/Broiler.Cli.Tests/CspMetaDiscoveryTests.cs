using Broiler.HtmlBridge.Scripting;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 7 (item 1): CSP <b>discovery</b> tests, isolated from policy parse/evaluation. These exercise
/// only <see cref="CspMetaDiscovery.FindPolicyContent"/> — "where is the policy declared in the
/// document" — and assert nothing about what the policy allows (that is
/// <see cref="ContentSecurityPolicyTests"/>). The exit criterion is that CSP tests distinguish parse,
/// discovery, policy and load/execution decisions.
/// </summary>
public sealed class CspMetaDiscoveryTests
{
    [Fact]
    public void Finds_The_Directive_String_Of_A_Csp_Meta_Tag()
    {
        const string html =
            "<html><head><meta http-equiv=\"Content-Security-Policy\" content=\"script-src 'self'\"></head><body></body></html>";
        Assert.Equal("script-src 'self'", CspMetaDiscovery.FindPolicyContent(html));
    }

    [Fact]
    public void Returns_The_Raw_Unparsed_Content_Not_A_Policy_Object()
    {
        // Discovery returns the directive string verbatim; it does not evaluate or normalise it.
        const string html =
            "<meta http-equiv='content-security-policy' content='default-src none; script-src \"nonce-abc\"'>";
        Assert.Equal("default-src none; script-src \"nonce-abc\"", CspMetaDiscovery.FindPolicyContent(html));
    }

    [Fact]
    public void Returns_Null_When_No_Csp_Meta_Present()
    {
        Assert.Null(CspMetaDiscovery.FindPolicyContent("<html><head><title>x</title></head></html>"));
    }

    [Fact]
    public void Ignores_NonCsp_Meta_Tags()
    {
        const string html =
            "<meta charset=\"utf-8\"><meta http-equiv=\"refresh\" content=\"5\"><meta name=\"viewport\" content=\"width=device-width\">";
        Assert.Null(CspMetaDiscovery.FindPolicyContent(html));
    }

    [Fact]
    public void Returns_The_First_Csp_Meta_When_Several_Are_Present()
    {
        const string html =
            "<meta http-equiv=\"Content-Security-Policy\" content=\"script-src 'self'\">" +
            "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'\">";
        Assert.Equal("script-src 'self'", CspMetaDiscovery.FindPolicyContent(html));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Returns_Null_For_Empty_Input(string? html)
    {
        Assert.Null(CspMetaDiscovery.FindPolicyContent(html!));
    }
}
