using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 4 item 1, stage P4.4b: the MATERIALIZED
/// nested browsing contexts (regime A — an <c>&lt;iframe&gt;</c>/<c>&lt;object&gt;</c>/<c>&lt;frame&gt;</c>
/// sub-document) are severed from the script-visible <c>_document</c> tree. The sub-document is now a
/// canonical <see cref="Broiler.Dom.DomDocument"/> referenced through a container↔document map, no longer
/// an in-tree <c>#subdoc-root</c> sentinel child of the container. The box builder projects the referenced
/// document into the render box tree via a content-document resolver so subframe geometry still composes
/// into the main coordinate frame (the cross-layer fix that unblocked this sever). These characterizations
/// pin: the sentinel is gone from the tree; <c>contentDocument</c> still works; subframe geometry still
/// composes; and <c>srcdoc</c> serialization + reassignment still round-trip through the map.
/// </summary>
public sealed class SubDocumentSeverMigrationTests
{
    private const string FrameHost = """
<!DOCTYPE html>
<html><body style="margin:0; width:2000px; height:2000px;">
  <iframe id="fr"></iframe>
</body></html>
""";

    private static DomBridge AttachFrameHost(out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, FrameHost, "file:///test.html");
        bridge.FireWindowLoadEvent();
        return bridge;
    }

    [Fact]
    public void Materialized_SubDocument_Is_Not_A_SubdocRoot_Child_In_The_Serialized_Tree()
    {
        using var bridge = AttachFrameHost(out var context);

        // Build the sub-document by accessing contentDocument, then serialize the main document.
        context.Eval("""
            (() => {
                var f = document.getElementById('fr');
                f.srcdoc = '<!DOCTYPE html><html><body><p id="p">hi</p></body></html>';
                return f.contentDocument.getElementById('p').textContent;
            })()
            """);

        var html = bridge.SerializeToHtml();

        // The severed sub-document must not appear as a #subdoc-root element inside the main tree,
        // and the iframe's own sub-document content (the <p>) must not leak into the main document.
        Assert.DoesNotContain("#subdoc-root", html);
        Assert.DoesNotContain("subdoc", html);
    }

    [Fact]
    public void ContentDocument_Still_Exposes_The_SubDocument_Dom()
    {
        using var bridge = AttachFrameHost(out var context);

        var result = context.Eval("""
            (() => {
                var f = document.getElementById('fr');
                f.srcdoc = '<!DOCTYPE html><html><body><p id="p">hi</p></body></html>';
                var doc = f.contentDocument;
                return doc.nodeType + '|' + doc.getElementById('p').textContent + '|' +
                       doc.body.tagName.toLowerCase();
            })()
            """);

        // nodeType 9 (Document), the <p> is found, and body resolves.
        Assert.Equal("9|hi|body", result.ToString());
    }

    [Fact]
    public void Subframe_Element_Geometry_Composes_Into_The_Main_Frame_After_Sever()
    {
        using var bridge = AttachFrameHost(out var context);

        var result = context.Eval("""
            (() => {
                var iframe = document.getElementById('fr');
                iframe.style.cssText = 'position:absolute; left:100px; top:300px; width:400px; height:300px; border:0';
                iframe.srcdoc = '<!DOCTYPE html><html><body style="margin:0"><div id="target" style="position:absolute; left:30px; top:40px; width:50px; height:50px;"></div></body></html>';
                var r = iframe.contentDocument.getElementById('target').getBoundingClientRect();
                return r.left + ',' + r.top + ',' + r.width + ',' + r.height;
            })()
            """);

        // iframe content origin (100,300) + target inset (30,40) = (130,340), size 50x50 — the geometry
        // is composed from the referenced content document, not an in-tree #subdoc-root subtree.
        Assert.Equal("130,340,50,50", result.ToString());
    }

    [Fact]
    public void SrcDoc_Serialization_RoundTrips_Through_The_Map()
    {
        using var bridge = AttachFrameHost(out var context);

        context.Eval("""
            (() => {
                var f = document.getElementById('fr');
                f.srcdoc = '<!DOCTYPE html><html><body><p id="p">hello</p></body></html>';
                // Mutate the sub-document so the serialized srcdoc reflects live content, not the raw attribute.
                f.contentDocument.getElementById('p').textContent = 'mutated';
                return 0;
            })()
            """);

        var html = bridge.SerializeToHtml();

        // The iframe carries a srcdoc attribute serialized from the live (severed) sub-document.
        Assert.Contains("srcdoc", html);
        Assert.Contains("mutated", html);
    }

    [Fact]
    public void Reassigning_SrcDoc_Rebuilds_The_Content_Document()
    {
        using var bridge = AttachFrameHost(out var context);

        var result = context.Eval("""
            (() => {
                var f = document.getElementById('fr');
                f.srcdoc = '<!DOCTYPE html><html><body><p id="a">first</p></body></html>';
                var first = f.contentDocument.getElementById('a').textContent;
                f.srcdoc = '<!DOCTYPE html><html><body><p id="b">second</p></body></html>';
                var oldGone = (f.contentDocument.getElementById('a') === null);
                var second = f.contentDocument.getElementById('b').textContent;
                return first + '|' + oldGone + '|' + second;
            })()
            """);

        Assert.Equal("first|true|second", result.ToString());
    }
}
