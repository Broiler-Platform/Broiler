using Broiler.Graphics;
using Broiler.UI.Standard;

namespace Broiler.UI.RichEdit.Standard.Tests;

public sealed class StandardRichEditRenderTests
{
    [Fact]
    public void Multi_Paragraph_Text_Draws_One_Line_Per_Paragraph()
    {
        RichEditScene scene = RichEditStandardHarness.Create(new BSize(300, 200), "one\ntwo\nthree");

        BRenderList list = scene.Session.RenderFrame();

        string[] texts = list.Commands.OfType<BRenderCommand.DrawText>().Select(c => c.Text.Text).ToArray();
        Assert.Equal(new[] { "one", "two", "three" }, texts);
        scene.Session.Dispose();
    }

    [Fact]
    public void Long_Line_Wraps_Into_Multiple_Visual_Lines()
    {
        RichEditScene scene = RichEditStandardHarness.Create(new BSize(120, 200), "the quick brown fox jumps over the lazy dog again");

        BRenderList list = scene.Session.RenderFrame();

        int lineCount = list.Commands.OfType<BRenderCommand.DrawText>().Count();
        Assert.True(lineCount > 1, $"expected wrapping into multiple lines, got {lineCount}");
        scene.Session.Dispose();
    }

    [Fact]
    public void Selection_Draws_Highlight_Rectangles()
    {
        RichEditScene scene = RichEditStandardHarness.Create(new BSize(300, 120), "hello world");
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 0), new RichTextPosition(0, 5));
        scene.Session.SetFocus(scene.Edit);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(list.Commands.OfType<BRenderCommand.FillRect>(), c => c.Color == scene.Edit.SelectionBackground);
        scene.Session.Dispose();
    }

    [Fact]
    public void Focused_Edit_Draws_A_Caret()
    {
        RichEditScene scene = RichEditStandardHarness.Create(new BSize(300, 120), "hi");
        scene.Session.SetFocus(scene.Edit);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(list.Commands.OfType<BRenderCommand.FillRect>(), c => c.Color == scene.Edit.CaretColor && c.Rect.Width <= 2);
        scene.Session.Dispose();
    }

    [Fact]
    public void Focus_Draws_A_Ring_And_Unfocused_Draws_A_Border()
    {
        RichEditScene scene = RichEditStandardHarness.Create(new BSize(300, 120), "hi");

        BRenderList unfocused = scene.Session.RenderFrame();
        Assert.Contains(scene.Edit.BorderColor, StrokeColors(unfocused));

        scene.Session.SetFocus(scene.Edit);
        BRenderList focused = scene.Session.RenderFrame();
        Assert.Contains(scene.Edit.FocusRing, StrokeColors(focused));
        scene.Session.Dispose();
    }

    [Fact]
    public void Disabled_Edit_Uses_Disabled_Surface_And_Draws_No_Caret()
    {
        RichEditScene scene = RichEditStandardHarness.Create(new BSize(300, 120), "hi");
        scene.Edit.IsEnabled = false;
        scene.Session.SetFocus(scene.Edit);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(StandardControlPaint.SurfaceDisabled, FillColors(list));
        Assert.DoesNotContain(list.Commands.OfType<BRenderCommand.FillRect>(), c => c.Color == scene.Edit.CaretColor && c.Rect.Width <= 2);
        scene.Session.Dispose();
    }

    [Fact]
    public void Empty_Edit_Draws_Placeholder()
    {
        RichEditScene scene = RichEditStandardHarness.Create(new BSize(300, 120));
        scene.Edit.PlaceholderText = "Type here";

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(list.Commands.OfType<BRenderCommand.DrawText>(), c => c.Text.Text == "Type here");
        scene.Session.Dispose();
    }

    [Fact]
    public void Render_List_Balances_Clip_Stack()
    {
        RichEditScene scene = RichEditStandardHarness.Create(new BSize(300, 120), "clip test");
        scene.Session.SetFocus(scene.Edit);

        BRenderList list = scene.Session.RenderFrame();

        list.Validate(); // throws if PushClip/PopClip are unbalanced
        scene.Session.Dispose();
    }

    private static IEnumerable<BColor> StrokeColors(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.StrokeRoundedRect>().Select(c => c.Color)
            .Concat(list.Commands.OfType<BRenderCommand.StrokeRect>().Select(c => c.Color));

    private static IEnumerable<BColor> FillColors(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.FillRoundedRect>().Select(c => c.Color)
            .Concat(list.Commands.OfType<BRenderCommand.FillRect>().Select(c => c.Color));
}
