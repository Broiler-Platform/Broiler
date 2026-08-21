using Broiler.Graphics;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Drawing an inline image: the picture reaches the render list, takes its
/// display width in the line, makes the line tall enough for it, and degrades to
/// an outline when nothing can decode it.
/// </summary>
public sealed class StandardRichEditImageRenderTests
{
    private static readonly byte[] Bytes = [1, 2, 3, 4];

    private static InlineImage Image(double width = 60, double height = 40) =>
        new(Bytes, "image/png", width, height, "a logo");

    private static RichEditScene WithImage(InlineImage image, string before = "", string after = "")
    {
        RichEditScene scene = Create(new BSize(400, 200));
        RichTextParagraph paragraph = RichTextParagraph.Create(before, InlineStyle.Default)
            .InsertText(before.Length, InlineImage.PlaceholderText, InlineStyle.Default with { Image = image })
            .InsertText(before.Length + 1, after, InlineStyle.Default);
        scene.Edit.Document = RichTextDocument.FromParagraphs([paragraph]);
        return scene;
    }

    private static IEnumerable<BRenderCommand.DrawImage> DrawnImages(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.DrawImage>();

    [Fact(Timeout = 600000)]
    public void Draws_An_Inline_Image_At_Its_Display_Size()
    {
        RichEditScene scene = WithImage(Image(60, 40));

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.DrawImage drawn = Assert.Single(DrawnImages(list));
        Assert.Equal(60, drawn.Destination.Width, 3);
        Assert.Equal(40, drawn.Destination.Height, 3);
        Assert.Equal(1, scene.Host.CreatedImages);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Uploads_An_Image_Once_However_Many_Frames_Are_Drawn()
    {
        RichEditScene scene = WithImage(Image());

        scene.Session.RenderFrame();
        scene.Session.RenderFrame();
        scene.Session.RenderFrame();

        Assert.Equal(1, scene.Host.CreatedImages);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void An_Image_Takes_Its_Width_In_The_Line()
    {
        RichEditScene scene = WithImage(Image(60, 40), before: "ab", after: "cd");

        BRenderList list = scene.Session.RenderFrame();
        BRenderCommand.DrawImage drawn = Assert.Single(DrawnImages(list));

        // The text after the picture starts where the picture ends, so the caret
        // and selection metrics agree with what is on screen.
        BRenderCommand.DrawText after = Assert.Single(
            list.Commands.OfType<BRenderCommand.DrawText>().Where(c => c.Text.Text == "cd"));
        Assert.Equal(drawn.Destination.Right, after.Origin.X, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void An_Image_Makes_Its_Line_Tall_Enough_To_Show_It()
    {
        RichEditScene tall = WithImage(Image(60, 120), before: "ab");
        RichEditScene plain = WithImage(Image(60, 10), before: "ab");

        BRenderCommand.DrawImage tallImage = Assert.Single(DrawnImages(tall.Session.RenderFrame()));
        BRenderCommand.DrawImage shortImage = Assert.Single(DrawnImages(plain.Session.RenderFrame()));

        // A picture taller than the text is drawn at full height rather than
        // clipped to the line height the text alone would give.
        Assert.Equal(120, tallImage.Destination.Height, 3);
        Assert.Equal(10, shortImage.Destination.Height, 3);

        // The text beside it moves down with the taller line.
        double tallTextY = TextOrigin(tall, "ab").Y;
        double shortTextY = TextOrigin(plain, "ab").Y;
        Assert.Equal(tallTextY, shortTextY, 3);
        Assert.True(tallImage.Destination.Bottom > shortImage.Destination.Bottom);
        tall.Session.Dispose();
        plain.Session.Dispose();
    }

    private static BPoint TextOrigin(RichEditScene scene, string text) =>
        scene.Session.RenderFrame().Commands
            .OfType<BRenderCommand.DrawText>()
            .First(c => c.Text.Text == text)
            .Origin;

    [Fact(Timeout = 600000)]
    public void Falls_Back_To_An_Outline_When_The_Image_Cannot_Be_Decoded()
    {
        RichEditScene scene = WithImage(Image());
        scene.Host.ImagePixelSize = null;

        BRenderList list = scene.Session.RenderFrame();

        Assert.Empty(DrawnImages(list));
        Assert.Equal(1, scene.Host.CreatedImages);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void An_Image_With_No_Stated_Size_Uses_Its_Decoded_Pixel_Size()
    {
        RichEditScene scene = WithImage(new InlineImage(Bytes, "image/png", 0, 0));

        // The harness host reports every image as 20x10 pixels.
        BRenderCommand.DrawImage drawn = Assert.Single(DrawnImages(scene.Session.RenderFrame()));

        Assert.Equal(20, drawn.Destination.Width, 3);
        Assert.Equal(10, drawn.Destination.Height, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Two_Images_On_One_Line_Are_Both_Drawn()
    {
        RichEditScene scene = Create(new BSize(400, 200));
        RichTextParagraph paragraph = RichTextParagraph.Empty
            .InsertText(0, InlineImage.PlaceholderText, InlineStyle.Default with { Image = Image(30, 20) })
            .InsertText(1, InlineImage.PlaceholderText, InlineStyle.Default with { Image = Image(30, 20) });
        scene.Edit.Document = RichTextDocument.FromParagraphs([paragraph]);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Equal(2, DrawnImages(list).Count());
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Releases_The_Handles_Of_A_Document_That_Was_Replaced()
    {
        RichEditScene scene = WithImage(Image());
        scene.Session.RenderFrame();
        Assert.Equal(1, scene.Host.CreatedImages);

        // Opening a second document must not leave the first one's pictures
        // uploaded for the life of the control.
        scene.Edit.Document = RichTextDocument.FromPlainText("plain");
        scene.Session.RenderFrame();

        Assert.Equal(1, scene.Host.ReleasedImages);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Keeps_An_Image_Uploaded_While_The_Document_Is_Edited_Around_It()
    {
        RichEditScene scene = WithImage(Image(), before: "ab");
        scene.Session.RenderFrame();

        scene.Edit.Selection = RichTextRange.Caret(scene.Edit.Document.Start);
        scene.Edit.ExecuteCommand(RichEditCommand.InsertText, "xy");
        scene.Session.RenderFrame();

        // Every edit produces a new document snapshot; the picture in it is the
        // same object, so it is neither released nor uploaded a second time.
        Assert.Equal(1, scene.Host.CreatedImages);
        Assert.Equal(0, scene.Host.ReleasedImages);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Releases_Its_Image_Handles_When_Detached()
    {
        RichEditScene scene = WithImage(Image());
        scene.Session.RenderFrame();
        Assert.Equal(1, scene.Host.CreatedImages);

        scene.Session.Dispose();

        Assert.Equal(1, scene.Host.ReleasedImages);
    }
}
