using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for HTML character-reference decoding in text content. The shared
/// <c>Broiler.Dom.Html.HtmlTokenizer</c> previously emitted character data verbatim, so
/// <c>&amp;nbsp;</c> etc. stayed as the literal text "&amp;nbsp;" in the DOM — and, because
/// the serializer HTML-encodes text on the way out, the WPT bridge→serialize→reparse render
/// path then double-encoded it to <c>&amp;amp;nbsp;</c> and painted the literal entity
/// (css-anchor-position position-area-inline-container). The tokenizer now decodes named,
/// decimal, and hex references in ordinary character data (but not in raw-text
/// <c>&lt;script&gt;</c>/<c>&lt;style&gt;</c> content).
/// </summary>
public sealed class HtmlEntityDecodingTests
{
    private const string Url = "file:///entities.html";

    private static string TextContent(string bodyHtml, string id)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, $"<!DOCTYPE html><html><body>{bodyHtml}</body></html>", Url);
        return context.Eval($"document.getElementById('{id}').textContent")?.ToString() ?? "";
    }

    [Theory]
    [InlineData("<div id='t'>&nbsp;</div>", " ")]      // named
    [InlineData("<div id='t'>&#160;</div>", " ")]      // decimal numeric
    [InlineData("<div id='t'>&#xA0;</div>", " ")]      // hex numeric
    [InlineData("<div id='t'>&amp;</div>", "&")]
    [InlineData("<div id='t'>&lt;&gt;</div>", "<>")]
    [InlineData("<div id='t'>&copy;</div>", "©")]
    [InlineData("<div id='t'>a &amp; b</div>", "a & b")]     // entity amid text
    [InlineData("<div id='t'>Tom & Jerry</div>", "Tom & Jerry")] // bare ampersand is literal
    public void DecodesCharacterReferencesInText(string html, string expected)
    {
        Assert.Equal(expected, TextContent(html, "t"));
    }

    [Fact]
    public void NbspSurvivesSerializeRoundTrip_NotDoubleEncoded()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!DOCTYPE html><html><body><div id='t'>&nbsp;&nbsp;X</div></body></html>", Url);
        var serialized = bridge.SerializeToHtml();

        // The bug produced "&amp;nbsp;". The fix serializes the decoded U+00A0 as a raw
        // char or a numeric reference — never the literal named entity re-escaped.
        Assert.DoesNotContain("&amp;nbsp;", serialized);

        // Re-parsing the serialized HTML must still yield the two non-breaking spaces.
        using var context2 = new JSContext();
        var bridge2 = new DomBridge();
        bridge2.Attach(context2, serialized, Url);
        var text = context2.Eval("document.getElementById('t').textContent")?.ToString() ?? "";
        Assert.Equal("  X", text);
    }

    [Fact]
    public void ScriptContentIsNotEntityDecoded()
    {
        // Raw-text element content must stay verbatim: a "&amp;&amp;" in JS must not become "&&".
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context,
            "<!DOCTYPE html><html><body><script id='s' type='text/plain'>a &amp;&amp; b</script></body></html>", Url);
        var text = context.Eval("document.getElementById('s').textContent")?.ToString() ?? "";
        Assert.Equal("a &amp;&amp; b", text);
    }
}
