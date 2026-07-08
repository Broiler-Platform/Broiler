using Broiler.Graphics;
using Broiler.UI.FontDialog;
using Broiler.UI.FontDialog.Standard;
using Broiler.UI.Standard;
using Broiler.UI.ToggleButton;

namespace Broiler.UI.Standard.Tests;

public sealed class FontDialogControlTests
{
    [Fact]
    public void Standard_Font_Dialog_Updates_Selected_Font_From_Controls()
    {
        var dialog = new StandardFontDialog();
        dialog.SetFontFamilies(["Alpha Sans", "Beta Serif"]);

        dialog.FamilyList.SelectItem("Beta Serif");
        dialog.SizeEdit.Text = "18";
        dialog.WeightButton.Click();
        dialog.ItalicToggle.ToggleState = UiToggleState.On;

        Assert.Equal("Beta Serif", dialog.SelectedFont.FamilyName);
        Assert.Equal(18, dialog.SelectedFont.SizeInPixels);
        Assert.Equal(BFontWeight.Medium, dialog.SelectedFont.Weight);
        Assert.Equal(BFontSlant.Italic, dialog.SelectedFont.Slant);
    }

    [Fact]
    public void Standard_Font_Dialog_Renders_Selected_Font_Preview()
    {
        var host = new TestHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var dialog = new StandardFontDialog
        {
            SelectedFont = new BFontStyle("Consolas", 22, BFontWeight.Bold, BFontSlant.Italic),
            SampleText = "Preview text",
        };

        session.AddRoot(dialog);
        BRenderList renderList = session.RenderFrame();

        renderList.Validate();
        Assert.Contains(
            renderList.Commands.OfType<BRenderCommand.DrawText>(),
            command =>
                command.Text.Text == "Preview text" &&
                command.Text.Font.FamilyName == "Consolas" &&
                command.Text.Font.SizeInPixels == 22 &&
                command.Text.Font.Weight == BFontWeight.Bold &&
                command.Text.Font.Slant == BFontSlant.Italic);
    }

    [Fact]
    public void Standard_Font_Dialog_Adds_Selected_Custom_Family_To_List()
    {
        var dialog = new StandardFontDialog();
        dialog.SetFontFamilies(["Alpha Sans"]);

        dialog.SelectedFont = new BFontStyle("Custom Face", 16);

        Assert.Contains(dialog.FamilyList.Items, item => item.Text == "Custom Face");
        Assert.Equal("Custom Face", dialog.FamilyList.SelectedItemId);
    }

    [Fact]
    public void Font_Dialog_Result_Value_Round_Trips_Fonts()
    {
        var font = new BFontStyle("Family|With\\Escapes", 13.5, BFontWeight.SemiBold, BFontSlant.Oblique);

        string value = UiFontDialog.FormatFontValue(font);
        bool parsed = UiFontDialog.TryParseFontValue(value, out BFontStyle result);

        Assert.True(parsed);
        Assert.Equal(font, result);
    }

    private sealed class TestHost : IUiHost
    {
        public BSize ViewportSize { get; } = new(640, 360);

        public double Scale => 1.0;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }
}
