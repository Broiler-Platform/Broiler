using System.Linq;
using Broiler.Dom;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 4 item 3 (roadmap: "Remove the parallel <c>InnerHtml</c> string. innerHTML becomes
/// parse/replace of canonical children; serialization always reads canonical nodes.").
///
/// The bridge used to shadow raw-text element content in
/// <c>ElementRuntimeState.InnerHtml</c>, read as a fallback by the <c>&lt;style&gt;</c> CSS source
/// accessor, <c>textContent</c>, and serialization. These tests pin the invariant that makes that
/// parallel string redundant — <b>raw-text content always materialises as a canonical
/// <see cref="DomText"/> child</b>, even after the full anchor/render resolve pipeline — and
/// characterise the behaviours the removal must preserve.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class InnerHtmlParallelStateRemovalTests
{
    private static DomElement? FindById(DomNode node, string id)
    {
        if (node is DomElement el && el.Id == id)
            return el;
        foreach (var child in node.ChildNodes)
            if (FindById(child, id) is { } found)
                return found;
        return null;
    }

    private static string TextChildData(DomElement element) =>
        string.Concat(element.ChildNodes.OfType<DomText>().Select(t => t.Data));

    // ------------------------------------------------------------------
    //  Invariant proof: raw-text content lives in a canonical DomText child
    // ------------------------------------------------------------------

    [Fact]
    public void StyleElement_Content_Is_A_DomText_Child_After_Attach()
    {
        const string html = @"<!DOCTYPE html><html><head>
<style id=""sheet"">#box { color: red; }</style>
</head><body><div id=""box""></div></body></html>";

        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///test.html");

        var sheet = FindById(bridge.DocumentElement, "sheet");
        Assert.NotNull(sheet);
        // The <style> source is a canonical DomText child — not a bridge-side string.
        Assert.Contains(sheet!.ChildNodes, c => c is DomText);
        Assert.Contains("color: red", TextChildData(sheet!));
    }

    [Fact]
    public void StyleElement_Keeps_A_DomText_Child_Through_The_Anchor_Resolve_Pipeline()
    {
        // The anchor pipeline neutralises unsupported CSS by rewriting the <style> text node in
        // place; the roadmap/tests historically claimed it could leave the CSS in InnerHtml with no
        // DomText child. This pins that it does NOT — the content stays a canonical DomText child.
        const string html = @"<!DOCTYPE html><html><head>
<style id=""sheet"">
#anchored { position: absolute; top: anchor(--a bottom); }
#a { anchor-name: --a; }
</style>
</head><body>
<div id=""a"" style=""position: relative; left: 10px; top: 10px; width: 50px; height: 50px;""></div>
<div id=""anchored"">x</div>
</body></html>";

        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///test.html");

        bridge.ResolveAnchorPositions();

        var sheet = FindById(bridge.DocumentElement, "sheet");
        Assert.NotNull(sheet);
        // After the resolve pipeline the <style> still carries its (neutralised) CSS as a DomText
        // child; content never migrated to a bridge-side InnerHtml string.
        Assert.Contains(sheet!.ChildNodes, c => c is DomText);
        Assert.False(string.IsNullOrEmpty(TextChildData(sheet!)));
    }

    [Fact]
    public void ScriptElement_Content_Is_A_DomText_Child_After_Attach()
    {
        const string html = @"<!DOCTYPE html><html><head>
<script id=""s"" type=""application/json"">{""k"":1}</script>
</head><body></body></html>";

        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///test.html");

        var s = FindById(bridge.DocumentElement, "s");
        Assert.NotNull(s);
        Assert.Contains(s!.ChildNodes, c => c is DomText);
        Assert.Contains("\"k\":1", TextChildData(s!));
    }

    // ------------------------------------------------------------------
    //  Behaviours the removal must preserve
    // ------------------------------------------------------------------

    [Fact]
    public void StyleElement_Cascade_Resolves_Through_GetComputedStyle()
    {
        // Proves the <style> CSS source is read from canonical DomText children end-to-end
        // (parse -> cascade -> getComputedStyle), with no InnerHtml fallback involved.
        const string html = @"<!DOCTYPE html><html><head>
<style>#target { color: rgb(1, 2, 3); }</style>
</head><body>
<div id=""target"">t</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.getPropertyValue('color');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("rgb(1, 2, 3)", result);
    }

    [Fact]
    public void InnerHtml_Getter_Serialises_Canonical_Children()
    {
        const string html = @"<!DOCTYPE html><html><body>
<div id=""host""><span>a</span><b>c</b></div>
<div id=""result""></div>
<script>
document.getElementById('result').textContent = document.getElementById('host').innerHTML;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("<span>a</span><b>c</b>", result);
    }

    [Fact]
    public void InnerHtml_Setter_Parses_And_Replaces_Canonical_Children()
    {
        const string html = @"<!DOCTYPE html><html><body>
<div id=""host"">old</div>
<div id=""result""></div>
<script>
var host = document.getElementById('host');
host.innerHTML = '<p id=""fresh"">new</p>';
document.getElementById('result').textContent =
    host.children.length + ':' + (host.querySelector('#fresh') ? host.querySelector('#fresh').textContent : 'none');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("1:new", result);
    }

    [Fact]
    public void ChildlessElement_TextContent_Is_Empty()
    {
        const string html = @"<!DOCTYPE html><html><body>
<div id=""empty""></div>
<div id=""result""></div>
<script>
var v = document.getElementById('empty').textContent;
document.getElementById('result').textContent = '[' + v + ']';
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("[]", result);
    }

    [Fact]
    public void CloneNode_Deep_Copies_Style_Source_Via_DomText_Child()
    {
        const string html = @"<!DOCTYPE html><html><body>
<div id=""host""><style id=""s"">.x { color: green; }</style></div>
<div id=""result""></div>
<script>
var s = document.getElementById('s');
var clone = s.cloneNode(true);
document.getElementById('result').textContent = clone.textContent;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(".x { color: green; }", result);
    }

    [Fact]
    public void Serialization_Reads_Style_And_Script_From_Canonical_Children()
    {
        // A script mutates the DOM so serialization is exercised (no-script HTML round-trips raw).
        const string html = @"<!DOCTYPE html><html><head>
<style>.a { color: red; }</style>
</head><body>
<div id=""x""></div>
<script>document.getElementById('x').setAttribute('data-ran','1');</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(".a { color: red; }", result);
    }
}
