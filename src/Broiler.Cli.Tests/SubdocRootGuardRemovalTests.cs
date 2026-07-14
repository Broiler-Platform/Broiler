using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 4 item 1 residual: the now-inert
/// <c>#subdoc-root</c> tag-name special cases are removed. P4.4b severed the materialized nested
/// browsing context from the <c>_document</c> tree (a sub-document is a canonical
/// <see cref="Broiler.Dom.DomDocument"/> referenced through a container↔document map, never an in-tree
/// <c>#subdoc-root</c> sentinel child), so every <c>IsSubDocRoot</c> guard and <c>"#subdoc-root"</c>
/// TagName check across the bridge became dead code — the element is never built, so the checks never
/// fire. This deletes them: the <c>IsSubDocRoot</c>/<c>IsSubDocRootNode</c> helpers, the child/sibling
/// navigation filters, the <c>CollectWindowFrames</c> / serialization / style-scope / style-collection
/// skips, and the document-level <c>surroundContents</c> sentinel guard.
///
/// The reflection guards assert the helpers are gone (and must not be reintroduced — the canonical tree,
/// with sub-documents severed, is the single authority). The characterizations exercise the exact DOM
/// navigation, style-collection, serialization and getRootNode paths whose <c>#subdoc-root</c> filters
/// were removed, on both a normal tree and an iframe host, to pin behaviour-preservation.
/// </summary>
public sealed class SubdocRootGuardRemovalTests
{
    [Theory]
    [InlineData("IsSubDocRoot")]
    [InlineData("IsSubDocRootNode")]
    public void DomBridge_Has_No_SubdocRoot_TagName_Helper(string methodName)
    {
        // The dead sentinel-tag guards must not be reintroduced: no #subdoc-root element is ever
        // constructed (P4.4b), so any TagName check on it is unreachable.
        var method = typeof(DomBridge).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.Null(method);
    }

    private static DomBridge Attach(string html, out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        return bridge;
    }

    [Fact]
    public void Node_Child_And_Sibling_Navigation_Round_Trips()
    {
        using var bridge = Attach(
            "<!DOCTYPE html><html><body><ul id=\"l\"><li id=\"a\">a</li><li id=\"b\">b</li><li id=\"c\">c</li></ul></body></html>",
            out var context);

        var result = context.Eval("""
            (() => {
                var l = document.getElementById('l');
                var b = document.getElementById('b');
                return [
                    l.childNodes.length,            // 3 <li>
                    l.firstChild.id,                // a
                    l.lastChild.id,                 // c
                    b.nextSibling.id,               // c
                    b.previousSibling.id            // a
                ].join('|');
            })()
            """);

        Assert.Equal("3|a|c|c|a", result.ToString());
    }

    [Fact]
    public void Element_Child_And_Sibling_Navigation_Round_Trips()
    {
        using var bridge = Attach(
            "<!DOCTYPE html><html><body><ul id=\"l\"><li id=\"a\">a</li><li id=\"b\">b</li><li id=\"c\">c</li></ul></body></html>",
            out var context);

        var result = context.Eval("""
            (() => {
                var l = document.getElementById('l');
                var b = document.getElementById('b');
                return [
                    l.children.length,              // 3
                    l.childElementCount,            // 3
                    l.firstElementChild.id,         // a
                    l.lastElementChild.id,          // c
                    b.nextElementSibling.id,        // c
                    b.previousElementSibling.id     // a
                ].join('|');
            })()
            """);

        Assert.Equal("3|3|a|c|c|a", result.ToString());
    }

    [Fact]
    public void GetRootNode_Of_A_Connected_Element_Is_The_Document()
    {
        using var bridge = Attach(
            "<!DOCTYPE html><html><body><div id=\"x\"></div></body></html>", out var context);

        var result = context.Eval("(document.getElementById('x').getRootNode() === document)");

        Assert.Equal("true", result.ToString());
    }

    [Fact]
    public void Iframe_Host_Main_Document_Navigation_And_Serialization_Ignore_The_SubDocument()
    {
        using var bridge = Attach(
            "<!DOCTYPE html><html><body><iframe id=\"fr\"></iframe><p id=\"tail\">tail</p></body></html>",
            out var context);

        var result = context.Eval("""
            (() => {
                var f = document.getElementById('fr');
                f.srcdoc = '<!DOCTYPE html><html><body><span id="inner">leak?</span></body></html>';
                // Force the sub-document to materialize.
                var inner = f.contentDocument.getElementById('inner').textContent;
                var body = document.body;
                // The severed sub-document must not appear as an extra child of <body>.
                return [
                    inner,                          // leak?
                    body.childElementCount,         // 2: <iframe> + <p>
                    body.lastElementChild.id        // tail
                ].join('|');
            })()
            """);

        Assert.Equal("leak?|2|tail", result.ToString());

        var html = bridge.SerializeToHtml();
        Assert.DoesNotContain("subdoc", html);
    }

    [Fact]
    public void Style_Collection_Applies_Across_An_Iframe_Host_Without_Leaking_The_SubDocument()
    {
        using var bridge = Attach(
            "<!DOCTYPE html><html><head><style>#t { color: rgb(1, 2, 3); }</style></head>" +
            "<body><iframe id=\"fr\"></iframe><div id=\"t\">t</div></body></html>",
            out var context);

        var result = context.Eval("""
            (() => {
                var f = document.getElementById('fr');
                f.srcdoc = '<!DOCTYPE html><html><head><style>#t { color: rgb(9, 9, 9); }</style></head><body></body></html>';
                f.contentDocument; // materialize the sub-document
                // The main document's own <style> must still resolve (style collection walks the main
                // tree only — the sub-document's <style> must not leak into the parent cascade).
                return window.getComputedStyle(document.getElementById('t')).color;
            })()
            """);

        Assert.Equal("rgb(1, 2, 3)", result.ToString());
    }
}
