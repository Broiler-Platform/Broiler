using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// A frame's document carries the same collection lookups as the main one.
/// <para>
/// <c>getElementsByClassName</c> and <c>getElementsByName</c> were registered on the main document
/// but not on a sub-document, which builds its own query surface. A script inside a frame is a
/// script like any other, so calling either there hit the failure this branch has been chasing
/// throughout: the method read as <see langword="undefined"/> rather than as absent, and calling it
/// threw <c>TypeError: undefined is not a function</c>, aborting the frame's whole
/// <c>&lt;script&gt;</c>.
/// </para>
/// <para>
/// The class lookup goes through the same <c>ClassNameSet</c> rule as the main document and the
/// element-scoped search, so all three agree; that agreement is asserted here rather than assumed.
/// </para>
/// </summary>
public sealed class SubDocumentCollectionLookupTests
{
    private const string Frame =
        "<div class='panel wide' name='box' id='a'></div>" +
        "<div class='panel' id='b'></div>" +
        "<span class='wide' id='c'></span>" +
        "<input name='q' id='d'>" +
        "<input name='box' id='e'>";

    private static (JSContext Context, DomBridge Bridge) AttachWithFrame()
    {
        var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(
            context,
            $"<!doctype html><html><body><iframe id='f' srcdoc=\"{Frame}\"></iframe></body></html>",
            "https://example.com/host.html");
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();
        return (context, bridge);
    }

    private static string Eval(JSContext context, string expression) => context.Eval(expression).ToString();

    /// <summary>Reaching either method inside a frame must not throw.</summary>
    [Fact(Timeout = 600000)]
    public void FrameDocument_HasBothCollectionLookups()
    {
        var (context, bridge) = AttachWithFrame();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("function", Eval(context, "typeof document.getElementById('f').contentDocument.getElementsByClassName"));
        Assert.Equal("function", Eval(context, "typeof document.getElementById('f').contentDocument.getElementsByName"));
    }

    /// <summary>The class lookup searches the frame's own tree, with the shared set semantics.</summary>
    [Theory]
    [InlineData("panel", "a,b")]
    [InlineData("wide", "a,c")]
    [InlineData("panel wide", "a")]
    [InlineData("wide panel", "a")]
    [InlineData("panel missing", "")]
    [InlineData("", "")]
    public void FrameDocument_GetElementsByClassName_UsesTheSharedSetRule(string query, string expectedIds)
    {
        var (context, bridge) = AttachWithFrame();
        using var _ = context;
        using var __ = bridge;

        var ids = Eval(context, $$"""
            (function() {
                var d = document.getElementById('f').contentDocument;
                return Array.prototype.map.call(d.getElementsByClassName('{{query}}'),
                    function(el) { return el.id; }).join(',');
            })()
            """);

        Assert.Equal(expectedIds, ids);
    }

    /// <summary>The name lookup likewise, matching any element carrying the attribute.</summary>
    [Theory]
    [InlineData("box", "a,e")]
    [InlineData("q", "d")]
    [InlineData("nothing", "")]
    public void FrameDocument_GetElementsByName_MatchesTheAttribute(string query, string expectedIds)
    {
        var (context, bridge) = AttachWithFrame();
        using var _ = context;
        using var __ = bridge;

        var ids = Eval(context, $$"""
            (function() {
                var d = document.getElementById('f').contentDocument;
                return Array.prototype.map.call(d.getElementsByName('{{query}}'),
                    function(el) { return el.id; }).join(',');
            })()
            """);

        Assert.Equal(expectedIds, ids);
    }

    /// <summary>
    /// The frame's lookups are scoped to the frame. The host document carries neither class nor name,
    /// so a search there finds nothing while the frame's finds its own — which is what proves the
    /// sub-document is searching its own tree rather than the main one.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void FrameDocument_SearchesItsOwnTree_NotTheHosts()
    {
        var (context, bridge) = AttachWithFrame();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("0", Eval(context, "String(document.getElementsByClassName('panel').length)"));
        Assert.Equal("0", Eval(context, "String(document.getElementsByName('box').length)"));
        Assert.Equal("2", Eval(context, "String(document.getElementById('f').contentDocument.getElementsByClassName('panel').length)"));
        Assert.Equal("2", Eval(context, "String(document.getElementById('f').contentDocument.getElementsByName('box').length)"));
    }
}
