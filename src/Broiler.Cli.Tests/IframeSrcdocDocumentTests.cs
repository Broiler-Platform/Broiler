using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// An <c>&lt;iframe srcdoc&gt;</c> carries its nested document's markup inline (HTML §4.8.5), and
/// when the attribute is present it is what the frame navigates to — <c>src</c> is not consulted.
/// <para>
/// The fragment builder read only <c>src</c>, so every srcdoc frame produced no embedded document
/// at all: the box laid out and painted its border, and the host page showed straight through the
/// content area. That is why the WPT <c>css/css-view-transitions</c> iframe family differed from
/// its reference by exactly the frame's area — 512×384 of a 1024×768 viewport, i.e. the 74.5%
/// match those tests all shared (issue #1552 problems 5 and 6).
/// </para>
/// <para>Expected behaviour throughout is the reference browser's, measured on the same markup.
/// </para>
/// </summary>
public class IframeSrcdocDocumentTests
{
    private const int Width = 200;
    private const int Height = 150;

    /// <summary>Renders a green host page carrying one frame, and returns the pixel at the centre
    /// of the frame's content box.</summary>
    private static (int R, int G, int B) RenderFrameCentre(string frameMarkup)
    {
        var html = $$"""
<!DOCTYPE html>
<body style="margin:0;background:green">
<style>iframe{width:{{Width}}px;height:{{Height}}px;border:0}</style>
{{frameMarkup}}
</body>
""";
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 400, 300);
        var pixel = bitmap.GetPixel(Width / 2, Height / 2);
        return (pixel.R, pixel.G, pixel.B);
    }

    private static readonly (int R, int G, int B) Blue = (0, 0, 255);
    private static readonly (int R, int G, int B) Red = (255, 0, 0);
    private static readonly (int R, int G, int B) HostGreen = (0, 128, 0);

    /// <summary>The bug itself: a srcdoc document renders, rather than leaving the frame empty.
    /// </summary>
    [Fact]
    public void Srcdoc_RendersItsDocument()
    {
        Assert.Equal(Blue, RenderFrameCentre(
            """<iframe srcdoc="<body style='margin:0;background:blue'></body>"></iframe>"""));
    }

    /// <summary>
    /// <c>srcdoc</c> wins over <c>src</c>: the frame navigates to the inline markup and never
    /// fetches the URL.
    /// </summary>
    [Fact]
    public void Srcdoc_TakesPrecedenceOverSrc()
    {
        Assert.Equal(Blue, RenderFrameCentre(
            """<iframe srcdoc="<body style='margin:0;background:blue'></body>" src="data:text/html,<body style='margin:0;background:red'></body>"></iframe>"""));
    }

    /// <summary>A frame with only <c>src</c> is unaffected — the path that already worked keeps
    /// working.</summary>
    [Fact]
    public void SrcOnly_StillRendersItsDocument()
    {
        Assert.Equal(Red, RenderFrameCentre(
            """<iframe src="data:text/html,<body style='margin:0;background:red'></body>"></iframe>"""));
    }

    /// <summary>
    /// An empty <c>srcdoc</c> is not a document to paint: the reference browser leaves the frame
    /// showing nothing, so the host page shows through rather than a blank canvas appearing.
    /// </summary>
    [Fact]
    public void EmptySrcdoc_PaintsNothing()
    {
        Assert.Equal(HostGreen, RenderFrameCentre("""<iframe srcdoc=""></iframe>"""));
    }

    /// <summary>Neither attribute is still an empty frame.</summary>
    [Fact]
    public void FrameWithNeitherAttribute_PaintsNothing()
    {
        Assert.Equal(HostGreen, RenderFrameCentre("<iframe></iframe>"));
    }

    /// <summary>
    /// The srcdoc markup is a whole document, not a fragment: its own <c>&lt;style&gt;</c> applies
    /// to it and not to the host (the host's green would otherwise be overwritten).
    /// </summary>
    [Fact]
    public void SrcdocStylesApplyInsideTheFrameOnly()
    {
        var html = """
<!DOCTYPE html>
<body style="margin:0;background:green">
<style>iframe{width:200px;height:150px;border:0}</style>
<iframe srcdoc="<style>body{margin:0;background:blue}</style><body></body>"></iframe>
</body>
""";
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 400, 300);

        var inside = bitmap.GetPixel(100, 75);
        Assert.Equal((0, 0, 255), (inside.R, inside.G, inside.B));

        // Outside the frame the host page keeps its own background.
        var outside = bitmap.GetPixel(300, 220);
        Assert.Equal((0, 128, 0), (outside.R, outside.G, outside.B));
    }

    /// <summary>
    /// A <c>&lt;frame&gt;</c> has no <c>srcdoc</c> content attribute, so one written on it is
    /// inert and the element falls back to <c>src</c>.
    /// </summary>
    [Fact]
    public void FrameElement_HasNoSrcdoc()
    {
        var html = """
<!DOCTYPE html>
<body style="margin:0;background:green">
<style>frame{display:block;width:200px;height:150px;border:0}</style>
<frame srcdoc="<body style='margin:0;background:blue'></body>" src="data:text/html,<body style='margin:0;background:red'></body>"></frame>
</body>
""";
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 400, 300);
        var pixel = bitmap.GetPixel(100, 75);
        Assert.Equal((255, 0, 0), (pixel.R, pixel.G, pixel.B));
    }
}
