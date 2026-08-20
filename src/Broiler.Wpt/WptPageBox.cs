using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Broiler.CSS;

namespace Broiler.Wpt;

/// <summary>
/// The page box a paged render lays out against — CSS Paged Media 3's <c>@page</c> <c>size</c> and
/// <c>margin</c>, resolved from a document's author style sheets.
/// </summary>
/// <param name="BoxSize">The page box: the whole sheet, margins included.</param>
/// <param name="MarginLeft">Left page margin.</param>
/// <param name="MarginTop">Top page margin.</param>
/// <param name="MarginRight">Right page margin.</param>
/// <param name="MarginBottom">Bottom page margin.</param>
/// <remarks>
/// <para>
/// This is what makes a paged render measurable at all. The page <em>area</em> — the box less its
/// margins — is the containing block content is laid out in, what <c>vw</c>/<c>vh</c> resolve
/// against, and where the fragmentation boundaries fall. Nothing else defines it: paginating at
/// the viewport instead is not an approximation of the page area but a different set of
/// boundaries, which is why enabling fragmentation before this existed moved the print reftests
/// 252 → 228 (the losses concentrated in <c>css/CSS2/pagination</c>, whose page area is two
/// inches and was being cut at 768px). <c>WptDocumentRenderer.RenderPaged</c> lays a print test out
/// against the box resolved here, behind <see cref="WptTestRunner.PagedPrint"/>.
/// </para>
/// <para>
/// Only the unconditional <c>@page</c> is read. A page selector — <c>:first</c>, <c>:left</c>,
/// <c>:right</c>, or a named page — describes particular pages of the flow, and a per-page box
/// size is not something this model can carry; taking one anyway paints the wrong page's geometry
/// everywhere.
/// </para>
/// </remarks>
internal readonly record struct WptPageBox(
    SizeF BoxSize,
    float MarginLeft,
    float MarginTop,
    float MarginRight,
    float MarginBottom)
{
    /// <summary>The page area: the box less its margins, and never smaller than one pixel.</summary>
    internal SizeF AreaSize => new(
        Math.Max(1, BoxSize.Width - MarginLeft - MarginRight),
        Math.Max(1, BoxSize.Height - MarginTop - MarginBottom));

    /// <summary>This box with the margin on one physical <paramref name="side"/> replaced.</summary>
    internal WptPageBox WithMargin(WptPageSide side, float margin) => side switch
    {
        WptPageSide.Top => this with { MarginTop = margin },
        WptPageSide.Right => this with { MarginRight = margin },
        WptPageSide.Bottom => this with { MarginBottom = margin },
        _ => this with { MarginLeft = margin },
    };

    /// <summary>
    /// Resolves the page box for <paramref name="html"/>, starting from a default box of
    /// <paramref name="defaultBoxSize"/> with no margins.
    /// </summary>
    /// <remarks>
    /// The defaults cancel out of a reftest comparison — a test and its reference are rendered the
    /// same way, and every page test that cares states its own <c>size</c> and <c>margin</c>. What
    /// matters is that both sides resolve them identically, which is why this reads the document
    /// rather than taking them from the runner.
    /// </remarks>
    internal static WptPageBox Resolve(string html, SizeF defaultBoxSize)
    {
        var box = new WptPageBox(defaultBoxSize, 0, 0, 0, 0);
        double? areaWidth = null, areaHeight = null;
        var axes = WptPageAxes.Resolve(html);

        foreach (var (declarations, _) in EnumerateUnconditionalPageBlocks(html))
        {
            double fontSize = FontSizeOf(declarations.Declarations.ToList());

            foreach (var declaration in declarations.Declarations)
            {
                var value = declaration.Value.Text.Trim();
                var name = declaration.Name.ToLowerInvariant();

                // The flow-relative margins name a physical side through the page's own axes, and
                // then behave exactly like the physical longhand they resolved to — percentage
                // included. `page-box-008-print` and `-009-print` state the same 16/32/48/80 ring
                // from `margin-inline-start: 2%` and friends on a 400×800 page, which only comes out
                // if each percentage is taken against the dimension its physical side runs along.
                if (TryLogicalMargin(name, out bool inline, out bool start))
                {
                    var side = axes.Side(inline, start);
                    float basis = side is WptPageSide.Top or WptPageSide.Bottom
                        ? box.BoxSize.Height
                        : box.BoxSize.Width;

                    if (TryParseLength(value, basis, fontSize, out var logical))
                        box = box.WithMargin(side, logical);
                    continue;
                }

                switch (name)
                {
                    // `width` and `height` size the page *area* — the box less its margins — the
                    // way they size any other box's content. margin-boxes/dimensions-011 states the
                    // same page as `width: 20em; height: 16em; margin: 6em` and its reference as
                    // `size: 32em 28em; margin: 0`, which only agree if the margins are added on.
                    case "width":
                        areaWidth = TryLength(value, defaultBoxSize.Width, fontSize) ?? areaWidth;
                        break;
                    case "height":
                        areaHeight = TryLength(value, defaultBoxSize.Height, fontSize) ?? areaHeight;
                        break;
                    case "size":
                        if (TryParseSize(value, defaultBoxSize, fontSize, out var size))
                            box = box with { BoxSize = size };
                        break;
                    case "margin":
                        if (TryParseMarginShorthand(value, box.BoxSize, fontSize, out var m))
                            box = box with
                            {
                                MarginTop = m.Top, MarginRight = m.Right,
                                MarginBottom = m.Bottom, MarginLeft = m.Left,
                            };
                        break;
                    case "margin-top":
                        if (TryParseLength(value, box.BoxSize.Height, fontSize, out var mt))
                            box = box with { MarginTop = mt };
                        break;
                    case "margin-right":
                        if (TryParseLength(value, box.BoxSize.Width, fontSize, out var mr))
                            box = box with { MarginRight = mr };
                        break;
                    case "margin-bottom":
                        if (TryParseLength(value, box.BoxSize.Height, fontSize, out var mb))
                            box = box with { MarginBottom = mb };
                        break;
                    case "margin-left":
                        if (TryParseLength(value, box.BoxSize.Width, fontSize, out var ml))
                            box = box with { MarginLeft = ml };
                        break;
                }
            }
        }

        if (areaWidth is { } aw)
            box = box with { BoxSize = new SizeF((float)(aw + box.MarginLeft + box.MarginRight), box.BoxSize.Height) };
        if (areaHeight is { } ah)
            box = box with { BoxSize = new SizeF(box.BoxSize.Width, (float)(ah + box.MarginTop + box.MarginBottom)) };

        return box;
    }

    /// <summary>
    /// The document's <c>@page</c> rules that carry no page selector: each one's declarations, and
    /// its block as written.
    /// </summary>
    /// <remarks>
    /// The raw text comes with them because the parser does not descend into the at-rules nested
    /// inside an <c>@page</c> — the sixteen margin boxes of CSS Paged Media 3 §5 are left in it,
    /// and <see cref="WptPageMarginBoxes"/> reads them from there.
    /// </remarks>
    internal static IEnumerable<(CssDeclarationBlock Declarations, string BlockText)>
        EnumerateUnconditionalPageBlocks(string html)
    {
        foreach (var source in EnumerateStyleSources(html))
        {
            CssStyleSheet sheet;
            try { sheet = new CssParser().ParseStyleSheet(source); }
            catch { continue; }

            foreach (var rule in sheet.Rules)
            {
                if (rule is not CssAtRule { Declarations: { } declarations } atRule)
                    continue;

                // The selector does not reliably land in the prelude: the parser splits an at-rule's
                // name at the first delimiter, so `@page square` can arrive with the name
                // "page square" and an empty prelude. Both spellings are checked.
                var name = atRule.Name.Trim();
                int split = name.IndexOfAny([' ', '\t', ':']);
                var head = split < 0 ? name : name[..split];

                if (!head.Equals("page", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (split >= 0 || !string.IsNullOrWhiteSpace(atRule.Prelude))
                    continue;

                yield return (declarations, atRule.BlockText ?? string.Empty);
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="name"/> is one of the four flow-relative margin longhands, and which
    /// axis and end it names. The <c>margin-block</c> and <c>margin-inline</c> shorthands are not
    /// among them: they set two sides at once and no page test states one.
    /// </summary>
    private static bool TryLogicalMargin(string name, out bool inline, out bool start)
    {
        foreach (var (longhand, isInline, isStart) in WptPageAxes.LogicalLonghands("margin"))
        {
            if (!name.Equals(longhand, StringComparison.Ordinal))
                continue;

            (inline, start) = (isInline, isStart);
            return true;
        }

        (inline, start) = (false, false);
        return false;
    }

    /// <summary>
    /// The text of each <c>&lt;style&gt;</c> element in the markup. Deliberately a scan rather than
    /// a parse: this runs before the document is built, to decide the surface it will be rendered
    /// on, and a <c>@page</c> rule can only come from a style sheet the document carries.
    /// </summary>
    internal static IEnumerable<string> EnumerateStyleSources(string html)
    {
        int index = 0;
        while (true)
        {
            int open = html.IndexOf("<style", index, StringComparison.OrdinalIgnoreCase);
            if (open < 0)
                yield break;

            int contentStart = html.IndexOf('>', open);
            if (contentStart < 0)
                yield break;

            int close = html.IndexOf("</style", contentStart, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
                yield break;

            yield return html[(contentStart + 1)..close];
            index = close + 1;
        }
    }

    /// <summary>
    /// CSS Paged Media 3 §3.1 <c>size</c>: one or two lengths, a named page size, and/or an
    /// orientation keyword. <c>auto</c> keeps the default.
    /// </summary>
    private static bool TryParseSize(string value, SizeF defaultBoxSize, double fontSize, out SizeF size)
    {
        size = defaultBoxSize;

        var tokens = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return false;

        List<float> lengths = [];
        SizeF? named = null;
        bool landscape = false, portrait = false;

        foreach (var token in tokens)
        {
            if (token.Equals("auto", StringComparison.OrdinalIgnoreCase))
                continue;
            if (token.Equals("landscape", StringComparison.OrdinalIgnoreCase)) { landscape = true; continue; }
            if (token.Equals("portrait", StringComparison.OrdinalIgnoreCase)) { portrait = true; continue; }

            if (NamedPageSizes.TryGetValue(token, out var namedSize)) { named = namedSize; continue; }

            if (TryParseLength(token, 0, fontSize, out var length) && length > 0)
                lengths.Add(length);
            else
                return false;
        }

        if (lengths.Count == 1)
            size = new SizeF(lengths[0], lengths[0]);      // one length is a square page
        else if (lengths.Count >= 2)
            size = new SizeF(lengths[0], lengths[1]);
        else if (named is { } n)
            size = n;
        else if (!landscape && !portrait)
            return false;

        // The orientation keywords rotate the page, and only the named/default sizes take them —
        // an explicit pair of lengths already states the orientation.
        if (lengths.Count == 0)
        {
            if (landscape && size.Width < size.Height)
                size = new SizeF(size.Height, size.Width);
            else if (portrait && size.Width > size.Height)
                size = new SizeF(size.Height, size.Width);
        }

        return true;
    }

    private readonly record struct Margins(float Top, float Right, float Bottom, float Left);

    /// <summary>The <c>margin</c> shorthand: one to four lengths, in the usual TRBL order.</summary>
    private static bool TryParseMarginShorthand(string value, SizeF boxSize, double fontSize, out Margins margins)
    {
        margins = default;

        var tokens = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length is 0 or > 4)
            return false;

        var parsed = new float[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            // Percentages on the block-axis margins resolve against the page width, as elsewhere in
            // CSS; the axis only matters for a percentage, and using width for all of them is the
            // rule rather than a shortcut.
            if (!TryParseLength(tokens[i], boxSize.Width, fontSize, out parsed[i]))
                return false;
        }

        margins = parsed.Length switch
        {
            1 => new Margins(parsed[0], parsed[0], parsed[0], parsed[0]),
            2 => new Margins(parsed[0], parsed[1], parsed[0], parsed[1]),
            3 => new Margins(parsed[0], parsed[1], parsed[2], parsed[1]),
            _ => new Margins(parsed[0], parsed[1], parsed[2], parsed[3]),
        };
        return true;
    }

    /// <summary>An absolute CSS length in pixels, or a percentage of <paramref name="percentBasis"/>.</summary>
    private static bool TryParseLength(string token, float percentBasis, double fontSize, out float pixels)
    {
        if (TryLength(token, percentBasis, fontSize) is { } length)
        {
            pixels = (float)length;
            return true;
        }

        pixels = 0;
        return false;
    }

    /// <summary>
    /// A CSS length in pixels: absolute units, a percentage of <paramref name="percentBasis"/>, or
    /// a font-relative one against <paramref name="fontSize"/>. <c>null</c> for <c>auto</c>, for a
    /// missing value, and for anything that is not a length.
    /// </summary>
    internal static double? TryLength(string? token, double percentBasis, double fontSize)
    {
        var v = token?.Trim();
        if (string.IsNullOrEmpty(v) || v.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var (suffix, perUnit) in LengthUnits)
        {
            if (!v.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            var number = v[..^suffix.Length];
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var scalar))
                return null;

            return suffix switch
            {
                "%" => scalar / 100d * percentBasis,
                "em" or "rem" => scalar * fontSize,
                _ => scalar * perUnit,
            };
        }

        // A bare number is only a length when it is zero.
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var bare) && bare == 0)
            return 0;

        return null;
    }

    /// <summary>The CSS initial font size, which a font-relative length falls back to.</summary>
    internal const double DefaultFontSize = 16;

    /// <summary>
    /// The font size <paramref name="declarations"/> establish, for the font-relative lengths on
    /// the boxes that inherit from them. Read from <c>font-size</c>, or from the <c>font</c>
    /// shorthand's size component, which is the token before the <c>/</c> line height.
    /// </summary>
    internal static double FontSizeOf(IReadOnlyList<CssDeclaration> declarations)
    {
        double size = DefaultFontSize;
        foreach (var declaration in declarations)
        {
            if (declaration.Name.Equals("font-size", StringComparison.OrdinalIgnoreCase))
            {
                size = TryLength(declaration.Value.Text, size, size) ?? size;
            }
            else if (declaration.Name.Equals("font", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var token in declaration.Value.Text
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    var head = token.Split('/')[0];
                    if (TryLength(head, size, size) is { } parsed and > 0)
                    {
                        size = parsed;
                        break;
                    }
                }
            }
        }

        return size;
    }

    /// <summary>CSS length units, in pixels per unit, longest suffix first so <c>mm</c> is not read as <c>m</c>.</summary>
    private static readonly (string Suffix, double PerUnit)[] LengthUnits =
    [
        ("rem", 0d), ("em", 0d),
        ("px", 1d), ("in", 96d), ("cm", 96d / 2.54d), ("mm", 96d / 25.4d),
        ("q", 96d / 101.6d), ("pt", 96d / 72d), ("pc", 16d), ("%", 0d),
    ];

    /// <summary>The named page sizes of CSS Paged Media 3 §3.1, in portrait orientation at 96dpi.</summary>
    private static readonly Dictionary<string, SizeF> NamedPageSizes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["a5"] = new(148f * 96f / 25.4f, 210f * 96f / 25.4f),
            ["a4"] = new(210f * 96f / 25.4f, 297f * 96f / 25.4f),
            ["a3"] = new(297f * 96f / 25.4f, 420f * 96f / 25.4f),
            ["b5"] = new(176f * 96f / 25.4f, 250f * 96f / 25.4f),
            ["b4"] = new(250f * 96f / 25.4f, 353f * 96f / 25.4f),
            ["jis-b5"] = new(182f * 96f / 25.4f, 257f * 96f / 25.4f),
            ["jis-b4"] = new(257f * 96f / 25.4f, 364f * 96f / 25.4f),
            ["letter"] = new(8.5f * 96f, 11f * 96f),
            ["legal"] = new(8.5f * 96f, 14f * 96f),
            ["ledger"] = new(11f * 96f, 17f * 96f),
        };
}
