using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>form.q</c> — <c>HTMLFormElement</c>'s named getter (HTML §4.10.3): an unknown property name on
/// a form resolves to the control carrying that name.
/// <para>
/// <c>form.elements</c> already offered named access; the form itself did not. The miss does not
/// announce itself either, which is what makes it expensive: google.com's homepage takes the form,
/// reads <c>u=r.q</c> from it, and hands the result to its search-box component, which stores it and
/// only later reads <c>F.value</c> — so the throw, <c>Cannot get property value of undefined</c>,
/// surfaces in a component several calls away from the lookup that actually failed.
/// </para>
/// </summary>
public sealed class FormNamedAccessTests
{
    private const string Page = """
        <!doctype html><html><body>
          <form name="f" id="searchform" action="/search">
            <input name="q" id="query" value="broiler">
            <input name="btnK" id="submit-btn" type="submit" value="Search">
            <select name="picker" id="picker-el"><option>one</option></select>
            <textarea name="notes" id="notes-el">text</textarea>
            <input name="action" id="named-action" value="decoy">
            <input id="only-an-id" value="no-name">
          </form>
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

    /// <summary>The homepage's own sequence: find the form by name, then take its q control.</summary>
    [Fact]
    public void FormNamedAccess_YieldsTheControl_TheWayTheHomepageTakesIt()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        var thrown = Record.Exception(() => context.Eval("""
            globalThis.__value = (function() {
                var r = document.getElementsByName('f')[0];
                var u = r.q;
                return u.value;
            })();
            """));

        Assert.Null(thrown);
        Assert.Equal("broiler", Eval(context, "String(globalThis.__value)"));
    }

    /// <summary>Every kind of listed control is reachable by its name, not just text inputs.</summary>
    [Theory]
    [InlineData("q", "query")]
    [InlineData("btnK", "submit-btn")]
    [InlineData("picker", "picker-el")]
    [InlineData("notes", "notes-el")]
    public void FormNamedAccess_ReachesEveryControlKind(string name, string expectedId)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal(expectedId, Eval(context, $"String(document.getElementById('searchform').{name}.id)"));
    }

    /// <summary>
    /// The named getter is a fallback, not an override. WebIDL consults named properties only when
    /// the object does not already answer, so a control named <c>action</c> must not displace the
    /// form's own <c>action</c>.
    /// </summary>
    [Fact]
    public void FormNamedAccess_DoesNotShadowTheFormsOwnMembers()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("/search", Eval(context, "String(document.getElementById('searchform').action)"));
        Assert.Equal("object", Eval(context, "typeof document.getElementById('searchform').elements"));
        Assert.Equal("number", Eval(context, "typeof document.getElementById('searchform').length"));
    }

    /// <summary>
    /// A name nothing carries stays undefined — an absent named property is an absent property, and
    /// returning null instead would change what <c>typeof</c> reports and break feature detection.
    /// The controls collection keeps its own contract, where a miss is null.
    /// </summary>
    [Fact]
    public void FormNamedAccess_WithNoSuchName_IsUndefined()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("undefined", Eval(context, "typeof document.getElementById('searchform').nothingIsNamedThis"));
        Assert.Equal("object", Eval(context, "typeof document.getElementById('searchform').elements.nothingIsNamedThis"));
    }

    /// <summary>The form and its collection agree, since one lookup answers for both.</summary>
    [Theory]
    [InlineData("q")]
    [InlineData("btnK")]
    [InlineData("notes")]
    public void FormAndItsElementsCollectionAgree(string name)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("true", Eval(context, $"String(document.getElementById('searchform').{name} === document.getElementById('searchform').elements.{name})"));
    }

    /// <summary>
    /// Matching is on the <c>name</c> attribute. A control with only an id is not reachable this way
    /// in Broiler — HTML would also expose it by id, which this pins as a known limit rather than
    /// leaving it to be discovered.
    /// </summary>
    [Fact]
    public void FormNamedAccess_MatchesTheNameAttribute()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("named-action", Eval(context, "String(document.getElementById('searchform')['action'] && document.getElementById('searchform').elements['action'].id)"));
        Assert.Equal("undefined", Eval(context, "typeof document.getElementById('searchform')['only-an-id']"));
    }
}
