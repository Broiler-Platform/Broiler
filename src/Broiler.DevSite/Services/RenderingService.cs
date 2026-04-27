using Broiler.HTML.Image;

namespace Broiler.DevSite.Services;

/// <summary>
/// Wraps the HtmlRenderer.Image rendering API for use by the ASP.NET Core DevSite.
/// Register as a scoped service via <c>builder.Services.AddScoped&lt;RenderingService&gt;()</c>.
/// </summary>
public sealed class RenderingService
{
    /// <summary>
    /// Renders HTML to a PNG-encoded byte array.
    /// </summary>
    public byte[] RenderHtmlToPng(string html, int width = 1024, int height = 768)
    {
        return HtmlRender.RenderToPng(html, width, height, BColor.White);
    }

    /// <summary>
    /// Renders HTML to a <see cref="BBitmap"/>. The caller owns the returned bitmap and must dispose it.
    /// </summary>
    public BBitmap RenderHtmlToBitmap(string html, int width = 1024, int height = 768)
    {
        return HtmlRender.RenderToImage(html, width, height, BColor.White);
    }

    /// <summary>
    /// Renders HTML scrolled to the element identified by <paramref name="elementId"/>
    /// and returns a PNG-encoded byte array of the viewport at that scroll position.
    /// Returns <c>null</c> if the element is not found.
    /// </summary>
    public byte[]? RenderAtAnchor(string html, string elementId, int width = 1024, int height = 768)
    {
        using var bitmap = HtmlRender.RenderToImageAtAnchor(html, elementId, width, height, BColor.White);
        return bitmap?.Encode(BImageFormat.Png, 100);
    }
}
