using Broiler.Graphics;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Capitalization is a display property: the control draws capitals while the
/// document keeps the casing the author typed. These tests pin both halves of
/// that — what reaches the render list, and what the model still holds.
/// </summary>
public sealed class StandardRichEditCapitalizationTests
{
    private static RichEditScene WithSelectionAll(string text)
    {
        RichEditScene scene = Create(new BSize(640, 200), text);
        scene.Session.SetFocus(scene.Edit);
        scene.Edit.Selection = new RichTextRange(scene.Edit.Document.Start, scene.Edit.Document.End);
        return scene;
    }

    private static IEnumerable<BTextRun> DrawnRuns(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.DrawText>().Select(c => c.Text);

    [Fact(Timeout = 600000)]
    public void All_Caps_Draws_Capitals()
    {
        RichEditScene scene = WithSelectionAll("Elene Bartky");
        scene.Edit.ExecuteCommand(RichEditCommand.AllCaps);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(DrawnRuns(list), run => run.Text == "ELENE BARTKY");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void All_Caps_Leaves_The_Stored_Text_Untouched()
    {
        RichEditScene scene = WithSelectionAll("Elene Bartky");
        scene.Edit.ExecuteCommand(RichEditCommand.AllCaps);
        scene.Session.RenderFrame();

        Assert.Equal("Elene Bartky", scene.Edit.Document.PlainText);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Turning_All_Caps_Off_Restores_The_Original_Casing_On_Screen()
    {
        RichEditScene scene = WithSelectionAll("Elene Bartky");
        scene.Edit.ExecuteCommand(RichEditCommand.AllCaps);
        scene.Session.RenderFrame();
        scene.Edit.ExecuteCommand(RichEditCommand.AllCaps);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(DrawnRuns(list), run => run.Text == "Elene Bartky");
        scene.Session.Dispose();
    }

    /// <summary>
    /// Small caps splits the run where the stored case changes: capitals keep the
    /// run's size, letters typed in lower case are drawn as capitals at a reduced
    /// size.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Small_Caps_Draws_Lowercase_Letters_As_Smaller_Capitals()
    {
        RichEditScene scene = WithSelectionAll("Ab");
        scene.Edit.ExecuteCommand(RichEditCommand.SmallCaps);

        BRenderList list = scene.Session.RenderFrame();
        BTextRun[] runs = DrawnRuns(list).Where(run => run.Text is "A" or "B").ToArray();

        BTextRun full = Assert.Single(runs, run => run.Text == "A");
        BTextRun reduced = Assert.Single(runs, run => run.Text == "B");
        Assert.True(
            reduced.Font.SizeInPixels < full.Font.SizeInPixels,
            "the letter typed in lower case should be drawn smaller");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Small_Caps_Leaves_The_Stored_Text_Untouched()
    {
        RichEditScene scene = WithSelectionAll("Ab");
        scene.Edit.ExecuteCommand(RichEditCommand.SmallCaps);
        scene.Session.RenderFrame();

        Assert.Equal("Ab", scene.Edit.Document.PlainText);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Two_Capitalization_Kinds_Replace_Each_Other()
    {
        RichEditScene scene = WithSelectionAll("Ab");
        scene.Edit.ExecuteCommand(RichEditCommand.AllCaps);
        scene.Edit.ExecuteCommand(RichEditCommand.SmallCaps);

        Assert.False(scene.Edit.GetCommandState(RichEditCommand.AllCaps).IsToggled);
        Assert.True(scene.Edit.GetCommandState(RichEditCommand.SmallCaps).IsToggled);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Command_State_Reports_The_Active_Capitalization()
    {
        RichEditScene scene = WithSelectionAll("hello");

        Assert.False(scene.Edit.GetCommandState(RichEditCommand.AllCaps).IsToggled);
        scene.Edit.ExecuteCommand(RichEditCommand.AllCaps);
        Assert.True(scene.Edit.GetCommandState(RichEditCommand.AllCaps).IsToggled);
        scene.Edit.ExecuteCommand(RichEditCommand.AllCaps);
        Assert.False(scene.Edit.GetCommandState(RichEditCommand.AllCaps).IsToggled);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Clearing_Formatting_Removes_Capitalization()
    {
        RichEditScene scene = WithSelectionAll("Elene Bartky");
        scene.Edit.ExecuteCommand(RichEditCommand.AllCaps);
        scene.Edit.ExecuteCommand(RichEditCommand.ClearFormatting);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(DrawnRuns(list), run => run.Text == "Elene Bartky");
        scene.Session.Dispose();
    }

    /// <summary>
    /// Caret placement and drawing must go through the same measurement, or the
    /// caret drifts away from the glyphs. Capitals are not reliably wider than
    /// their lowercase forms, so the assertion is agreement, not direction: the
    /// caret at the end of the text lands exactly where the drawn glyphs end.
    /// </summary>
    [Theory]
    [InlineData(RichEditCommand.AllCaps, "Elene Bartky")]
    [InlineData(RichEditCommand.SmallCaps, "Elene Bartky")]
    public void Caret_Lands_Where_The_Drawn_Glyphs_End(RichEditCommand command, string text)
    {
        RichEditScene scene = WithSelectionAll(text);
        scene.Edit.ExecuteCommand(command);
        scene.Edit.Selection = RichTextRange.Caret(scene.Edit.Document.End);
        scene.Session.RenderFrame();

        BRenderList list = scene.Session.RenderFrame();
        BRenderCommand.DrawText last = list.Commands.OfType<BRenderCommand.DrawText>().Last();
        double drawnEnd = last.Origin.X + BTextMeasurer.MeasureAdvance(last.Text.Text, last.Text.Font);

        Assert.Equal(drawnEnd, CaretLeft(list), precision: 3);
        scene.Session.Dispose();
    }

    /// <summary>Small caps must not change how much room the text takes when nothing is capitalized.</summary>
    [Fact(Timeout = 600000)]
    public void Small_Caps_Of_An_All_Lowercase_Run_Is_Narrower_Than_All_Caps()
    {
        double small = TextWidth("hello", RichEditCommand.SmallCaps);
        double full = TextWidth("hello", RichEditCommand.AllCaps);

        Assert.True(small < full, $"small caps ({small}) should be narrower than all caps ({full})");
    }

    private static double TextWidth(string text, RichEditCommand command)
    {
        RichEditScene scene = WithSelectionAll(text);
        scene.Edit.ExecuteCommand(command);
        scene.Edit.Selection = RichTextRange.Caret(scene.Edit.Document.End);
        scene.Session.RenderFrame();

        BRenderList list = scene.Session.RenderFrame();
        double width = list.Commands
            .OfType<BRenderCommand.DrawText>()
            .Sum(drawn => BTextMeasurer.MeasureAdvance(drawn.Text.Text, drawn.Text.Font));

        scene.Session.Dispose();
        return width;
    }

    /// <summary>The caret is the thin filled rectangle furthest along the line.</summary>
    private static double CaretLeft(BRenderList list) =>
        list.Commands
            .OfType<BRenderCommand.FillRect>()
            .Where(fill => fill.Rect.Width <= 2)
            .Select(fill => fill.Rect.Left)
            .Max();
}
