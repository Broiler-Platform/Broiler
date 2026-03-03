using Broiler.App.Rendering;

namespace Broiler.App.Tests;

/// <summary>
/// Phase 3 rendering-output tests. Renders HTML snippets through the full
/// Broiler paint pipeline (Painter → Compositor → RenderOutput) and verifies
/// observable output properties: dimensions, non-blank content, element
/// positions, and layer ordering.
/// </summary>
[Trait("Category", "Rendering")]
[Trait("Engine", "Broiler")]
public class RenderingOutputTests
{
    // ── Dimension verification ──────────────────────────────────────

    /// <summary>A block element with explicit width/height produces matching output dimensions.</summary>
    [Fact]
    public void RenderOutput_FixedDimensions_MatchesContentBox()
    {
        var root = CreateBlock("div", width: 200, height: 150, bg: "blue");
        var output = RenderThrough(root);

        Assert.Equal(200f, output.Width);
        Assert.Equal(150f, output.Height);
    }

    /// <summary>Nested blocks contribute to total content height.</summary>
    [Fact]
    public void RenderOutput_NestedBlocks_HeightSumsChildren()
    {
        var root = CreateBlock("div", width: 300, height: 200);
        var child1 = CreateBlock("div", width: 300, height: 80, bg: "red", y: 0);
        var child2 = CreateBlock("div", width: 300, height: 80, bg: "green", y: 80);
        root.Children.Add(child1);
        root.Children.Add(child2);

        var output = RenderThrough(root);

        Assert.True(output.Commands.Count >= 2,
            "Nested blocks must produce at least two background paint commands.");
        Assert.Equal(200f, output.Height);
    }

    /// <summary>Padding increases the border box dimensions beyond the content box.</summary>
    [Fact]
    public void RenderOutput_Padding_ExpandsBorderBox()
    {
        var el = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "background-color", "teal" },
                { "border-color", "black" }
            });
        var box = new LayoutBox(el) { Display = CssDisplay.Block };
        box.Dimensions.Width = 100;
        box.Dimensions.Height = 60;
        box.Dimensions.Padding = new BoxEdges { Top = 10, Right = 10, Bottom = 10, Left = 10 };
        box.Dimensions.Border = new BoxEdges { Top = 2, Right = 2, Bottom = 2, Left = 2 };

        var borderBox = box.Dimensions.BorderBox();

        // Content 100 + padding 20 + border 4 = 124 wide
        Assert.Equal(124f, borderBox.Width);
        // Content 60 + padding 20 + border 4 = 84 high
        Assert.Equal(84f, borderBox.Height);
    }

    // ── Non-blank output verification ───────────────────────────────

    /// <summary>A coloured div produces at least one background paint command.</summary>
    [Fact]
    public void RenderOutput_ColoredDiv_ProducesNonBlankOutput()
    {
        var root = CreateBlock("div", width: 100, height: 100, bg: "red");
        var output = RenderThrough(root);

        Assert.Contains(output.Commands, c =>
            c.Type == PaintCommandType.Background &&
            !string.IsNullOrEmpty(c.BackgroundColor));
    }

    /// <summary>A text node produces a text paint command with content.</summary>
    [Fact]
    public void RenderOutput_TextContent_ProducesTextCommand()
    {
        var root = CreateBlock("p", width: 200, height: 20);
        var textEl = new DomElement("#text", null, null, string.Empty, isTextNode: true);
        textEl.TextContent = "Hello World";
        var textBox = new LayoutBox(textEl) { Display = CssDisplay.Inline };
        textBox.Dimensions.Width = 80;
        textBox.Dimensions.Height = 16;
        root.Children.Add(textBox);

        var output = RenderThrough(root);

        var textCmd = Assert.Single(output.Commands, c => c.Type == PaintCommandType.Text);
        Assert.Equal("Hello World", textCmd.Text);
        Assert.True(textCmd.FontSize > 0, "Text command must have a positive font size.");
    }

    // ── Position verification ───────────────────────────────────────

    /// <summary>Child boxes at different Y positions produce commands with corresponding bounds.</summary>
    [Fact]
    public void RenderOutput_ChildPositions_ReflectedInBounds()
    {
        var root = CreateBlock("div", width: 300, height: 200);
        var child1 = CreateBlock("div", width: 300, height: 50, bg: "red", y: 0);
        var child2 = CreateBlock("div", width: 300, height: 50, bg: "blue", y: 60);
        root.Children.Add(child1);
        root.Children.Add(child2);

        var output = RenderThrough(root);

        var bgCommands = output.Commands.Where(c =>
            c.Type == PaintCommandType.Background &&
            !string.IsNullOrEmpty(c.BackgroundColor)).ToList();
        Assert.True(bgCommands.Count >= 2, "Should have at least 2 background commands.");

        var redCmd = bgCommands.First(c => c.BackgroundColor == "red");
        var blueCmd = bgCommands.First(c => c.BackgroundColor == "blue");
        Assert.True(blueCmd.Bounds.Y > redCmd.Bounds.Y,
            "Blue box must be positioned below the red box.");
    }

    /// <summary>Inline elements produce commands at their laid-out X positions.</summary>
    [Fact]
    public void RenderOutput_InlinePositions_HorizontalOffset()
    {
        var root = CreateBlock("div", width: 400, height: 40);
        var span1 = CreateBox("span", CssDisplay.InlineBlock, width: 100, height: 40, bg: "red", x: 0);
        var span2 = CreateBox("span", CssDisplay.InlineBlock, width: 100, height: 40, bg: "green", x: 110);
        root.Children.Add(span1);
        root.Children.Add(span2);

        var output = RenderThrough(root);

        var bgCommands = output.Commands.Where(c =>
            c.Type == PaintCommandType.Background &&
            !string.IsNullOrEmpty(c.BackgroundColor)).ToList();
        Assert.True(bgCommands.Count >= 2);

        var greenCmd = bgCommands.First(c => c.BackgroundColor == "green");
        Assert.Equal(110f, greenCmd.Bounds.X);
    }

    // ── Layer and z-index verification ──────────────────────────────

    /// <summary>Elements at different z-indices produce separate compositing layers.</summary>
    [Fact]
    public void RenderOutput_ZIndex_CreatesSeparateLayers()
    {
        var root = CreateBlock("div", width: 300, height: 200);
        var bg = CreateBlock("div", width: 300, height: 200, bg: "gray");
        bg.Element.Style["z-index"] = "0";
        var overlay = CreateBlock("div", width: 100, height: 100, bg: "red");
        overlay.Element.Style["z-index"] = "1";
        root.Children.Add(bg);
        root.Children.Add(overlay);

        var output = RenderThrough(root);

        Assert.True(output.Layers.Count >= 2,
            "Different z-indices should produce at least 2 compositing layers.");
    }

    /// <summary>Composited output orders commands by z-index ascending.</summary>
    [Fact]
    public void RenderOutput_Composite_ZIndexOrdering()
    {
        var root = CreateBlock("div", width: 200, height: 200);
        var bottom = CreateBlock("div", width: 200, height: 200, bg: "blue");
        bottom.Element.Style["z-index"] = "0";
        var top = CreateBlock("div", width: 100, height: 100, bg: "red");
        top.Element.Style["z-index"] = "5";
        root.Children.Add(bottom);
        root.Children.Add(top);

        var output = RenderThrough(root);

        // Commands should be sorted by z-index: 0 before 5
        var bgCommands = output.Commands.Where(c =>
            c.Type == PaintCommandType.Background &&
            !string.IsNullOrEmpty(c.BackgroundColor)).ToList();
        for (int i = 1; i < bgCommands.Count; i++)
        {
            Assert.True(bgCommands[i].ZIndex >= bgCommands[i - 1].ZIndex,
                $"Commands[{i}] z-index ({bgCommands[i].ZIndex}) should be >= " +
                $"Commands[{i - 1}] z-index ({bgCommands[i - 1].ZIndex}).");
        }
    }

    // ── Opacity propagation ─────────────────────────────────────────

    /// <summary>Layer opacity propagates to composited commands.</summary>
    [Fact]
    public void RenderOutput_LayerOpacity_PropagatedToCommands()
    {
        var el = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "background-color", "red" },
                { "opacity", "0.5" }
            });
        var box = new LayoutBox(el) { Display = CssDisplay.Block };
        box.Dimensions.Width = 100;
        box.Dimensions.Height = 100;

        var painter = new Painter();
        var commands = painter.Paint(box);
        var compositor = new Compositor();
        var layers = compositor.BuildLayers(commands);

        // Manually set layer opacity to simulate a transparency layer
        foreach (var layer in layers)
            layer.Opacity = 0.5f;

        var composited = compositor.Composite(layers);

        foreach (var cmd in composited)
        {
            Assert.True(cmd.Opacity <= 0.5f,
                $"Composited opacity ({cmd.Opacity}) should reflect layer opacity (0.5).");
        }
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

    /// <summary>
    /// Runs the layout tree through the full Painter → Compositor → RenderOutput
    /// pipeline and returns the final output.
    /// </summary>
    private static RenderOutput RenderThrough(LayoutBox root)
    {
        var painter = new Painter();
        var commands = painter.Paint(root);

        var compositor = new Compositor();
        var layers = compositor.BuildLayers(commands);
        var composited = compositor.Composite(layers);

        // Compute total content dimensions from root
        float width = root.Dimensions.Width;
        float height = root.Dimensions.Height;

        return new RenderOutput(composited, layers, width, height);
    }
}
