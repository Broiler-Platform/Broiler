using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>Element.dataset</c> — the HTML <c>DOMStringMap</c> view over an element's <c>data-*</c>
/// attributes (HTML §3.2.6.6).
/// <para>
/// It did not exist, and a missing object is not a quiet failure: reading through it throws, and a
/// thrown error aborts the whole <c>&lt;script&gt;</c>. google.com's async-request module — the code
/// that runs when a search is issued — reads <c>b.dataset.ved</c> and writes ids back through the
/// same map, so its absence stopped that script with <c>Cannot set property eqid of undefined</c>
/// and took every later statement with it.
/// </para>
/// </summary>
public sealed class DatasetTests
{
    private static (JSContext Context, DomBridge Bridge) Attach(string body)
    {
        var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, $"<!doctype html><html><body>{body}</body></html>", "https://example.com/");
        return (context, bridge);
    }

    private static string Eval(JSContext context, string expression) =>
        context.Eval($"String({expression})").ToString();

    /// <summary>
    /// The reported failure, reduced: a property written through <c>dataset</c> onto an element that
    /// carries no such attribute yet. This is the case a snapshot object cannot serve — the write
    /// has to reach the element, not a throwaway copy — so it asserts the attribute afterwards.
    /// </summary>
    [Fact]
    public void WritingANewKey_ReachesTheElementAsADataAttribute()
    {
        var (context, bridge) = Attach("<div id='t'></div>");
        using var _ = context;
        using var __ = bridge;

        var thrown = Record.Exception(() => context.Eval("document.getElementById('t').dataset.eqid = 'q1';"));

        Assert.Null(thrown);
        Assert.Equal("q1", Eval(context, "document.getElementById('t').getAttribute('data-eqid')"));
        Assert.Equal("q1", Eval(context, "document.getElementById('t').dataset.eqid"));
    }

    /// <summary>An attribute the markup carries is readable without anything having written it.</summary>
    [Fact]
    public void MarkupAttributes_AreReadable()
    {
        var (context, bridge) = Attach("<div id='t' data-ved='abc' data-eqid='q1'></div>");
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("abc", Eval(context, "document.getElementById('t').dataset.ved"));
        Assert.Equal("q1", Eval(context, "document.getElementById('t').dataset.eqid"));
    }

    /// <summary>
    /// The name mapping runs both ways: <c>dataset.fooBar</c> is <c>data-foo-bar</c>, and an
    /// attribute set directly is visible under its camel-cased name.
    /// </summary>
    [Fact]
    public void NamesMapBetweenCamelCaseAndDashes_InBothDirections()
    {
        var (context, bridge) = Attach("<div id='t'></div>");
        using var _ = context;
        using var __ = bridge;

        context.Eval("document.getElementById('t').dataset.fooBar = 'k';");
        Assert.Equal("k", Eval(context, "document.getElementById('t').getAttribute('data-foo-bar')"));

        context.Eval("document.getElementById('t').setAttribute('data-a-b', 'v');");
        Assert.Equal("v", Eval(context, "document.getElementById('t').dataset.aB"));
    }

    /// <summary>
    /// The map enumerates. <c>Object.keys</c>, the spread operator and <c>JSON.stringify</c> all
    /// filter the key list through <c>getOwnPropertyDescriptor</c>, so a map that answers only
    /// <c>ownKeys</c> enumerates as empty — which is why both are asserted.
    /// </summary>
    [Fact]
    public void TheMapEnumerates_AndSerializes()
    {
        var (context, bridge) = Attach("<div id='t' data-one='1' data-two-part='2' class='c'></div>");
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("one,twoPart", Eval(context, "Object.keys(document.getElementById('t').dataset).sort().join(',')"));
        Assert.Equal("1", Eval(context, "JSON.parse(JSON.stringify(document.getElementById('t').dataset)).one"));
    }

    /// <summary>Non-<c>data-</c> attributes are not part of the map.</summary>
    [Fact]
    public void OtherAttributes_AreNotInTheMap()
    {
        var (context, bridge) = Attach("<div id='t' data-one='1' title='hello'></div>");
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("false", Eval(context, "'title' in document.getElementById('t').dataset"));
        Assert.Equal("undefined", Eval(context, "typeof document.getElementById('t').dataset.title"));
    }

    /// <summary><c>in</c> distinguishes a present key from an absent one.</summary>
    [Fact]
    public void MembershipIsAnswered()
    {
        var (context, bridge) = Attach("<div id='t' data-ved='abc'></div>");
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("true", Eval(context, "'ved' in document.getElementById('t').dataset"));
        Assert.Equal("false", Eval(context, "'nope' in document.getElementById('t').dataset"));
    }

    /// <summary>Deleting through the map removes the attribute from the element.</summary>
    [Fact]
    public void DeletingAKey_RemovesTheAttribute()
    {
        var (context, bridge) = Attach("<div id='t' data-ved='abc'></div>");
        using var _ = context;
        using var __ = bridge;

        context.Eval("delete document.getElementById('t').dataset.ved;");

        Assert.Equal("null", Eval(context, "document.getElementById('t').getAttribute('data-ved')"));
        Assert.Equal("false", Eval(context, "'ved' in document.getElementById('t').dataset"));
    }

    /// <summary>
    /// An empty attribute value is a value, not an absence — the distinction the traps have to keep
    /// so <c>data-x=""</c> reads as <c>""</c> and enumerates, rather than looking like no attribute.
    /// </summary>
    [Fact]
    public void AnEmptyValue_IsAValueNotAnAbsence()
    {
        var (context, bridge) = Attach("<div id='t' data-empty=''></div>");
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("", Eval(context, "document.getElementById('t').dataset.empty"));
        Assert.Equal("true", Eval(context, "'empty' in document.getElementById('t').dataset"));
        Assert.Equal("empty", Eval(context, "Object.keys(document.getElementById('t').dataset).join(',')"));
    }

    /// <summary>
    /// The map is live and singular: it reads the element's attributes on every access, so a change
    /// made through <c>setAttribute</c> is visible through a reference taken beforehand, and the
    /// element hands back the same object each time as a browser does.
    /// </summary>
    [Fact]
    public void TheMapIsLive_AndTheSameObjectEachTime()
    {
        var (context, bridge) = Attach("<div id='t' data-n='1'></div>");
        using var _ = context;
        using var __ = bridge;

        context.Eval("globalThis.held = document.getElementById('t').dataset;");
        context.Eval("document.getElementById('t').setAttribute('data-n', '2');");

        Assert.Equal("2", Eval(context, "globalThis.held.n"));
        Assert.Equal("true", Eval(context, "document.getElementById('t').dataset === document.getElementById('t').dataset"));
    }

    /// <summary>An element created by script has the map too, not only one parsed from markup.</summary>
    [Fact]
    public void ACreatedElement_HasTheMap()
    {
        var (context, bridge) = Attach("");
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("object", Eval(context, "typeof document.createElement('div').dataset"));
        Assert.Equal("v", Eval(context, "(function(){var e = document.createElement('div'); e.dataset.k = 'v'; return e.getAttribute('data-k');})()"));
    }
}
