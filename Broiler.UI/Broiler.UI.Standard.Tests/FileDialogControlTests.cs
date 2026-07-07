using Broiler.Graphics;
using Broiler.UI.FileDialog.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class FileDialogControlTests
{
    [Fact]
    public void Standard_File_Dialog_Filters_Files_Appends_Default_Extension_And_Navigates_Up()
    {
        using var temp = new TempDirectory();
        string nested = Path.Combine(temp.Path, "docs");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(temp.Path, "alpha.txt"), string.Empty);
        File.WriteAllText(Path.Combine(temp.Path, "Beta.RTF"), string.Empty);

        var dialog = new StandardFileDialog
        {
            CurrentDirectory = temp.Path,
            FileNameFilter = "*.rtf",
            DefaultExtension = "rtf",
        };

        Assert.Contains(dialog.FilesList.Items, item => item.Text == "Beta.RTF");
        Assert.DoesNotContain(dialog.FilesList.Items, item => item.Text == "alpha.txt");

        dialog.FileName = "draft";
        Assert.Equal(Path.Combine(temp.Path, "draft.rtf"), dialog.SelectedPath);

        dialog.CurrentDirectory = nested;
        dialog.UpButton.Click();

        Assert.Equal(temp.Path, dialog.CurrentDirectory);
    }

    [Fact]
    public void Standard_File_Dialog_Renders_Current_Directory_Line()
    {
        using var temp = new TempDirectory();
        var host = new TestHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var dialog = new StandardFileDialog
        {
            CurrentDirectory = temp.Path,
        };

        session.AddRoot(dialog);
        BRenderList renderList = session.RenderFrame();

        renderList.Validate();
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == temp.Path);
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

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "broiler-filedialog-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
