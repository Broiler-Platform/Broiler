using System.Drawing;
using Broiler.HTML.Image;
using SkiaSharp;

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
    /// Temporary compatibility shim that still renders HTML to an <see cref="SKBitmap"/>.
    /// The caller owns the returned bitmap and must dispose it.
    /// </summary>
    public SKBitmap RenderHtmlToImage(string html, int width = 1024, int height = 768)
    {
        return HtmlRender.RenderToImage(html, width, height, SKColors.White);
    }

    /// <summary>
    /// Renders the given HTML and compares the result pixel-by-pixel against a
    /// <paramref name="reference"/> bitmap, returning a <see cref="PixelDiffResult"/>.
    /// The caller is responsible for disposing the returned result.
    /// </summary>
    public PixelDiffResult CompareWithReference(string html, SKBitmap reference, int width = 1024, int height = 768)
    {
        using var actual = RenderHtmlToImage(html, width, height);
        return PixelDiffRunner.Compare(actual, reference);
    }

    /// <summary>
    /// Renders HTML scrolled to the element identified by <paramref name="elementId"/>
    /// and returns a PNG-encoded byte array of the viewport at that scroll position.
    /// Returns <c>null</c> if the element is not found.
    /// </summary>
    public byte[]? RenderAtAnchor(string html, string elementId, int width = 1024, int height = 768)
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.MaxSize = new SizeF(width, 99999);
        container.SetHtml(html);

        // Layout on a tall temporary canvas to measure the full page.
        using var layoutBmp = new SKBitmap(width, 2000, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var layoutCanvas = new SKCanvas(layoutBmp);
        layoutCanvas.Clear(SKColors.White);
        container.PerformLayout(layoutCanvas, new RectangleF(0, 0, width, 99999));

        var rect = container.GetElementRectangle(elementId);
        if (rect is null)
            return null;

        float scrollY = rect.Value.Y;

        // Constrain to the requested viewport and scroll to the anchor.
        container.Location = new PointF(0, scrollY);
        container.MaxSize = new SizeF(width, height);

        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.Translate(0, -scrollY);
        container.PerformPaint(canvas, new RectangleF(0, scrollY, width, height));
        canvas.Restore();

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
