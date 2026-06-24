using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Broiler.HtmlBridge;

/// <summary>CSS display property values.</summary>
public enum CssDisplay { Block, Inline, InlineBlock, None, Flex, Grid }

/// <summary>CSS position property values.</summary>
public enum CssPosition { Static, Relative, Absolute, Fixed }

/// <summary>CSS visibility property values.</summary>
public enum CssVisibility { Visible, Hidden, Collapse }

/// <summary>CSS vertical-align property values for inline/inline-block layout.</summary>
public enum CssVerticalAlign { Baseline, Top, Middle, Bottom, TextTop, TextBottom }

/// <summary>CSS float property values.</summary>
public enum CssFloat { None, Left, Right }

/// <summary>CSS clear property values.</summary>
public enum CssClear { None, Left, Right, Both }

/// <summary>CSS flex-direction property values.</summary>
public enum FlexDirection { Row, RowReverse, Column, ColumnReverse }

/// <summary>CSS flex-wrap property values.</summary>
public enum FlexWrap { NoWrap, Wrap, WrapReverse }

/// <summary>CSS align-items property values.</summary>
public enum AlignItems { Stretch, FlexStart, FlexEnd, Center, Baseline }

/// <summary>CSS justify-content property values.</summary>
public enum JustifyContent { FlexStart, FlexEnd, Center, SpaceBetween, SpaceAround, SpaceEvenly }

/// <summary>Unit type for grid track sizing.</summary>
public enum GridTrackUnit { Pixel, Fraction, Percent, Auto }

/// <summary>Represents a single grid track size such as <c>1fr</c>, <c>200px</c>, or <c>auto</c>.</summary>
/// <remarks>Initializes a new <see cref="GridTrackSize"/>.</remarks>
public class GridTrackSize(float value, GridTrackUnit unit)
{
    /// <summary>Numeric value of the track size.</summary>
    public float Value { get; set; } = value;     /// <summary>Unit of the track size.</summary>
    public GridTrackUnit Unit { get; set; } = unit;
}

internal static class GridTrackParsingLimits
{
    internal const int MaxTrackListCharacters = 65536;
}

/// <summary>A rectangle defined by position and size.</summary>
/// <remarks>Initializes a new <see cref="Rect"/>.</remarks>
public struct Rect(float x, float y, float width, float height)
{
    /// <summary>Horizontal position.</summary>
    public float X = x;
    /// <summary>Vertical position.</summary>
    public float Y = y;
    /// <summary>Width.</summary>
    public float Width = width;
    /// <summary>Height.</summary>
    public float Height = height;
}

/// <summary>Edge sizes for margin, border, or padding.</summary>
public class BoxEdges
{
    /// <summary>Top edge.</summary>
    public float Top;
    /// <summary>Right edge.</summary>
    public float Right;
    /// <summary>Bottom edge.</summary>
    public float Bottom;
    /// <summary>Left edge.</summary>
    public float Left;
}

/// <summary>Dimensions of a CSS box including content area and surrounding edges.</summary>
public class BoxDimensions
{
    /// <summary>Content box X position.</summary>
    public float X;
    /// <summary>Content box Y position.</summary>
    public float Y;
    /// <summary>Content box width.</summary>
    public float Width;
    /// <summary>Content box height.</summary>
    public float Height;
    /// <summary>Margin edges.</summary>
    public BoxEdges Margin = new();
    /// <summary>Border edges.</summary>
    public BoxEdges Border = new();
    /// <summary>Padding edges.</summary>
    public BoxEdges Padding = new();

    /// <summary>Returns the padding box rectangle.</summary>
    public Rect PaddingBox() => new(
        X - Padding.Left, Y - Padding.Top,
        Width + Padding.Left + Padding.Right, Height + Padding.Top + Padding.Bottom);

    /// <summary>Returns the border box rectangle.</summary>
    public Rect BorderBox()
    {
        var p = PaddingBox();
        return new Rect(p.X - Border.Left, p.Y - Border.Top,
            p.Width + Border.Left + Border.Right, p.Height + Border.Top + Border.Bottom);
    }

    /// <summary>Returns the margin box rectangle.</summary>
    public Rect MarginBox()
    {
        var b = BorderBox();
        return new Rect(b.X - Margin.Left, b.Y - Margin.Top,
            b.Width + Margin.Left + Margin.Right, b.Height + Margin.Top + Margin.Bottom);
    }
}

/// <summary>A box in the layout tree with computed dimensions and CSS properties.</summary>
/// <remarks>Initializes a new <see cref="LayoutBox"/> for the given element.</remarks>
public class LayoutBox(DomElement element)
{
    /// <summary>The DOM element this box represents.</summary>
    public DomElement Element = element;
    /// <summary>Computed box dimensions.</summary>
    public BoxDimensions Dimensions = new();
    /// <summary>Resolved display value.</summary>
    public CssDisplay Display;
    /// <summary>Resolved position value.</summary>
    public CssPosition Position;
    /// <summary>Resolved float value.</summary>
    public CssFloat Float;
    /// <summary>Resolved clear value.</summary>
    public CssClear Clear;
    /// <summary>Resolved visibility value.</summary>
    public CssVisibility Visibility;
    /// <summary>Resolved vertical-align value for inline/inline-block elements.</summary>
    public CssVerticalAlign VerticalAlign;
    /// <summary>Child layout boxes.</summary>
    public List<LayoutBox> Children = [];

    /// <summary>Resolved flex-direction for flex containers.</summary>
    public FlexDirection FlexDirection;
    /// <summary>Resolved flex-wrap for flex containers.</summary>
    public FlexWrap FlexWrap;
    /// <summary>Resolved align-items for flex and grid containers.</summary>
    public AlignItems AlignItems;
    /// <summary>Resolved justify-content for flex containers.</summary>
    public JustifyContent JustifyContent;
    /// <summary>Flex grow factor for flex items.</summary>
    public float FlexGrow;
    /// <summary>Flex shrink factor for flex items.</summary>
    public float FlexShrink = 1f;
    /// <summary>Gap between items in flex or grid containers.</summary>
    public float Gap;
    /// <summary>Grid template column track definitions.</summary>
    public List<GridTrackSize> GridTemplateColumns = [];
    /// <summary>Grid template row track definitions.</summary>
    public List<GridTrackSize> GridTemplateRows = [];
    /// <summary>Grid column placement index (0-based).</summary>
    public int GridColumn;
    /// <summary>Grid row placement index (0-based).</summary>
    public int GridRow;
}

/// <summary>
/// Implements CSS Box Model Level 3 layout with block, inline, and inline-block
/// formatting contexts, float/clear, and positioning.
/// </summary>
public class CssBoxModel
{
    private const float DefaultFontSize = 16f;
    private static readonly HashSet<string> BlockTags = new(
        StringComparer.OrdinalIgnoreCase)
    { "div", "p", "h1", "h2", "h3", "h4", "h5", "h6",
      "ul", "ol", "li", "dl", "dt", "dd",
      "section", "article", "header",
      "footer", "main", "nav", "aside", "blockquote", "form", "table" };

    /// <summary>Builds a layout tree from a DOM tree and performs layout.</summary>
    /// <param name="root">The root DOM element.</param>
    /// <param name="containerWidth">Available width for the root block.</param>
    /// <returns>The root <see cref="LayoutBox"/> with computed dimensions.</returns>
    public LayoutBox BuildLayoutTree(DomElement root, float containerWidth)
    {
        var box = CreateLayoutBox(root);
        box.Dimensions.Width = containerWidth;
        DispatchLayout(box, containerWidth);
        return box;
    }

    private void DispatchLayout(LayoutBox box, float containerWidth)
    {
        switch (box.Display)
        {
            case CssDisplay.Flex:
                LayoutFlex(box, containerWidth);
                break;
            case CssDisplay.Grid:
                LayoutGrid(box, containerWidth);
                break;
            default:
                LayoutBlock(box, containerWidth);
                break;
        }
    }

    private LayoutBox CreateLayoutBox(DomElement element)
    {
        var box = new LayoutBox(element)
        {
            Display = ResolveDisplay(element),
            Position = ResolvePosition(element),
            Float = ResolveFloat(element),
            Clear = ResolveClear(element),
            Visibility = ResolveVisibility(element),
            VerticalAlign = ResolveVerticalAlign(element)
        };
        if (box.Display == CssDisplay.None) return box;

        if (box.Display == CssDisplay.Flex)
        {
            box.FlexDirection = ResolveFlexDirection(element);
            box.FlexWrap = ResolveFlexWrap(element);
            box.AlignItems = ResolveAlignItems(element);
            box.JustifyContent = ResolveJustifyContent(element);
            box.Gap = ParseCssValue(GetStyle(element, "gap"), 0f, 0f);
        }
        else if (box.Display == CssDisplay.Grid)
        {
            box.GridTemplateColumns = ParseTrackList(GetStyle(element, "grid-template-columns"));
            box.GridTemplateRows = ParseTrackList(GetStyle(element, "grid-template-rows"));
            box.AlignItems = ResolveAlignItems(element);
            box.Gap = ParseCssValue(GetStyle(element, "gap"), 0f, 0f);
        }

        box.FlexGrow = ParseCssValue(GetStyle(element, "flex-grow"), 0f, 0f);
        box.FlexShrink = ParseCssValue(GetStyle(element, "flex-shrink"), 0f, 1f);

        string gridCol = GetStyle(element, "grid-column");
        if (gridCol != null && int.TryParse(gridCol, NumberStyles.Integer, CultureInfo.InvariantCulture, out int gc))
            box.GridColumn = gc - 1; // convert 1-based CSS to 0-based

        string gridRow = GetStyle(element, "grid-row");
        if (gridRow != null && int.TryParse(gridRow, NumberStyles.Integer, CultureInfo.InvariantCulture, out int gr))
            box.GridRow = gr - 1;

        foreach (var child in element.Children)
        {
            var childBox = CreateLayoutBox(child);
            if (childBox.Display != CssDisplay.None)
                box.Children.Add(childBox);
        }
        return box;
    }

    private void LayoutBlock(LayoutBox box, float containerWidth)
    {
        ResolveEdges(box, containerWidth);

        float edgeH = box.Dimensions.Margin.Left + box.Dimensions.Margin.Right
            + box.Dimensions.Border.Left + box.Dimensions.Border.Right
            + box.Dimensions.Padding.Left + box.Dimensions.Padding.Right;

        string explicitWidth = GetStyle(box.Element, "width");
        if (!string.IsNullOrWhiteSpace(explicitWidth) &&
            !explicitWidth.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            box.Dimensions.Width = ParseCssValue(explicitWidth, containerWidth, containerWidth - edgeH);
        }
        else
        {
            box.Dimensions.Width = containerWidth - edgeH;
        }

        float contentWidth = box.Dimensions.Width;
        float cursorY = 0f;
        float lineX = 0f;
        float lineHeight = 0f;
        var floatLeftX = 0f;
        var floatRightX = contentWidth;

        foreach (var child in box.Children)
        {
            if (child.Position == CssPosition.Absolute || child.Position == CssPosition.Fixed)
            {
                LayoutOutOfFlow(child, contentWidth);
                continue;
            }

            if (child.Clear != CssClear.None)
            {
                if (child.Clear == CssClear.Left || child.Clear == CssClear.Both)
                    floatLeftX = 0f;
                if (child.Clear == CssClear.Right || child.Clear == CssClear.Both)
                    floatRightX = contentWidth;
                // Clearing also moves cursor below any floated content.
                cursorY += lineHeight;
                lineHeight = 0f;
                lineX = floatLeftX;
            }

            if (child.Float != CssFloat.None)
            {
                float floatAvail = floatRightX - floatLeftX;
                LayoutBlock(child, floatAvail);
                var mb = child.Dimensions.MarginBox();
                if (child.Float == CssFloat.Left)
                {
                    child.Dimensions.X = box.Dimensions.X + box.Dimensions.Padding.Left + floatLeftX;
                    child.Dimensions.Y = box.Dimensions.Y + box.Dimensions.Padding.Top + cursorY;
                    floatLeftX += mb.Width;
                }
                else
                {
                    floatRightX -= mb.Width;
                    child.Dimensions.X = box.Dimensions.X + box.Dimensions.Padding.Left + floatRightX;
                    child.Dimensions.Y = box.Dimensions.Y + box.Dimensions.Padding.Top + cursorY;
                }
                if (mb.Height > lineHeight) lineHeight = mb.Height;
                continue;
            }

            if (child.Display == CssDisplay.Block || child.Display == CssDisplay.Flex
                || child.Display == CssDisplay.Grid)
            {
                // Flush any inline line before placing a block-level box.
                cursorY += lineHeight;
                lineHeight = 0f;
                lineX = floatLeftX;

                DispatchLayout(child, contentWidth);
                child.Dimensions.X = box.Dimensions.X + box.Dimensions.Padding.Left
                    + child.Dimensions.Margin.Left + child.Dimensions.Border.Left;
                child.Dimensions.Y = box.Dimensions.Y + box.Dimensions.Padding.Top + cursorY
                    + child.Dimensions.Margin.Top + child.Dimensions.Border.Top;
                cursorY += child.Dimensions.MarginBox().Height;
            }
            else // Inline or InlineBlock
            {
                float availableWidth = floatRightX - floatLeftX;
                if (child.Display == CssDisplay.InlineBlock)
                    LayoutBlock(child, availableWidth);
                else
                    ResolveEdges(child, availableWidth);

                var mb = child.Dimensions.MarginBox();
                if (child.Display == CssDisplay.Inline && child.Element.IsTextNode)
                    mb = EstimateTextBox(child, availableWidth);

                // Wrap to next line if needed.
                if (lineX + mb.Width > availableWidth && lineX > floatLeftX)
                {
                    cursorY += lineHeight;
                    lineHeight = 0f;
                    lineX = floatLeftX;
                }

                child.Dimensions.X = box.Dimensions.X + box.Dimensions.Padding.Left + lineX
                    + child.Dimensions.Margin.Left + child.Dimensions.Border.Left;
                child.Dimensions.Y = box.Dimensions.Y + box.Dimensions.Padding.Top + cursorY
                    + child.Dimensions.Margin.Top + child.Dimensions.Border.Top;

                lineX += mb.Width;
                if (mb.Height > lineHeight) lineHeight = mb.Height;

                // Lay out inline children recursively.
                if (child.Display == CssDisplay.Inline)
                    LayoutInlineChildren(child, availableWidth);
            }
        }

        cursorY += lineHeight;

        string explicitHeight = GetStyle(box.Element, "height");
        if (!string.IsNullOrWhiteSpace(explicitHeight) &&
            !explicitHeight.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            // containerHeight is 0 because CSS1 percentage heights only resolve
            // when the containing block has an explicit height; when absent the
            // default (cursorY = content height) is used.
            box.Dimensions.Height = ParseCssValue(explicitHeight, 0f, cursorY);
        }
        else
        {
            box.Dimensions.Height = cursorY;
        }

        ApplyPositionOffset(box);
    }

    private void LayoutInlineChildren(LayoutBox box, float containerWidth)
    {
        float x = 0f;
        foreach (var child in box.Children)
        {
            ResolveEdges(child, containerWidth);
            var mb = child.Dimensions.MarginBox();
            if (child.Element.IsTextNode)
                mb = EstimateTextBox(child, containerWidth);

            child.Dimensions.X = box.Dimensions.X + box.Dimensions.Padding.Left + x
                + child.Dimensions.Margin.Left + child.Dimensions.Border.Left
                + child.Dimensions.Padding.Left;
            child.Dimensions.Y = box.Dimensions.Y + box.Dimensions.Padding.Top;
            x += mb.Width;
        }
        if (x > box.Dimensions.Width) box.Dimensions.Width = x;
    }

    private void LayoutOutOfFlow(LayoutBox box, float containerWidth)
    {
        LayoutBlock(box, containerWidth);
        // Fixed-position elements are placed relative to the viewport origin.
        if (box.Position == CssPosition.Fixed)
        {
            box.Dimensions.X = ParseCssValue(GetStyle(box.Element, "left"), containerWidth, 0f);
            box.Dimensions.Y = ParseCssValue(GetStyle(box.Element, "top"), 0f, 0f);
        }
    }

    private static void ApplyPositionOffset(LayoutBox box)
    {
        if (box.Position != CssPosition.Static)
        {
            box.Dimensions.X += ParseCssValue(GetStyle(box.Element, "left"), 0f, 0f);
            box.Dimensions.Y += ParseCssValue(GetStyle(box.Element, "top"), 0f, 0f);
        }
    }

    private static Rect EstimateTextBox(LayoutBox box, float containerWidth)
    {
        string text = box.Element.TextContent ?? string.Empty;
        float w = Math.Min(text.Length * DefaultFontSize * 0.6f, containerWidth);
        box.Dimensions.Width = w;
        box.Dimensions.Height = DefaultFontSize;
        return new Rect(0, 0, w, DefaultFontSize);
    }

    private static void ResolveEdges(LayoutBox box, float containerWidth)
    {
        var d = box.Dimensions;
        var el = box.Element;
        d.Margin.Top = ParseCssValue(GetStyle(el, "margin-top"), containerWidth, 0f);
        d.Margin.Right = ParseCssValue(GetStyle(el, "margin-right"), containerWidth, 0f);
        d.Margin.Bottom = ParseCssValue(GetStyle(el, "margin-bottom"), containerWidth, 0f);
        d.Margin.Left = ParseCssValue(GetStyle(el, "margin-left"), containerWidth, 0f);
        d.Border.Top = ParseCssValue(GetStyle(el, "border-top-width"), containerWidth, 0f);
        d.Border.Right = ParseCssValue(GetStyle(el, "border-right-width"), containerWidth, 0f);
        d.Border.Bottom = ParseCssValue(GetStyle(el, "border-bottom-width"), containerWidth, 0f);
        d.Border.Left = ParseCssValue(GetStyle(el, "border-left-width"), containerWidth, 0f);
        d.Padding.Top = ParseCssValue(GetStyle(el, "padding-top"), containerWidth, 0f);
        d.Padding.Right = ParseCssValue(GetStyle(el, "padding-right"), containerWidth, 0f);
        d.Padding.Bottom = ParseCssValue(GetStyle(el, "padding-bottom"), containerWidth, 0f);
        d.Padding.Left = ParseCssValue(GetStyle(el, "padding-left"), containerWidth, 0f);
    }

    private CssDisplay ResolveDisplay(DomElement el)
    {
        string val = GetStyle(el, "display");
        if (val != null)
        {
            if (val.Equals("block", StringComparison.OrdinalIgnoreCase)) return CssDisplay.Block;
            if (val.Equals("inline-block", StringComparison.OrdinalIgnoreCase)) return CssDisplay.InlineBlock;
            if (val.Equals("none", StringComparison.OrdinalIgnoreCase)) return CssDisplay.None;
            if (val.Equals("inline", StringComparison.OrdinalIgnoreCase)) return CssDisplay.Inline;
            if (val.Equals("flex", StringComparison.OrdinalIgnoreCase)) return CssDisplay.Flex;
            if (val.Equals("grid", StringComparison.OrdinalIgnoreCase)) return CssDisplay.Grid;
        }
        if (el.IsTextNode) return CssDisplay.Inline;
        return BlockTags.Contains(el.TagName) ? CssDisplay.Block : CssDisplay.Inline;
    }

    private static CssPosition ResolvePosition(DomElement el)
    {
        string val = GetStyle(el, "position");
        if (val == null) return CssPosition.Static;
        if (val.Equals("relative", StringComparison.OrdinalIgnoreCase)) return CssPosition.Relative;
        if (val.Equals("absolute", StringComparison.OrdinalIgnoreCase)) return CssPosition.Absolute;
        if (val.Equals("fixed", StringComparison.OrdinalIgnoreCase)) return CssPosition.Fixed;
        return CssPosition.Static;
    }

    private static CssFloat ResolveFloat(DomElement el)
    {
        string val = GetStyle(el, "float");
        if (val == null) return CssFloat.None;
        if (val.Equals("left", StringComparison.OrdinalIgnoreCase)) return CssFloat.Left;
        if (val.Equals("right", StringComparison.OrdinalIgnoreCase)) return CssFloat.Right;
        return CssFloat.None;
    }

    private static CssClear ResolveClear(DomElement el)
    {
        string val = GetStyle(el, "clear");
        if (val == null) return CssClear.None;
        if (val.Equals("left", StringComparison.OrdinalIgnoreCase)) return CssClear.Left;
        if (val.Equals("right", StringComparison.OrdinalIgnoreCase)) return CssClear.Right;
        if (val.Equals("both", StringComparison.OrdinalIgnoreCase)) return CssClear.Both;
        return CssClear.None;
    }

    private static CssVisibility ResolveVisibility(DomElement el)
    {
        string val = GetStyle(el, "visibility");
        if (val == null) return CssVisibility.Visible;
        if (val.Equals("hidden", StringComparison.OrdinalIgnoreCase)) return CssVisibility.Hidden;
        if (val.Equals("collapse", StringComparison.OrdinalIgnoreCase)) return CssVisibility.Collapse;
        return CssVisibility.Visible;
    }

    private static CssVerticalAlign ResolveVerticalAlign(DomElement el)
    {
        string val = GetStyle(el, "vertical-align");
        if (val == null) return CssVerticalAlign.Baseline;
        if (val.Equals("top", StringComparison.OrdinalIgnoreCase)) return CssVerticalAlign.Top;
        if (val.Equals("middle", StringComparison.OrdinalIgnoreCase)) return CssVerticalAlign.Middle;
        if (val.Equals("bottom", StringComparison.OrdinalIgnoreCase)) return CssVerticalAlign.Bottom;
        if (val.Equals("text-top", StringComparison.OrdinalIgnoreCase)) return CssVerticalAlign.TextTop;
        if (val.Equals("text-bottom", StringComparison.OrdinalIgnoreCase)) return CssVerticalAlign.TextBottom;
        return CssVerticalAlign.Baseline;
    }

    private static FlexDirection ResolveFlexDirection(DomElement el)
    {
        string val = GetStyle(el, "flex-direction");
        if (val == null) return FlexDirection.Row;
        if (val.Equals("row-reverse", StringComparison.OrdinalIgnoreCase)) return FlexDirection.RowReverse;
        if (val.Equals("column", StringComparison.OrdinalIgnoreCase)) return FlexDirection.Column;
        if (val.Equals("column-reverse", StringComparison.OrdinalIgnoreCase)) return FlexDirection.ColumnReverse;
        return FlexDirection.Row;
    }

    private static FlexWrap ResolveFlexWrap(DomElement el)
    {
        string val = GetStyle(el, "flex-wrap");
        if (val == null) return FlexWrap.NoWrap;
        if (val.Equals("wrap", StringComparison.OrdinalIgnoreCase)) return FlexWrap.Wrap;
        if (val.Equals("wrap-reverse", StringComparison.OrdinalIgnoreCase)) return FlexWrap.WrapReverse;
        return FlexWrap.NoWrap;
    }

    private static AlignItems ResolveAlignItems(DomElement el)
    {
        string val = GetStyle(el, "align-items");
        if (val == null) return AlignItems.Stretch;
        if (val.Equals("flex-start", StringComparison.OrdinalIgnoreCase)) return AlignItems.FlexStart;
        if (val.Equals("flex-end", StringComparison.OrdinalIgnoreCase)) return AlignItems.FlexEnd;
        if (val.Equals("center", StringComparison.OrdinalIgnoreCase)) return AlignItems.Center;
        if (val.Equals("baseline", StringComparison.OrdinalIgnoreCase)) return AlignItems.Baseline;
        return AlignItems.Stretch;
    }

    private static JustifyContent ResolveJustifyContent(DomElement el)
    {
        string val = GetStyle(el, "justify-content");
        if (val == null) return JustifyContent.FlexStart;
        if (val.Equals("flex-end", StringComparison.OrdinalIgnoreCase)) return JustifyContent.FlexEnd;
        if (val.Equals("center", StringComparison.OrdinalIgnoreCase)) return JustifyContent.Center;
        if (val.Equals("space-between", StringComparison.OrdinalIgnoreCase)) return JustifyContent.SpaceBetween;
        if (val.Equals("space-around", StringComparison.OrdinalIgnoreCase)) return JustifyContent.SpaceAround;
        if (val.Equals("space-evenly", StringComparison.OrdinalIgnoreCase)) return JustifyContent.SpaceEvenly;
        return JustifyContent.FlexStart;
    }

    /// <summary>Parses a space-separated track list such as <c>"1fr 200px auto"</c>.</summary>
    private static List<GridTrackSize> ParseTrackList(string value)
    {
        var tracks = new List<GridTrackSize>();
        if (string.IsNullOrWhiteSpace(value)) return tracks;

        // Guard pathological crash tests that assign multi-megabyte track lists.
        // This parser only supports the simple subset used by Broiler layout, so
        // refusing absurd inputs keeps the runner responsive without affecting
        // ordinary authored grids.
        if (value.Length > GridTrackParsingLimits.MaxTrackListCharacters)
            return tracks;

        foreach (string token in value.Split([' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                tracks.Add(new GridTrackSize(0f, GridTrackUnit.Auto));
            }
            else if (token.EndsWith("fr", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(token.AsSpan(0, token.Length - 2),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out float fr))
                    tracks.Add(new GridTrackSize(fr, GridTrackUnit.Fraction));
            }
            else if (token.EndsWith("%", StringComparison.Ordinal))
            {
                if (float.TryParse(token.AsSpan(0, token.Length - 1),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
                    tracks.Add(new GridTrackSize(pct, GridTrackUnit.Percent));
            }
            else
            {
                string num = token;
                if (token.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                    num = token.Substring(0, token.Length - 2);
                if (float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float px))
                    tracks.Add(new GridTrackSize(px, GridTrackUnit.Pixel));
            }
        }
        return tracks;
    }

    /// <summary>Performs flexbox layout on a flex container.</summary>
    private void LayoutFlex(LayoutBox box, float containerWidth)
    {
        ResolveEdges(box, containerWidth);
        box.Dimensions.Width = containerWidth
            - box.Dimensions.Margin.Left - box.Dimensions.Margin.Right
            - box.Dimensions.Border.Left - box.Dimensions.Border.Right
            - box.Dimensions.Padding.Left - box.Dimensions.Padding.Right;

        float contentWidth = box.Dimensions.Width;
        bool isRow = box.FlexDirection == FlexDirection.Row
                  || box.FlexDirection == FlexDirection.RowReverse;
        bool isReverse = box.FlexDirection == FlexDirection.RowReverse
                      || box.FlexDirection == FlexDirection.ColumnReverse;
        float mainSize = isRow ? contentWidth : float.MaxValue;

        // Lay out each child to determine its intrinsic size.
        foreach (var child in box.Children)
        {
            ResolveEdges(child, contentWidth);
            DispatchLayout(child, contentWidth);
        }

        // Split children into flex lines.
        var lines = new List<List<LayoutBox>>();
        var currentLine = new List<LayoutBox>();
        float lineMain = 0f;

        foreach (var child in box.Children)
        {
            var mb = child.Dimensions.MarginBox();
            float childMain = isRow ? mb.Width : mb.Height;

            if (box.FlexWrap != FlexWrap.NoWrap && currentLine.Count > 0
                && lineMain + childMain + (currentLine.Count > 0 ? box.Gap : 0f) > mainSize)
            {
                lines.Add(currentLine);
                currentLine = [];
                lineMain = 0f;
            }

            if (currentLine.Count > 0) lineMain += box.Gap;
            lineMain += childMain;
            currentLine.Add(child);
        }
        if (currentLine.Count > 0) lines.Add(currentLine);

        if (box.FlexWrap == FlexWrap.WrapReverse) lines.Reverse();

        float crossOffset = 0f;
        float totalHeight = 0f;

        foreach (var line in lines)
        {
            if (isReverse) line.Reverse();

            // Compute total main size and remaining space.
            float totalMain = 0f;
            float totalGaps = (line.Count - 1) * box.Gap;
            foreach (var child in line)
            {
                var mb = child.Dimensions.MarginBox();
                totalMain += isRow ? mb.Width : mb.Height;
            }

            float freeSpace = mainSize - totalMain - totalGaps;

            // Distribute extra space via flex-grow or shrink.
            if (freeSpace > 0)
            {
                float totalGrow = line.Sum(c => c.FlexGrow);
                if (totalGrow > 0)
                {
                    foreach (var child in line)
                    {
                        float extra = freeSpace * (child.FlexGrow / totalGrow);
                        if (isRow)
                            child.Dimensions.Width += extra;
                        else
                            child.Dimensions.Height += extra;
                    }
                    freeSpace = 0f;
                }
            }
            else if (freeSpace < 0)
            {
                float totalShrink = line.Sum(c => c.FlexShrink);
                if (totalShrink > 0)
                {
                    foreach (var child in line)
                    {
                        float reduction = (-freeSpace) * (child.FlexShrink / totalShrink);
                        if (isRow)
                            child.Dimensions.Width = Math.Max(0f, child.Dimensions.Width - reduction);
                        else
                            child.Dimensions.Height = Math.Max(0f, child.Dimensions.Height - reduction);
                    }
                    freeSpace = 0f;
                }
            }

            // Compute justify-content offsets.
            float mainCursor = 0f;
            float spaceBefore = 0f;
            float spaceBetween = box.Gap;

            if (freeSpace > 0)
            {
                switch (box.JustifyContent)
                {
                    case JustifyContent.FlexEnd:
                        mainCursor = freeSpace;
                        break;
                    case JustifyContent.Center:
                        mainCursor = freeSpace / 2f;
                        break;
                    case JustifyContent.SpaceBetween:
                        spaceBetween = line.Count > 1
                            ? (freeSpace + totalGaps) / (line.Count - 1) : box.Gap;
                        break;
                    case JustifyContent.SpaceAround:
                        spaceBefore = freeSpace / (line.Count * 2);
                        spaceBetween = spaceBefore * 2 + box.Gap;
                        mainCursor = spaceBefore;
                        break;
                    case JustifyContent.SpaceEvenly:
                        spaceBefore = freeSpace / (line.Count + 1);
                        spaceBetween = spaceBefore + box.Gap;
                        mainCursor = spaceBefore;
                        break;
                    default: // FlexStart
                        break;
                }
            }

            // Compute cross size of this line.
            float lineCross = 0f;
            foreach (var child in line)
            {
                var mb = child.Dimensions.MarginBox();
                float cc = isRow ? mb.Height : mb.Width;
                if (cc > lineCross) lineCross = cc;
            }

            // Position each child.
            foreach (var child in line)
            {
                var mb = child.Dimensions.MarginBox();
                float childCross = isRow ? mb.Height : mb.Width;

                float crossPos;
                switch (box.AlignItems)
                {
                    case AlignItems.FlexEnd:
                        crossPos = crossOffset + lineCross - childCross;
                        break;
                    case AlignItems.Center:
                        crossPos = crossOffset + (lineCross - childCross) / 2f;
                        break;
                    case AlignItems.Baseline:
                    case AlignItems.FlexStart:
                        crossPos = crossOffset;
                        break;
                    default: // Stretch
                        crossPos = crossOffset;
                        if (isRow)
                            child.Dimensions.Height = lineCross
                                - child.Dimensions.Margin.Top - child.Dimensions.Margin.Bottom
                                - child.Dimensions.Border.Top - child.Dimensions.Border.Bottom
                                - child.Dimensions.Padding.Top - child.Dimensions.Padding.Bottom;
                        else
                            child.Dimensions.Width = lineCross
                                - child.Dimensions.Margin.Left - child.Dimensions.Margin.Right
                                - child.Dimensions.Border.Left - child.Dimensions.Border.Right
                                - child.Dimensions.Padding.Left - child.Dimensions.Padding.Right;
                        break;
                }

                if (isRow)
                {
                    child.Dimensions.X = box.Dimensions.X + box.Dimensions.Padding.Left
                        + mainCursor + child.Dimensions.Margin.Left + child.Dimensions.Border.Left;
                    child.Dimensions.Y = box.Dimensions.Y + box.Dimensions.Padding.Top
                        + crossPos + child.Dimensions.Margin.Top + child.Dimensions.Border.Top;
                }
                else
                {
                    child.Dimensions.X = box.Dimensions.X + box.Dimensions.Padding.Left
                        + crossPos + child.Dimensions.Margin.Left + child.Dimensions.Border.Left;
                    child.Dimensions.Y = box.Dimensions.Y + box.Dimensions.Padding.Top
                        + mainCursor + child.Dimensions.Margin.Top + child.Dimensions.Border.Top;
                }

                mainCursor += (isRow ? mb.Width : mb.Height) + spaceBetween;
            }

            crossOffset += lineCross + box.Gap;
            if (isRow)
            {
                if (crossOffset > totalHeight) totalHeight = crossOffset;
            }
            else
            {
                if (mainCursor > totalHeight) totalHeight = mainCursor;
            }
        }

        if (isRow)
            box.Dimensions.Height = crossOffset > 0 ? crossOffset - box.Gap : 0f;
        else
            box.Dimensions.Height = totalHeight > 0 ? totalHeight - box.Gap : 0f;

        ApplyPositionOffset(box);
    }

    /// <summary>Resolves track sizes to pixel values.</summary>
    private static float[] ResolveTracks(List<GridTrackSize> tracks, float containerSize,
        int autoCount, float totalGap)
    {
        float available = containerSize - totalGap;
        float usedFixed = 0f;
        float totalFr = 0f;
        int autos = 0;

        foreach (var t in tracks)
        {
            switch (t.Unit)
            {
                case GridTrackUnit.Pixel:
                    usedFixed += t.Value;
                    break;
                case GridTrackUnit.Percent:
                    usedFixed += containerSize * t.Value / 100f;
                    break;
                case GridTrackUnit.Fraction:
                    totalFr += t.Value;
                    break;
                case GridTrackUnit.Auto:
                    autos++;
                    break;
            }
        }

        float remaining = Math.Max(0f, available - usedFixed);
        float frUnit = totalFr > 0 ? remaining / (totalFr + autos) : (autos > 0 ? remaining / autos : 0f);

        var result = new float[tracks.Count];
        for (int i = 0; i < tracks.Count; i++)
        {
            switch (tracks[i].Unit)
            {
                case GridTrackUnit.Pixel:
                    result[i] = tracks[i].Value;
                    break;
                case GridTrackUnit.Percent:
                    result[i] = containerSize * tracks[i].Value / 100f;
                    break;
                case GridTrackUnit.Fraction:
                    result[i] = frUnit * tracks[i].Value;
                    break;
                case GridTrackUnit.Auto:
                    result[i] = frUnit;
                    break;
            }
        }
        return result;
    }

    /// <summary>Performs grid layout on a grid container.</summary>
    private void LayoutGrid(LayoutBox box, float containerWidth)
    {
        ResolveEdges(box, containerWidth);
        box.Dimensions.Width = containerWidth
            - box.Dimensions.Margin.Left - box.Dimensions.Margin.Right
            - box.Dimensions.Border.Left - box.Dimensions.Border.Right
            - box.Dimensions.Padding.Left - box.Dimensions.Padding.Right;

        float contentWidth = box.Dimensions.Width;

        int cols = box.GridTemplateColumns.Count;
        int rows = box.GridTemplateRows.Count;

        // Default to a single-column grid when no template is specified.
        if (cols == 0)
        {
            cols = 1;
            box.GridTemplateColumns.Add(new GridTrackSize(1f, GridTrackUnit.Fraction));
        }

        // Ensure enough rows to place all items.
        int itemCount = box.Children.Count;
        int neededRows = Math.Max(rows, (int)Math.Ceiling((double)itemCount / cols));
        while (box.GridTemplateRows.Count < neededRows)
            box.GridTemplateRows.Add(new GridTrackSize(0f, GridTrackUnit.Auto));
        rows = box.GridTemplateRows.Count;

        float colGapTotal = (cols - 1) * box.Gap;
        float rowGapTotal = (rows - 1) * box.Gap;

        float[] colSizes = ResolveTracks(box.GridTemplateColumns, contentWidth, 0, colGapTotal);

        // First pass: lay out children to determine auto row heights.
        var autoRowHeights = new float[rows];

        for (int i = 0; i < box.Children.Count; i++)
        {
            var child = box.Children[i];
            int col = child.GridColumn;
            int row = child.GridRow;

            // Auto-place items that have no explicit placement.
            if (col == 0 && row == 0 && i > 0)
            {
                col = i % cols;
                row = i / cols;
                child.GridColumn = col;
                child.GridRow = row;
            }
            else if (col == 0 && row == 0 && i == 0)
            {
                child.GridColumn = 0;
                child.GridRow = 0;
            }

            if (col >= cols) col = cols - 1;
            if (row >= rows) row = rows - 1;

            float cellWidth = col < colSizes.Length ? colSizes[col] : 0f;
            ResolveEdges(child, cellWidth);
            DispatchLayout(child, cellWidth);

            var mb = child.Dimensions.MarginBox();
            if (mb.Height > autoRowHeights[row])
                autoRowHeights[row] = mb.Height;
        }

        // Resolve row tracks, substituting auto heights.
        for (int r = 0; r < rows; r++)
        {
            if (box.GridTemplateRows[r].Unit == GridTrackUnit.Auto)
                box.GridTemplateRows[r] = new GridTrackSize(autoRowHeights[r], GridTrackUnit.Pixel);
        }

        float[] rowSizes = ResolveTracks(box.GridTemplateRows, float.MaxValue, 0, rowGapTotal);

        // Compute column and row offsets.
        float[] colOffsets = new float[cols];
        for (int c = 0; c < cols; c++)
            colOffsets[c] = c > 0 ? colOffsets[c - 1] + colSizes[c - 1] + box.Gap : 0f;

        float[] rowOffsets = new float[rows];
        for (int r = 0; r < rows; r++)
            rowOffsets[r] = r > 0 ? rowOffsets[r - 1] + rowSizes[r - 1] + box.Gap : 0f;

        // Position children in their grid cells.
        for (int i = 0; i < box.Children.Count; i++)
        {
            var child = box.Children[i];
            int col = child.GridColumn;
            int row = child.GridRow;

            if (col >= cols) col = cols - 1;
            if (row >= rows) row = rows - 1;

            float cellWidth = colSizes[col];
            float cellHeight = rowSizes[row];

            // Apply align-items on the cross axis (vertical in grid).
            float crossPos = 0f;
            var mb = child.Dimensions.MarginBox();
            switch (box.AlignItems)
            {
                case AlignItems.FlexEnd:
                    crossPos = cellHeight - mb.Height;
                    break;
                case AlignItems.Center:
                    crossPos = (cellHeight - mb.Height) / 2f;
                    break;
                case AlignItems.Stretch:
                    child.Dimensions.Height = cellHeight
                        - child.Dimensions.Margin.Top - child.Dimensions.Margin.Bottom
                        - child.Dimensions.Border.Top - child.Dimensions.Border.Bottom
                        - child.Dimensions.Padding.Top - child.Dimensions.Padding.Bottom;
                    break;
                default: // FlexStart, Baseline
                    break;
            }

            child.Dimensions.X = box.Dimensions.X + box.Dimensions.Padding.Left
                + colOffsets[col] + child.Dimensions.Margin.Left + child.Dimensions.Border.Left;
            child.Dimensions.Y = box.Dimensions.Y + box.Dimensions.Padding.Top
                + rowOffsets[row] + crossPos
                + child.Dimensions.Margin.Top + child.Dimensions.Border.Top;
        }

        // Set container height to total row heights plus gaps.
        float totalH = 0f;
        for (int r = 0; r < rows; r++) totalH += rowSizes[r];
        totalH += rowGapTotal;
        box.Dimensions.Height = totalH;

        ApplyPositionOffset(box);
    }

    private static string GetStyle(DomElement el, string property)
    {
        el.Style.TryGetValue(property, out string value);
        return value;
    }

    /// <summary>
    /// Parses a CSS length value such as "10px", "50%", "2cm", or "auto".
    /// Percentages are resolved against <paramref name="containerSize"/>.
    /// Physical units (cm, mm, in) are converted to pixels at 96 DPI.
    /// </summary>
    public static float ParseCssValue(string value, float containerSize, float defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return defaultValue;
        if (value.EndsWith("%", StringComparison.Ordinal))
            return float.TryParse(value.AsSpan(0, value.Length - 1),
                NumberStyles.Float, CultureInfo.InvariantCulture, out float pct)
                ? containerSize * pct / 100f : defaultValue;

        // Check for physical units with conversion factors (96 DPI).
        if (value.EndsWith("cm", StringComparison.OrdinalIgnoreCase))
        {
            var num = value[..^2];
            return float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float cm)
                ? cm * 37.7952756f : defaultValue; // 96 / 2.54
        }
        if (value.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
        {
            var num = value[..^2];
            return float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float mm)
                ? mm * 3.77952756f : defaultValue; // 96 / 25.4
        }
        if (value.EndsWith("in", StringComparison.OrdinalIgnoreCase))
        {
            var num = value[..^2];
            return float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float inches)
                ? inches * 96f : defaultValue;
        }

        string numeric = value;
        foreach (var suffix in new[] { "px", "em", "rem", "pt" })
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            { numeric = value.Substring(0, value.Length - suffix.Length); break; }

        return float.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out float result)
            ? result : defaultValue;
    }
}
