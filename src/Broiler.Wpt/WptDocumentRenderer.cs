using System.Drawing;
using Broiler.CSS;
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
        string? baseUrl,
        BBitmap? baseLayer = null) =>
        RenderToImage(document, html: null, width, height, backgroundColor,
            stylesheetLoad, imageLoad, baseUrl, baseLayer);

    /// <summary>
    /// Renders <paramref name="document"/> — or <paramref name="html"/>, when no document was
    /// projected — onto a surface that starts as <paramref name="baseLayer"/>.
    /// </summary>
    /// <remarks>
    /// The base layer is what the render composites onto, exactly as the flat
    /// <paramref name="backgroundColor"/> is when there is none: the engine paints over whatever the
    /// surface already holds, so a canvas background the root propagates blends with the page
    /// background beneath it rather than replacing it. That is the whole of
    /// <c>page-box-002-print</c>, whose half-transparent red body over a blue <c>@page</c> has to
    /// come out violet.
    /// </remarks>
    internal static BBitmap RenderToImage(
        DomDocument? document,
        string? html,
        int width,
        int height,
        BColor backgroundColor,
        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs>? imageLoad,
        string? baseUrl,
        BBitmap? baseLayer = null) =>
        RenderToImage(document, html, width, height, backgroundColor,
            stylesheetLoad, imageLoad, baseUrl, baseLayer, out _);

    private static BBitmap RenderToImage(
        DomDocument? document,
        string? html,
        int width,
        int height,
        BColor backgroundColor,
        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs>? imageLoad,
        string? baseUrl,
        BBitmap? baseLayer,
        out Fragment? tree)
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

        if (document is not null)
        {
            // The string path publishes this from the markup it is handed
            // (HtmlContainerInt.SetHtmlWithStyleSet); binding a document does not, so the document's
            // own doctype has to say it here. Without this the render inherits whatever this thread
            // last rendered — which happens to be right, since the bridge parsed this very test, but
            // only by accident and only for as long as one thread renders one document.
            Broiler.Layout.DocumentModeContext.CurrentQuirksMode = SelectsQuirksMode(document);
            container.SetDocumentWithStyleSet(document, baseStyleSet: null, baseUrl: baseUrl);
        }
        else
        {
            container.SetHtmlWithStyleSet(html ?? string.Empty, baseStyleSet: null, baseUrl: baseUrl);
        }

        bitmap.Clear(backgroundColor);
        if (baseLayer is not null)
            BlitOnto(bitmap, baseLayer, 0, 0);

        var clip = new RectangleF(0, 0, width, height);
        container.PerformLayout(bitmap, clip);
        container.PerformPaint(bitmap, clip);

        tree = container.LatestFragmentTree;
        if (tree is not null)
            CompositeEmbeddedDocuments(tree, bitmap, stylesheetLoad, imageLoad);

        return bitmap;
    }

    /// <summary>
    /// Renders a document over the paint its own <c>@page</c> puts on the sheet: the page
    /// background, and the border and padding between the page's margin and its page area.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One page, not a paginated flow — this is the unpaginated render every <c>-print</c> reftest
    /// gets by default (<see cref="WptTestRunner.PagedPrint"/>), with the sheet's own paint put back
    /// under it. Without this a page background is simply not drawn, and a test whose reference
    /// states the same colour as a <c>body</c> background matches it by 0.0 %:
    /// <c>page-box-001-print</c>, <c>-002</c>, <c>-003</c> and <c>page-background-image-print</c> are
    /// each that failure.
    /// </para>
    /// <para>
    /// The flow is rendered into the page area — the border box less the page's border and padding —
    /// so a page that states them moves the content in by them, the way
    /// <c>page-box-011-print</c>'s reference moves it in with a <c>body</c> border and padding of
    /// its own.
    /// </para>
    /// </remarks>
    internal static BBitmap RenderDecorated(
        DomDocument? document,
        string html,
        WptPageBox page,
        WptPageDecoration decoration,
        BColor backgroundColor,
        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs>? imageLoad,
        string? baseUrl)
    {
        int sheetWidth = Math.Max(1, (int)Math.Round(page.BoxSize.Width));
        int sheetHeight = Math.Max(1, (int)Math.Round(page.BoxSize.Height));

        using var backdrop = decoration.Render(page, stylesheetLoad, imageLoad, baseUrl);
        var insets = decoration.MeasureInsets(page, stylesheetLoad, imageLoad, baseUrl);
        var borderBox = WptPageDecoration.BorderBox(page);

        int areaX = (int)Math.Round(borderBox.X + insets.Left);
        int areaY = (int)Math.Round(borderBox.Y + insets.Top);
        int areaWidth = Math.Max(1, (int)Math.Round(borderBox.Width - insets.Left - insets.Right));
        int areaHeight = Math.Max(1, (int)Math.Round(borderBox.Height - insets.Top - insets.Bottom));

        using var underneath = Crop(backdrop, areaX, areaY, areaWidth, areaHeight, backgroundColor);

        BBitmap flow;
        Fragment? tree;
        var previousBackdrop = Broiler.Layout.Engine.CanvasBackdrop.Current;
        Broiler.Layout.Engine.CanvasBackdrop.Current = UniformColor(underneath);
        try
        {
            flow = RenderToImage(
                document, html, areaWidth, areaHeight, backgroundColor,
                stylesheetLoad, imageLoad, baseUrl, baseLayer: underneath, tree: out tree);
        }
        finally
        {
            Broiler.Layout.Engine.CanvasBackdrop.Current = previousBackdrop;
        }

        using var _ = flow;

        var output = new BBitmap(sheetWidth, sheetHeight);
        output.Clear(backgroundColor);

        // A root element that generates no box generates no page either, so nothing of the sheet is
        // painted — not the flow, and not the `@page`'s own background and border.
        // `root-element-display-none-print` states exactly that, and its reference is an empty
        // document.
        if (!GeneratesPageContent(tree))
            return output;

        BlitOnto(output, backdrop, 0, 0);
        BlitOnto(output, flow, areaX, areaY);

        return output;
    }

    /// <summary>
    /// Whether a laid-out document puts anything on a page: some box below the fragment tree's root
    /// that is neither <c>display: none</c> nor collapsed to nothing.
    /// </summary>
    /// <remarks>
    /// The tree's root is the canvas rather than the root element, so a document whose root element
    /// is <c>display: none</c> still produces one — with every fragment under it <c>none</c> and
    /// zero-sized, which is what this looks for. An empty <c>&lt;body&gt;</c> is not that case: its
    /// box is the width of the page even when it is no lines tall, so a page whose only content is
    /// its own background still counts as generated.
    /// </remarks>
    private static bool GeneratesPageContent(Fragment? tree)
    {
        if (tree is null)
            return false;

        foreach (var child in tree.Children)
        {
            if (GeneratesBox(child))
                return true;
        }

        return false;

        static bool GeneratesBox(Fragment fragment)
        {
            if (!string.Equals(fragment.Style.Display, "none", StringComparison.OrdinalIgnoreCase)
                && (fragment.Size.Width > 0 || fragment.Size.Height > 0))
            {
                return true;
            }

            foreach (var child in fragment.Children)
            {
                if (GeneratesBox(child))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// The single colour <paramref name="bitmap"/> is painted in, or <c>null</c> when it is not all
    /// one colour.
    /// </summary>
    /// <remarks>
    /// What the canvas can be told it is compositing against
    /// (<c>Broiler.Layout.Engine.CanvasBackdrop</c>): the paint walker flattens a translucent
    /// propagated background into one opaque colour, so a backdrop it can use has to <em>be</em> one
    /// colour. A page whose background is an image or a gradient answers null and keeps the white
    /// assumption, which is what every render did before this existed.
    /// </remarks>
    private static BColor? UniformColor(BBitmap bitmap)
    {
        if (bitmap.Width == 0 || bitmap.Height == 0)
            return null;

        var first = bitmap.GetPixel(0, 0);
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != first)
                    return null;
            }
        }

        return first;
    }

    /// <summary>A rectangle of <paramref name="source"/>, padded with <paramref name="fill"/>.</summary>
    private static BBitmap Crop(BBitmap source, int x, int y, int width, int height, BColor fill)
    {
        var crop = new BBitmap(width, height);
        crop.Clear(fill);

        for (int row = 0; row < height; row++)
        {
            int sy = y + row;
            if (sy < 0 || sy >= source.Height)
                continue;

            for (int column = 0; column < width; column++)
            {
                int sx = x + column;
                if (sx < 0 || sx >= source.Width)
                    continue;

                crop.SetPixel(column, row, source.GetPixel(sx, sy));
            }
        }

        return crop;
    }

    /// <summary>The most pages a paged render lays out and composes.</summary>
    /// <remarks>
    /// A bound is needed because the surface is allocated before the content height is known, and
    /// every paged WPT reftest is a handful of pages at most — the whole point of one is to show a
    /// break landing where it should, which takes two.
    /// </remarks>
    internal const int MaxRenderedPages = 8;

    /// <summary>
    /// Renders <paramref name="document"/> (or <paramref name="html"/>, when no document was
    /// projected) as a sequence of pages of <paramref name="page"/>, stacked into one bitmap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The flow is laid out once, on a surface <see cref="MaxRenderedPages"/> page areas tall, with
    /// the container's page size set to one page area. The two are different things and the
    /// container already keeps them apart: the page area is what <c>vw</c>/<c>vh</c> and the
    /// fragmentation boundaries resolve against, while the surface is what gets rasterised. Page
    /// <c>k</c> is then the band of that surface from <c>k·H</c> to <c>(k+1)·H</c>, blitted into the
    /// output at its page's margin origin.
    /// </para>
    /// <para>
    /// The page count comes from the laid-out content height rather than being fixed, because a
    /// fixed count would pad every render with blank pages — and the comparison is a percentage of
    /// pixels, so blank pages both sides share would quietly inflate every match rate.
    /// </para>
    /// </remarks>
    internal static BBitmap RenderPaged(
        DomDocument? document,
        string html,
        WptPageBox page,
        BColor backgroundColor,
        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs>? imageLoad,
        string? baseUrl,
        WptPageDecoration? decoration = null)
    {
        // The page's own paint, and the border and padding it insets the page area by. Every page
        // of the flow gets the same sheet, so this is resolved once and stamped per page below.
        using var backdrop = decoration?.Render(page, stylesheetLoad, imageLoad, baseUrl);
        var insets = decoration?.MeasureInsets(page, stylesheetLoad, imageLoad, baseUrl) ?? (0, 0, 0, 0);

        var area = page.AreaSize;
        int areaWidth = Math.Max(1, (int)Math.Round(area.Width - insets.Item1 - insets.Item3));
        int areaHeight = Math.Max(1, (int)Math.Round(area.Height - insets.Item2 - insets.Item4));
        int areaX = (int)Math.Round(page.MarginLeft + insets.Item1);
        int areaY = (int)Math.Round(page.MarginTop + insets.Item2);

        using var surface = new BBitmap(areaWidth, areaHeight * MaxRenderedPages);

        using var container = new HtmlContainer
        {
            Location = new PointF(0, 0),
            MaxSize = new SizeF(surface.Width, surface.Height),
            AvoidAsyncImagesLoading = true,
            AvoidImagesLateLoading = true,
        };

        if (stylesheetLoad != null)
            container.StylesheetLoad += stylesheetLoad;
        if (imageLoad != null)
            container.ImageLoad += imageLoad;

        if (document is not null)
        {
            Broiler.Layout.DocumentModeContext.CurrentQuirksMode = SelectsQuirksMode(document);
            container.SetDocumentWithStyleSet(document, baseStyleSet: null, baseUrl: baseUrl);
        }
        else
        {
            container.SetHtmlWithStyleSet(html, baseStyleSet: null, baseUrl: baseUrl);
        }

        surface.Clear(backgroundColor);

        // Every page band starts as the sheet's page area, so a canvas background the root
        // propagates composites onto the page background instead of hiding it.
        if (backdrop is not null)
        {
            using var underneath = Crop(backdrop, areaX, areaY, areaWidth, areaHeight, backgroundColor);
            for (int p = 0; p < MaxRenderedPages; p++)
                BlitOnto(surface, underneath, 0, p * areaHeight);
        }

        var clip = new RectangleF(0, 0, surface.Width, surface.Height);

        SetPageSize(container, new SizeF(areaWidth, areaHeight));
        container.PerformLayout(surface, clip);

        SetPageSize(container, new SizeF(surface.Width, surface.Height));
        container.PerformPaint(surface, clip);

        if (container.LatestFragmentTree is { } tree)
            CompositeEmbeddedDocuments(tree, surface, stylesheetLoad, imageLoad);

        int pages = Math.Clamp(
            (int)Math.Ceiling(container.ActualSize.Height / areaHeight - 0.01), 1, MaxRenderedPages);

        int boxWidth = Math.Max(1, (int)Math.Round(page.BoxSize.Width));
        int boxHeight = Math.Max(1, (int)Math.Round(page.BoxSize.Height));

        var output = new BBitmap(boxWidth, boxHeight * pages);
        output.Clear(backgroundColor);

        // The sheet's own paint under everything, then the margin ring, then the flow's band over
        // the page area both leave blank. The last two never overlap — a margin box lives in the
        // margin by construction — so their order only decides which one pays for the rounding at
        // the page area's edge, and the flow is the one the rest of the render is measured against.
        if (backdrop is not null)
        {
            for (int p = 0; p < pages; p++)
                BlitOnto(output, backdrop, 0, p * boxHeight);
        }

        PaintMarginBoxes(output, html, page, pages, boxWidth, boxHeight, stylesheetLoad, imageLoad, baseUrl);

        for (int p = 0; p < pages; p++)
            BlitBand(output, surface, p * areaHeight, areaX, p * boxHeight + areaY, areaWidth, areaHeight);

        return output;
    }

    /// <summary>
    /// Paints each page's CSS Paged Media 3 §5 margin boxes into <paramref name="output"/>.
    /// </summary>
    /// <remarks>
    /// Once per page rather than once per document, because a margin box may say which page it is
    /// on — <c>content: "Page " counter(page) " of " counter(pages)</c> is the reason page margin
    /// boxes exist at all. A document with no margin boxes builds no overlay and renders nothing
    /// extra, which is every test outside <c>css-page/margin-boxes</c>.
    /// </remarks>
    private static void PaintMarginBoxes(
        BBitmap output,
        string html,
        WptPageBox page,
        int pages,
        int boxWidth,
        int boxHeight,
        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs>? imageLoad,
        string? baseUrl)
    {
        var (boxes, pageDeclarations) = WptPageMarginBoxes.Resolve(html);
        if (boxes.Count == 0)
            return;

        var measured = MeasureMarginBoxes(page, boxes, pageDeclarations, stylesheetLoad, imageLoad, baseUrl);

        for (int p = 0; p < pages; p++)
        {
            var overlayHtml = WptPageMarginOverlay.Build(
                page, boxes, pageDeclarations, measured, p + 1, pages);
            if (overlayHtml is null)
                return;

            using var overlay = HtmlRender.RenderToImageWithStyleSet(
                overlayHtml, boxWidth, boxHeight,
                styleSet: null,
                stylesheetLoad: stylesheetLoad,
                imageLoad: imageLoad,
                baseUrl: baseUrl);

            BlitOnto(output, overlay, 0, p * boxHeight);
        }
    }

    /// <summary>
    /// The size each margin box comes out as on its own — its outer size when it states one, and
    /// its max-content size when it does not. CSS Paged Media 3 §5.3.2 shares an edge out by both.
    /// </summary>
    /// <remarks>
    /// Read off a render rather than out of a box tree: the measure document paints each box, and
    /// its border, in a colour of its own, so the extent of that colour <em>is</em> the border box.
    /// It costs one render per document, not per page, and it is the same renderer that will draw
    /// the page — so a box that measures one way and draws another is not a failure mode this can
    /// have.
    /// </remarks>
    private static IReadOnlyDictionary<WptMarginBoxSlot, SizeF> MeasureMarginBoxes(
        WptPageBox page,
        IReadOnlyDictionary<WptMarginBoxSlot, IReadOnlyList<CssDeclaration>> boxes,
        IReadOnlyList<CssDeclaration> pageDeclarations,
        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs>? imageLoad,
        string? baseUrl)
    {
        var measured = new Dictionary<WptMarginBoxSlot, SizeF>();
        var slots = WptPageMarginOverlay.MeasuredSlots(boxes);
        if (slots.Count == 0)
            return measured;

        var html = WptPageMarginOverlay.MeasureDocument(
            page, boxes, pageDeclarations, slots, out var surfaceSize);

        using var surface = HtmlRender.RenderToImageWithStyleSet(
            html, Math.Max(1, surfaceSize.Width), Math.Max(1, surfaceSize.Height),
            styleSet: null,
            stylesheetLoad: stylesheetLoad,
            imageLoad: imageLoad,
            baseUrl: baseUrl);

        var extents = new (int MinX, int MinY, int MaxX, int MaxY)[slots.Count];
        for (int i = 0; i < slots.Count; i++)
            extents[i] = (int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);

        var wanted = new Dictionary<(int, int, int), int>();
        for (int i = 0; i < slots.Count; i++)
            wanted[WptPageMarginOverlay.MeasureRgb(i)] = i;

        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                var pixel = surface.GetPixel(x, y);
                if (!wanted.TryGetValue((pixel.R, pixel.G, pixel.B), out int i))
                    continue;

                ref var extent = ref extents[i];
                extent.MinX = Math.Min(extent.MinX, x);
                extent.MinY = Math.Min(extent.MinY, y);
                extent.MaxX = Math.Max(extent.MaxX, x);
                extent.MaxY = Math.Max(extent.MaxY, y);
            }
        }

        for (int i = 0; i < slots.Count; i++)
        {
            var extent = extents[i];
            measured[slots[i]] = extent.MaxX < extent.MinX
                ? SizeF.Empty
                : new SizeF(extent.MaxX - extent.MinX + 1, extent.MaxY - extent.MinY + 1);
        }

        return measured;
    }

    /// <summary>
    /// Sets the container's page size — a different thing from its <c>MaxSize</c>, and the reason
    /// this render needs to say both. The page is what <c>vw</c>/<c>vh</c> and the fragmentation
    /// boundaries resolve against; the surface is what gets rasterised.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called twice, with two different values, because the container derives its paint viewport
    /// as <c>min(MaxSize, PageSize)</c> — so a page-sized page size would bound the paint to page
    /// one and silently drop the rest, which is exactly the blank-page bug this render was built
    /// against. Laying out against the page and painting against the surface says the two things
    /// the one field has to mean at the two moments it is read. Nothing between the two calls
    /// reads it: layout consumes it for the viewport and the boundaries, paint consumes it only in
    /// <c>GetPaintViewport</c>.
    /// </para>
    /// <para>
    /// Through reflection because the setter is not on this side of the assembly boundary:
    /// <c>HtmlContainerInt</c> has held the page and the surface apart all along, and it is
    /// public, but the <c>HtmlContainer.HtmlContainerInt</c> that owns the instance is
    /// <c>internal</c>. Adding the passthrough is a <c>Broiler.HTML</c> change this session cannot
    /// push (403), and taking it as a patch would make the main repository stop compiling the
    /// moment the submodule tree is reverted to its pinned pointer — the failure mode
    /// <c>CLAUDE.md</c> warns about. A runner-side reflection keeps the paged render buildable and
    /// testable on a clean checkout; if the property is ever renamed,
    /// <c>PagedPrintRenderTests</c> fails immediately and loudly.
    /// </para>
    /// </remarks>
    private static void SetPageSize(HtmlContainer container, SizeF pageSize)
    {
        var containerInt = ContainerIntProperty.GetValue(container)
            ?? throw new InvalidOperationException("HtmlContainer has no HtmlContainerInt.");

        var pageSizeProperty = containerInt.GetType().GetProperty("PageSize")
            ?? throw new InvalidOperationException("HtmlContainerInt has no PageSize property.");

        pageSizeProperty.SetValue(containerInt, pageSize);
    }

    private static readonly System.Reflection.PropertyInfo ContainerIntProperty =
        typeof(HtmlContainer).GetProperty(
            "HtmlContainerInt",
            System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("HtmlContainer has no HtmlContainerInt property.");

    /// <summary>Copies one page-height band of <paramref name="source"/> onto <paramref name="target"/>.</summary>
    private static void BlitBand(
        BBitmap target, BBitmap source, int sourceTop, int destX, int destY, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            int sy = sourceTop + y;
            int ty = destY + y;
            if (sy < 0 || sy >= source.Height || ty < 0 || ty >= target.Height)
                continue;

            for (int x = 0; x < width; x++)
            {
                int tx = destX + x;
                if (x >= source.Width || tx < 0 || tx >= target.Width)
                    continue;

                target.SetPixel(tx, ty, source.GetPixel(x, sy));
            }
        }
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
