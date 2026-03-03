using Broiler.App.Rendering;

namespace Broiler.App.Tests;

/// <summary>
/// Phase 3 cross-feature rendering tests. Verifies interactions between
/// CSS features that are otherwise only tested in isolation: floats with
/// positioning, tables with overflow, inline-block with text-align, and
/// z-index with opacity.
/// </summary>
[Trait("Category", "Rendering")]
[Trait("Engine", "Broiler")]
public class CrossFeatureRenderingTests
{
    // ── Float + Positioning ─────────────────────────────────────────

    /// <summary>
    /// A floated element inside a relatively positioned container should
    /// retain its float direction and render within the container bounds.
    /// </summary>
    [Fact]
    public void Float_InsideRelativeContainer_RendersWithinBounds()
    {
        var container = CreateBlock("div", width: 300, height: 200, x: 20, y: 20);
        container.Position = CssPosition.Relative;

        var floated = CreateBlock("div", width: 80, height: 60, bg: "orange", x: 20, y: 20);
        floated.Float = CssFloat.Left;
        container.Children.Add(floated);

        var output = RenderThrough(container);

        var floatCmd = output.Commands.FirstOrDefault(c =>
            c.Type == PaintCommandType.Background && c.BackgroundColor == "orange");
        Assert.NotNull(floatCmd);

        // Float bounds must be within the container area
        Assert.True(floatCmd.Bounds.X >= container.Dimensions.X,
            "Float X must be >= container X.");
        Assert.True(floatCmd.Bounds.Y >= container.Dimensions.Y,
            "Float Y must be >= container Y.");
    }

    /// <summary>
    /// A left float and a right float in the same container produce
    /// commands with non-overlapping horizontal extents.
    /// </summary>
    [Fact]
    public void Float_LeftAndRight_NonOverlappingHorizontally()
    {
        var container = CreateBlock("div", width: 400, height: 100);

        var leftFloat = CreateBlock("div", width: 100, height: 80, bg: "red", x: 0, y: 0);
        leftFloat.Float = CssFloat.Left;

        var rightFloat = CreateBlock("div", width: 100, height: 80, bg: "blue", x: 300, y: 0);
        rightFloat.Float = CssFloat.Right;

        container.Children.Add(leftFloat);
        container.Children.Add(rightFloat);

        var output = RenderThrough(container);

        var bgCommands = output.Commands.Where(c =>
            c.Type == PaintCommandType.Background &&
            !string.IsNullOrEmpty(c.BackgroundColor)).ToList();
        Assert.True(bgCommands.Count >= 2, "Should have commands for both floats.");

        var redCmd = bgCommands.First(c => c.BackgroundColor == "red");
        var blueCmd = bgCommands.First(c => c.BackgroundColor == "blue");

        float redRight = redCmd.Bounds.X + redCmd.Bounds.Width;
        Assert.True(blueCmd.Bounds.X >= redRight,
            $"Right float (X={blueCmd.Bounds.X}) must not overlap left float (right edge={redRight}).");
    }

    /// <summary>
    /// A clear:both element after floats should be positioned below both floats.
    /// </summary>
    [Fact]
    public void Clear_AfterFloats_PositionedBelow()
    {
        var container = CreateBlock("div", width: 400, height: 200);

        var floated = CreateBlock("div", width: 100, height: 60, bg: "red", x: 0, y: 0);
        floated.Float = CssFloat.Left;
        container.Children.Add(floated);

        var cleared = CreateBlock("div", width: 400, height: 40, bg: "green", x: 0, y: 60);
        cleared.Clear = CssClear.Both;
        container.Children.Add(cleared);

        var output = RenderThrough(container);

        var redCmd = output.Commands.First(c =>
            c.Type == PaintCommandType.Background && c.BackgroundColor == "red");
        var greenCmd = output.Commands.First(c =>
            c.Type == PaintCommandType.Background && c.BackgroundColor == "green");

        float floatBottom = redCmd.Bounds.Y + redCmd.Bounds.Height;
        Assert.True(greenCmd.Bounds.Y >= floatBottom,
            $"Cleared element (Y={greenCmd.Bounds.Y}) must be at or below float bottom ({floatBottom}).");
    }

    // ── Table + Overflow ────────────────────────────────────────────

    /// <summary>
    /// A table cell with overflow content produces paint commands that
    /// extend to the cell's content bounds, not just the visible area.
    /// </summary>
    [Fact]
    public void Table_CellWithOverflowContent_PaintsContent()
    {
        var table = CreateBlock("table", width: 300, height: 100);
        var row = CreateBlock("tr", width: 300, height: 100);
        var cell = CreateBlock("td", width: 300, height: 100, bg: "lightblue");

        var overflowContent = CreateBlock("div", width: 500, height: 200, bg: "red");
        cell.Children.Add(overflowContent);
        row.Children.Add(cell);
        table.Children.Add(row);

        var output = RenderThrough(table);

        // The overflow div should still produce a paint command
        var overflowCmd = output.Commands.FirstOrDefault(c =>
            c.Type == PaintCommandType.Background && c.BackgroundColor == "red");
        Assert.NotNull(overflowCmd);
        Assert.Equal(500f, overflowCmd.Bounds.Width);
    }

    /// <summary>
    /// Multiple table rows stack vertically with correct Y offsets.
    /// </summary>
    [Fact]
    public void Table_MultipleRows_StackVertically()
    {
        var table = CreateBlock("table", width: 200, height: 120);

        var row1 = CreateBlock("tr", width: 200, height: 40, y: 0);
        var cell1 = CreateBlock("td", width: 200, height: 40, bg: "red");
        row1.Children.Add(cell1);

        var row2 = CreateBlock("tr", width: 200, height: 40, y: 40);
        var cell2 = CreateBlock("td", width: 200, height: 40, bg: "green");
        row2.Children.Add(cell2);

        var row3 = CreateBlock("tr", width: 200, height: 40, y: 80);
        var cell3 = CreateBlock("td", width: 200, height: 40, bg: "blue");
        row3.Children.Add(cell3);

        table.Children.Add(row1);
        table.Children.Add(row2);
        table.Children.Add(row3);

        var output = RenderThrough(table);

        var bgCommands = output.Commands
            .Where(c => c.Type == PaintCommandType.Background && !string.IsNullOrEmpty(c.BackgroundColor))
            .OrderBy(c => c.Bounds.Y)
            .ToList();
        Assert.True(bgCommands.Count >= 3, "Should have background commands for all 3 cells.");

        // Verify vertical stacking
        for (int i = 1; i < bgCommands.Count; i++)
        {
            Assert.True(bgCommands[i].Bounds.Y >= bgCommands[i - 1].Bounds.Y,
                $"Row {i} Y ({bgCommands[i].Bounds.Y}) must be >= row {i - 1} Y ({bgCommands[i - 1].Bounds.Y}).");
        }
    }

    // ── Inline-Block + Text Alignment ───────────────────────────────

    /// <summary>
    /// Inline-block elements are placed side by side horizontally.
    /// </summary>
    [Fact]
    public void InlineBlock_SideBySide_HorizontalLayout()
    {
        var container = CreateBlock("div", width: 400, height: 50);

        var ib1 = CreateBox("span", CssDisplay.InlineBlock, width: 80, height: 40, bg: "red", x: 0, y: 0);
        var ib2 = CreateBox("span", CssDisplay.InlineBlock, width: 80, height: 40, bg: "green", x: 85, y: 0);
        var ib3 = CreateBox("span", CssDisplay.InlineBlock, width: 80, height: 40, bg: "blue", x: 170, y: 0);

        container.Children.Add(ib1);
        container.Children.Add(ib2);
        container.Children.Add(ib3);

        var output = RenderThrough(container);

        var bgCommands = output.Commands
            .Where(c => c.Type == PaintCommandType.Background && !string.IsNullOrEmpty(c.BackgroundColor))
            .OrderBy(c => c.Bounds.X)
            .ToList();
        Assert.True(bgCommands.Count >= 3, "Should have 3 inline-block background commands.");

        // All at same Y (same line)
        float firstY = bgCommands[0].Bounds.Y;
        foreach (var cmd in bgCommands)
        {
            Assert.Equal(firstY, cmd.Bounds.Y);
        }
    }

    /// <summary>
    /// Inline-block elements with text children produce both background
    /// and text paint commands at consistent positions.
    /// </summary>
    [Fact]
    public void InlineBlock_WithText_ProducesBothCommandTypes()
    {
        var container = CreateBlock("div", width: 300, height: 40);

        var span = CreateBox("span", CssDisplay.InlineBlock, width: 100, height: 30, bg: "yellow", x: 10, y: 5);
        var textEl = new DomElement("#text", null, null, string.Empty, isTextNode: true);
        textEl.TextContent = "Button";
        var textBox = new LayoutBox(textEl) { Display = CssDisplay.Inline };
        textBox.Dimensions.X = 10;
        textBox.Dimensions.Y = 5;
        textBox.Dimensions.Width = 50;
        textBox.Dimensions.Height = 14;
        span.Children.Add(textBox);

        container.Children.Add(span);

        var output = RenderThrough(container);

        Assert.Contains(output.Commands, c => c.Type == PaintCommandType.Background);
        Assert.Contains(output.Commands, c => c.Type == PaintCommandType.Text && c.Text == "Button");
    }

    // ── Z-Index + Opacity Interaction ───────────────────────────────

    /// <summary>
    /// Elements with opacity < 1 create an implicit stacking context; their
    /// paint commands reflect the reduced opacity.
    /// </summary>
    [Fact]
    public void ZIndex_WithOpacity_CreateStackingContext()
    {
        var container = CreateBlock("div", width: 200, height: 200);

        var baseLayer = CreateBlock("div", width: 200, height: 200, bg: "white");
        baseLayer.Element.Style["z-index"] = "0";
        baseLayer.Element.Style["opacity"] = "1.0";

        var overlay = CreateBlock("div", width: 100, height: 100, bg: "red");
        overlay.Element.Style["z-index"] = "1";
        overlay.Element.Style["opacity"] = "0.7";

        container.Children.Add(baseLayer);
        container.Children.Add(overlay);

        var painter = new Painter();
        var commands = painter.Paint(container);

        // Find the overlay command and verify opacity
        var overlayCmd = commands.FirstOrDefault(c =>
            c.Type == PaintCommandType.Background && c.BackgroundColor == "red");
        Assert.NotNull(overlayCmd);
        Assert.Equal(0.7f, overlayCmd.Opacity, 2);
    }

    /// <summary>
    /// Border and background on the same element produce commands at the
    /// same z-index level.
    /// </summary>
    [Fact]
    public void Border_AndBackground_SameZIndex()
    {
        var el = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "background-color", "lightgray" },
                { "border-color", "black" },
                { "z-index", "3" }
            });
        var box = new LayoutBox(el) { Display = CssDisplay.Block };
        box.Dimensions.Width = 150;
        box.Dimensions.Height = 100;
        box.Dimensions.Border = new BoxEdges { Top = 2, Right = 2, Bottom = 2, Left = 2 };

        var painter = new Painter();
        var commands = painter.PaintBox(box);

        var bgCmd = commands.First(c => c.Type == PaintCommandType.Background);
        var borderCmd = commands.First(c => c.Type == PaintCommandType.Border);

        Assert.Equal(bgCmd.ZIndex, borderCmd.ZIndex);
        Assert.Equal(3, bgCmd.ZIndex);
    }

    // ── Infrastructure ──────────────────────────────────────────────

    private static LayoutBox CreateBlock(string tag, float width, float height,
        string? bg = null, float x = 0, float y = 0)
    {
        return CreateBox(tag, CssDisplay.Block, width, height, bg, x, y);
    }

    private static LayoutBox CreateBox(string tag, CssDisplay display,
        float width, float height, string? bg = null, float x = 0, float y = 0)
    {
        var style = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (bg != null) style["background-color"] = bg;

        var el = new DomElement(tag, null, null, string.Empty, style);
        var box = new LayoutBox(el) { Display = display };
        box.Dimensions.X = x;
        box.Dimensions.Y = y;
        box.Dimensions.Width = width;
        box.Dimensions.Height = height;
        return box;
    }

    private static RenderOutput RenderThrough(LayoutBox root)
    {
        var painter = new Painter();
        var commands = painter.Paint(root);

        var compositor = new Compositor();
        var layers = compositor.BuildLayers(commands);
        var composited = compositor.Composite(layers);

        return new RenderOutput(composited, layers, root.Dimensions.Width, root.Dimensions.Height);
    }
}
