using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>HTMLTemplateElement.content</c> and <c>document.importNode()</c> — the pair every web
/// component reaches for, as <c>importNode(template.content, true)</c>.
/// <para>
/// REGRESSION GUARD (WPT issue #1491 problem 29): both were missing. <c>t.content</c> was
/// <c>undefined</c> and <c>importNode</c> was not a function, so the stamping idiom yielded nothing
/// and every component built an empty shadow root.
/// </para>
/// <para>
/// This fixture used to record a deliberate deviation here — the parser kept a template's children
/// as its own and <c>content</c> was a snapshot copy of them. That is no longer true: the children
/// are moved into the fragment at the end of the parse, as HTML §4.12.3 requires, and
/// <see cref="Content_Is_The_Templates_Own_Children_Not_A_Copy"/> is the assertion that used to pin
/// the deviation. Serialization still round-trips a template, which was the deviation's stated
/// reason: it reaches through to the fragment. See <c>TemplateContentTests</c> for the full
/// behaviour and <c>docs/broiler-js-gaps-closed.md</c> for what the copy cost.
/// </para>
/// </summary>
public sealed class TemplateContentAndImportNodeTests
{
    private static string Eval(string html, string script)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        return context.Eval(script).ToString();
    }

    private const string TemplateDoc =
        "<!DOCTYPE html><html><body>" +
        "<template id=\"t\"><style>li{color:red}</style><li class=\"item\">One</li></template>" +
        "<div id=\"host\"></div></body></html>";

    [Fact(Timeout = 600000)]
    public void Content_Exposes_The_Template_Children_As_A_Fragment()
    {
        Assert.Equal("2", Eval(TemplateDoc, "document.getElementById('t').content.childNodes.length"));
        Assert.Equal("object", Eval(TemplateDoc, "typeof document.getElementById('t').content"));
    }

    [Fact(Timeout = 600000)]
    public void Content_Returns_The_Same_Fragment_Every_Time()
    {
        // A component may populate `content` before stamping it, so the fragment has to be stable
        // rather than minted fresh per access.
        Assert.Equal("true", Eval(TemplateDoc,
            "var t = document.getElementById('t'); t.content === t.content"));
    }

    [Fact(Timeout = 600000)]
    public void Content_Is_The_Templates_Own_Children_Not_A_Copy()
    {
        // This assertion is the inverse of what it was. It read `2` — the template kept its own
        // children and `content` held a copy of them — which pinned a deviation from HTML §4.12.3
        // rather than the specification. The parser now moves them into the fragment, so the element
        // is childless and the fragment IS the children.
        Assert.Equal("0", Eval(TemplateDoc, "document.getElementById('t').childNodes.length"));
        Assert.Equal("2", Eval(TemplateDoc, "document.getElementById('t').content.childNodes.length"));

        // The deviation's stated reason was serialization round-tripping; it still does.
        Assert.Contains("<li class=\"item\">One</li>", Eval(TemplateDoc,
            "document.getElementById('t').outerHTML"));
    }

    [Fact(Timeout = 600000)]
    public void ImportNode_Deep_Copies_The_Whole_Subtree()
    {
        Assert.Equal("2", Eval(TemplateDoc,
            "document.importNode(document.getElementById('t').content, true).childNodes.length"));
    }

    [Fact(Timeout = 600000)]
    public void ImportNode_Without_Deep_Copies_The_Node_Alone()
    {
        // `deep` defaults to false, so a bare importNode() takes the node without its children.
        Assert.Equal("0", Eval(TemplateDoc,
            "document.importNode(document.getElementById('t').content).childNodes.length"));
    }

    [Fact(Timeout = 600000)]
    public void ImportNode_Yields_A_Copy_Not_The_Original()
    {
        Assert.Equal("false", Eval(TemplateDoc,
            "var c = document.getElementById('t').content; document.importNode(c, true) === c"));
    }

    [Fact(Timeout = 600000)]
    public void The_Stamping_Idiom_Populates_A_Shadow_Root()
    {
        // The whole point: what every component in WPT's shadow-dom tests does.
        Assert.Equal("2", Eval(TemplateDoc,
            "var r = document.getElementById('host').attachShadow({mode:'open'});" +
            "r.appendChild(document.importNode(document.getElementById('t').content, true));" +
            "r.childNodes.length"));
    }

    [Fact(Timeout = 600000)]
    public void The_Stamped_Copy_Keeps_Its_Classes_So_Selectors_Still_Match()
    {
        Assert.Equal("item", Eval(TemplateDoc,
            "var f = document.importNode(document.getElementById('t').content, true);" +
            "f.querySelector('li').className"));
    }
}
