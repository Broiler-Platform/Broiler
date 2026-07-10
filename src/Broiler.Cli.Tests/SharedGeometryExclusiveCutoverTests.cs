using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// RF-BRIDGE-1b increment 6 (staged cutover): verifies the default-OFF
/// <see cref="DomBridge.UseSharedGeometryExclusively"/> flag. When flipped on, unzoomed
/// geometry queries answer exclusively from the shared snapshot — a boxed element reads
/// its real geometry and a boxless (detached / <c>display:none</c>) element reads zero,
/// with no estimator consultation. This locks in the deletion-enabling behavior so the
/// final estimator removal is a small step once the zoom-correct snapshot lands.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class SharedGeometryExclusiveCutoverTests
{
    private static string Measure(string html, string id, bool exclusive)
    {
        var prev = DomBridge.UseSharedGeometryExclusively;
        DomBridge.UseSharedGeometryExclusively = exclusive;
        try
        {
            using var ctx = new JSContext();
            var bridge = new DomBridge();
            bridge.Attach(ctx, html, "file:///cutover.html");
            return ctx.Eval(
                $"(function(){{var e=document.getElementById('{id}');" +
                "return [e.offsetWidth,e.offsetHeight,e.clientWidth,e.scrollWidth].join(',');})()").ToString();
        }
        finally { DomBridge.UseSharedGeometryExclusively = prev; }
    }

    [Fact]
    public void DefaultsOn()
    {
        // RF-BRIDGE-1b increment 6 cutover: exclusive-shared geometry is the default.
        // Box-generating elements resolve from the shared snapshot (or the estimator when
        // transiently absent from it); only genuinely boxless elements (display:none/
        // contents) read zero. See the flag comment in SharedLayoutGeometry.cs.
        Assert.True(DomBridge.UseSharedGeometryExclusively);
    }

    [Fact]
    public void Exclusive_Boxed_Element_Reads_Real_Shared_Geometry()
    {
        const string html = "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head>" +
            "<body><div id='b' style='width:120px;height:40px'></div></body></html>";

        // offsetWidth, offsetHeight, clientWidth, scrollWidth (empty box → scrollWidth == width)
        Assert.Equal("120,40,120,120", Measure(html, "b", exclusive: true));
    }

    [Fact]
    public void Exclusive_DisplayNone_Element_Reads_Zero_Not_Estimator()
    {
        // display:none produces no box; exclusive-shared reports zero geometry (correct),
        // rather than the estimator's declared-size guess.
        const string html = "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head>" +
            "<body><div id='n' style='display:none;width:100px;height:50px'></div></body></html>";

        Assert.Equal("0,0,0,0", Measure(html, "n", exclusive: true));
    }
}
