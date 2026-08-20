using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

    /// <summary>A stretch of consecutive pages whose content all names the same page.</summary>
    private readonly record struct PageRun(string? Name, int FirstBand, int LastBand)
    {
        /// <summary>How many pages the run occupies.</summary>
        internal int Count => LastBand - FirstBand + 1;
    }

    /// <summary>
    /// The runs of pages a laid-out flow divides into: each stretch of the document that names one
    /// page, and the bands of <paramref name="tree"/> it occupies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A page takes its <c>@page</c> rule from the content on it, so the names have to be read back
    /// off the laid-out flow rather than from the document as a whole — the same document can put
    /// one page on <c>@page b</c> and leave its neighbours on the unconditional rule, which is what
    /// <c>page-name-unnamed-trailing-001</c> is built from.
    /// </para>
    /// <para>
    /// <b>Runs, and not a name per band.</b> Reading a name off whatever fragment happens to start
    /// each band answers a question the flow cannot always be asked: content that overflows its page
    /// area lands on bands that are not pages of its own, and scanning them invents runs that no
    /// part of the document asked for — <c>page-size-007-print</c>'s <c>larger</c> container, six
    /// hundred pixels wide on a two-hundred-pixel page, produced a phantom trailing page that way.
    /// The document's <em>order</em> of runs is a property of the document, so it is read from the
    /// document order of the flow; only how many pages each run fills depends on the page area, and
    /// that is read from the pass that used the right one.
    /// </para>
    /// <para>
    /// A fragment contributes its bounds to the current run only when its whole subtree names one
    /// page — an ancestor that spans a name change is descended into instead, because it is not on a
    /// page of its own. That is the same rule <c>CssBox.StartPageName</c> applies in the flow: a box
    /// wrapping content that names a different page does not itself claim a page, so
    /// <c>page-name-unnamed-trailing-001</c>'s plain <c>.page</c> wrapper gives way to the
    /// <c>page: landscape</c> child inside it.
    /// </para>
    /// <para>
    /// Only block-level, in-flow fragments are read. An inline fragment's <c>Location</c> is not in
    /// the surface's coordinates — the walk sees inline boxes reporting a top of 40 on a page that
    /// begins at 300 — and an inline-level box may not carry a page name in the first place
    /// (<c>CssBox.TakesAPageName</c>), which is what
    /// <c>&lt;canvas style="page: landscape"&gt;</c> in that test is there to check. An out-of-flow
    /// one is laid out against a containing block rather than the flow, so it names no page either
    /// (<c>CssBox.ParticipatesInPageNamePropagation</c>).
    /// </para>
    /// </remarks>
    private static List<PageRun> ReadRuns(Fragment? tree, int areaHeight, int pages)
    {
        var runs = new List<PageRun>();
        if (tree is null || areaHeight <= 0)
            return runs;

        foreach (var child in tree.Children)
            Visit(child, null);

        // The runs have to partition the pass's pages exactly, because how many pages a flow fills
        // is settled by its content height and not by this walk: a fragment's bounds can fall short
        // of it (a page held open by out-of-flow content) or run past it (a box overflowing its page
        // area). Reading the count off the walk instead cost nine tests — `fixedpos-003`,
        // `monolithic-overflow-012` and the rest — every one of them a page-count disagreement.
        // So the walk says where each run *starts*, and a run ends where the next one begins; only
        // the last one is closed by the content height. A run's own bottom edge is deliberately not
        // read: a box that overflows its page area runs past the pages it is on, which is what made
        // `page-name-unnamed-trailing-001`'s landscape page swallow the plain page after it.
        for (int i = 0; i < runs.Count; i++)
        {
            int last = i == runs.Count - 1 ? pages - 1 : runs[i + 1].FirstBand - 1;
            runs[i] = runs[i] with { LastBand = Math.Max(runs[i].FirstBand, last) };
        }

        return runs;

        void Visit(Fragment fragment, string? inherited)
        {
            if (!InThePageFlow(fragment))
                return;

            var name = Named(fragment.Style.Page) ?? inherited;

            if (Uniform(fragment, name))
            {
                Extend(name, fragment.Bounds);
                return;
            }

            foreach (var child in fragment.Children)
                Visit(child, name);
        }

        // Whether nothing below `fragment` names a page other than `name`, so the whole subtree
        // sits on one kind of page and its bounds describe that run.
        static bool Uniform(Fragment fragment, string? name)
        {
            foreach (var child in fragment.Children)
            {
                if (!InThePageFlow(child))
                    continue;

                var childName = Named(child.Style.Page) ?? name;
                if (!string.Equals(childName, name, StringComparison.OrdinalIgnoreCase)
                    || !Uniform(child, childName))
                {
                    return false;
                }
            }

            return true;
        }

        void Extend(string? name, RectangleF bounds)
        {
            // Same name as the run in progress: the same run continues, wherever this box sits.
            if (runs.Count > 0 && string.Equals(runs[^1].Name, name, StringComparison.OrdinalIgnoreCase))
                return;

            // A page-name change forces a break, so a new run starts on the page its content starts
            // on. When it starts on a page the run before it already holds, the break did not
            // happen and the name has no page of its own — the first run wins the page.
            int first = Band(bounds.Top);
            if (runs.Count == 0)
                runs.Add(new PageRun(name, 0, 0));
            else if (first > runs[^1].FirstBand)
                runs.Add(new PageRun(name, first, first));
        }

        int Band(float y) => Math.Clamp((int)(y / areaHeight), 0, Math.Max(0, pages - 1));
    }

    /// <summary>
    /// Whether a fragment is laid out in the page's block flow, so its page name names a page and
    /// its bounds say which ones.
    /// </summary>
    private static bool InThePageFlow(Fragment fragment) =>
        !IsInlineLevel(fragment.Style.Display)
        && !fragment.Style.Position.Equals("absolute", StringComparison.OrdinalIgnoreCase)
        && !fragment.Style.Position.Equals("fixed", StringComparison.OrdinalIgnoreCase);

    private static bool IsInlineLevel(string? display)
    {
        var value = display?.Trim();
        return string.IsNullOrEmpty(value)
            || value.Equals("none", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("inline", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A declared <c>page</c> value, or <c>null</c> when it names no page.</summary>
    private static string? Named(string? page)
    {
        var value = page?.Trim();
        return string.IsNullOrEmpty(value)
            || value.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }

    /// <summary>
    /// The pages of a <paramref name="pages"/>-page render that take part in the comparison: the
    /// ones <c>&lt;meta name="reftest-pages"&gt;</c> names, or all of them when it names none.
    /// </summary>
    /// <remarks>
    /// A page the document asks for that the render does not have is dropped rather than blanked,
    /// and a declaration that leaves nothing at all falls back to every page — a comparison against
    /// an empty image says less than one against the wrong number of pages.
    /// </remarks>
    private static IReadOnlyList<int> ComparedPages(string html, int pages)
    {
        var requested = WptReftestPages.Parse(html);
        if (requested is null)
            return Enumerable.Range(1, pages).ToArray();

        var kept = requested.Where(page => page <= pages).ToArray();
        return kept.Length == 0 ? Enumerable.Range(1, pages).ToArray() : kept;
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
    /// projected) as a sequence of pages, stacked into one bitmap. <paramref name="page"/> is the
    /// box a page with no name of its own is printed on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The flow is laid out on a surface <see cref="MaxRenderedPages"/> page areas tall, with the
    /// container's page size set to one page area. The two are different things and the container
    /// already keeps them apart: the page area is what <c>vw</c>/<c>vh</c> and the fragmentation
    /// boundaries resolve against, while the surface is what gets rasterised. Page <c>k</c> is then
    /// the band of that surface from <c>k·H</c> to <c>(k+1)·H</c>, blitted into the output at its
    /// page's margin origin.
    /// </para>
    /// <para>
    /// <b>One layout per page area, not one per document.</b> A named <c>@page</c> rule may size the
    /// page differently, and then the flow on those pages has to divide against <em>that</em> area —
    /// where the floats wrap, where the page breaks fall. One continuous surface has one width and
    /// one band height, so it can express one area and no more. What it can do is be laid out again:
    /// the document is laid out once per distinct page area, and each run of consecutive pages
    /// sharing a name is taken from the pass that used its area. That is sound because a page-name
    /// change forces a break (CSS Paged Media 3 §5.3), so a run always starts at a page boundary in
    /// every pass, and its content divides against its own area alone.
    /// </para>
    /// <para>
    /// The order the runs come in is read from the pass for the unnamed page; only each run's
    /// <em>length</em> is taken from its own pass, because that is the part the area decides.
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
        WptPageDecoration? decoration = null,
        Func<string?, bool, WptPageBox>? resolvePage = null)
    {
        // A page's own paint, cached per box: pages of different sizes cannot share one, and the
        // same box recurs on every page of a run.
        var backdrops = new Dictionary<WptPageBox, BBitmap>();
        var passes = new List<PagedPass>();

        try
        {
            var basePass = LayOut(
                document, html, page, backgroundColor, decoration, BackdropFor,
                stylesheetLoad, imageLoad, baseUrl);
            passes.Add(basePass);

            int boxWidth = Math.Max(1, (int)Math.Round(page.BoxSize.Width));
            int boxHeight = Math.Max(1, (int)Math.Round(page.BoxSize.Height));

            // A root element that generates no box generates no page either, so the sheet keeps none
            // of its own paint — not the `@page` background or border, and not the margin boxes.
            // `root-element-display-none-print` states exactly that against a blank reference, and
            // the unpaginated path has honoured it since the page paint landed; this one was still
            // stamping a decorated first page. One blank sheet: an empty document is one empty page.
            if (!basePass.GeneratesContent)
            {
                var blank = new BBitmap(boxWidth, boxHeight);
                blank.Clear(backgroundColor);
                return blank;
            }

            // Each output page: which pass it is taken from, which band of that pass, and the name
            // that put it there.
            var slots = Schedule(
                basePass, page, resolvePage,
                name => Pass(name), MaxRenderedPages);

            // Which of those pages the comparison actually wants. A test that only needs a page to
            // exist so the next one is reachable says so, and its reference is then a shorter
            // document; emitting the pages it excluded compares two documents of different lengths.
            var compared = ComparedPages(html, slots.Count);

            // Page one may have a box of its own — `@page :first` describes it and nothing else — so
            // each emitted page carries the box it is actually printed on. The sheet is as wide as
            // the widest of them and as tall as they add up to.
            var sheets = compared
                .Select(number => resolvePage is null
                    ? page
                    : resolvePage(slots[number - 1].Name, number == 1))
                .ToArray();

            var slotTop = new int[sheets.Length];
            int sheetWidth = 1, sheetHeight = 0;
            for (int slot = 0; slot < sheets.Length; slot++)
            {
                slotTop[slot] = sheetHeight;
                sheetWidth = Math.Max(sheetWidth, (int)Math.Round(sheets[slot].BoxSize.Width));
                sheetHeight += Math.Max(1, (int)Math.Round(sheets[slot].BoxSize.Height));
            }

            var output = new BBitmap(sheetWidth, Math.Max(1, sheetHeight));
            output.Clear(backgroundColor);

            // The sheet's own paint under everything, then the margin ring, then the flow's band over
            // the page area both leave blank. The last two never overlap — a margin box lives in the
            // margin by construction — so their order only decides which one pays for the rounding at
            // the page area's edge, and the flow is the one the rest of the render is measured against.
            for (int slot = 0; slot < sheets.Length; slot++)
            {
                if (BackdropFor(sheets[slot]) is { } sheetBackdrop)
                    BlitOnto(output, sheetBackdrop, 0, slotTop[slot]);
            }

            // Margin boxes are still stamped from one box for the whole document — they are declared
            // by the unconditional rule in every test that has them, and none of those names a page.
            var stamp = sheets.Length > 0 ? sheets[0] : page;
            PaintMarginBoxes(
                output, html, stamp, compared.Count,
                Math.Max(1, (int)Math.Round(stamp.BoxSize.Width)),
                Math.Max(1, (int)Math.Round(stamp.BoxSize.Height)),
                stylesheetLoad, imageLoad, baseUrl);

            for (int slot = 0; slot < sheets.Length; slot++)
            {
                // `compared` is one-based, the way the markup writes it.
                var source = slots[compared[slot] - 1];
                var sheet = sheets[slot];
                BlitBand(
                    output, source.Pass.Surface, source.Band * source.Pass.AreaHeight,
                    (int)Math.Round(sheet.MarginLeft + source.Pass.InsetLeft),
                    slotTop[slot] + (int)Math.Round(sheet.MarginTop + source.Pass.InsetTop),
                    source.Pass.AreaWidth, source.Pass.AreaHeight);
            }

            return output;

            PagedPass Pass(string name)
            {
                var box = resolvePage!(name, false);
                if (box == page)
                    return basePass;

                var named = LayOut(
                    document, html, box, backgroundColor, decoration, BackdropFor,
                    stylesheetLoad, imageLoad, baseUrl);
                passes.Add(named);
                return named;
            }
        }
        finally
        {
            foreach (var pass in passes)
                pass.Dispose();
            foreach (var backdrop in backdrops.Values)
                backdrop.Dispose();
        }

        BBitmap? BackdropFor(WptPageBox box)
        {
            if (decoration is null)
                return null;

            if (!backdrops.TryGetValue(box, out var rendered))
                backdrops[box] = rendered = decoration.Render(box, stylesheetLoad, imageLoad, baseUrl);

            return rendered;
        }
    }

    /// <summary>
    /// One layout of the whole document against one page area, rasterised onto a continuous surface
    /// <see cref="MaxRenderedPages"/> areas tall.
    /// </summary>
    private sealed class PagedPass : IDisposable
    {
        internal required BBitmap Surface { get; init; }

        /// <summary>The page area this pass laid the flow out against — one band of the surface.</summary>
        internal required int AreaWidth { get; init; }

        /// <inheritdoc cref="AreaWidth"/>
        internal required int AreaHeight { get; init; }

        /// <summary>Where the page area starts inside the page box, past the border and padding.</summary>
        internal required float InsetLeft { get; init; }

        /// <inheritdoc cref="InsetLeft"/>
        internal required float InsetTop { get; init; }

        /// <summary>How many pages the flow filled in this pass.</summary>
        internal required int Pages { get; init; }

        /// <summary>The runs of pages this pass's flow divided into, in document order.</summary>
        internal required List<PageRun> Runs { get; init; }

        /// <summary>Whether the root element generated a box at all.</summary>
        internal required bool GeneratesContent { get; init; }

        public void Dispose() => Surface.Dispose();
    }

    /// <summary>Lays the document out against <paramref name="page"/>'s area and rasterises it.</summary>
    private static PagedPass LayOut(
        DomDocument? document,
        string html,
        WptPageBox page,
        BColor backgroundColor,
        WptPageDecoration? decoration,
        Func<WptPageBox, BBitmap?> backdropFor,
        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs>? imageLoad,
        string? baseUrl)
    {
        var insets = decoration?.MeasureInsets(page, stylesheetLoad, imageLoad, baseUrl) ?? (0f, 0f, 0f, 0f);

        var area = page.AreaSize;
        int areaWidth = Math.Max(1, (int)Math.Round(area.Width - insets.Item1 - insets.Item3));
        int areaHeight = Math.Max(1, (int)Math.Round(area.Height - insets.Item2 - insets.Item4));
        int areaX = (int)Math.Round(page.MarginLeft + insets.Item1);
        int areaY = (int)Math.Round(page.MarginTop + insets.Item2);

        var surface = new BBitmap(areaWidth, areaHeight * MaxRenderedPages);

        try
        {
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
            if (backdropFor(page) is { } backdrop)
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

            return new PagedPass
            {
                Surface = surface,
                AreaWidth = areaWidth,
                AreaHeight = areaHeight,
                InsetLeft = insets.Item1,
                InsetTop = insets.Item2,
                Pages = pages,
                Runs = ReadRuns(container.LatestFragmentTree, areaHeight, pages),
                GeneratesContent = GeneratesPageContent(container.LatestFragmentTree),
            };
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    /// <summary>Where each output page's pixels come from.</summary>
    private readonly record struct PageSlot(PagedPass Pass, int Band, string? Name);

    /// <summary>
    /// Assembles the output's pages: the sequence of page-name runs the unnamed pass found, with
    /// each run's pages taken from the pass that used that run's own page area.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A run's <em>length</em> is what its area decides, so it is read from that run's own pass, not
    /// from the pass that established the order. <c>page-size-007-print</c> is the shape: three
    /// containers of nine identical floats, each on a differently sized named page, and each is
    /// meant to fill one page and put its last float on the next — which only comes out right if the
    /// floats wrap against the width of the page they are on and break at its height.
    /// </para>
    /// <para>
    /// Matching a run to its counterpart in its own pass is positional: the <c>r</c>-th run of name
    /// <c>N</c> here is the <c>r</c>-th run of name <c>N</c> there. The flow is the same document in
    /// both, and only the pagination differs, so the runs occur in the same order. When the other
    /// pass does not have that run at all — it paginated so differently that the name never got a
    /// page — the run keeps the length and bands the ordering pass gave it rather than vanishing.
    /// </para>
    /// </remarks>
    private static List<PageSlot> Schedule(
        PagedPass basePass,
        WptPageBox page,
        Func<string?, bool, WptPageBox>? resolvePage,
        Func<string, PagedPass> passFor,
        int maxPages)
    {
        var slots = new List<PageSlot>();
        var placed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var run in basePass.Runs)
        {
            var pass = basePass;
            var placement = run;

            if (run.Name is { } name && resolvePage is not null)
            {
                int ordinal = placed.TryGetValue(name, out int seen) ? seen : 0;
                placed[name] = ordinal + 1;

                var named = passFor(name);
                if (!ReferenceEquals(named, basePass) && RunAt(named.Runs, name, ordinal) is { } own)
                {
                    pass = named;
                    placement = own;
                }
            }

            for (int band = placement.FirstBand; band <= placement.LastBand && slots.Count < maxPages; band++)
                slots.Add(new PageSlot(pass, band, run.Name));
        }

        // A document whose flow named nothing at all still has pages: the run walk only describes
        // where the names change, and a flow with no block-level content in it changes nowhere.
        if (slots.Count == 0)
        {
            for (int band = 0; band < basePass.Pages && slots.Count < maxPages; band++)
                slots.Add(new PageSlot(basePass, band, null));
        }

        return slots;
    }

    /// <summary>The <paramref name="ordinal"/>-th run of <paramref name="name"/>, or null.</summary>
    private static PageRun? RunAt(List<PageRun> runs, string name, int ordinal)
    {
        int seen = 0;
        foreach (var run in runs)
        {
            if (!string.Equals(run.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (seen++ == ordinal)
                return run;
        }

        return null;
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
                // CSS Color Adjust §2.4: this frame's canvas is transparent unless its used colour
                // scheme differs from *this element's* — the embedding element's, which may carry
                // its own `color-scheme` independent of the containing document's. Pinned around
                // the render so the frame's own canvas paint sees its embedder, exactly as
                // HtmlRender.CompositeEmbeddedDocuments does it on the string-render path.
                using var embedded = Broiler.Layout.Engine.EmbeddedCanvas.Pin(fragment.Style.ColorScheme);
                using var sub = HtmlRender.RenderToImageWithStyleSet(
                    fragment.EmbeddedDocumentHtml!,
                    dw,
                    dh,
                    styleSet: null,
                    stylesheetLoad: stylesheetLoad,
                    imageLoad: imageLoad,
                    baseUrl: fragment.EmbeddedDocumentBaseUrl);
                BlitFrameOnto(target, sub, dx, dy);
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

    /// <summary>
    /// Composites a rendered frame over <paramref name="target"/>, source-over rather than a copy:
    /// a frame whose canvas is transparent (CSS Color Adjust §2.4) has to let the embedder show
    /// through instead of punching it out. An opaque source takes the copy path, so a frame that
    /// fills its own canvas — every frame, before that rule was modelled — is unchanged.
    /// <para>
    /// Separate from <see cref="BlitOnto"/> deliberately: that one also stacks the <c>@page</c>
    /// backdrop, flow and margin-box layers of a print render, which are composed by replacement
    /// and are no part of this rule.
    /// </para>
    /// </summary>
    private static void BlitFrameOnto(BBitmap target, BBitmap source, int destX, int destY)
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

                target.SetPixel(tx, ty, BlendOver(source.GetPixel(x, y), target.GetPixel(tx, ty)));
            }
        }
    }

    /// <summary>
    /// Porter-Duff source-over on straight (non-premultiplied) alpha. Mirrors
    /// <c>HtmlRender.BlendOver</c>, which the string-render path composites frames with.
    /// </summary>
    private static BColor BlendOver(BColor source, BColor destination)
    {
        if (source.A == 255 || destination.A == 0)
            return source;
        if (source.A == 0)
            return destination;

        float sa = source.A / 255f;
        float da = destination.A / 255f;
        float outA = sa + da * (1f - sa);
        if (outA <= 0f)
            return BColor.Transparent;

        byte Channel(byte s, byte d) =>
            (byte)Math.Clamp((int)Math.Round((s * sa + d * da * (1f - sa)) / outA), 0, 255);

        return BColor.FromArgb(
            (byte)Math.Clamp((int)Math.Round(outA * 255f), 0, 255),
            Channel(source.R, destination.R),
            Channel(source.G, destination.G),
            Channel(source.B, destination.B));
    }
}
