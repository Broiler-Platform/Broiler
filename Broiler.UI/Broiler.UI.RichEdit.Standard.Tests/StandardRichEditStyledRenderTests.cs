using Broiler.Graphics;
using Broiler.Input.Text;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

public sealed class StandardRichEditStyledRenderTests
{
    private static RichEditScene WithSelectionAll(string text)
    {
        RichEditScene scene = Create(new BSize(320, 160), text);
        scene.Session.SetFocus(scene.Edit);
        scene.Edit.Selection = new RichTextRange(scene.Edit.Document.Start, scene.Edit.Document.End);
        return scene;
    }

    private static void Collapse(RichEditScene scene) =>
        scene.Edit.Selection = RichTextRange.Caret(scene.Edit.Document.Start);

    private static IEnumerable<BTextRun> DrawnRuns(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.DrawText>().Select(c => c.Text);

    private static IEnumerable<BRenderCommand.FillRect> Fills(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.FillRect>();

    [Fact]
    public void Bold_Run_Is_Drawn_With_A_Bold_Font_Weight()
    {
        RichEditScene scene = WithSelectionAll("hello");
        scene.Edit.ExecuteCommand(RichEditCommand.Bold);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(DrawnRuns(list), r => r.Text == "hello" && r.Font.Weight == BFontWeight.Bold);
        scene.Session.Dispose();
    }

    [Fact]
    public void Italic_Run_Is_Drawn_With_An_Italic_Slant()
    {
        RichEditScene scene = WithSelectionAll("hello");
        scene.Edit.ExecuteCommand(RichEditCommand.Italic);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(DrawnRuns(list), r => r.Text == "hello" && r.Font.Slant == BFontSlant.Italic);
        scene.Session.Dispose();
    }

    [Fact]
    public void Foreground_Command_Colors_The_Run_Text()
    {
        RichEditScene scene = WithSelectionAll("hello");
        scene.Edit.ExecuteCommand(RichEditCommand.SetForeground, BColor.Red);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(DrawnRuns(list), r => r.Text == "hello" && r.Color == BColor.Red);
        scene.Session.Dispose();
    }

    [Fact]
    public void Underline_Run_Draws_A_Thin_Rule_In_The_Text_Color()
    {
        RichEditScene scene = WithSelectionAll("hello");
        scene.Edit.ExecuteCommand(RichEditCommand.SetForeground, BColor.Red);
        scene.Edit.ExecuteCommand(RichEditCommand.Underline);
        Collapse(scene);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(Fills(list), c => c.Color == BColor.Red && c.Rect.Width > 2 && c.Rect.Height <= 3);
        scene.Session.Dispose();
    }

    [Fact]
    public void Strikethrough_Run_Draws_A_Thin_Rule()
    {
        RichEditScene scene = WithSelectionAll("hello");
        scene.Edit.ExecuteCommand(RichEditCommand.SetForeground, BColor.Green);
        scene.Edit.ExecuteCommand(RichEditCommand.Strikethrough);
        Collapse(scene);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(Fills(list), c => c.Color == BColor.Green && c.Rect.Width > 2 && c.Rect.Height <= 3);
        scene.Session.Dispose();
    }

    [Fact]
    public void Background_Command_Fills_A_Highlight_Behind_The_Run()
    {
        BColor highlight = BColor.FromArgb(0xFF, 0xFF, 0xF0, 0x88);
        RichEditScene scene = WithSelectionAll("hello");
        scene.Edit.ExecuteCommand(RichEditCommand.SetBackground, highlight);
        Collapse(scene);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(Fills(list), c => c.Color == highlight && c.Rect.Width > 2);
        scene.Session.Dispose();
    }

    [Fact]
    public void Mixed_Styles_Split_A_Line_Into_Multiple_Draw_Runs()
    {
        RichEditScene scene = Create(new BSize(320, 160), "abcd");
        scene.Session.SetFocus(scene.Edit);
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 0), new RichTextPosition(0, 2));
        scene.Edit.ExecuteCommand(RichEditCommand.Bold);
        scene.Edit.Selection = RichTextRange.Caret(scene.Edit.Document.Start);

        BRenderList list = scene.Session.RenderFrame();
        BTextRun[] runs = DrawnRuns(list).ToArray();

        Assert.Contains(runs, r => r.Text == "ab" && r.Font.Weight == BFontWeight.Bold);
        Assert.Contains(runs, r => r.Text == "cd" && r.Font.Weight != BFontWeight.Bold);
        scene.Session.Dispose();
    }

    [Fact]
    public void Composition_Preview_Text_Is_Drawn_At_The_Caret()
    {
        RichEditScene scene = Create(new BSize(320, 160), "hi");
        scene.Session.SetFocus(scene.Edit);
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 2));

        scene.Route.Dispatch(Composition("ni", TextCompositionState.Updated));
        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(DrawnRuns(list), r => r.Text == "ni");
        scene.Session.Dispose();
    }

    [Fact]
    public void Plain_Text_Still_Draws_One_Run_Per_Line()
    {
        RichEditScene scene = Create(new BSize(320, 160), "one\ntwo");

        BRenderList list = scene.Session.RenderFrame();

        string[] texts = DrawnRuns(list).Select(r => r.Text).ToArray();
        Assert.Equal(new[] { "one", "two" }, texts);
        scene.Session.Dispose();
    }
}
