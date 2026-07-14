using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 4 item 1, P4.4c: the
/// <c>ElementRuntimeState.OwnerDocRoot</c> parallel-state field is eliminated. It shadowed the
/// canonical owner-document (null for main-document nodes, the sub-document root for sub-document
/// nodes). After P4.4b made a (sub-)document root a canonical <see cref="Broiler.Dom.DomDocument"/>,
/// a node's owning document is derived from the canonical tree — a connected node's absolute root is
/// its owning document (<c>DomBridge.GetOwningDocument</c>) — while a sub-document's detached
/// <c>createElement</c> node is adopted into its content document (<c>DomDocument.AdoptNode</c>) so its
/// canonical <c>ownerDocument</c> is correct without a parallel field. The subtree-propagation helper
/// (<c>AdoptSubtreeIntoDocument</c>) is deleted with it.
///
/// The reflection guards assert the field and helper are gone (and must not return). The
/// characterizations pin <c>ownerDocument</c> across every regime (main / detached-main-created /
/// sub-document connected / sub-document detached-created / iframe content) plus iframe hit-testing,
/// which relied on the old null-OwnerDocRoot heuristic.
/// </summary>
public sealed class OwnerDocRootRemovalTests
{
    [Fact]
    public void ElementRuntimeState_Has_No_OwnerDocRoot_Field()
    {
        var ers = typeof(DomBridge).Assembly.GetType("Broiler.HtmlBridge.Dom.Runtime.ElementRuntimeState");
        Assert.NotNull(ers);
        Assert.Null(ers!.GetProperty("OwnerDocRoot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.Null(ers.GetField("OwnerDocRoot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
    }

    [Fact]
    public void DomBridge_Has_No_AdoptSubtreeIntoDocument_Helper()
    {
        // Subtree owner-root propagation is gone: connected nodes derive their owning document from
        // tree position. The tree-derivation helper must exist in its place.
        Assert.Null(typeof(DomBridge).GetMethod("AdoptSubtreeIntoDocument", BindingFlags.NonPublic | BindingFlags.Static));
        Assert.NotNull(typeof(DomBridge).GetMethod("GetOwningDocument", BindingFlags.NonPublic | BindingFlags.Static));
    }

    private static DomBridge Attach(string html, out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        return bridge;
    }

    [Fact]
    public void OwnerDocument_Of_Main_Document_Nodes_Is_The_Main_Document()
    {
        using var bridge = Attach(
            "<!DOCTYPE html><html><body><div id=\"x\"></div></body></html>", out var context);

        var result = context.Eval("""
            (() => {
                var connected = document.getElementById('x').ownerDocument === document;
                var detached = document.createElement('span').ownerDocument === document;
                return connected + '|' + detached;
            })()
            """);

        Assert.Equal("true|true", result.ToString());
    }

    [Fact]
    public void OwnerDocument_Of_Programmatic_SubDocument_Nodes_Is_That_SubDocument()
    {
        using var bridge = Attach("<!DOCTYPE html><html><body></body></html>", out var context);

        var result = context.Eval("""
            (() => {
                var sub = document.implementation.createHTMLDocument('t');
                var connected = sub.body.ownerDocument === sub;
                var notMain = sub.body.ownerDocument !== document;
                // A node created by (but not yet inserted into) the sub-document reports the sub-document.
                var created = sub.createElement('div');
                var detached = created.ownerDocument === sub;
                return connected + '|' + notMain + '|' + detached;
            })()
            """);

        Assert.Equal("true|true|true", result.ToString());
    }

    [Fact]
    public void OwnerDocument_Of_Iframe_Content_Nodes_Is_The_Content_Document()
    {
        using var bridge = Attach(
            "<!DOCTYPE html><html><body><iframe id=\"fr\"></iframe></body></html>", out var context);

        var result = context.Eval("""
            (() => {
                var f = document.getElementById('fr');
                f.srcdoc = '<!DOCTYPE html><html><body><p id="p">hi</p></body></html>';
                var doc = f.contentDocument;
                var p = doc.getElementById('p');
                return (p.ownerDocument === doc) + '|' + (p.ownerDocument !== document);
            })()
            """);

        Assert.Equal("true|true", result.ToString());
    }

    [Fact]
    public void Iframe_Content_HitTesting_Still_Resolves_A_Viewport()
    {
        // Regression guard for the old null-OwnerDocRoot heuristic: an iframe content document renders
        // (has a viewport) and must return hits, unlike a detached programmatic document.
        using var bridge = Attach(
            "<!DOCTYPE html><html><body><iframe id=\"fr\"></iframe></body></html>", out var context);

        var result = context.Eval("""
            (() => {
                var f = document.getElementById('fr');
                f.srcdoc = '<!DOCTYPE html><html><body><div style="height:20px"></div></body></html>';
                var hits = f.contentDocument.elementsFromPoint(0, 5);
                return hits.length > 0;
            })()
            """);

        Assert.Equal("true", result.ToString());
    }
}
