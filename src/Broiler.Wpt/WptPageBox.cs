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
/// The unconditional <c>@page</c> is read, and so is the rule for the one named page the document
/// puts its content on — the runner renders a single sheet, and that sheet is page one. A
/// pseudo-class selector (<c>:first</c>, <c>:left</c>, <c>:right</c>) never is, and neither is any
/// named rule once the document uses more than one name: those describe particular pages of a flow,
/// and a per-page box size is not something this model can carry; taking one anyway paints the
/// wrong page's geometry everywhere. See
/// <see cref="EnumerateAppliedPageBlocks"/> for the guard and the test that states it.
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

    /// <summary>The margin on one physical <paramref name="side"/>.</summary>
    internal float Margin(WptPageSide side) => side switch
    {
        WptPageSide.Top => MarginTop,
        WptPageSide.Right => MarginRight,
        WptPageSide.Bottom => MarginBottom,
        _ => MarginLeft,
    };

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

        // `auto` is a margin value, not an unparseable one, and the difference decides the page
        // box: an auto margin takes whatever the page area leaves over, so it cannot be resolved
        // until `size`, `width` and `height` have all been seen. Which sides are auto is tracked
        // here and settled after the loop.
        var auto = new AutoMargins();

        foreach (var (declarations, _) in EnumerateAppliedPageBlocks(html))
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

                    if (IsAuto(value))
                        auto = auto.With(side, true).Also(ref box, side, 0);
                    else if (TryParseLength(value, basis, fontSize, out var logical))
                        auto = auto.With(side, false).Also(ref box, side, logical);
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
                        {
                            box = box with
                            {
                                MarginTop = m.Top, MarginRight = m.Right,
                                MarginBottom = m.Bottom, MarginLeft = m.Left,
                            };
                            auto = m.Auto;
                        }
                        break;
                    case "margin-top":
                        auto = ParseSide(value, WptPageSide.Top, box.BoxSize.Height, fontSize, auto, ref box);
                        break;
                    case "margin-right":
                        auto = ParseSide(value, WptPageSide.Right, box.BoxSize.Width, fontSize, auto, ref box);
                        break;
                    case "margin-bottom":
                        auto = ParseSide(value, WptPageSide.Bottom, box.BoxSize.Height, fontSize, auto, ref box);
                        break;
                    case "margin-left":
                        auto = ParseSide(value, WptPageSide.Left, box.BoxSize.Width, fontSize, auto, ref box);
                        break;
                }
            }
        }

        box = SettleAxis(box, areaWidth, auto, horizontal: true);
        box = SettleAxis(box, areaHeight, auto, horizontal: false);

        return box;
    }

    /// <summary>
    /// The <c>@page</c> rules that describe the sheet this document is rendered on: each one's
    /// declarations, and its block as written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every unconditional <c>@page</c>, and then — layered over them, as the cascade orders it —
    /// the rule for the named page the document actually uses, if there is exactly one such name.
    /// The runner renders a single sheet, and that sheet is page one, so it takes the box of the
    /// page the flow starts on. <c>page-name-table-001-print</c> is the pair that states it: a table
    /// on <c>page: square</c>, a <c>@page square</c> that sizes the sheet 5in and paints it
    /// <c>#eee</c>, and a reference that spells the same page as the unconditional rule. Reading
    /// only the unconditional one left the sheet the default size under a red background and scored
    /// 0.0 %.
    /// </para>
    /// <para>
    /// <strong>Exactly one</strong> name is the whole of the guard, and it is what keeps this from
    /// being the earlier attempt that read every selectored rule. A document that uses two named
    /// pages needs a per-page box, which one surface cannot carry, so none of them is taken;
    /// <c>page-margin-auto-print</c> (six names) and a document that names none are both left
    /// exactly as they were. A pseudo-class selector — <c>:first</c>, <c>:left</c>, <c>:right</c> —
    /// is never read: it describes particular pages of a flow this model does not paginate.
    /// </para>
    /// <para>
    /// The raw text comes with each block because the parser does not descend into the at-rules
    /// nested inside an <c>@page</c> — the sixteen margin boxes of CSS Paged Media 3 §5 are left in
    /// it, and <see cref="WptPageMarginBoxes"/> reads them from there.
    /// </para>
    /// </remarks>
    internal static IEnumerable<(CssDeclarationBlock Declarations, string BlockText)>
        EnumerateAppliedPageBlocks(string html)
    {
        var used = SoleUsedPageName(html);

        foreach (var (selector, block) in EnumeratePageBlocks(html))
        {
            if (selector.Length == 0)
                yield return block;
        }

        if (used is null)
            yield break;

        foreach (var (selector, block) in EnumeratePageBlocks(html))
        {
            if (selector.Equals(used, StringComparison.OrdinalIgnoreCase))
                yield return block;
        }
    }

    /// <summary>
    /// Every <c>@page</c> rule in the document, as its selector — empty for the unconditional rule —
    /// and its block. A selector carrying a pseudo-class is skipped outright.
    /// </summary>
    private static IEnumerable<(string Selector, (CssDeclarationBlock Declarations, string BlockText) Block)>
        EnumeratePageBlocks(string html)
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

                var selector = ((split < 0 ? string.Empty : name[split..]) + " " + atRule.Prelude).Trim();
                if (selector.Contains(':'))
                    continue;

                yield return (selector, (declarations, atRule.BlockText ?? string.Empty));
            }
        }
    }

    /// <summary>
    /// The one page name this document puts content on, or <c>null</c> when it names none or names
    /// more than one.
    /// </summary>
    /// <remarks>
    /// The <c>page</c> property, read from the style rules and from the <c>style</c> attributes —
    /// the two places a WPT page test puts it. <c>auto</c> is the initial value and names no page.
    /// Deliberately a scan for the same reason the rest of this file is one: it runs before the
    /// document is built, to decide the surface it will be rendered on.
    /// </remarks>
    private static string? SoleUsedPageName(string html)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in EnumerateStyleSources(html))
        {
            CssStyleSheet sheet;
            try { sheet = new CssParser().ParseStyleSheet(source); }
            catch { continue; }

            foreach (var rule in sheet.Rules)
            {
                if (rule is CssStyleRule styleRule)
                    CollectPageNames(styleRule.Declarations, names);
            }
        }

        foreach (var declarations in EnumerateStyleAttributes(html))
        {
            CssDeclarationBlock block;
            try { block = new CssParser().ParseDeclarations(declarations); }
            catch { continue; }

            CollectPageNames(block, names);
        }

        return names.Count == 1 ? names.First() : null;

        static void CollectPageNames(CssDeclarationBlock block, HashSet<string> into)
        {
            foreach (var declaration in block.Declarations)
            {
                if (!declaration.Name.Trim().Equals("page", StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = declaration.Value.Text.Trim();
                if (value.Length != 0 && !value.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    into.Add(value);
            }
        }
    }

    /// <summary>The text of each <c>style</c> attribute in the markup.</summary>
    private static IEnumerable<string> EnumerateStyleAttributes(string html)
    {
        foreach (System.Text.RegularExpressions.Match match in StyleAttribute.Matches(html))
            yield return match.Groups["css"].Value;
    }

    private static readonly System.Text.RegularExpressions.Regex StyleAttribute = new(
        """\bstyle\s*=\s*("(?<css>[^"]*)"|'(?<css>[^']*)')""",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase
            | System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Which of the page box's four margins were declared <c>auto</c>.</summary>
    private readonly record struct AutoMargins(bool Top, bool Right, bool Bottom, bool Left)
    {
        internal AutoMargins With(WptPageSide side, bool value) => side switch
        {
            WptPageSide.Top => this with { Top = value },
            WptPageSide.Right => this with { Right = value },
            WptPageSide.Bottom => this with { Bottom = value },
            _ => this with { Left = value },
        };

        internal bool On(WptPageSide side) => side switch
        {
            WptPageSide.Top => Top,
            WptPageSide.Right => Right,
            WptPageSide.Bottom => Bottom,
            _ => Left,
        };

        /// <summary>Sets <paramref name="box"/>'s margin as well, so a caller states both at once.</summary>
        internal AutoMargins Also(ref WptPageBox box, WptPageSide side, float margin)
        {
            box = box.WithMargin(side, margin);
            return this;
        }
    }

    /// <summary>CSS Paged Media 3 §3.2's <c>auto</c>, which is a margin value rather than a length.</summary>
    private static bool IsAuto(string value) =>
        value.Equals("auto", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads one physical margin longhand into <paramref name="box"/>, recording whether it was
    /// <c>auto</c>. A value that is neither <c>auto</c> nor a length leaves both untouched.
    /// </summary>
    private static AutoMargins ParseSide(
        string value, WptPageSide side, float percentBasis, double fontSize,
        AutoMargins auto, ref WptPageBox box)
    {
        if (IsAuto(value))
            return auto.With(side, true).Also(ref box, side, 0);

        return TryParseLength(value, percentBasis, fontSize, out var length)
            ? auto.With(side, false).Also(ref box, side, length)
            : auto;
    }

    /// <summary>
    /// Settles one axis of the page box once every declaration has been seen: what the page area
    /// leaves over goes to the <c>auto</c> margins on that axis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>width</c>/<c>height</c> size the page <em>area</em>, and what happens to the box around it
    /// turns on whether a margin on that axis is <c>auto</c>.
    /// </para>
    /// <para>
    /// <b>No <c>auto</c> margin:</b> the area plus its margins <em>is</em> the box, and a declared
    /// <c>size</c> gives way to it. That is the over-constrained resolution
    /// <c>page-size-013-print</c> states outright — <c>size: 500px; margin: 50px; width: 200px;
    /// height: 300px</c> against a reference of <c>size: 300px 400px; margin: 50px</c> — and it is
    /// also how <c>margin-boxes/dimensions-011</c> writes one page two ways
    /// (<c>width: 20em; height: 16em; margin: 6em</c> = <c>size: 32em 28em; margin: 0</c>).
    /// </para>
    /// <para>
    /// <b>An <c>auto</c> margin:</b> the box stands and the <c>auto</c> sides take what is left
    /// over, which is what makes <c>auto</c> centre the page area.
    /// <c>page-margin-auto-print</c> states it: a 20em × 7em page holding a 12em × 3em area
    /// centres it under 64px side and 32px block margins. One <c>auto</c> beside a stated margin
    /// takes the whole remainder rather than half, which is how that test's
    /// <c>@page bbb { margin-top: 0 }</c> pushes the area to the top.
    /// </para>
    /// <para>
    /// The remainder is signed. An area larger than its box gives a negative one and the margins go
    /// negative with it rather than clamping: <c>page-margin-auto-negative-print</c> states a 300px
    /// page with a 340px area and expects it to hang 20px off every edge.
    /// </para>
    /// <para>
    /// With no <c>width</c>/<c>height</c> at all there is nothing to centre, so an <c>auto</c>
    /// margin stays the zero it was recorded as and the box keeps the size it declared.
    /// </para>
    /// </remarks>
    private static WptPageBox SettleAxis(
        WptPageBox box, double? areaSize, AutoMargins auto, bool horizontal)
    {
        if (areaSize is not { } areaExtent)
            return box;

        var (start, end) = horizontal
            ? (WptPageSide.Left, WptPageSide.Right)
            : (WptPageSide.Top, WptPageSide.Bottom);

        float area = (float)areaExtent;
        float startMargin = box.Margin(start);
        float endMargin = box.Margin(end);
        bool autoStart = auto.On(start), autoEnd = auto.On(end);

        if (!autoStart && !autoEnd)
        {
            float extent = area + startMargin + endMargin;
            return box with
            {
                BoxSize = horizontal
                    ? new SizeF(extent, box.BoxSize.Height)
                    : new SizeF(box.BoxSize.Width, extent),
            };
        }

        float boxExtent = horizontal ? box.BoxSize.Width : box.BoxSize.Height;
        float remainder = boxExtent - area
            - (autoStart ? 0 : startMargin)
            - (autoEnd ? 0 : endMargin);

        if (autoStart && autoEnd)
            return box.WithMargin(start, remainder / 2).WithMargin(end, remainder / 2);

        return autoStart
            ? box.WithMargin(start, remainder)
            : box.WithMargin(end, remainder);
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

    private readonly record struct Margins(
        float Top, float Right, float Bottom, float Left, AutoMargins Auto);

    /// <summary>The <c>margin</c> shorthand: one to four lengths, in the usual TRBL order.</summary>
    private static bool TryParseMarginShorthand(string value, SizeF boxSize, double fontSize, out Margins margins)
    {
        margins = default;

        var tokens = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length is 0 or > 4)
            return false;

        var parsed = new float[tokens.Length];
        var isAuto = new bool[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            // `auto` is a value of the property, not a failure to parse it. Reading it as one used
            // to reject the whole shorthand, so `@page { margin: auto }` left every margin at zero
            // and the page area was never centred.
            if (IsAuto(tokens[i]))
            {
                isAuto[i] = true;
                continue;
            }

            // Percentages on the block-axis margins resolve against the page width, as elsewhere in
            // CSS; the axis only matters for a percentage, and using width for all of them is the
            // rule rather than a shortcut.
            if (!TryParseLength(tokens[i], boxSize.Width, fontSize, out parsed[i]))
                return false;
        }

        // The usual one-to-four expansion, applied to the values and their auto flags alike.
        (int top, int right, int bottom, int left) = parsed.Length switch
        {
            1 => (0, 0, 0, 0),
            2 => (0, 1, 0, 1),
            3 => (0, 1, 2, 1),
            _ => (0, 1, 2, 3),
        };

        margins = new Margins(
            parsed[top], parsed[right], parsed[bottom], parsed[left],
            new AutoMargins(isAuto[top], isAuto[right], isAuto[bottom], isAuto[left]));
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
