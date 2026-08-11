using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using Broiler.CSS;
using Broiler.Graphics;
using Broiler.HTML.Core.Entities;
using Broiler.HTML.Image;

// Both Broiler.Graphics and Broiler.HTML.Image define a BBitmap; the renderer's is the one the
// container paints into and the one the comparer reads.
using BBitmap = Broiler.HTML.Image.BBitmap;

namespace Broiler.Wpt;

/// <summary>
/// The paint an <c>@page</c> rule puts on the sheet itself — CSS Paged Media 3 §7's page
/// background, and the border and padding that sit between the page's margin and its page area.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WptPageBox"/> already reads the <c>@page</c> for the geometry a paged render lays out
/// against; this reads it for the pixels underneath that. They are different questions and stay in
/// different files: a page box exists for every document, while a page decoration exists only for
/// the handful that ask for one, and everything else must render byte-identically to before.
/// </para>
/// <para>
/// Two boxes, because the page paints in two places. §7.2 puts the page background over the
/// <em>whole</em> page box, margin area included — <c>page-background-004-print</c> states a 500×300
/// page with a 50px margin and its reference is 500×300 of solid yellow — while the border and
/// padding belong to the box the margin leaves, which is where <c>page-box-008-print</c> paints its
/// margin ring blue and its padding ring hotpink. So the backdrop is a full-sheet box carrying the
/// background declarations and an inset box carrying the border and padding ones.
/// </para>
/// <para>
/// Nothing here re-implements CSS. The declarations are copied onto two ordinary divs and handed to
/// the same renderer that will draw the page, which is what makes a page background that is an
/// image, a gradient, or a <c>border-image</c> work without this file knowing they exist. The same
/// reason drives <see cref="MeasureInsets"/>: the distance from the border box to the page area is
/// read off a render of the box rather than by parsing <c>border-width</c> here.
/// </para>
/// </remarks>
/// <param name="BackgroundCss">Declarations painting the whole sheet.</param>
/// <param name="BoxCss">Declarations bordering and padding the box the page's margins leave.</param>
/// <param name="HasInsets">
/// Whether <paramref name="BoxCss"/> carries a border or padding — the only two things that move
/// the page area in from that box, and so the only reason to measure it.
/// </param>
internal sealed record WptPageDecoration(string BackgroundCss, string BoxCss, bool HasInsets)
{
    /// <summary>
    /// The decoration <paramref name="html"/>'s unconditional <c>@page</c> rules declare, or
    /// <c>null</c> when they declare none — which is every document that does not style the sheet,
    /// and the reason this costs nothing for the rest of the suite.
    /// </summary>
    internal static WptPageDecoration? Resolve(string html)
    {
        // Later declarations win, and a shorthand must not be reordered behind the longhand that
        // follows it, so the cascade is kept as a name-keyed list in source order.
        var background = new List<CssDeclaration>();
        var box = new List<CssDeclaration>();
        bool paints = false, insets = false;

        foreach (var (declarations, _) in WptPageBox.EnumerateUnconditionalPageBlocks(html))
        {
            foreach (var declaration in declarations.Declarations)
            {
                var name = declaration.Name.Trim().ToLowerInvariant();

                // `visibility` applies to the page context and to nothing else in the document
                // (css-page-3 §5.1), so it goes on both boxes: a hidden page keeps the space its
                // border and padding take and paints neither them nor its background.
                // `page-visibility-hidden-001-print` is the pair that states it, matching a
                // reference whose page border is `solid transparent`.
                if (name == "visibility")
                {
                    Replace(background, name, declaration);
                    Replace(box, name, declaration);
                }
                else if (IsBackgroundProperty(name))
                {
                    Replace(background, name, declaration);
                    paints = true;
                }
                else if (IsBoxProperty(name))
                {
                    Replace(box, name, declaration);
                    paints = true;
                    insets |= !name.StartsWith("border-radius", StringComparison.Ordinal)
                        && !name.StartsWith("border-image", StringComparison.Ordinal);
                }
            }
        }

        // A page that says only `visibility` paints nothing and moves nothing; it renders exactly as
        // an undecorated one, so it is not worth a decorated render.
        if (!paints)
            return null;

        return new WptPageDecoration(Serialize(background), Serialize(box), insets);

        static void Replace(List<CssDeclaration> into, string name, CssDeclaration declaration)
        {
            into.RemoveAll(existing => existing.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));
            into.Add(declaration);
        }
    }

    /// <summary>
    /// Renders the sheet's backdrop: the page background over the whole page box, and the page's
    /// border and padding on the box its margins leave.
    /// </summary>
    internal BBitmap Render(
        WptPageBox page,
        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs>? imageLoad,
        string? baseUrl)
    {
        int sheetWidth = Math.Max(1, (int)Math.Round(page.BoxSize.Width));
        int sheetHeight = Math.Max(1, (int)Math.Round(page.BoxSize.Height));
        var borderBox = BorderBox(page);

        var markup = new StringBuilder()
            .Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>")
            .Append("html,body{margin:0;padding:0;border:0;}")
            .Append("#s{position:absolute;left:0;top:0;")
            .Append("width:").Append(Px(sheetWidth)).Append(";height:").Append(Px(sheetHeight)).Append(';')
            .Append(BackgroundCss).Append('}')
            .Append("#b{position:absolute;box-sizing:border-box;")
            .Append("left:").Append(Px(borderBox.X)).Append(";top:").Append(Px(borderBox.Y)).Append(';')
            .Append("width:").Append(Px(borderBox.Width)).Append(";height:").Append(Px(borderBox.Height)).Append(';')
            .Append(BoxCss).Append('}')
            .Append("</style></head><body><div id=\"s\"></div><div id=\"b\"></div></body></html>")
            .ToString();

        return HtmlRender.RenderToImageWithStyleSet(
            markup, sheetWidth, sheetHeight,
            styleSet: null,
            backgroundColor: Broiler.Graphics.BColor.White,
            stylesheetLoad: stylesheetLoad,
            imageLoad: imageLoad,
            baseUrl: baseUrl);
    }

    /// <summary>
    /// How far the page area sits inside the page's border box — the border and padding the
    /// <c>@page</c> declares, in used pixels.
    /// </summary>
    /// <remarks>
    /// Measured off a render rather than parsed: <c>border-width</c> reaches its used value through
    /// the shorthand, the style keyword (a <c>border-style</c> of <c>none</c> zeroes a stated width),
    /// the physical/logical mapping that <c>page-box-008-print</c>'s <c>padding-block-start</c> goes
    /// through, and percentages that resolve against the box. The renderer resolves all of that
    /// already, so the probe states the box, fills its content area with a colour nothing else uses,
    /// and reads the extent back — the same trick
    /// <c>WptDocumentRenderer.MeasureMarginBoxes</c> uses on the margin ring.
    /// </remarks>
    internal (float Left, float Top, float Right, float Bottom) MeasureInsets(
        WptPageBox page,
        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetLoad,
        EventHandler<HtmlImageLoadEventArgs>? imageLoad,
        string? baseUrl)
    {
        if (!HasInsets)
            return (0, 0, 0, 0);

        var borderBox = BorderBox(page);
        int width = Math.Max(1, (int)Math.Round(borderBox.Width));
        int height = Math.Max(1, (int)Math.Round(borderBox.Height));

        var markup = new StringBuilder()
            .Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>")
            .Append("html,body{margin:0;padding:0;border:0;}")
            .Append("#b{position:absolute;left:0;top:0;box-sizing:border-box;")
            .Append("width:").Append(Px(width)).Append(";height:").Append(Px(height)).Append(';')
            .Append(BoxCss).Append("background:none;}")
            .Append("#c{width:100%;height:100%;background:rgb(0,255,0);}")
            .Append("</style></head><body><div id=\"b\"><div id=\"c\"></div></div></body></html>")
            .ToString();

        using var probe = HtmlRender.RenderToImageWithStyleSet(
            markup, width, height,
            styleSet: null,
            backgroundColor: Broiler.Graphics.BColor.White,
            stylesheetLoad: stylesheetLoad,
            imageLoad: imageLoad,
            baseUrl: baseUrl);

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int y = 0; y < probe.Height; y++)
        {
            for (int x = 0; x < probe.Width; x++)
            {
                var pixel = probe.GetPixel(x, y);
                if (pixel.R != 0 || pixel.G != 255 || pixel.B != 0)
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        // A box whose content area came out empty — a border thicker than the page — insets nothing
        // rather than a negative page area.
        if (maxX < minX)
            return (0, 0, 0, 0);

        return (minX, minY, Math.Max(0, width - 1 - maxX), Math.Max(0, height - 1 - maxY));
    }

    /// <summary>The page box less its margins: the box the page's border and padding live on.</summary>
    internal static RectangleF BorderBox(WptPageBox page) => new(
        page.MarginLeft,
        page.MarginTop,
        Math.Max(1, page.BoxSize.Width - page.MarginLeft - page.MarginRight),
        Math.Max(1, page.BoxSize.Height - page.MarginTop - page.MarginBottom));

    private static bool IsBackgroundProperty(string name) =>
        name == "background" || name.StartsWith("background-", StringComparison.Ordinal);

    /// <remarks>
    /// <c>border-radius</c> and <c>border-image</c> come along with the border because they change
    /// the shape it draws, not the space it takes.
    /// </remarks>
    private static bool IsBoxProperty(string name) =>
        name == "border" || name.StartsWith("border-", StringComparison.Ordinal)
        || name == "padding" || name.StartsWith("padding-", StringComparison.Ordinal);

    private static string Serialize(IEnumerable<CssDeclaration> declarations)
    {
        var css = new StringBuilder();
        foreach (var declaration in declarations)
        {
            css.Append(declaration.Name.Trim()).Append(':').Append(declaration.Value.Text.Trim());
            if (declaration.Important)
                css.Append(" !important");
            css.Append(';');
        }

        return css.ToString();
    }

    private static string Px(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture) + "px";
}
