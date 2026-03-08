using Broiler.Scripting;

namespace Broiler.Scripting.Tests;

public class ContentSecurityPolicyTests
{
    [Fact]
    public void Default_AllowsEval()
    {
        var csp = new ContentSecurityPolicy();
        Assert.True(csp.AllowsEval);
        Assert.False(csp.StrictDynamic);
    }

    [Fact]
    public void Parse_ScriptSrcWithoutUnsafeEval_DisallowsEval()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self'");
        Assert.False(csp.AllowsEval);
    }

    [Fact]
    public void Parse_ScriptSrcWithUnsafeEval_AllowsEval()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self' 'unsafe-eval'");
        Assert.True(csp.AllowsEval);
    }

    [Fact]
    public void Parse_StrictDynamic_IsDetected()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self' 'strict-dynamic'");
        Assert.True(csp.StrictDynamic);
        Assert.False(csp.AllowsEval);
    }

    [Fact]
    public void Parse_EmptyPolicy_AllowsEval()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("");
        Assert.True(csp.AllowsEval);
    }

    [Fact]
    public void Parse_NullPolicy_AllowsEval()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse(null!);
        Assert.True(csp.AllowsEval);
    }

    [Fact]
    public void Parse_WhitespacePolicy_AllowsEval()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("   ");
        Assert.True(csp.AllowsEval);
    }

    [Fact]
    public void EnforceEval_WhenDisallowed_Throws()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self'");
        var ex = Assert.Throws<InvalidOperationException>(() => csp.EnforceEval());
        Assert.Contains("unsafe-eval", ex.Message);
    }

    [Fact]
    public void EnforceEval_WhenAllowed_DoesNotThrow()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self' 'unsafe-eval'");
        csp.EnforceEval(); // Should not throw
    }

    [Fact]
    public void Parse_MultipleCalls_LastWins()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self' 'unsafe-eval'");
        Assert.True(csp.AllowsEval);

        csp.Parse("script-src 'self'");
        Assert.False(csp.AllowsEval);
    }

    [Fact]
    public void Parse_MultipleDirectives_OnlyScriptSrcMatters()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("default-src 'none'; script-src 'self' 'unsafe-eval'; style-src 'self'");
        Assert.True(csp.AllowsEval);
    }

    [Fact]
    public void Parse_CaseInsensitive()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("Script-Src 'self' 'unsafe-eval'");
        Assert.True(csp.AllowsEval);
    }

    [Fact]
    public void ScriptSrcTokens_ReturnsTokens()
    {
        var csp = new ContentSecurityPolicy();
        csp.Parse("script-src 'self' 'unsafe-eval' https://example.com");
        Assert.Contains("'self'", csp.ScriptSrcTokens);
        Assert.Contains("'unsafe-eval'", csp.ScriptSrcTokens);
        Assert.Contains("https://example.com", csp.ScriptSrcTokens);
    }
}
