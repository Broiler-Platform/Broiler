namespace Broiler.Cli.Tests;

/// <summary>
/// A <c>&lt;template&gt;</c>'s children live in its contents fragment, not in its own child list
/// (HTML §4.12.3) — the parser puts them there, and the element itself is left childless.
/// </summary>
/// <remarks>
/// <para>
/// The fragment used to be built from a deep <em>copy</em> of children that stayed in the tree, and
/// the consequences went beyond the two sides disagreeing. A template's contents were reachable from
/// the document, so <c>t.querySelector('.row')</c> found them where a browser answers <c>null</c>,
/// and a page walking itself processed markup it was meant to stamp later. Writing <c>t.innerHTML</c>
/// rewrote the element's children while <c>content</c> kept the cached copy, so building a template
/// dynamically and then stamping it produced the <b>old</b> markup with nothing to say the write had
/// gone elsewhere.
/// </para>
/// <para>
/// <b>Every expectation is a Chromium answer</b> taken through Playwright.
/// </para>
/// </remarks>
public class TemplateContentTests
{
    private static string Run(string body, string script)
    {
        var html = $"<!doctype html><html><body>{body}<div id=\"result\"></div>\n<script>\n{script}\n</script></body></html>";
        var serialized = CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
        const string start = "<div id=\"result\">";
        var open = serialized.IndexOf(start, StringComparison.Ordinal);
        Assert.True(open >= 0, $"probe did not run; document was:\n{serialized}");
        open += start.Length;
        var close = serialized.IndexOf("</div>", open, StringComparison.Ordinal);
        Assert.True(close > open, $"probe wrote nothing; document was:\n{serialized}");
        // The probe writes into textContent and this reads the serialized document back, so markup
        // in the result arrives escaped — correctly, and several of these assertions are about
        // markup. Undo exactly that escaping rather than asserting on the escaped spelling.
        return serialized[open..close]
            .Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&amp;", "&");
    }

    private const string Template = "<template id=\"t\"><div class=\"row\">hello</div><span>x</span></template>";

    /// <summary>The element is childless and the fragment holds the children.</summary>
    [Fact(Timeout = 600000)]
    public void The_Children_Are_In_The_Fragment_Not_The_Element()
    {
        var result = Run(Template, """
var t = document.getElementById('t');
document.getElementById('result').textContent = [
  t.childNodes.length, t.content.nodeType, t.content.childNodes.length,
  t.content.firstChild.className, t.content.querySelector('.row').textContent
].join('|');
""");
        Assert.Equal("0|11|2|row|hello", result);
    }

    /// <summary>
    /// The contents are not in the document, so a query over the template finds nothing — the
    /// symptom that made a page process markup it meant to stamp later.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Contents_Are_Not_Reachable_From_The_Document()
    {
        var result = Run(Template, """
var t = document.getElementById('t');
document.getElementById('result').textContent = [
  String(t.querySelector('.row')),
  String(document.querySelector('.row')),
  document.getElementsByTagName('span').length
].join('|');
""");
        Assert.Equal("null|null|0", result);
    }

    /// <summary>The element owns the fragment, so a mutation through <c>content</c> is what
    /// <c>innerHTML</c> reads back.</summary>
    [Fact(Timeout = 600000)]
    public void The_Fragment_Is_Owned_Not_Copied()
    {
        var result = Run(Template, """
var t = document.getElementById('t');
t.content.firstChild.textContent = 'mutated';
document.getElementById('result').textContent = [
  t.content === t.content,
  t.content.firstChild.textContent,
  t.innerHTML.indexOf('mutated') >= 0
].join('|');
""");
        Assert.Equal("true|mutated|true", result);
    }

    /// <summary>
    /// Writing <c>innerHTML</c> replaces the contents, which is how a template is built dynamically.
    /// It used to write the element's children and leave <c>content</c> on the previous markup.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Writing_InnerHtml_Replaces_The_Contents()
    {
        var result = Run(Template, """
var t = document.getElementById('t');
t.innerHTML = '<p>new</p>';
document.getElementById('result').textContent = [
  t.content.childNodes.length, t.content.firstChild.tagName, t.childNodes.length, t.innerHTML
].join('|');
""");
        Assert.Equal("1|P|0|<p>new</p>", result);
    }

    /// <summary>The stamping idiom the whole interface exists for still works, and does not consume
    /// the fragment.</summary>
    [Fact(Timeout = 600000)]
    public void Stamping_The_Contents_Leaves_Them_Intact()
    {
        var result = Run(Template + "<div id=\"host\"></div><div id=\"host2\"></div>", """
var t = document.getElementById('t');
document.getElementById('host').appendChild(t.content.cloneNode(true));
document.getElementById('host2').appendChild(document.importNode(t.content, true));
document.getElementById('result').textContent = [
  document.getElementById('host').innerHTML,
  document.getElementById('host2').childNodes.length,
  t.content.childNodes.length
].join('|');
""");
        Assert.Equal("<div class=\"row\">hello</div><span>x</span>|2|2", result);
    }

    /// <summary>A template inside a template is diverted too, at both levels.</summary>
    [Fact(Timeout = 600000)]
    public void A_Nested_Template_Is_Diverted_As_Well()
    {
        var result = Run("<template id=\"outer\"><div><template id=\"inner\"><b>deep</b></template></div></template>", """
var outer = document.getElementById('outer');
var inner = outer.content.querySelector('template');
document.getElementById('result').textContent = [
  outer.childNodes.length, outer.content.childNodes.length,
  inner.childNodes.length, inner.content.childNodes.length, inner.content.firstChild.tagName
].join('|');
""");
        Assert.Equal("0|1|0|1|B", result);
    }

    /// <summary>
    /// A template the document did not parse — one built by <c>createElement</c> — is not diverted:
    /// only the parser diverts, so <c>appendChild</c> appends to the element as in a browser, while
    /// <c>innerHTML</c> still reaches the fragment.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Script_Built_Template_Appends_To_The_Element_And_InnerHtml_To_The_Fragment()
    {
        var result = Run("", """
var t = document.createElement('template');
t.appendChild(document.createElement('b'));
var afterAppend = t.childNodes.length + '/' + t.content.childNodes.length;
t.innerHTML = '<i>via innerHTML</i>';
document.getElementById('result').textContent =
  afterAppend + '|' + t.content.childNodes.length + '/' + t.content.firstChild.tagName;
""");
        Assert.Equal("1/0|1/I", result);
    }

    /// <summary>Serialization round-trips the contents: they are the template's markup even though
    /// they are not its children.</summary>
    [Fact(Timeout = 600000)]
    public void The_Contents_Round_Trip_Through_Serialization()
    {
        var result = Run(Template, """
var t = document.getElementById('t');
document.getElementById('result').textContent = t.outerHTML;
""");
        Assert.Equal("<template id=\"t\"><div class=\"row\">hello</div><span>x</span></template>", result);
    }
}
