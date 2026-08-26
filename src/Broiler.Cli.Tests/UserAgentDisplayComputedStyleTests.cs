namespace Broiler.Cli.Tests;

/// <summary>
/// The user-agent stylesheet's <c>display</c>, as <c>getComputedStyle</c> reports it.
/// </summary>
/// <remarks>
/// <para>
/// Nothing the UA stylesheet said about <c>display</c> reached script: every element whose display
/// comes from the UA sheet rather than an author rule answered <c>inline</c>, the CSS initial value.
/// A plain <c>&lt;div&gt;</c> answered <c>inline</c> rather than <c>block</c>, a <c>&lt;table&gt;</c>
/// <c>inline</c> rather than <c>table</c>, and a <c>&lt;script&gt;</c> or <c>&lt;head&gt;</c>
/// <c>inline</c> rather than <c>none</c> — all 32 tags below gave the same answer. Rendering was
/// never affected, so this was a CSSOM gap alone: the renderer reads the box tree, and the bridge's
/// own internal consumers read the sparse projection that carries the UA seed.
/// </para>
/// <para>
/// Every expectation below is Chromium's measured answer to the same markup, from one probe run
/// against both. Two deliberate divergences are pinned separately at the bottom: both are gaps in
/// the shared tag→display table rather than in the path that now reads it.
/// </para>
/// </remarks>
public class UserAgentDisplayComputedStyleTests
{
    private static string Display(string setup, string report)
    {
        var html = $@"<!DOCTYPE html><html><head><title>Test</title></head><body>
<div id=""result""></div>
<script>
function disp(el) {{ return window.getComputedStyle(el).display; }}
function make(tag) {{ var e = document.createElement(tag); document.body.appendChild(e); return e; }}
{setup}
document.getElementById('result').textContent = {report};
</script></body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");
    }

    /// <summary>
    /// The block-level and inline-level defaults — the everyday case, and the one a page reads when
    /// it asks whether an element is visible before touching <c>style.display</c>.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("div", "block")]
    [InlineData("p", "block")]
    [InlineData("h1", "block")]
    [InlineData("section", "block")]
    [InlineData("article", "block")]
    [InlineData("ul", "block")]
    [InlineData("form", "block")]
    [InlineData("fieldset", "block")]
    [InlineData("legend", "block")]
    [InlineData("details", "block")]
    [InlineData("span", "inline")]
    [InlineData("a", "inline")]
    [InlineData("em", "inline")]
    [InlineData("label", "inline")]
    [InlineData("br", "inline")]
    [InlineData("img", "inline")]
    [InlineData("button", "inline-block")]
    [InlineData("input", "inline-block")]
    [InlineData("textarea", "inline-block")]
    [InlineData("select", "inline-block")]
    public void AnElementReportsItsUserAgentDisplay(string tag, string expected)
        => Assert.Contains($">{expected}<", Display($"var e = make('{tag}');", "disp(e)"));

    /// <summary>
    /// The elements the UA stylesheet hides. These are the ones the earlier record in
    /// <c>NoscriptRenderingTests</c> named — it reports <c>inline</c> "for a <c>&lt;script&gt;</c>
    /// too, and for every other element the UA stylesheet hides".
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("script", "none")]
    [InlineData("style", "none")]
    [InlineData("head", "none")]
    [InlineData("title", "none")]
    [InlineData("template", "none")]
    public void AHiddenElementReportsNone(string tag, string expected)
        => Assert.Contains($">{expected}<", Display($"var e = make('{tag}');", "disp(e)"));

    /// <summary>The table display types, which are neither block nor inline and so were the furthest
    /// from the value that was reported.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("table", "table")]
    [InlineData("thead", "table-header-group")]
    [InlineData("tr", "table-row")]
    [InlineData("td", "table-cell")]
    public void ATablePartReportsItsTableDisplay(string tag, string expected)
        => Assert.Contains($">{expected}<", Display(
            $"var t = make('table'); var e = document.createElement('{tag}'); t.appendChild(e);",
            "disp(e)"));

    /// <summary>A list item computes to <c>list-item</c>, not to <c>block</c>.</summary>
    [Fact(Timeout = 600000)]
    public void AListItemReportsListItem()
        => Assert.Contains(">list-item<", Display(
            "var u = make('ul'); var e = document.createElement('li'); u.appendChild(e);", "disp(e)"));

    /// <summary>
    /// The seed is non-clobbering, which is what keeps it a <em>default</em>: an author rule and an
    /// inline style both still win, and the UA value is only what an undeclared <c>display</c> falls
    /// back to.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnAuthorOrInlineDisplayStillWins()
        => Assert.Contains(">flex|none|block<", Display(
            "var s = document.createElement('style');"
            + "s.textContent = '#authored { display: flex }';"
            + "document.head.appendChild(s);"
            + "var a = make('div'); a.id = 'authored';"
            + "var b = make('div'); b.style.display = 'none';"
            + "var c = make('div');",
            "[disp(a), disp(b), disp(c)].join('|')"));

    /// <summary>
    /// HTML §15.3 <c>[hidden]</c>: the attribute is a UA <c>display: none</c> rule, and it loses to
    /// an author declaration in exactly the same way.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheHiddenAttributeReportsNoneAndStillLosesToAnAuthorRule()
        => Assert.Contains(">none|block<", Display(
            "var s = document.createElement('style');"
            + "s.textContent = '#shown { display: block }';"
            + "document.head.appendChild(s);"
            + "var h = make('div'); h.setAttribute('hidden', '');"
            + "var v = make('div'); v.setAttribute('hidden', ''); v.id = 'shown';",
            "[disp(h), disp(v)].join('|')"));

    /// <summary>
    /// The two tags where Broiler and Chromium still differ, pinned so that changing either is
    /// deliberate. Both are gaps in the shared <c>CssUserAgentDefaults.DisplayValues</c> table — the
    /// same table the renderer reads — rather than in the path that now surfaces it, and both
    /// predate this change; they were simply invisible while every element answered <c>inline</c>.
    /// <c>&lt;option&gt;</c> is absent from the table, so it falls back to the CSS initial value
    /// where Chromium says <c>block</c>. <c>&lt;summary&gt;</c> is in it as <c>list-item</c>
    /// unconditionally, but HTML §15.3.9 scopes that to <c>details &gt; summary:first-of-type</c>:
    /// Chromium answers <c>list-item</c> for a summary inside a <c>&lt;details&gt;</c> — which the
    /// flat table now gets right — and <c>block</c> for a bare one, which it cannot express.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheTwoTagsWhereTheTableDivergesFromChromiumArePinned()
        => Assert.Contains(">inline|list-item|list-item<", Display(
            "var s = make('select'); var o = document.createElement('option'); s.appendChild(o);"
            + "var d = make('details'); var inner = document.createElement('summary');"
            + "d.appendChild(inner);"
            + "var bare = make('summary');",
            "[disp(o), disp(inner), disp(bare)].join('|')"));
}
