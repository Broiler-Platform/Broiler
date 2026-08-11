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

    /// <summary>
    /// The other half of the same rule, which used to be unconditional: a document with no doctype
    /// serialises without one.
    /// </summary>
    /// <remarks>
    /// This is not cosmetic. Whoever re-parses the serialised page re-derives the document mode
    /// from it (<c>DocumentModeContext.IsQuirksHtml</c>, via
    /// <c>HtmlContainerInt.SetHtmlWithStyleSet</c>), so emitting a doctype that was never there
    /// turned every quirks-mode document into a standards-mode one — silently, and for every
    /// consumer of the render path at once. No quirk could fire on the WPT path at all, whose
    /// entire <c>quirks/</c> directory is doctype-less by construction.
    /// </remarks>
    [Fact]
    public void A_Document_With_No_Doctype_Serializes_Without_One()
    {
        using var bridge = Attach("<html><head></head><body>hi</body></html>", out _);

        var html = bridge.SerializeToHtml();
        Assert.DoesNotContain("DOCTYPE", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hi", html);
    }

    /// <summary>
    /// It is the *mode* that round-trips, not the doctype's text. <c>IsQuirksHtml</c> keys off the
    /// doctype's name alone, so an XHTML doctype — what the CSS2.1 <c>.xht</c> tests carry —
    /// selects standards mode through the bare emitted form exactly as it did through the original.
    /// Its public and system identifiers were already dropped here and still are.
    /// </summary>
    [Fact]
    public void A_Doctype_With_Identifiers_Still_Selects_Standards_Mode()
    {
        using var bridge = Attach(
            "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" " +
            "\"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\"><html><body>hi</body></html>",
            out _);

        var html = bridge.SerializeToHtml();
        Assert.StartsWith("<!DOCTYPE html>", html.TrimStart());
        Assert.False(Broiler.Layout.DocumentModeContext.IsQuirksHtml(html));
    }

    /// <summary>
    /// A doctype whose name is not <c>html</c> selects quirks mode, so serialising it as no
    /// doctype at all is what preserves the mode — the bare <c>&lt;!DOCTYPE html&gt;</c> this
    /// serializer can emit would have flipped it to standards.
    /// </summary>
    [Fact]
    public void A_Non_Html_Doctype_Round_Trips_As_Quirks_Mode()
    {
        using var bridge = Attach("<!DOCTYPE foo><html><body>hi</body></html>", out _);

        var html = bridge.SerializeToHtml();
        Assert.True(Broiler.Layout.DocumentModeContext.IsQuirksHtml(html));
    }
}
