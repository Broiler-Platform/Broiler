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

    // ── Phase 7 item 2: parser-backed discovery (Broiler.Dom.Html tokenizer) ──

    [Fact]
    public void Ignores_A_Csp_Meta_Inside_An_Html_Comment()
    {
        // The former regex scan matched <meta> anywhere in the string, including inside a comment; the
        // tokenizer only sees real start tags, so a commented-out policy is correctly not discovered.
        const string html =
            "<head><!-- <meta http-equiv=\"Content-Security-Policy\" content=\"script-src 'none'\"> --></head>";
        Assert.Null(CspMetaDiscovery.FindPolicyContent(html));
    }

    [Fact]
    public void Ignores_A_Csp_Meta_Written_As_Text_Inside_A_Script_Body()
    {
        // <script> is a raw-text element: a meta literal inside it is script text, not a document meta.
        const string html =
            "<script>var s = '<meta http-equiv=\"Content-Security-Policy\" content=\"script-src http://evil\">';</script>";
        Assert.Null(CspMetaDiscovery.FindPolicyContent(html));
    }

    [Fact]
    public void Does_Not_Truncate_A_Content_Value_Containing_A_Greater_Than_Sign()
    {
        // The old <meta[^>]*> regex stopped at the first '>', truncating a policy whose value contained
        // one; the tokenizer honours the quotes and returns the whole directive string.
        const string html =
            "<meta http-equiv=\"Content-Security-Policy\" content=\"script-src 'self'; report-uri /r?a=1&b=2>x\">";
        Assert.Equal("script-src 'self'; report-uri /r?a=1&b=2>x", CspMetaDiscovery.FindPolicyContent(html));
    }
}
