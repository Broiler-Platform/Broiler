using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class RenderingStagesTests
{
    [Fact]
    public void Painter_Paint_SimpleBox_GeneratesCommands()
    {
        var el = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { { "background-color", "red" } });
        var box = new LayoutBox(el) { Display = CssDisplay.Block };
        box.Dimensions.Width = 100;
        box.Dimensions.Height = 50;

        var painter = new Painter();
        var commands = painter.Paint(box);
        Assert.True(commands.Count > 0);
        AssertPaintCommandInvariants(commands);
    }

    [Fact]
    public void Painter_Paint_TextNode_GeneratesTextCommand()
    {
        var el = new DomElement("#text", null, null, string.Empty, isTextNode: true);
        el.TextContent = "Hello";
        var box = new LayoutBox(el) { Display = CssDisplay.Inline };
        box.Dimensions.Width = 50;
        box.Dimensions.Height = 16;

        var painter = new Painter();
        var commands = painter.Paint(box);
        Assert.Contains(commands, c => c.Type == PaintCommandType.Text);
        AssertPaintCommandInvariants(commands);
    }

    [Fact]
    public void Compositor_BuildLayers_GroupsByZIndex()
    {
        var commands = new List<PaintCommand>
        {
            new() { Type = PaintCommandType.Background, ZIndex = 0, Opacity = 1f },
            new() { Type = PaintCommandType.Background, ZIndex = 1, Opacity = 1f },
            new() { Type = PaintCommandType.Text, ZIndex = 0, Opacity = 1f }
        };

        var compositor = new Compositor();
        var layers = compositor.BuildLayers(commands);
        Assert.True(layers.Count >= 2, "Should have at least 2 layers for different z-indices");
    }

    [Fact]
    public void Compositor_Composite_OrdersByZIndex()
    {
        var commands = new List<PaintCommand>
        {
            new() { Type = PaintCommandType.Background, ZIndex = 1, Opacity = 1f },
            new() { Type = PaintCommandType.Text, ZIndex = 0, Opacity = 1f }
        };

        var compositor = new Compositor();
        var layers = compositor.BuildLayers(commands);
        var result = compositor.Composite(layers);
        Assert.True(result.Count == 2);
        Assert.Equal(0, result[0].ZIndex);
    }

    [Fact]
    public void RenderOutput_StoresProperties()
    {
        var commands = new List<PaintCommand>();
        var layers = new List<PaintLayer>();
        var output = new RenderOutput(commands, layers, 800f, 600f);
        Assert.Equal(800f, output.Width);
        Assert.Equal(600f, output.Height);
    }

    [Fact]
    public void PaintCommand_DefaultOpacity_IsOne()
    {
        var cmd = new PaintCommand();
        Assert.Equal(1.0f, cmd.Opacity);
    }

    /// <summary>
    /// Validates basic paint-level invariants for a list of <see cref="PaintCommand"/>s.
    /// Phase 4 integration: ensures no negative bounds, opacity in [0,1], finite values,
    /// and text commands have non-empty text.
    /// </summary>
    private static void AssertPaintCommandInvariants(List<PaintCommand> commands)
    {
        for (int i = 0; i < commands.Count; i++)
        {
            var cmd = commands[i];
            Assert.True(cmd.Bounds.Width >= 0, $"Commands[{i}].Bounds.Width is negative ({cmd.Bounds.Width})");
            Assert.True(cmd.Bounds.Height >= 0, $"Commands[{i}].Bounds.Height is negative ({cmd.Bounds.Height})");
            Assert.True(!float.IsNaN(cmd.Opacity) && !float.IsInfinity(cmd.Opacity),
                $"Commands[{i}].Opacity is not finite ({cmd.Opacity})");
            Assert.True(cmd.Opacity >= 0f && cmd.Opacity <= 1f,
                $"Commands[{i}].Opacity out of range ({cmd.Opacity})");

            if (cmd.Type == PaintCommandType.Text)
            {
                Assert.False(string.IsNullOrEmpty(cmd.Text),
                    $"Commands[{i}]: Text command has empty text");
                Assert.True(cmd.FontSize > 0,
                    $"Commands[{i}]: Text command has non-positive FontSize ({cmd.FontSize})");
            }
        }
    }
}
