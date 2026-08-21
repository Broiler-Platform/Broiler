using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// WPT: html/rendering/non-replaced-elements/the-frameset-and-frame-elements/
/// frameset-visibility-hidden.html
///
/// A <c>&lt;frameset&gt;</c> with <c>visibility: hidden</c> must not paint its
/// frames' embedded documents — <c>visibility</c> is inherited, so the frame
/// box (a replaced element hosting a nested document) is itself hidden and its
/// replaced content is suppressed.  The reference is an empty page.
///
/// Before the fix, <c>CompositeEmbeddedDocuments</c> blitted every embedded
/// document unconditionally, so the frame's red content leaked through and the
/// test scored a 0% pixel match (ReferenceOverlayExposed).
/// </summary>
public class FramesetVisibilityTests
{
    // A minimal red page, embedded inline so the test needs no file resources.
    private const string RedFrameSrc =
        "data:text/html,<body style='margin:0;background:red'>";

    /// <summary>
    /// <c>visibility: hidden</c> on the frameset hides the frame content: the
    /// canvas stays white where the red frame would otherwise paint.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Frameset_Visibility_Hidden_Suppresses_Frame_Content()
    {
        var html = $@"<!DOCTYPE html>
<frameset cols=""100%"" style=""visibility: hidden"">
  <frame src=""{RedFrameSrc}"">
</frameset>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 200, baseUrl: "file:///test.html");

        var pixel = bitmap.GetPixel(100, 100);
        Assert.True(pixel.R >= 245 && pixel.G >= 245 && pixel.B >= 245,
            $"Expected an empty (white) canvas because the frameset is hidden, "
            + $"but got ({pixel.R},{pixel.G},{pixel.B}) — the frame content leaked through.");
    }

    /// <summary>
    /// Control: without <c>visibility: hidden</c> the same frame paints its red
    /// content, proving the frame path is exercised and the guard is specific
    /// to the hidden case.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Frameset_Visible_Paints_Frame_Content()
    {
        var html = $@"<!DOCTYPE html>
<frameset cols=""100%"">
  <frame src=""{RedFrameSrc}"">
</frameset>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 200, 200, baseUrl: "file:///test.html");

        var pixel = bitmap.GetPixel(100, 100);
        Assert.True(pixel.R >= 200 && pixel.G <= 40 && pixel.B <= 40,
            $"Expected the red frame content to paint but got ({pixel.R},{pixel.G},{pixel.B}).");
    }
}
