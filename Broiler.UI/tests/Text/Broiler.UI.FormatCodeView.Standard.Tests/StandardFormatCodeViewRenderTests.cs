using Broiler.Graphics;
using Broiler.UI.Standard;

namespace Broiler.UI.FormatCodeView.Standard.Tests;

public sealed class StandardFormatCodeViewRenderTests
{
    [Fact]
    public void Typed_Tokens_Render_With_Distinct_Roles_And_No_Child_Controls()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create("x", new InlineStyle { Bold = true })]);
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            new FormatCodeProjector().Project(document));

        BRenderList list = scene.Session.RenderFrame();
        BRenderCommand.DrawText[] text = list.Commands.OfType<BRenderCommand.DrawText>().ToArray();

        Assert.Contains(text, command => command.Text.Text == "[Bold ON]" && command.Text.Color == scene.View.InlineCodeForeground);
        Assert.Contains(text, command => command.Text.Text == "x" && command.Text.Color == scene.View.Foreground);
        Assert.Empty(scene.View.Children);
    }

    [Fact]
    public void Selection_Caret_And_Clip_Are_Deterministic()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(240, 100),
            FormatCodeViewStandardHarness.Project("hello"));
        scene.View.SetSelection(1, 4);
        scene.Session.SetFocus(scene.View);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(list.Commands.OfType<BRenderCommand.FillRect>(), command => command.Color == scene.View.SelectionBackground);
        Assert.Contains(list.Commands.OfType<BRenderCommand.FillRect>(), command => command.Color == scene.View.CaretColor && command.Rect.Width <= 1);
        list.Validate();
    }

    [Fact]
    public void Wrapping_And_Both_Scrollbars_Follow_View_Policies()
    {
        string text = string.Join('\n', Enumerable.Repeat(new string('x', 100), 20));
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(130, 70),
            FormatCodeViewStandardHarness.Project(text));

        scene.Session.RenderFrame();
        Assert.True(scene.View.VisualLineCount > 20);
        Assert.True(scene.View.HasVerticalScrollbar);
        Assert.False(scene.View.HasHorizontalScrollbar);

        scene.View.Wrapping = FormatCodeViewWrapping.NoWrap;
        scene.Session.RenderFrame();
        Assert.True(scene.View.HasHorizontalScrollbar);
        Assert.True(scene.View.HasVerticalScrollbar);
    }

    [Fact]
    public void Million_Character_Line_Submits_Only_The_Visible_Slice()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(300, 80),
            FormatCodeViewStandardHarness.Project(new string('x', 1_000_000)));
        scene.View.Wrapping = FormatCodeViewWrapping.NoWrap;

        BRenderList list = scene.Session.RenderFrame();
        int submittedCharacters = list.Commands.OfType<BRenderCommand.DrawText>()
            .Sum(command => command.Text.Text.Length);

        Assert.True(submittedCharacters < 100, $"Expected a virtualized slice, submitted {submittedCharacters} characters.");
        Assert.Equal(1, scene.View.VisualLineCount);
    }

    public static IEnumerable<object[]> Themes()
    {
        yield return [StandardThemeTokens.Light];
        yield return [StandardThemeTokens.Dark];
        yield return [StandardThemeTokens.HighContrastLight];
        yield return [StandardThemeTokens.HighContrastDark];
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void Theme_And_Text_Scale_Produce_Valid_Render_Lists(StandardThemeTokens theme)
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 120),
            FormatCodeViewStandardHarness.Project("[literal] text"));
        scene.View.Font = scene.View.Font with { SizeInPixels = 24 };
        scene.View.ApplyTheme(theme);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(list.Commands.OfType<BRenderCommand.FillRoundedRect>(), command => command.Color == theme.SurfaceAlt);
        Assert.Contains(list.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Color == theme.Text || command.Text.Color == theme.Danger);
        list.Validate();
    }

    [Fact]
    public void Factory_Produces_The_Contract_Control()
    {
        var factory = new StandardFormatCodeViewFactory();

        Assert.Equal(typeof(UiFormatCodeView), factory.ContractType);
        Assert.IsType<StandardFormatCodeView>(factory.Create(default));
    }
}
