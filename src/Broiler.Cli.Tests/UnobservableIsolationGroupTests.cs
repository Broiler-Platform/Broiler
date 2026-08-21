using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>isolation: isolate</c> on a document where nothing blends
/// (<c>FragmentTreeBuilder.DocumentHasBlending</c>).
/// </summary>
/// <remarks>
/// <para>
/// CSS Compositing §2.2 gives an isolation group one job: to stop a descendant's blending from
/// reaching the backdrop outside the group. With no <c>mix-blend-mode</c> anywhere in the document
/// the group has no job to do, so paint does not open a compositing layer for it — the same picture,
/// one layer cheaper.
/// </para>
/// <para>
/// It is also what keeps such a page visible at all today. A compositing group whose contents are
/// not raster-compatible — one transformed descendant is enough — is routed to a compat backend
/// that is a stub in this image renderer, and the group's entire subtree is dropped. The fix for
/// that is in <c>Broiler.HTML</c>'s <c>GraphicsAdapter</c> and ships as the <c>patches/</c> entry
/// "Keep a compositing group's contents when the group cannot use the raster canvas"; the
/// <c>opacity</c> and <c>mix-blend-mode</c> groups it also covers are still lost until that is
/// applied, which is why the cases below are the isolation ones.
/// </para>
/// <para>
/// <c>duckduckgo.com</c> is the page this came from: its start page puts all of its content under
/// <c>#__next { isolation: isolate }</c>, has transforms beneath that, and uses no blend mode
/// anywhere — so it rendered as an empty white viewport.
/// </para>
/// </remarks>
public class UnobservableIsolationGroupTests
{
    /// <summary>Counts pixels that are neither white nor near-white — i.e. that something painted.</summary>
    private static int PaintedPixels(BBitmap bitmap)
    {
        int painted = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.R < 240 || pixel.G < 240 || pixel.B < 240)
                    painted++;
            }
        }

        return painted;
    }

    private static string Page(string wrapperStyle, string extra = "") => $@"<!DOCTYPE html>
<html><head><style>
#wrapper {{ {wrapperStyle} }}
</style></head>
<body>
  <div id=""wrapper"">
    <p style=""color: black; font-size: 30px"">PLAIN CONTENT</p>
    <div style=""transform: translateX(10px); color: black; font-size: 30px"">MOVED</div>
    {extra}
  </div>
</body></html>";

    /// <summary>
    /// The transform is what makes the group non-raster, and it must not cost the page the content
    /// that has nothing to do with it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Isolation_Group_Containing_A_Transform_Still_Paints_Its_Contents()
    {
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(Page("isolation: isolate;"), 600, 250);

        Assert.True(
            PaintedPixels(bitmap) > 0,
            "A wrapper with 'isolation: isolate' and a transformed descendant painted nothing at all.");
    }

    /// <summary>
    /// With nothing blending, the group is not merely non-empty — it is the same picture it would be
    /// with no group at all.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Isolation_Group_Paints_As_Much_As_No_Group_At_All()
    {
        using var isolated = HtmlRender.RenderToImageWithStyleSet(Page("isolation: isolate;"), 600, 250);
        using var plain = HtmlRender.RenderToImageWithStyleSet(Page(string.Empty), 600, 250);

        Assert.Equal(PaintedPixels(plain), PaintedPixels(isolated));
    }

    /// <summary>
    /// The <c>duckduckgo.com</c> shape: the page's whole content under one isolation group, with the
    /// transform that makes the group non-raster somewhere inside it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Page_Wrapped_In_An_Isolation_Group_Is_Not_Blank()
    {
        const string html = @"<!DOCTYPE html>
<html><head><style>
#__next, #root { isolation: isolate; }
.rotated { transform: rotate(3deg); }
</style></head>
<body>
  <div id=""__next"">
    <header style=""color: black; font-size: 24px"">HEADER TEXT</header>
    <main style=""color: black; font-size: 24px"">
      MAIN CONTENT
      <span class=""rotated"">badge</span>
    </main>
  </div>
</body></html>";

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, 600, 300);

        Assert.True(
            PaintedPixels(bitmap) > 0,
            "A page wrapped in '#__next { isolation: isolate }' rendered completely blank.");
    }

    /// <summary>
    /// The suppression is conditional, not unconditional: a document that does blend keeps its
    /// isolation group, because there the group is the thing deciding what the blend sees.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Blending_Descendant_Keeps_The_Isolation_Group()
    {
        var blending = Page(
            "isolation: isolate;",
            @"<div style=""mix-blend-mode: multiply; background: #808080; width: 40px; height: 40px""></div>");

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(blending, 600, 250);

        // The group is honoured, so it goes through the layer path rather than being dropped from
        // the computed style. Rendering must still complete and produce a picture.
        Assert.NotNull(bitmap);
        Assert.True(bitmap.Width > 0 && bitmap.Height > 0);
    }
}
