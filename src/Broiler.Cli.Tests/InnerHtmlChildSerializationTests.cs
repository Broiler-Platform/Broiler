using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>innerHTML</c>'s read side serializes every child, not only the element ones.
/// </summary>
/// <remarks>
/// <para>
/// It filtered its child list with <c>.OfType&lt;DomElement&gt;()</c>, so every text and comment
/// child was dropped: <c>&lt;div&gt;ab&lt;b&gt;c&lt;/b&gt;d&lt;!--k--&gt;&lt;/div&gt;</c> read back
/// as <c>"&lt;b&gt;c&lt;/b&gt;"</c>, and a div holding nothing but text read back as the empty
/// string while its own <c>textContent</c>, <c>childNodes</c> and <c>outerHTML</c> all reported the
/// text correctly. The value is wrong rather than absent, which is what let it sit unnoticed.
/// </para>
/// <para>
/// The filter is a leftover from the facade era, when a text child was a string on its parent's
/// element record rather than a node; construction has produced canonical <c>DomText</c> and
/// <c>DomComment</c> children for some time. <c>outerHTML</c> never had the bug because it hands the
/// whole subtree to the serializer in one call instead of re-serializing children one at a time —
/// so the two accessors disagreed about the same tree, which is the sharpest way to state it.
/// </para>
/// </remarks>
public sealed class InnerHtmlChildSerializationTests
{
    private static DomBridge Attach(out JSContext context, string body)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, $"<!DOCTYPE html><html><body>{body}</body></html>", "https://example.com/i.html");
        return bridge;
    }

    [Fact(Timeout = 600000)]
    public void InnerHtml_Keeps_Text_And_Comment_Children()
    {
        using var bridge = Attach(out var context, "<div id=a>ab<b>c</b>d<!--k--></div>");

        Assert.Equal(
            "ab<b>c</b>d<!--k-->",
            context.Eval("document.getElementById('a').innerHTML").ToString());
    }

    /// <summary>An element whose only children are text read back as the empty string — the loudest
    /// shape of the same bug, and the one a page is most likely to hit.</summary>
    [Fact(Timeout = 600000)]
    public void InnerHtml_Of_A_Text_Only_Element_Is_Its_Text()
    {
        using var bridge = Attach(out var context, "<div id=a>hello</div>");

        Assert.Equal("hello", context.Eval("document.getElementById('a').innerHTML").ToString());
        Assert.Equal(
            "xy",
            context.Eval("(() => { var e = document.createElement('div'); e.textContent = 'xy'; return e.innerHTML; })()")
                .ToString());
    }

    /// <summary>The two accessors read the same tree and must agree about it; only one of them was
    /// wrong.</summary>
    [Fact(Timeout = 600000)]
    public void InnerHtml_And_OuterHtml_Agree()
    {
        using var bridge = Attach(out var context, "<div id=a>ab<b>c</b>d</div>");

        Assert.Equal(
            "true",
            context.Eval("""
                (() => {
                    var a = document.getElementById('a');
                    return String(a.outerHTML === '<div id="a">' + a.innerHTML + '</div>');
                })()
                """).ToString());
    }

    /// <summary>Text is HTML-escaped on the way out, and a raw-text element's content is not — the
    /// same rule the whole-subtree serializer already applied, now reached through this path too.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void InnerHtml_Escapes_Text_Except_Inside_A_Raw_Text_Element()
    {
        using var bridge = Attach(out var context, "<div id=a></div><script id=s>if (1 < 2) {}</script>");

        Assert.Equal(
            "a &lt;b&gt; &amp; c",
            context.Eval("(() => { var a = document.getElementById('a'); a.textContent = 'a <b> & c'; return a.innerHTML; })()")
                .ToString());
        Assert.Equal("if (1 < 2) {}", context.Eval("document.getElementById('s').innerHTML").ToString());
    }

    /// <summary>A round trip through the accessor must not lose the text it just wrote.</summary>
    [Fact(Timeout = 600000)]
    public void InnerHtml_Round_Trips()
    {
        using var bridge = Attach(out var context, "<div id=a></div>");

        Assert.Equal(
            "one <em>two</em> three",
            context.Eval("(() => { var a = document.getElementById('a'); a.innerHTML = 'one <em>two</em> three'; return a.innerHTML; })()")
                .ToString());
    }
}
