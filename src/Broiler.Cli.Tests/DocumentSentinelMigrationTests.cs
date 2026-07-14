using Broiler.Dom;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 4 item 1, final sentinel: the <c>#document</c> wrapper element (<c>_documentNode</c>) is
/// replaced by the canonical <see cref="Broiler.Dom.DomDocument"/>, so the JS <c>document</c> object
/// maps directly to the canonical document and <c>&lt;html&gt;</c>/doctype become its direct children.
///
/// These characterizations pin the document-root behaviours the migration must preserve: node
/// identity (nodeType 9 / nodeName "#document"), documentElement/head/body, childNodes/firstChild
/// (doctype + html), getElementById/querySelector, getRootNode()/isConnected returning the document,
/// and serialization round-tripping the doctype + html.
/// </summary>
public sealed class DocumentSentinelMigrationTests
{
    private static string Run(string bodyScript, string headExtra = "")
    {
        var html = $@"<!DOCTYPE html><html><head>{headExtra}</head><body>
<div id=""result""></div>
<script>
function put(v) {{ document.getElementById('result').textContent = String(v); }}
{bodyScript}
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
    }

    [Fact]
    public void Document_NodeType_And_NodeName()
    {
        Assert.Contains("9|#document", Run("put(document.nodeType + '|' + document.nodeName);"));
    }

    [Fact]
    public void Document_DocumentElement_Head_Body()
    {
        Assert.Contains("HTML|HEAD|BODY", Run(
            "put(document.documentElement.tagName + '|' + document.head.tagName + '|' + document.body.tagName);"));
    }

    [Fact]
    public void Document_FirstChild_Is_Doctype_And_ChildNodes_Order()
    {
        // doctype then <html>: firstChild is the DocumentType (nodeType 10), last element is <html>.
        var result = Run(
            "put(document.firstChild.nodeType + '|' + document.documentElement.nodeName);");
        Assert.Contains("10|HTML", result);
    }

    [Fact]
    public void Document_GetElementById_And_QuerySelector()
    {
        Assert.Contains("found|found", Run(
            "var a = document.getElementById('result') ? 'found' : 'no';" +
            "var b = document.querySelector('#result') ? 'found' : 'no';" +
            "put(a + '|' + b);"));
    }

    [Fact]
    public void Element_GetRootNode_Returns_Document()
    {
        Assert.Contains("true|true", Run(
            "var el = document.getElementById('result');" +
            "put((el.getRootNode() === document) + '|' + el.isConnected);"));
    }

    [Fact]
    public void Document_AppendedElement_Is_Connected_And_Queryable()
    {
        var result = Run(
            "var d = document.createElement('div'); d.id = 'made';" +
            "document.body.appendChild(d);" +
            "put((document.getElementById('made') === d) + '|' + d.isConnected);");
        Assert.Contains("true|true", result);
    }

    [Fact]
    public void Serialization_RoundTrips_Doctype_And_Html()
    {
        // A script mutation forces the DOM->serialize path (no-script HTML round-trips raw).
        var result = Run("document.body.setAttribute('data-ran', '1');");
        Assert.Contains("<!DOCTYPE html>", result, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<html", result);
    }

    [Fact]
    public void Bridge_Document_Is_Canonical_And_Elements_Exclude_Root()
    {
        const string html = "<!DOCTYPE html><html><head></head><body><p id=\"x\">hi</p></body></html>";
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///test.html");

        // Canonical document reports nodeType Document (9) and carries the doctype + <html> tree.
        Assert.Equal(DomNodeType.Document, bridge.Document.NodeType);
        Assert.NotNull(bridge.Document.DocumentElement);
        // Elements enumeration includes real elements (html/head/body/p) but not a "#document" node.
        Assert.Contains(bridge.Elements, e => e.TagName == "p");
        Assert.DoesNotContain(bridge.Elements, e => e.TagName.StartsWith('#'));
    }
}
