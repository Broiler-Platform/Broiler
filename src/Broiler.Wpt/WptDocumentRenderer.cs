using System.Drawing;
using Broiler.Dom;
using Broiler.Graphics;
using Broiler.HTML.Core.Entities;
using Broiler.HTML.Image;
using Broiler.Layout.IR;

// Both Broiler.Graphics and Broiler.HTML.Image define a BBitmap; the renderer's is the one the
// container paints into and the one the comparer reads.
using BBitmap = Broiler.HTML.Image.BBitmap;

namespace Broiler.Wpt;

/// <summary>
/// Renders a <see cref="DomDocument"/> the way <c>HtmlRender.RenderToImageWithStyleSet</c> renders
/// an HTML string: the same container, the same layout and paint calls, the same embedded-document
/// compositing — bound with <c>SetDocumentWithStyleSet</c> instead of <c>SetHtmlWithStyleSet</c>.
/// </summary>
/// <remarks>
/// <para>
/// It exists because the string entry point is the only public one, and the round trip through it
/// is lossy in a way no amount of care in the serializer can fix: HTML tree construction is not the
/// inverse of a DOM. It unconditionally creates a <c>&lt;body&gt;</c>, so a document a script left
/// without one comes back with one — which is not a wrong render of the test, it is a correct
/// render of a different document. WPT's
/// <c>quirks/tables-inherit-color-from-body-quirk-004…-007</c> are built on exactly that case.
/// </para>
/// <para>
/// This lives in the runner rather than beside <c>HtmlRender</c> because that type is in the
/// <c>Broiler.HTML</c> submodule, which this session cannot push to; putting the document overload
/// there would make the main repository stop compiling the moment the submodule tree is reverted
/// to its pinned pointer. Everything it needs from the renderer is already public — the container,
/// its document binding, its layout and paint passes, and the fragment tree.
/// </para>
/// </remarks>
internal static class WptDocumentRenderer
{
    internal static BBitmap RenderToImage(
        DomDocument document,
        int width,
        int height,
        BColor backgroundColor,
        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs>? imageLoad,
        string? baseUrl)
    {
        var bitmap = new BBitmap(width, height);

        using var container = new HtmlContainer
        {
            Location = new PointF(0, 0),
            MaxSize = new SizeF(width, height),
            AvoidAsyncImagesLoading = true,
            AvoidImagesLateLoading = true,
        };

        if (stylesheetLoad != null)
            container.StylesheetLoad += stylesheetLoad;
        if (imageLoad != null)
            container.ImageLoad += imageLoad;

        // The string path publishes this from the markup it is handed
        // (HtmlContainerInt.SetHtmlWithStyleSet); binding a document does not, so the document's
        // own doctype has to say it here. Without this the render inherits whatever this thread
        // last rendered — which happens to be right, since the bridge parsed this very test, but
        // only by accident and only for as long as one thread renders one document.
        Broiler.Layout.DocumentModeContext.CurrentQuirksMode = SelectsQuirksMode(document);

        container.SetDocumentWithStyleSet(document, baseStyleSet: null, baseUrl: baseUrl);

        bitmap.Clear(backgroundColor);

        var clip = new RectangleF(0, 0, width, height);
        container.PerformLayout(bitmap, clip);
        container.PerformPaint(bitmap, clip);

        if (container.LatestFragmentTree is { } tree)
            CompositeEmbeddedDocuments(tree, bitmap, stylesheetLoad, imageLoad);

        return bitmap;
    }

    /// <summary>
    /// Quirks mode from the document's own doctype, by the same rule the string path applies to
    /// markup: no doctype, or one whose name is not <c>html</c>.
    /// </summary>
    private static bool SelectsQuirksMode(DomDocument document) =>
        document.DocumentType is not { } doctype
        || !doctype.Name.Equals("html", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Renders each embedded document (<c>&lt;iframe&gt;</c>, <c>&lt;object&gt;</c>,
    /// <c>&lt;frame&gt;</c>) over its box, mirroring <c>HtmlRender.CompositeEmbeddedDocuments</c>.
    /// The fragment carries the embedded document as markup, so this half is a string render in
    /// both paths.
    /// </summary>
    /// <remarks>
    /// This walk carries no depth counter, and deliberately: the one in <c>HtmlRender</c> bounds
    /// <em>nested document renders</em>, not fragment-tree levels, and stays constant as that walk
    /// descends. Counting levels here instead would stop compositing an <c>&lt;iframe&gt;</c> more
    /// than a few fragments deep — which is most of them. A self-embedding document is still
    /// bounded, by the counter inside the render each embed goes back through.
    /// </remarks>
    private static void CompositeEmbeddedDocuments(
        Fragment fragment,
        BBitmap target,
        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs>? imageLoad)
    {
        if (!string.IsNullOrEmpty(fragment.EmbeddedDocumentHtml))
        {
            var border = fragment.Border;
            var padding = fragment.Padding;

            int dx = (int)Math.Round(fragment.Location.X + (float)(border.Left + padding.Left));
            int dy = (int)Math.Round(fragment.Location.Y + (float)(border.Top + padding.Top));
            int dw = (int)Math.Round(fragment.Size.Width
                - (float)(border.Left + border.Right + padding.Left + padding.Right));
            int dh = (int)Math.Round(fragment.Size.Height
                - (float)(border.Top + border.Bottom + padding.Top + padding.Bottom));

            // A frameset track can still resolve to billions of pixels; allocating its RGBA buffer
            // would overflow Int32 and take the whole page render down with it.
            bool fitsAllocation = (long)dw * dh * 4 <= int.MaxValue;

            if (dw > 0 && dh > 0 && fitsAllocation)
            {
                using var sub = HtmlRender.RenderToImageWithStyleSet(
                    fragment.EmbeddedDocumentHtml!,
                    dw,
                    dh,
                    styleSet: null,
                    stylesheetLoad: stylesheetLoad,
                    imageLoad: imageLoad,
                    baseUrl: fragment.EmbeddedDocumentBaseUrl);
                BlitOnto(target, sub, dx, dy);
            }
        }

        foreach (var child in fragment.Children)
            CompositeEmbeddedDocuments(child, target, stylesheetLoad, imageLoad);
    }

    private static void BlitOnto(BBitmap target, BBitmap source, int destX, int destY)
    {
        for (int y = 0; y < source.Height; y++)
        {
            int ty = destY + y;
            if (ty < 0 || ty >= target.Height)
                continue;

            for (int x = 0; x < source.Width; x++)
            {
                int tx = destX + x;
                if (tx < 0 || tx >= target.Width)
                    continue;

                target.SetPixel(tx, ty, source.GetPixel(x, y));
            }
        }
    }
}
