using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for HTML character-reference decoding, in text content and in attribute
/// values. The shared <c>Broiler.Dom.Html.HtmlTokenizer</c> previously emitted character data
/// verbatim, so <c>&amp;nbsp;</c> etc. stayed as the literal text "&amp;nbsp;" in the DOM — and,
/// because the serializer HTML-encodes text on the way out, the WPT bridge→serialize→reparse
/// render path then double-encoded it to <c>&amp;amp;nbsp;</c> and painted the literal entity
/// (css-anchor-position position-area-inline-container).
/// <para>
/// Attribute values were the other half of the same gap, and were left undecoded for longer: the
/// tokenizer kept the source text and Broiler.HTML's box builder decoded it on its way to the
/// renderer, so the rendering was right and every reader of the DOM was wrong by exactly one level
/// of escaping. <c>getAttribute("href")</c> on <c>href="?a=1&amp;amp;b=2"</c> returned the escaped
/// spelling, an <c>[attr="…"]</c> selector had to be written against it, and serializing escaped
/// it again — so a DOM round-trip grew another <c>&amp;amp;</c> each time round.
/// </para>
/// <para>The tokenizer now decodes named, decimal, and hex references in both (but not in raw-text
/// <c>&lt;script&gt;</c>/<c>&lt;style&gt;</c> content, which has no references to decode).</para>
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

    [Fact(Timeout = 600000)]
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

    [Fact(Timeout = 600000)]
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

    // ── Attribute values ─────────────────────────────────────────────────────

    private static string Evaluate(string bodyHtml, string expression)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, $"<!DOCTYPE html><html><body>{bodyHtml}</body></html>", Url);
        return context.Eval(expression)?.ToString() ?? "";
    }

    /// <summary>The shape the gap was found through: an escaped query separator, so the URL the DOM
    /// reports is the URL the page meant.</summary>
    [Fact(Timeout = 600000)]
    public void GetAttributeReturnsTheDecodedValue()
    {
        Assert.Equal("?a=1&b=2",
            Evaluate("<a id='t' href='?a=1&amp;b=2'></a>", "document.getElementById('t').getAttribute('href')"));
    }

    [Theory]
    [InlineData("a&quot;b", "a\"b")]            // named
    [InlineData("a&amp;b", "a&b")]
    [InlineData("a&lt;b&gt;c", "a<b>c")]
    [InlineData("&#65;&#x42;", "AB")]           // decimal and hex numeric
    [InlineData("&amp;amp;", "&amp;")]          // one level of escaping removed, not two
    [InlineData("?a=1&copy=2", "?a=1&copy=2")]  // no terminator: left literal (ambiguous ampersand)
    [InlineData("plain value", "plain value")]
    public void DecodesCharacterReferencesInAttributeValues(string spelling, string expected)
    {
        Assert.Equal(expected,
            Evaluate($"<i id='t' data-x=\"{spelling}\"></i>", "document.getElementById('t').getAttribute('data-x')"));
    }

    /// <summary>An attribute selector matches the value, not its spelling.</summary>
    [Fact(Timeout = 600000)]
    public void AttributeSelectorMatchesTheDecodedValue()
    {
        Assert.Equal("1",
            Evaluate("<i data-x='a&amp;b'></i>", """String(document.querySelectorAll('[data-x="a&b"]').length)"""));
    }

    /// <summary>
    /// And the attribute round trip is stable: serializing re-escapes exactly the level the parse
    /// removed, so re-parsing the output gives the same value rather than one more
    /// <c>&amp;amp;</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AttributeValueSurvivesSerializeRoundTrip_NotDoubleEncoded()
    {
        var once = Evaluate("<i id='t' data-x='a&amp;b'></i>", "document.getElementById('t').outerHTML");

        Assert.Equal("""<i id="t" data-x="a&amp;b"></i>""", once);
        Assert.Equal("a&b", Evaluate(once, "document.getElementById('t').getAttribute('data-x')"));
    }

    /// <summary>
    /// The renderer reads the same one level of escaping. It is the other consumer of these values
    /// and it used to decode them itself; this pins that it no longer does.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheRendererReadsTheSameDecodedValue()
    {
        // The attribute is spelled `&amp;amp;`, so one decode leaves the five-character value
        // `&amp;`. A <style> element's content is raw text and is never reference-decoded, so the
        // selector's `&amp;` is those same five characters and the two meet. A second decode of the
        // attribute would leave a bare `&`, and this would paint nothing.
        var html = """
<!DOCTYPE html>
<body style="margin:0;background:white">
<style>[data-x="&amp;"] { background: blue; width: 100px; height: 100px }</style>
<div data-x="&amp;amp;"></div>
</body>
""";
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 200);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.Equal((0, 0, 255), (pixel.R, pixel.G, pixel.B));
    }
}
