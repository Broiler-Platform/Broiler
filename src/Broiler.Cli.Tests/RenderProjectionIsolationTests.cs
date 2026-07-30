using Broiler.Dom;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase-0 invariants for the neutral renderer handoff. Preparing or serializing
/// renderer input must never publish mutations against the script-visible document.
/// </summary>
public sealed class RenderProjectionIsolationTests
{
    private const string Html =
        "<!DOCTYPE html><html><head></head><body>" +
        "<input id='field'><!--render-only removal--><progress id='progress' value='1' max='2'></progress>" +
        "</body></html>";

    [Fact]
    public void GetRenderDocument_Reflects_Runtime_State_Without_Mutating_Live_Dom()
    {
        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, Html, "file:///projection.html");
        context.Eval("document.getElementById('field').value = 'runtime';");

        var liveDocument = bridge.Document;
        var liveInput = liveDocument.GetElementById("field")!;
        var liveProgress = liveDocument.GetElementById("progress")!;
        var liveVersion = liveDocument.Version;
        var liveMutationCount = 0;
        liveDocument.Mutated += _ => liveMutationCount++;

        var projection = bridge.GetRenderDocument();

        Assert.NotSame(liveDocument, projection);
        Assert.Equal(liveVersion, liveDocument.Version);
        Assert.Equal(0, liveMutationCount);
        Assert.Null(liveInput.GetAttribute("value"));
        Assert.Empty(liveProgress.ChildNodes);
        Assert.Contains(liveDocument.Body!.ChildNodes, static node => node is DomComment);

        Assert.Equal("runtime", projection.GetElementById("field")!.GetAttribute("value"));
        Assert.NotEmpty(projection.GetElementById("progress")!.ChildNodes);
        Assert.DoesNotContain(projection.Body!.ChildNodes, static node => node is DomComment);
    }

    [Fact]
    public void Serialization_And_Repeated_Projections_Are_Isolated()
    {
        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(context, Html, "file:///projection.html");
        context.Eval("document.getElementById('field').value = 'runtime';");

        var liveDocument = bridge.Document;
        var liveVersion = liveDocument.Version;
        var liveMutationCount = 0;
        liveDocument.Mutated += _ => liveMutationCount++;

        var first = bridge.GetRenderDocument();
        first.GetElementById("field")!.SetAttribute("data-projection-only", "yes");
        var serialized = bridge.SerializeToHtml();
        var second = bridge.GetRenderDocument();

        Assert.NotSame(first, second);
        Assert.Null(second.GetElementById("field")!.GetAttribute("data-projection-only"));
        Assert.Contains("value=\"runtime\"", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("render-only removal", serialized, StringComparison.Ordinal);
        Assert.Equal(liveVersion, liveDocument.Version);
        Assert.Equal(0, liveMutationCount);
        Assert.Null(liveDocument.GetElementById("field")!.GetAttribute("value"));
        Assert.Empty(liveDocument.GetElementById("progress")!.ChildNodes);
    }
}
