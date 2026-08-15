using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>document.getElementsByName(name)</c> — HTML §3.1.5: the elements whose <c>name</c>
/// <em>attribute</em> is identical to the argument, in tree order.
/// <para>
/// It was not implemented at all, and a missing method on the document reads as
/// <see langword="undefined"/> rather than as absent, so calling it threw
/// <c>TypeError: undefined is not a function</c> — which aborts the whole <c>&lt;script&gt;</c>, not
/// the one statement.
/// </para>
/// <para>
/// google.com's homepage bundle locates its search form this way, having looked for a form by id
/// first: <c>c=["f","gs"];for(var d=0;b=c[d++];)if(b=document.getElementsByName(b)[0])return b</c>.
/// Failing to find the form is not a cosmetic loss — it is the search box.
/// </para>
/// </summary>
public sealed class DocumentGetElementsByNameTests
{
    private const string Page = """
        <!doctype html><html><body>
          <form name="gs" id="searchform">
            <input name="q" id="first-q">
            <input name="q" id="second-q">
            <textarea name="q" id="third-q"></textarea>
            <input name="btnK">
            <input id="no-name">
            <input name="Q" id="capital-q">
          </form>
          <div name="gs" id="decoy-div"></div>
          <a name="anchor-style" id="named-anchor"></a>
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

    private static string IdsOf(JSContext context, string name) => Eval(context, $$"""
        (function() {
            var found = document.getElementsByName('{{name}}');
            return Array.prototype.map.call(found, function(el) { return el.id; }).join(',');
        })()
        """);

    /// <summary>The homepage bundle's form lookup, run as it spells it.</summary>
    [Fact]
    public void GetElementsByName_FindsTheSearchForm_TheWayTheHomepageBundleDoes()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var thrown = Record.Exception(() => context.Eval("""
            globalThis.__form = (function() {
                var b, c = ['f', 'gs'];
                for (var d = 0; b = c[d++];)
                    if (b = document.getElementsByName(b)[0]) return b;
                return null;
            })();
            """));

        Assert.Null(thrown);
        Assert.Equal("searchform", Eval(context, "String(globalThis.__form && globalThis.__form.id)"));
    }

    /// <summary>Every element carrying the attribute, in tree order — not just the first, not just controls.</summary>
    [Fact]
    public void GetElementsByName_ReturnsEveryMatchInTreeOrder()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("first-q,second-q,third-q", IdsOf(context, "q"));
    }

    /// <summary>
    /// Not confined to form controls: HTML says "all the HTML elements" with the attribute, so a
    /// <c>div</c> or an anchor carrying one counts.
    /// </summary>
    [Theory]
    [InlineData("gs", "searchform,decoy-div")]
    [InlineData("anchor-style", "named-anchor")]
    [InlineData("btnK", "")]
    public void GetElementsByName_MatchesAnyElementCarryingTheAttribute(string name, string expectedIds)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        // 'btnK' is present but its element has no id, so the joined ids are empty while the match
        // itself is not — length is asserted separately below.
        if (expectedIds.Length > 0)
            Assert.Equal(expectedIds, IdsOf(context, name));
        else
            Assert.Equal("1", Eval(context, $"String(document.getElementsByName('{name}').length)"));
    }

    /// <summary>The value is matched exactly — HTML compares it identically, so case matters.</summary>
    [Fact]
    public void GetElementsByName_ComparesTheValueCaseSensitively()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("first-q,second-q,third-q", IdsOf(context, "q"));
        Assert.Equal("capital-q", IdsOf(context, "Q"));
    }

    /// <summary>A name nothing carries is an empty result, not a throw and not a null.</summary>
    [Theory]
    [InlineData("nothing-has-this")]
    [InlineData("")]
    public void GetElementsByName_WithNoMatch_IsEmpty(string name)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("0", Eval(context, $"String(document.getElementsByName('{name}').length)"));
    }

    /// <summary>It is a function on the document, alongside the query methods it sits with.</summary>
    [Fact]
    public void GetElementsByName_IsRegisteredOnTheDocument()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("function", Eval(context, "typeof document.getElementsByName"));
        Assert.Equal("function", Eval(context, "typeof document.getElementsByClassName"));
        Assert.Equal("function", Eval(context, "typeof document.getElementsByTagName"));
    }
}
