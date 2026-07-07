using Broiler.Layout;
using System.Drawing;
using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;
using DomDocument = Broiler.Dom.DomDocument;
using DomElement = Broiler.Dom.DomElement;

namespace Broiler.Cli.Tests;

/// <summary>
/// RF-BRIDGE-1b increment ①: the renderer's <see cref="HtmlContainer.GetLayoutGeometry"/>
/// read-model lays out a canonical <see cref="DomDocument"/> headlessly (no paint
/// surface) and returns per-element border/padding/content boxes from the real layout
/// tree. These tests pin the box-model arithmetic the bridge will later consume in
/// place of its coarse estimators.
/// </summary>
public sealed class SharedLayoutGeometryTests
{
    private static IReadOnlyDictionary<DomElement, BoxGeometry> LayoutGeometry(
        string html, out DomDocument document)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///geom.html");
        document = bridge.GetRenderDocument();

        using var container = new HtmlContainer
        {
            Location = new PointF(0, 0),
            AvoidAsyncImagesLoading = true,
            AvoidImagesLateLoading = true,
        };
        container.SetDocumentWithStyleSet(document, baseUrl: "file:///geom.html");
        return container.GetLayoutGeometry(new SizeF(800, 600));
    }

    [Fact]
    public void Plain_Block_BorderBox_Equals_Content_When_No_Border_Or_Padding()
    {
        const string html = "<!DOCTYPE html><html><body style='margin:0'>" +
                            "<div id='target' style='width:200px;height:100px'></div></body></html>";

        var geometry = LayoutGeometry(html, out var document);
        var target = document.GetElementById("target");

        Assert.NotNull(target);
        Assert.True(geometry.ContainsKey(target!));
        var box = geometry[target!];

        Assert.Equal(200f, box.BorderBox.Width, 1);
        Assert.Equal(100f, box.BorderBox.Height, 1);
        // With no border or padding the three box levels coincide.
        Assert.Equal(box.BorderBox, box.PaddingBox);
        Assert.Equal(box.BorderBox, box.ContentBox);
    }

    [Fact]
    public void Block_With_Border_And_Padding_Expands_BorderBox_By_Box_Model()
    {
        // content 100x50, padding 10 each side, border 5 each side (content-box sizing):
        //   content 100x50, padding box 120x70, border box 130x80.
        const string html = "<!DOCTYPE html><html><body style='margin:0'>" +
                            "<div id='target' style='width:100px;height:50px;" +
                            "padding:10px;border:5px solid black'></div></body></html>";

        var geometry = LayoutGeometry(html, out var document);
        var target = document.GetElementById("target");

        Assert.NotNull(target);
        Assert.True(geometry.ContainsKey(target!));
        var box = geometry[target!];

        Assert.Equal(100f, box.ContentBox.Width, 1);
        Assert.Equal(50f, box.ContentBox.Height, 1);
        Assert.Equal(120f, box.PaddingBox.Width, 1);
        Assert.Equal(70f, box.PaddingBox.Height, 1);
        Assert.Equal(130f, box.BorderBox.Width, 1);
        Assert.Equal(80f, box.BorderBox.Height, 1);
    }

    [Fact]
    public void Geometry_Is_Keyed_By_The_Canonical_Document_Elements()
    {
        const string html = "<!DOCTYPE html><html><body style='margin:0'>" +
                            "<div id='target' style='width:50px;height:50px'></div></body></html>";

        var geometry = LayoutGeometry(html, out var document);

        // The keys are the same DomElement instances the document exposes, so a bridge
        // holding the canonical document can look geometry up directly.
        var target = document.GetElementById("target");
        Assert.NotNull(target);
        Assert.Contains(target!, geometry.Keys);
    }
}
