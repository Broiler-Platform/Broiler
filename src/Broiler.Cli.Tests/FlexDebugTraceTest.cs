using Broiler.HTML.Image;
using Broiler.HTML.Dom.Core.Dom;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class FlexDebugTraceTest(ITestOutputHelper output)
{
    [Fact]
    public void TraceFlexBlock()
    {
        CssLayoutEngine.DebugLog = new();
        var html = "<html><body style='margin:0'><div style='display:flex; width:600px'><div style='background:red; padding:10px'><span>Short</span></div><div style='background:blue; padding:10px; color:white'><span>Longer</span></div></div></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        foreach (var line in CssLayoutEngine.DebugLog)
            output.WriteLine(line);
        CssLayoutEngine.DebugLog = null;
    }

    [Fact]
    public void TraceFlexInlineBlock()
    {
        CssLayoutEngine.DebugLog = new();
        var html = "<html><body style='margin:0'><div style='display:flex; width:600px'><div style='display:inline-block; background:red; padding:10px'><span>Short</span></div><div style='display:inline-block; background:blue; padding:10px; color:white'><span>Longer</span></div></div></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 800, 100);
        foreach (var line in CssLayoutEngine.DebugLog)
            output.WriteLine(line);
        CssLayoutEngine.DebugLog = null;
    }
}
