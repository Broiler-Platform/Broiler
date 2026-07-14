using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 slice P3.13: the nested-browsing-context
/// <c>document</c> object surface (<c>BuildDocument</c> plus every getElementById/createElement/
/// querySelector/append(Child)/createEvent callback and <c>document.implementation</c>) is now a
/// co-located binding module (<see cref="SubDocumentBinding"/>) consumed through the explicit
/// <see cref="ISubDocumentHost"/> contract. The characterizations exercise the extracted surface
/// end-to-end: the detached <c>createDocument</c>/<c>createHTMLDocument</c> roots (regime B, via the
/// main document's <c>implementation</c>) and the severed <c>&lt;iframe srcdoc&gt;</c> content document
/// (regime A — the path P4.4b unblocked).
/// </summary>
public sealed class SubDocumentBindingModuleTests
{
    private static DomBridge Attach(out JSContext context, string html = "<!DOCTYPE html><html><body></body></html>")
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        return bridge;
    }

    [Fact]
    public void SubDocument_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(SubDocumentBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.False(typeof(ISubDocumentHost).IsPublic);
    }

    [Fact]
    public void DomBridge_Consumes_SubDocument_Through_The_Host_Contract()
    {
        Assert.True(typeof(ISubDocumentHost).IsAssignableFrom(typeof(DomBridge)));
        Assert.Contains(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(SubDocumentBinding));
    }

    [Fact]
    public void CreateHtmlDocument_Builds_Structure_And_Supports_Lookup_Through_The_Module()
    {
        using var bridge = Attach(out var context);

        var result = context.Eval("""
            (() => {
                var doc = document.implementation.createHTMLDocument('Title');
                var p = doc.createElement('p');
                p.id = 'x';
                p.textContent = 'hi';
                doc.body.appendChild(p);
                return doc.nodeType + '|' + doc.title + '|' + doc.body.tagName.toLowerCase() + '|' +
                       (doc.getElementById('x') === p) + '|' + doc.querySelector('#x').textContent + '|' +
                       (p.ownerDocument === doc);
            })()
            """);

        Assert.Equal("9|Title|body|true|hi|true", result.ToString());
    }

    [Fact]
    public void CreateDocument_Reports_Document_NodeType_And_Doctype_Through_The_Module()
    {
        using var bridge = Attach(out var context);

        var result = context.Eval("""
            (() => {
                var dt = document.implementation.createDocumentType('html', 'pub', 'sys');
                var doc = document.implementation.createDocument('http://www.w3.org/1999/xhtml', 'html', dt);
                return doc.nodeType + '|' + doc.documentElement.tagName.toLowerCase() + '|' +
                       doc.firstChild.nodeType + '|' + doc.firstChild.name;
            })()
            """);

        Assert.Equal("9|html|10|html", result.ToString());
    }

    [Fact]
    public void CreateEvent_And_InitEvent_Work_On_A_Module_Built_Document()
    {
        using var bridge = Attach(out var context);

        var result = context.Eval("""
            (() => {
                var doc = document.implementation.createHTMLDocument('t');
                var e = doc.createEvent('Event');
                e.initEvent('custom', true, false);
                return e.type + '|' + e.bubbles + '|' + e.cancelable;
            })()
            """);

        Assert.Equal("custom|true|false", result.ToString());
    }

    [Fact]
    public void AppendChild_And_RemoveChild_On_A_Module_Built_Document_Mutate_Its_Tree()
    {
        using var bridge = Attach(out var context);

        var result = context.Eval("""
            (() => {
                var doc = document.implementation.createHTMLDocument('t');
                var start = doc.childNodes.length;              // [doctype, <html>]
                var comment = doc.createComment('note');
                doc.appendChild(comment);                       // comment is a valid document child
                var afterAppend = doc.childNodes.length;
                doc.removeChild(doc.documentElement);           // removeChild targets element children
                var afterRemove = doc.childNodes.length;
                return start + '|' + afterAppend + '|' + afterRemove;
            })()
            """);

        // [doctype,<html>] → +comment = 3 → remove <html> = 2 (doctype + comment).
        Assert.Equal("2|3|2", result.ToString());
    }

    [Fact]
    public void Iframe_Srcdoc_ContentDocument_Surface_Is_Built_By_The_Module()
    {
        using var bridge = Attach(out var context,
            """
            <!DOCTYPE html><html><body>
            <iframe id="fr" srcdoc='<!DOCTYPE html><html><body><p id="t">hi</p></body></html>'></iframe>
            </body></html>
            """);

        var result = context.Eval("""
            (() => {
                var d = document.getElementById('fr').contentDocument;
                var p = d.getElementById('t');
                var made = d.createElement('span');
                return d.nodeType + '|' + p.textContent + '|' + made.tagName.toLowerCase() + '|' +
                       d.querySelectorAll('p').length;
            })()
            """);

        Assert.Equal("9|hi|span|1", result.ToString());
    }
}
