using Broiler.Graphics;
using Broiler.UI.FileDialog;
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

    [Fact]
    public void Standard_File_Dialog_Cycles_Named_File_Type_Filters()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "draft.rtf"), string.Empty);
        File.WriteAllText(Path.Combine(temp.Path, "page.html"), string.Empty);
        File.WriteAllText(Path.Combine(temp.Path, "notes.md"), string.Empty);

        var dialog = new StandardFileDialog
        {
            CurrentDirectory = temp.Path,
        };
        dialog.SetFileTypeFilters(
            [
                new UiFileDialogFilter("All documents", "*.rtf;*.html;*.md", ".rtf"),
                new UiFileDialogFilter("Markdown", "*.md", ".md"),
            ]);

        Assert.Contains(dialog.FilesList.Items, item => item.Text == "draft.rtf");
        Assert.Contains(dialog.FilesList.Items, item => item.Text == "page.html");
        Assert.Contains(dialog.FilesList.Items, item => item.Text == "notes.md");
        Assert.Equal("Format: All documents", dialog.FormatButton.Text);

        dialog.FileName = "new";
        Assert.Equal(Path.Combine(temp.Path, "new.rtf"), dialog.SelectedPath);

        dialog.FormatButton.Click();

        Assert.DoesNotContain(dialog.FilesList.Items, item => item.Text == "draft.rtf");
        Assert.DoesNotContain(dialog.FilesList.Items, item => item.Text == "page.html");
        Assert.Contains(dialog.FilesList.Items, item => item.Text == "notes.md");
        Assert.Equal("Format: Markdown", dialog.FormatButton.Text);
        Assert.Equal(Path.Combine(temp.Path, "new.md"), dialog.SelectedPath);
    }

    [Fact]
    public void Standard_File_Dialog_Shows_Places_And_Descriptive_Chrome()
    {
        var host = new TestHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var dialog = new StandardFileDialog();

        Assert.NotEmpty(dialog.PlacesList.Items);

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home) && Directory.Exists(home))
            Assert.Contains(dialog.PlacesList.Items, item => item.Text == "Home");

        session.AddRoot(dialog);
        BRenderList renderList = session.RenderFrame();

        renderList.Validate();
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "Places");
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text.StartsWith("Choose a document", StringComparison.Ordinal));
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "File name");
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
