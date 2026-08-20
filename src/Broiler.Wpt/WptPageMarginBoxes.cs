using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Broiler.CSS;

namespace Broiler.Wpt;

/// <summary>Which of CSS Paged Media 3 §5's sixteen margin boxes a rule names.</summary>
internal enum WptMarginBoxSlot
{
    TopLeftCorner, TopLeft, TopCenter, TopRight, TopRightCorner,
    RightTop, RightMiddle, RightBottom,
    BottomRightCorner, BottomRight, BottomCenter, BottomLeft, BottomLeftCorner,
    LeftBottom, LeftMiddle, LeftTop,
}

/// <summary>
/// CSS Paged Media 3 §5 page margin boxes — the sixteen boxes that live in a page's margin, and
/// the page furniture (running headers, folios, rules) a paged document puts there.
/// </summary>
/// <remarks>
/// <para>
/// A margin box is generated only when its rule gives it a <c>content</c> that is neither
/// <c>none</c> nor <c>normal</c>; an empty string is content and does generate one. Everything else
/// on the rule is an ordinary box property, so the boxes are emitted as a small HTML document laid
/// over the page and rendered by the engine itself rather than laid out by hand here — which is
/// also how WPT's own references state them, as flex and grid.
/// </para>
/// <para>
/// The nested rules are read out of the <c>@page</c> block's text. Broiler.CSS parses
/// <c>@page</c>'s declarations but does not descend into at-rules nested inside it
/// (<c>CssAtRule.Rules</c> comes back empty and the nested blocks are left in
/// <c>BlockText</c>), so this scans that text for the sixteen names. Doing it here keeps the whole
/// feature in the main repository; the alternative is a <c>Broiler.CSS</c> change this session
/// cannot push.
/// </para>
/// </remarks>
internal static class WptPageMarginBoxes
{
    /// <summary>
    /// The margin boxes an unconditional <c>@page</c> rule in <paramref name="html"/> declares,
    /// and the <c>@page</c>'s own inheritable declarations, which the boxes inherit from it.
    /// </summary>
    internal static (
        IReadOnlyDictionary<WptMarginBoxSlot, IReadOnlyList<CssDeclaration>> Boxes,
        IReadOnlyList<CssDeclaration> PageDeclarations) Resolve(string html)
    {
        var boxes = new Dictionary<WptMarginBoxSlot, List<CssDeclaration>>();
        var pageDeclarations = new List<CssDeclaration>();

        foreach (var block in WptPageBox.EnumerateAppliedPageBlocks(html))
        {
            foreach (var declaration in block.Declarations.Declarations)
            {
                if (!IsPageGeometryProperty(declaration.Name))
                    pageDeclarations.Add(declaration);
            }

            foreach (var (slot, declarations) in EnumerateMarginRules(block.BlockText))
            {
                // A later rule for the same slot adds to the earlier one, declaration by
                // declaration, the way two rules matching one element cascade.
                if (!boxes.TryGetValue(slot, out var existing))
                    boxes[slot] = existing = [];
                existing.AddRange(declarations);
            }
        }

        return (
            boxes.ToDictionary(p => p.Key, p => (IReadOnlyList<CssDeclaration>)p.Value),
            pageDeclarations);
    }

    /// <summary>
    /// <c>size</c> and the page margins describe the sheet rather than the boxes on it, so they are
    /// the two things a margin box must not inherit from the <c>@page</c> rule that holds it.
    /// </summary>
    private static bool IsPageGeometryProperty(string name) =>
        name.Equals("size", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("margin", StringComparison.OrdinalIgnoreCase);

    /// <summary>Each <c>@&lt;margin-box&gt;</c> rule in an <c>@page</c> block's text, in source order.</summary>
    private static IEnumerable<(WptMarginBoxSlot Slot, IReadOnlyList<CssDeclaration> Declarations)>
        EnumerateMarginRules(string blockText)
    {
        if (string.IsNullOrEmpty(blockText))
            yield break;

        int index = 0;
        while (true)
        {
            int at = blockText.IndexOf('@', index);
            if (at < 0)
                yield break;

            int open = blockText.IndexOf('{', at);
            if (open < 0)
                yield break;

            var name = blockText[(at + 1)..open].Trim();
            int close = MatchingBrace(blockText, open);
            if (close < 0)
                yield break;

            if (SlotNames.TryGetValue(name, out var slot))
            {
                var declarations = ParseDeclarations(blockText[(open + 1)..close]);
                if (declarations.Count > 0)
                    yield return (slot, declarations);
            }

            index = close + 1;
        }
    }

    /// <summary>The index of the <c>}</c> closing the <c>{</c> at <paramref name="open"/>, or -1.</summary>
    private static int MatchingBrace(string text, int open)
    {
        int depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            if (text[i] == '{')
                depth++;
            else if (text[i] == '}' && --depth == 0)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// The declarations of a margin box's body, parsed by wrapping them in a style rule — the
    /// declaration grammar is the same one, and this way the values come from the real parser
    /// rather than from a second one written here.
    /// </summary>
    private static IReadOnlyList<CssDeclaration> ParseDeclarations(string body)
    {
        try
        {
            var sheet = new CssParser().ParseStyleSheet("x{" + body + "}");
            foreach (var rule in sheet.Rules)
            {
                if (rule is CssStyleRule { Declarations: { } declarations })
                    return declarations.Declarations.ToList();
            }
        }
        catch
        {
            // A malformed margin box drops out, as an unparseable rule does anywhere in CSS.
        }

        return [];
    }

    private static readonly Dictionary<string, WptMarginBoxSlot> SlotNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["top-left-corner"] = WptMarginBoxSlot.TopLeftCorner,
            ["top-left"] = WptMarginBoxSlot.TopLeft,
            ["top-center"] = WptMarginBoxSlot.TopCenter,
            ["top-right"] = WptMarginBoxSlot.TopRight,
            ["top-right-corner"] = WptMarginBoxSlot.TopRightCorner,
            ["right-top"] = WptMarginBoxSlot.RightTop,
            ["right-middle"] = WptMarginBoxSlot.RightMiddle,
            ["right-bottom"] = WptMarginBoxSlot.RightBottom,
            ["bottom-right-corner"] = WptMarginBoxSlot.BottomRightCorner,
            ["bottom-right"] = WptMarginBoxSlot.BottomRight,
            ["bottom-center"] = WptMarginBoxSlot.BottomCenter,
            ["bottom-left"] = WptMarginBoxSlot.BottomLeft,
            ["bottom-left-corner"] = WptMarginBoxSlot.BottomLeftCorner,
            ["left-bottom"] = WptMarginBoxSlot.LeftBottom,
            ["left-middle"] = WptMarginBoxSlot.LeftMiddle,
            ["left-top"] = WptMarginBoxSlot.LeftTop,
        };

    /// <summary>
    /// Whether <paramref name="declarations"/> generate a box at all: CSS Paged Media 3 §5.1 makes
    /// a margin box exist only if its <c>content</c> is something, and <c>none</c>, <c>normal</c>
    /// and no declaration at all are the three ways of being nothing. An empty string is not one of
    /// them — <c>content-001</c> pins that, with a <c>@bottom-left { content: "" }</c> whose
    /// background has to paint beside three boxes whose backgrounds must not.
    /// </summary>
    internal static bool GeneratesBox(IReadOnlyList<CssDeclaration> declarations) =>
        ContentValue(declarations) is not null;

    /// <summary>The used <c>content</c> value, or <c>null</c> when the box is not generated.</summary>
    internal static string? ContentValue(IReadOnlyList<CssDeclaration> declarations)
    {
        string? content = null;
        foreach (var declaration in declarations)
        {
            if (declaration.Name.Equals("content", StringComparison.OrdinalIgnoreCase))
                content = declaration.Value.Text.Trim();
        }

        if (content is null
            || content.Equals("none", StringComparison.OrdinalIgnoreCase)
            || content.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return content;
    }

    /// <summary>The last declaration of <paramref name="name"/>, or <c>null</c>.</summary>
    internal static string? Declared(IReadOnlyList<CssDeclaration> declarations, string name)
    {
        string? value = null;
        foreach (var declaration in declarations)
        {
            if (declaration.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                value = declaration.Value.Text.Trim();
        }

        return value;
    }

    /// <summary>
    /// The declarations as CSS text, minus the ones this render takes over — <c>content</c>, which
    /// becomes a <c>::before</c> rule, and <c>vertical-align</c>, which has no meaning on the flex
    /// container the box is emitted as and is translated into one that has.
    /// </summary>
    internal static string DeclarationsCss(IReadOnlyList<CssDeclaration> declarations)
    {
        var text = new StringBuilder();
        foreach (var declaration in declarations)
        {
            if (declaration.Name.Equals("content", StringComparison.OrdinalIgnoreCase)
                || declaration.Name.Equals("vertical-align", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            text.Append(declaration.Name).Append(':').Append(declaration.Value.Text).Append(';');
        }

        return text.ToString();
    }

    /// <summary>
    /// The UA alignment CSS Paged Media 3 §5.3 gives each slot: a margin box's content is aligned
    /// toward the page it annotates, so a header sits on the bottom of the top margin and a left
    /// box's text runs to the right.
    /// </summary>
    internal static (string TextAlign, string VerticalAlign) DefaultAlignment(WptMarginBoxSlot slot) =>
        slot switch
        {
            WptMarginBoxSlot.TopLeftCorner => ("right", "bottom"),
            WptMarginBoxSlot.TopLeft => ("left", "bottom"),
            WptMarginBoxSlot.TopCenter => ("center", "bottom"),
            WptMarginBoxSlot.TopRight => ("right", "bottom"),
            WptMarginBoxSlot.TopRightCorner => ("left", "bottom"),
            WptMarginBoxSlot.RightTop => ("center", "top"),
            WptMarginBoxSlot.RightMiddle => ("center", "middle"),
            WptMarginBoxSlot.RightBottom => ("center", "bottom"),
            WptMarginBoxSlot.BottomRightCorner => ("left", "top"),
            WptMarginBoxSlot.BottomRight => ("right", "top"),
            WptMarginBoxSlot.BottomCenter => ("center", "top"),
            WptMarginBoxSlot.BottomLeft => ("left", "top"),
            WptMarginBoxSlot.BottomLeftCorner => ("right", "top"),
            WptMarginBoxSlot.LeftBottom => ("center", "bottom"),
            WptMarginBoxSlot.LeftMiddle => ("center", "middle"),
            _ => ("center", "top"),
        };

    /// <summary>Formats a CSS pixel length in the invariant culture.</summary>
    internal static string Px(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture) + "px";
}
