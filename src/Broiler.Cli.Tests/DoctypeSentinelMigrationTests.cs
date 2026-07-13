using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 4 second slice (P4.2, part of work item 1):
/// the <c>#doctype</c> sentinel element is replaced by the canonical <see cref="Broiler.Dom.DomDocumentType"/>.
/// The doctype was a fake-tag <c>DomElement</c> whose name/publicId/systemId lived in a parallel
/// <c>ElementRuntimeState.DocumentType</c> state class; it now IS a canonical DocumentType node that
/// carries those natively. These characterizations exercise every reachable doctype path through the
/// bridge — the parsed <c>&lt;!DOCTYPE&gt;</c>, <c>createDocumentType</c>, <c>createHTMLDocument</c>,
/// <c>cloneNode</c>, and serialization — and pin the observable behaviour across the migration.
/// </summary>
public sealed class DoctypeSentinelMigrationTests
{
    private static DomBridge Attach(string html, out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        return bridge;
    }

    [Fact]
    public void Parsed_Doctype_Reports_DocumentType_Node()
    {
        using var bridge = Attach("<!DOCTYPE html><html><body>x</body></html>", out var context);

        var result = context.Eval("""
            (() => {
                var dt = document.firstChild;
                return dt.nodeType + '|' + dt.nodeName + '|' + (dt.parentNode === document) + '|' + (dt.ownerDocument === document);
            })()
            """);

        Assert.Equal("10|html|true|true", result.ToString());
    }

    [Fact]
    public void CreateDocumentType_Carries_Name_PublicId_SystemId()
    {
        using var bridge = Attach("<!DOCTYPE html><html><body></body></html>", out var context);

        var result = context.Eval("""
            (() => {
                var dt = document.implementation.createDocumentType('html', 'pubX', 'sysY');
                return dt.nodeType + '|' + dt.name + '|' + dt.nodeName + '|' + dt.publicId + '|' + dt.systemId + '|' + (dt.internalSubset === null);
            })()
            """);

        Assert.Equal("10|html|html|pubX|sysY|true", result.ToString());
    }

    [Fact]
    public void CreateHtmlDocument_Has_Html_Doctype()
    {
        using var bridge = Attach("<!DOCTYPE html><html><body></body></html>", out var context);

        // createHTMLDocument must not throw and its doctype must be a DocumentType node named "html".
        var result = context.Eval("""
            (() => {
                var doc = document.implementation.createHTMLDocument('Title');
                var dt = doc.firstChild;
                return dt.nodeType + '|' + dt.nodeName;
            })()
            """);

        Assert.Equal("10|html", result.ToString());
    }

    [Fact]
    public void Doctype_CloneNode_Preserves_DocumentType_Fields()
    {
        using var bridge = Attach("<!DOCTYPE html><html><body></body></html>", out var context);

        var result = context.Eval("""
            (() => {
                var dt = document.implementation.createDocumentType('svg', 'p1', 's1');
                var c = dt.cloneNode(false);
                return c.nodeType + '|' + c.name + '|' + c.publicId + '|' + c.systemId + '|' + c.isEqualNode(dt);
            })()
            """);

        Assert.Equal("10|svg|p1|s1|true", result.ToString());
    }

    [Fact]
    public void Serialized_Document_Starts_With_Doctype()
    {
        using var bridge = Attach("<!DOCTYPE html><html><head></head><body>hi</body></html>", out _);

        var html = bridge.SerializeToHtml();
        Assert.StartsWith("<!DOCTYPE html>", html.TrimStart());
    }
}
