using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>btoa</c> / <c>atob</c> — the base64 pair on <c>WindowOrWorkerGlobalScope</c> (HTML §8.3).
/// <para>
/// Neither existed, so an unqualified <c>atob(…)</c> was a <c>ReferenceError</c> — which aborts the
/// whole script that called it, not just the call. Both are asserted unqualified as well as through
/// <c>window</c>, because the unqualified spelling is the one pages use and the one that used to
/// fail.
/// </para>
/// </summary>
public sealed class Base64Tests
{
    private static (JSContext Context, DomBridge Bridge) Attach()
    {
        var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!doctype html><html><body></body></html>", "https://example.com/");
        return (context, bridge);
    }

    private static string Eval(JSContext context, string expression) =>
        context.Eval($"String({expression})").ToString();

    [Fact]
    public void BothAreReachable_QualifiedAndUnqualified()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("function", Eval(context, "typeof atob"));
        Assert.Equal("function", Eval(context, "typeof btoa"));
        Assert.Equal("function", Eval(context, "typeof window.atob"));
        Assert.Equal("function", Eval(context, "typeof window.btoa"));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("f", "Zg==")]
    [InlineData("fo", "Zm8=")]
    [InlineData("foo", "Zm9v")]
    [InlineData("foob", "Zm9vYg==")]
    [InlineData("hello", "aGVsbG8=")]
    public void EncodesTheKnownVectors(string plain, string encoded)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal(encoded, Eval(context, $"btoa('{plain}')"));
        Assert.Equal(plain, Eval(context, $"atob('{encoded}')"));
    }

    /// <summary>
    /// The pair moves bytes, not text: each code unit is one byte in and one byte out, which is what
    /// makes it usable for the binary payloads pages base64 around.
    /// </summary>
    [Fact]
    public void RoundTripsABinaryString()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("true", Eval(context,
            "(function(){var s=''; for (var i=0;i<256;i++) s+=String.fromCharCode(i); return atob(btoa(s))===s;})()"));
    }

    /// <summary>A code point above U+00FF is not a byte, and the spec makes that an error rather than a truncation.</summary>
    [Fact]
    public void Btoa_RejectsBeyondLatin1()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("InvalidCharacterError", Eval(context,
            "(function(){try{ btoa('\\u20ac'); return 'no throw'; }catch(e){ return e.name; }})()"));
    }

    /// <summary>
    /// Forgiving-base64 decode: ASCII whitespace anywhere is ignored, and input whose length is not
    /// a multiple of four decodes without its padding.
    /// </summary>
    [Theory]
    [InlineData("aGVs bG8=", "hello")]
    [InlineData("aGVs\\nbG8=", "hello")]
    [InlineData("aGVsbG8", "hello")]
    [InlineData("Zm9v", "foo")]
    public void Atob_IsForgiving(string encoded, string expected)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal(expected, Eval(context, $"atob('{encoded}')"));
    }

    /// <summary>
    /// What forgiving does not mean. A one-character tail carries six bits and cannot encode a byte,
    /// and a character outside the alphabet is not decodable — both are errors, not silent
    /// truncations that would hand a page the wrong bytes.
    /// </summary>
    [Theory]
    [InlineData("a")]
    [InlineData("aGVsbG8=a")]
    [InlineData("aG!s")]
    [InlineData("a=GVsbG8")]
    public void Atob_RejectsWhatCannotDecode(string encoded)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("InvalidCharacterError", Eval(context,
            $"(function(){{try{{ atob('{encoded}'); return 'no throw'; }}catch(e){{ return e.name; }}}})()"));
    }
}
