using System.Drawing;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// RF-BRIDGE-1b STATIC snapshot-completeness invariant. The shared-geometry provider keys its
/// snapshot by the elements <c>HtmlContainer.GetLayoutGeometry</c> (→ <c>CollectLayoutGeometry</c>)
/// returns, so a box-generating element that the layout engine fails to collect would force the
/// bridge to fall back to the coarse <c>LayoutMetrics</c> estimator — which is what keeps the
/// estimator body alive (blocks RF-BRIDGE-1b increment 6 deletion).
///
/// <para><b>Finding (2026-07-10): this invariant HOLDS for every layout mode below</b> — block,
/// inline, inline-block (in an inline formatting context, nested, at 320/800/1024 viewports, with
/// and without a zoomed sibling), flex/grid item, table cell, abspos, float, and overflow scroll
/// containers are ALL collected. So the estimator-deletion blocker is NOT a static
/// <c>Broiler.Layout</c> box-construction gap (the engine does produce these boxes). The gaps that
/// surface under *pure* exclusive-shared geometry (the css-viewport zoom scroll-into-view WPT tests
/// and <c>GridChild_UsesContentSizing</c>) are therefore <b>DYNAMIC</b>: during the live bridge flow
/// (a <c>scrollIntoView</c> geometry query building the snapshot via
/// <c>BuildSharedGeometrySnapshot</c>) the document/provider state differs from this clean static
/// layout and the element goes missing. The fix belongs in the bridge snapshot-build path (parent
/// repo), not in <c>Broiler.Layout</c>. This test is the regression guard that keeps the static
/// side complete; a dynamic counterpart (assert the snapshot is complete mid-<c>scrollIntoView</c>)
/// is the next step to enumerate the dynamic gaps.</para>
///
/// <para>Lives in Broiler.Cli.Tests (not Broiler.Layout.Tests) because the collection entry point
/// <c>HtmlContainer.GetLayoutGeometry</c> is in <c>Broiler.HTML.Image</c>, which Broiler.Layout.Tests
/// does not reference; this project drives it exactly as the shared-geometry provider does.</para>
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class LayoutGeometryCompletenessTests
{
    private static string Wrap(string body) =>
        "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head><body>" +
        body + "</body></html>";

    // A .container matching the css-viewport zoom-scroll WPT fixtures (an inline-block
    // overflow:hidden box whose scrollHeight the bridge queries during scrollIntoView).
    private const string ContainerStyle =
        "display:inline-block;width:120px;height:100px;overflow:hidden;border:1px solid black;margin-right:12px";

    /// <summary>
    /// (mode, viewportW, viewportH, html) fixtures. Each has a <c>#target</c> element that
    /// generates a principal box and therefore MUST be present in the collected layout geometry.
    /// Viewport is parameterised because the known gaps only surface at the narrow viewport the
    /// failing WPT fixtures use (multiple inline-blocks wrap onto separate lines).
    /// </summary>
    public static IEnumerable<object[]> Cases() => new List<object[]>
    {
        new object[] { "block", 800, 600, Wrap("<div id='target' style='width:100px;height:50px'>x</div>") },
        new object[] { "inline", 800, 600, Wrap("<span id='target'>hello world</span>") },
        new object[] { "inline-block-in-body-ifc", 800, 600, Wrap("<div id='target' style='display:inline-block;overflow:hidden;width:100px;height:50px'>x</div>") },
        new object[] { "inline-block-in-body-ifc-narrow", 320, 240, Wrap("<div id='target' style='display:inline-block;overflow:hidden;width:120px;height:100px'>x</div>") },
        new object[] { "two-inline-blocks-in-body-narrow", 320, 240, Wrap($"<div id='target' style='{ContainerStyle}'><div style='height:1000px'></div></div><div style='{ContainerStyle}'><div style='height:1000px'></div></div>") },
        // The exact css-viewport ZoomScrollPadding structure (first container unzoomed, second zoom:2).
        // Tested at 1024x768 because that is the bridge's DEFAULT geometry viewport, which is what
        // BuildSharedGeometrySnapshot lays out at during scrollIntoView (the WPT runner renders at a
        // small viewport but never propagates it to the bridge's geometry viewport).
        new object[] { "zoom-scroll-padding-first-container-1024", 1024, 768, Wrap($"<div id='target' style='{ContainerStyle}'><div style='height:1000px'></div></div><div style='display:inline-block'><div style='{ContainerStyle};zoom:2'><div style='height:1000px'></div></div></div>") },
        new object[] { "two-inline-blocks-in-body-1024", 1024, 768, Wrap($"<div id='target' style='{ContainerStyle}'><div style='height:1000px'></div></div><div style='{ContainerStyle}'><div style='height:1000px'></div></div>") },
        new object[] { "zoom-scroll-padding-first-container-320", 320, 240, Wrap($"<div id='target' style='{ContainerStyle}'><div style='height:1000px'></div></div><div style='display:inline-block'><div style='{ContainerStyle};zoom:2'><div style='height:1000px'></div></div></div>") },
        // EXACT WPT structure incl. inter-child WHITESPACE text nodes (the real fixtures are indented;
        // the compact fixtures above are not). Suspected trigger for the dynamic snapshot miss.
        new object[] { "zoom-scroll-padding-whitespace-1024", 1024, 768,
            "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}\n" + ".container{" + ContainerStyle + "}\n.buffer{height:1000px}\n</style></head><body>\n" +
            "  <div id='target' class='container'>\n    <div class='buffer'></div>\n    <div class='buffer'></div>\n  </div>\n" +
            "  <div style='display:inline-block'>\n    <div class='container' style='zoom:2'>\n      <div class='buffer'></div>\n      <div class='buffer'></div>\n    </div>\n  </div>\n</body></html>" },
        new object[] { "inline-block-nested-in-block", 800, 600, Wrap("<div><div id='target' style='display:inline-block;overflow:hidden;width:100px;height:50px'>x</div></div>") },
        // RF-BRIDGE-1b §9.2.1.1 regression (session 95a4149e): an inline-block containing a BLOCK
        // child, when it has a display:none sibling (a <script>, or any hidden element), had its
        // principal box dropped — ContainsInlinesOnlyDeep did not skip display:none children, so the
        // block-inside-inline correction fired on <body> and mis-split the inline-block. The estimator
        // fallback masked it; this fixture guards the layout fix directly.
        new object[] { "inline-block-blockchild-with-script-sibling", 800, 600, Wrap("<div id='target' style='display:inline-block'><div style='height:50px'></div></div><script></script>") },
        new object[] { "inline-block-blockchild-with-hidden-sibling", 800, 600, Wrap("<div id='target' style='display:inline-block'><div style='height:50px'></div></div><div style='display:none'></div>") },
        new object[] { "flex-item", 800, 600, Wrap("<div style='display:flex'><div id='target' style='width:50px;height:20px'>x</div></div>") },
        new object[] { "grid-item-fixed", 800, 600, Wrap("<div style='display:grid;grid-template-columns:100px'><div id='target'>x</div></div>") },
        new object[] { "grid-item-content-sized", 800, 600, Wrap("<div style='display:grid'><div id='target'>content</div></div>") },
        new object[] { "table-cell", 800, 600, Wrap("<table><tr><td id='target'>x</td></tr></table>") },
        new object[] { "abspos", 800, 600, Wrap("<div style='position:relative;width:100px;height:100px'><div id='target' style='position:absolute;top:0;left:0;width:10px;height:10px'></div></div>") },
        new object[] { "float", 800, 600, Wrap("<div id='target' style='float:left;width:10px;height:10px'>x</div>") },
        new object[] { "overflow-scroll-block", 800, 600, Wrap("<div id='target' style='overflow:auto;width:100px;height:50px'><div style='height:200px'></div></div>") },
    };

    [Xunit.Theory]
    [Xunit.MemberData(nameof(Cases))]
    public void BoxGeneratingElement_Appears_In_Layout_Geometry(string mode, int viewportW, int viewportH, string html)
    {
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///completeness.html");
        var doc = bridge.GetRenderDocument();

        using var container = new Broiler.HTML.Image.HtmlContainer
        {
            AvoidAsyncImagesLoading = true,
            AvoidImagesLateLoading = true,
        };
        container.SetDocumentWithStyleSet(doc, baseUrl: "file:///completeness.html");
        var geometry = container.GetLayoutGeometry(new SizeF(viewportW, viewportH));

        var target = doc.GetElementById("target");
        Assert.NotNull(target);
        Assert.True(
            geometry.ContainsKey(target!),
            $"[{mode}] #target generates a box but is ABSENT from CollectLayoutGeometry — a " +
            $"Broiler.Layout snapshot-completeness gap that keeps the LayoutMetrics estimator " +
            $"load-bearing (blocks RF-BRIDGE-1b increment 6 estimator deletion).");
    }
}
