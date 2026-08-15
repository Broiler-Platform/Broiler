using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>getElementsByClassName</c> is defined on <c>Element</c> as well as on <c>Document</c>
/// (DOM §4.9), and only the document half was registered.
/// <para>
/// On an element it was therefore <see langword="undefined"/>, and calling it threw
/// <c>TypeError: undefined is not a function</c> — which aborts the entire
/// <c>&lt;script&gt;</c>, not the one statement. Scoping a class lookup to a container is how pages
/// narrow a search, so this was not an obscure spelling: google.com's One-Google-bar bundle writes
/// <c>d.getElementsByClassName("gb_C")[0]||d</c>, and died there once
/// <c>document.location</c> let it get that far.
/// </para>
/// </summary>
public sealed class ElementGetElementsByClassNameTests
{
    private const string Page = """
        <!doctype html><html><body>
          <div id="root" class="gb_C">
            <span id="a" class="gb_C"></span>
            <span id="b" class="other gb_C trailing"></span>
            <div id="nested"><span id="c" class="gb_C"></span></div>
            <span id="d" class="gb_Cx"></span>
            <span id="e" class="other"></span>
            <span id="multi" class="alpha beta"></span>
          </div>
          <span id="outside" class="gb_C"></span>
        </body></html>
        """;

    private static (JSContext Context, DomBridge Bridge) Attach()
    {
        var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, Page, "https://www.google.com/");
        return (context, bridge);
    }

    private static string Eval(JSContext context, string expression) => context.Eval(expression).ToString();

    /// <summary>The One-Google-bar lookup, run as the bundle spells it.</summary>
    [Fact]
    public void ElementGetElementsByClassName_ScopesTheLookup_TheWayTheOneGoogleBarDoes()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var thrown = Record.Exception(() => context.Eval("""
            var d = document.getElementById('root');
            globalThis.__picked = (d.getElementsByClassName('gb_C')[0] || d).id;
            """));

        Assert.Null(thrown);
        Assert.Equal("a", Eval(context, "String(globalThis.__picked)"));
    }

    /// <summary>
    /// Descendants only, in document order — the element itself carries the class and must not be in
    /// its own result, and an element outside the subtree is out of scope.
    /// </summary>
    [Fact]
    public void ElementGetElementsByClassName_ReturnsDescendantsInDocumentOrder()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var ids = Eval(context, """
            (function() {
                var found = document.getElementById('root').getElementsByClassName('gb_C');
                return Array.prototype.map.call(found, function(el) { return el.id; }).join(',');
            })()
            """);

        // 'root' itself is excluded, 'outside' is out of the subtree, 'd' is a different class
        // ('gb_Cx' — a prefix, not the same name), and 'c' is found through a nested parent.
        Assert.Equal("a,b,c", ids);
    }

    /// <summary>The argument is a set of names: an element must carry every one of them.</summary>
    [Theory]
    [InlineData("alpha", "multi")]
    [InlineData("beta", "multi")]
    [InlineData("alpha beta", "multi")]
    [InlineData("beta alpha", "multi")]
    [InlineData("alpha gamma", "")]
    [InlineData("gb_Cx", "d")]
    [InlineData("nomatch", "")]
    [InlineData("", "")]
    public void ElementGetElementsByClassName_RequiresEveryNameInTheSet(string query, string expectedIds)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var ids = Eval(context, $$"""
            (function() {
                var found = document.getElementById('root').getElementsByClassName('{{query}}');
                return Array.prototype.map.call(found, function(el) { return el.id; }).join(',');
            })()
            """);

        Assert.Equal(expectedIds, ids);
    }

    /// <summary>
    /// The document half keeps working, and the element half agrees with it when the search is rooted
    /// at an ancestor of every match — body's descendants are the whole set here (root, a, b, c and
    /// outside all carry the class). Rooting at <c>#root</c> is what narrows it, and that is the
    /// difference the element half exists to express.
    /// </summary>
    [Fact]
    public void DocumentAndElementHalvesAgree()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("5", Eval(context, "String(document.getElementsByClassName('gb_C').length)"));
        Assert.Equal("5", Eval(context, "String(document.body.getElementsByClassName('gb_C').length)"));
        Assert.Equal("3", Eval(context, "String(document.getElementById('root').getElementsByClassName('gb_C').length)"));
    }

    /// <summary>
    /// The document half takes the same set of names. It used to read its argument as a single class,
    /// so a multi-class query matched only an element whose class attribute was that literal string —
    /// which is to say nothing.
    /// </summary>
    [Theory]
    [InlineData("alpha", "multi")]
    [InlineData("alpha beta", "multi")]
    [InlineData("beta alpha", "multi")]
    [InlineData("alpha  beta", "multi")]
    [InlineData("alpha gamma", "")]
    [InlineData("gb_C", "root,a,b,c,outside")]
    [InlineData("other gb_C", "b")]
    [InlineData("", "")]
    public void DocumentGetElementsByClassName_RequiresEveryNameInTheSet(string query, string expectedIds)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var ids = Eval(context, $$"""
            (function() {
                var found = document.getElementsByClassName('{{query}}');
                return Array.prototype.map.call(found, function(el) { return el.id; }).join(',');
            })()
            """);

        Assert.Equal(expectedIds, ids);
    }

    /// <summary>
    /// The two halves answer the same question the same way. Rooted at an ancestor of the whole
    /// document, the element half must agree with the document half name for name — that agreement is
    /// what the shared class-set rule exists to guarantee.
    /// </summary>
    [Theory]
    [InlineData("gb_C")]
    [InlineData("alpha beta")]
    [InlineData("other")]
    [InlineData("nomatch")]
    public void BothHalvesAgreeWhenRootedAtTheDocumentElement(string query)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var ids = Eval(context, $$"""
            (function() {
                function idsOf(c) {
                    return Array.prototype.map.call(c, function(el) { return el.id; }).join(',');
                }
                return idsOf(document.getElementsByClassName('{{query}}')) + '/' +
                       idsOf(document.documentElement.getElementsByClassName('{{query}}'));
            })()
            """);

        var halves = ids.Split('/');
        Assert.Equal(halves[0], halves[1]);
    }

    /// <summary>It is a function on every element wrapper, not only on the one the page happened to ask.</summary>
    [Fact]
    public void EveryElementCarriesTheMethod()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("function", Eval(context, "typeof document.body.getElementsByClassName"));
        Assert.Equal("function", Eval(context, "typeof document.documentElement.getElementsByClassName"));
        Assert.Equal("function", Eval(context, "typeof document.createElement('div').getElementsByClassName"));
        Assert.Equal("function", Eval(context, "typeof document.getElementById('e').getElementsByClassName"));
    }
}
