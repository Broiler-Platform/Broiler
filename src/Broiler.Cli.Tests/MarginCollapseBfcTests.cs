using System.Drawing;
using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;
using DomDocument = Broiler.Dom.DomDocument;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression coverage for the Broiler.Layout margin-collapse / BFC fix: a first in-flow
/// child's top margin must NOT collapse through a parent that establishes a block formatting
/// context via <c>overflow != visible</c> (CSS 2.1 §9.4.1 / §8.3.1) — the parent contains the
/// margin instead of being shifted down by it. The bridge's DOM-shift scroll wrapper had been
/// masking this bug; native anchor-page scroll exposed it (anchor-center-scroll-001). Verified
/// through the real parse → layout pipeline via <c>offsetTop</c>.
/// </summary>
[Collection("SharedGeometryStatics")]
public sealed class MarginCollapseBfcTests
{
    private const string Url = "file:///margin-bfc.html";

    private static int OffsetTop(string html, string id)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, Url);
        DomDocument document = bridge.GetRenderDocument();
        using var container = new HtmlContainer
        {
            Location = new PointF(0, 0),
            AvoidAsyncImagesLoading = true,
            AvoidImagesLateLoading = true,
        };
        container.SetDocumentWithStyleSet(document, baseUrl: Url);
        var geometry = container.GetLayoutGeometry(new SizeF(400, 400));
        var el = document.GetElementById(id);
        Assert.NotNull(el);
        Assert.True(geometry.TryGetValue(el!, out var g), $"#{id} has no geometry");
        return (int)System.Math.Round(g.BorderBox.Y);
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("hidden")]
    [InlineData("scroll")]
    public void OverflowParent_ContainsFirstChildTopMargin(string overflow)
    {
        // A bare (no border/padding) overflow container whose first in-flow child has a top
        // margin establishes a BFC: the parent stays at the top and the margin is inside it.
        string html =
            "<!DOCTYPE html><html><head><style>" +
            "body { margin: 0; }" +
            $"#box {{ width: 200px; height: 300px; overflow: {overflow}; }}" +
            "#child { margin-top: 100px; width: 50px; height: 50px; }" +
            "</style></head><body><div id='box'><div id='child'></div></div></body></html>";

        Assert.Equal(0, OffsetTop(html, "box"));    // the BFC parent is NOT pushed down
        Assert.Equal(100, OffsetTop(html, "child")); // the margin is contained inside it
    }

    [Fact]
    public void VisibleParent_StillCollapsesFirstChildTopMargin()
    {
        // Control: a plain (overflow:visible) block does NOT establish a BFC, so the first
        // child's top margin collapses with / propagates through the parent — the parent is
        // shifted down and the child sits at the parent's top edge. Behaviour is unchanged.
        const string html =
            "<!DOCTYPE html><html><head><style>" +
            "body { margin: 0; }" +
            "#box { width: 200px; height: 300px; }" +
            "#child { margin-top: 100px; width: 50px; height: 50px; }" +
            "</style></head><body><div id='box'><div id='child'></div></div></body></html>";

        Assert.Equal(100, OffsetTop(html, "box"));   // margin propagates: parent pushed down
        Assert.Equal(100, OffsetTop(html, "child")); // child at the (shifted) parent top
    }
}
