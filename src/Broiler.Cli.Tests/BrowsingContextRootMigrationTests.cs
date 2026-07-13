using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 4 item 1, stage P4.4a: the DETACHED
/// browsing-context roots produced by <c>document.implementation.createDocument</c> and
/// <c>createHTMLDocument</c> are now backed by a canonical <see cref="Broiler.Dom.DomDocument"/>
/// instead of a <c>#subdoc-root</c> sentinel element. (The materialized iframe/object/frame roots
/// — regime A — remain on the sentinel path until a later stage, because they are tree children of
/// their container, which a DomDocument cannot be.) These characterizations exercise the
/// script-visible surface of a regime-B document end-to-end.
/// </summary>
public sealed class BrowsingContextRootMigrationTests
{
    private static DomBridge Attach(out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!DOCTYPE html><html><body></body></html>", "https://example.com/index.html");
        return bridge;
    }

    [Fact]
    public void CreateDocument_Is_A_Document_With_DocumentElement_And_Doctype()
    {
        using var bridge = Attach(out var context);

        var result = context.Eval("""
            (() => {
                var dt = document.implementation.createDocumentType('html', 'pub', 'sys');
                var doc = document.implementation.createDocument('http://www.w3.org/1999/xhtml', 'html', dt);
                return doc.nodeType + '|' + doc.documentElement.tagName.toLowerCase() + '|' +
                       (dt.ownerDocument === doc) + '|' + doc.firstChild.nodeType + '|' + doc.firstChild.name;
            })()
            """);

        // nodeType 9 (Document), <html> documentElement, doctype.ownerDocument === doc, first child is
        // the DocumentType (nodeType 10) named "html".
        Assert.Equal("9|html|true|10|html", result.ToString());
    }

    [Fact]
    public void CreateHtmlDocument_Has_Structure_And_Title()
    {
        using var bridge = Attach(out var context);

        var result = context.Eval("""
            (() => {
                var doc = document.implementation.createHTMLDocument('Hello');
                return doc.nodeType + '|' + doc.documentElement.tagName.toLowerCase() + '|' +
                       doc.body.tagName.toLowerCase() + '|' + doc.head.tagName.toLowerCase() + '|' +
                       doc.title + '|' + doc.firstChild.nodeType;
            })()
            """);

        Assert.Equal("9|html|body|head|Hello|10", result.ToString());
    }

    [Fact]
    public void CreateHtmlDocument_Supports_Element_Creation_And_Lookup()
    {
        using var bridge = Attach(out var context);

        var result = context.Eval("""
            (() => {
                var doc = document.implementation.createHTMLDocument('t');
                var p = doc.createElement('p');
                p.id = 'x';
                p.textContent = 'hi';
                doc.body.appendChild(p);
                var found = doc.getElementById('x');
                return (found === p) + '|' + found.textContent + '|' + (found.ownerDocument === doc) + '|' +
                       doc.querySelector('#x').tagName.toLowerCase();
            })()
            """);

        Assert.Equal("true|hi|true|p", result.ToString());
    }
}
