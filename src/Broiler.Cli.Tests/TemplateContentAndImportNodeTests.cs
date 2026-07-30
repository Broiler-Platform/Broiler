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
/// A deliberate deviation, covered by <see cref="Content_Is_A_Snapshot_Not_A_Live_View"/>: the spec
/// has the parser move a template's children into the content fragment, leaving the element
/// childless, whereas Broiler's parser keeps them as children so a template round-trips through
/// serialization. <c>content</c> is therefore a snapshot copy of them. Stamping, querying and
/// populating-before-stamping all behave; reading one side after mutating the other does not.
/// Nothing renders either way, since template contents are inert.
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

    [Fact]
    public void Content_Exposes_The_Template_Children_As_A_Fragment()
    {
        Assert.Equal("2", Eval(TemplateDoc, "document.getElementById('t').content.childNodes.length"));
        Assert.Equal("object", Eval(TemplateDoc, "typeof document.getElementById('t').content"));
    }

    [Fact]
    public void Content_Returns_The_Same_Fragment_Every_Time()
    {
        // A component may populate `content` before stamping it, so the fragment has to be stable
        // rather than minted fresh per access.
        Assert.Equal("true", Eval(TemplateDoc,
            "var t = document.getElementById('t'); t.content === t.content"));
    }

    [Fact]
    public void Content_Is_A_Snapshot_Not_A_Live_View()
    {
        // Pins the documented deviation rather than the spec: the fragment is a copy, so the
        // template element keeps its own children and the two do not track each other.
        Assert.Equal("2", Eval(TemplateDoc, "document.getElementById('t').childNodes.length"));
    }

    [Fact]
    public void ImportNode_Deep_Copies_The_Whole_Subtree()
    {
        Assert.Equal("2", Eval(TemplateDoc,
            "document.importNode(document.getElementById('t').content, true).childNodes.length"));
    }

    [Fact]
    public void ImportNode_Without_Deep_Copies_The_Node_Alone()
    {
        // `deep` defaults to false, so a bare importNode() takes the node without its children.
        Assert.Equal("0", Eval(TemplateDoc,
            "document.importNode(document.getElementById('t').content).childNodes.length"));
    }

    [Fact]
    public void ImportNode_Yields_A_Copy_Not_The_Original()
    {
        Assert.Equal("false", Eval(TemplateDoc,
            "var c = document.getElementById('t').content; document.importNode(c, true) === c"));
    }

    [Fact]
    public void The_Stamping_Idiom_Populates_A_Shadow_Root()
    {
        // The whole point: what every component in WPT's shadow-dom tests does.
        Assert.Equal("2", Eval(TemplateDoc,
            "var r = document.getElementById('host').attachShadow({mode:'open'});" +
            "r.appendChild(document.importNode(document.getElementById('t').content, true));" +
            "r.childNodes.length"));
    }

    [Fact]
    public void The_Stamped_Copy_Keeps_Its_Classes_So_Selectors_Still_Match()
    {
        Assert.Equal("item", Eval(TemplateDoc,
            "var f = document.importNode(document.getElementById('t').content, true);" +
            "f.querySelector('li').className"));
    }
}
