using Broiler.Code.Core.Shell;
using Broiler.Code.Workspaces;
using Broiler.Code.Workspaces.Model;
using Broiler.Code.Workspaces.Storage;
using Broiler.Graphics;
using Broiler.UI;
using Broiler.UI.Button;
using Broiler.UI.Button.Standard;
using Broiler.UI.CodeEditor;
using Broiler.UI.CodeEditor.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.Menu;
using Broiler.UI.Menu.Standard;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Splitter.Standard;
using Broiler.UI.TabView.Standard;
using Broiler.UI.Toolbar.Standard;
using Broiler.UI.TreeView.Standard;

namespace Broiler.Code.Core.Tests;

/// <summary>
/// The menu and toolbar as the user sees them, and New Project.
/// </summary>
public sealed class ShellChromeTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "broiler-chrome", Guid.NewGuid().ToString("n"));

    public ShellChromeTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Grant(string name)
    {
        string directory = Path.Combine(_root, name);
        Directory.CreateDirectory(directory);
        return directory;
    }

    [Fact(Timeout = 600000)]
    public void No_Menu_Text_Carries_A_Mnemonic_Ampersand()
    {
        using Fixture fixture = Fixture.Create();

        // UiMenu draws Text verbatim, so an ampersand meant as a mnemonic is
        // drawn as an ampersand. It belongs in AccessKey, which is how Writer
        // has always done it and how this shell did not.
        foreach (UiMenuItem item in AllItems(fixture.Controls.Menu.Items))
            Assert.DoesNotContain("&", item.Text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void The_Top_Level_Menus_Still_Have_Their_Access_Keys()
    {
        using Fixture fixture = Fixture.Create();

        UiMenuItem file = fixture.Controls.Menu.Items
            .First(item => item.Text == "File");
        UiMenuItem build = fixture.Controls.Menu.Items
            .First(item => item.Text == "Build");

        Assert.Equal('F', file.AccessKey);
        Assert.Equal('B', build.AccessKey);
        Assert.Equal('S', file.Children.First(c => c.CommandName == CodeCommandNames.Save).AccessKey);
    }

    [Fact(Timeout = 600000)]
    public void The_Toolbar_Carries_Buttons_Bound_To_Commands()
    {
        using Fixture fixture = Fixture.Create();

        UiButton[] buttons = [.. fixture.Controls.Toolbar.Children.OfType<UiButton>()];

        Assert.NotEmpty(buttons);
        Assert.Contains(buttons, button => button.CommandName == CodeCommandNames.New);
        Assert.Contains(buttons, button => button.CommandName == CodeCommandNames.Save);
        Assert.Contains(buttons, button => button.CommandName == CodeCommandNames.Build);

        // Labelled and wide enough to read, not left at a default size.
        Assert.All(buttons, button =>
        {
            Assert.NotEmpty(button.Text);
            Assert.True(button.PreferredSize.Width > button.Text.Length * 6);
        });
    }

    [Fact(Timeout = 600000)]
    public async Task A_Toolbar_Button_Runs_The_Same_Command_The_Menu_Does()
    {
        using Fixture fixture = Fixture.Create();
        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        UiButton newButton = fixture.Controls.Toolbar.Children
            .OfType<UiButton>()
            .First(button => button.CommandName == CodeCommandNames.New);

        newButton.Click();

        // Clicked is synchronous but the command is not, so wait for the tab
        // rather than assuming it is already there.
        await WaitFor(() => fixture.Controls.Tabs.Tabs.Count == 2);
        Assert.Equal(2, fixture.Controls.Tabs.Tabs.Count);
    }

    [Fact(Timeout = 600000)]
    public async Task A_Toolbar_Button_Reflects_Its_Commands_Availability()
    {
        using Fixture fixture = Fixture.Create();

        UiButton build = fixture.Controls.Toolbar.Children
            .OfType<UiButton>()
            .First(button => button.CommandName == CodeCommandNames.Build);
        UiButton save = fixture.Controls.Toolbar.Children
            .OfType<UiButton>()
            .First(button => button.CommandName == CodeCommandNames.Save);

        // No build service, and no document yet.
        Assert.False(build.IsEnabled);
        Assert.False(save.IsEnabled);

        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        Assert.True(save.IsEnabled);
        Assert.False(build.IsEnabled);
    }

    [Fact(Timeout = 600000)]
    public async Task New_Project_Writes_A_Standard_Solution_And_Opens_It()
    {
        string target = Grant("projects");
        using Fixture fixture = Fixture.Create();
        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        // The save dialog answers both halves at once: where, and what to call it.
        fixture.Dialogs.SaveTo = Path.Combine(target, "Demo.slnx");
        Assert.True(await fixture.Shell.InvokeAsync(CodeCommandNames.NewProject));

        Assert.True(File.Exists(Path.Combine(target, "Demo.slnx")));
        Assert.True(File.Exists(Path.Combine(target, "src", "Demo", "Demo.csproj")));
        Assert.True(File.Exists(Path.Combine(target, "src", "Demo", "Program.cs")));

        // Nothing Broiler-specific: the result is a solution any tool can open.
        string solution = await File.ReadAllTextAsync(Path.Combine(target, "Demo.slnx"));
        string project = await File.ReadAllTextAsync(Path.Combine(target, "src", "Demo", "Demo.csproj"));
        Assert.DoesNotContain("Broiler", solution, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Broiler", project, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Microsoft.NET.Sdk", project, StringComparison.Ordinal);

        // And it is the open workspace now, with its source editable.
        CodeWorkspace workspace = fixture.Shell.Workspace!;
        Assert.Single(workspace.Projects);
        Assert.Equal("Demo", workspace.Projects[0].Name);
        Assert.False(fixture.Editor.IsReadOnly);
    }

    [Fact(Timeout = 600000)]
    public async Task New_Project_Refuses_A_Name_That_Is_Not_An_Identifier()
    {
        string target = Grant("projects");
        using Fixture fixture = Fixture.Create();
        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        fixture.Dialogs.SaveTo = Path.Combine(target, "not a name.slnx");
        Assert.False(await fixture.Shell.InvokeAsync(CodeCommandNames.NewProject));

        // Nothing half-written left behind.
        Assert.Empty(Directory.GetFileSystemEntries(target));
    }

    [Fact(Timeout = 600000)]
    public async Task New_Project_Does_Not_Overwrite_An_Existing_One()
    {
        string target = Grant("projects");
        await File.WriteAllTextAsync(Path.Combine(target, "Demo.slnx"), "<Solution />\n");

        using Fixture fixture = Fixture.Create();
        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        fixture.Dialogs.SaveTo = Path.Combine(target, "Demo.slnx");
        Assert.False(await fixture.Shell.InvokeAsync(CodeCommandNames.NewProject));

        // The dialog's overwrite prompt is about one file. A template that
        // clobbered a solution would take the whole tree with it.
        Assert.Equal("<Solution />\n", await File.ReadAllTextAsync(Path.Combine(target, "Demo.slnx")));
        Assert.False(File.Exists(Path.Combine(target, "src", "Demo", "Demo.csproj")));
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100 && !condition(); attempt++)
            await Task.Delay(10);
    }

    private static IEnumerable<UiMenuItem> AllItems(IEnumerable<UiMenuItem> items)
    {
        foreach (UiMenuItem item in items)
        {
            yield return item;
            foreach (UiMenuItem child in AllItems(item.Children))
                yield return child;
        }
    }

    private sealed class Fixture(
        CodeShell shell, StandardCodeEditor editor, CodeShellControls controls, StubDialogs dialogs)
        : IDisposable
    {
        public CodeShell Shell { get; } = shell;

        public StandardCodeEditor Editor { get; } = editor;

        public CodeShellControls Controls { get; } = controls;

        public StubDialogs Dialogs { get; } = dialogs;

        public static Fixture Create()
        {
            var editor = new StandardCodeEditor { PreferredSize = new BSize(900, 600) };
            var controls = new CodeShellControls
            {
                Root = new StandardPanel(),
                Body = new StandardPanel(),
                DocumentArea = new StandardPanel(),
                Menu = new StandardMenu(),
                Toolbar = new StandardToolbar(),
                Explorer = new StandardTreeView(),
                ExplorerSplitter = new StandardSplitter(),
                Tabs = new StandardTabView(),
                Editor = editor,
                Problems = new StandardTreeView(),
                Status = new StandardLabel(),
                Output = new StandardLabel(),
                CreateButton = () => new StandardButton(),
            };

            var dialogs = new StubDialogs();
            var shell = new CodeShell(controls) { FileDialogs = dialogs };
            return new Fixture(shell, editor, controls, dialogs);
        }

        public void Dispose() => Shell.Dispose();
    }

    private sealed class StubDialogs : IFileDialogService
    {
        public string? OpenFrom { get; set; }

        public string? SaveTo { get; set; }

        public ValueTask<FileGrant?> RequestOpenAsync(
            FileDialogRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(GrantFor(OpenFrom));

        public ValueTask<FileGrant?> RequestSaveAsync(
            FileDialogRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(GrantFor(SaveTo));

        private static FileGrant? GrantFor(string? path) => path is null
            ? null
            : new FileGrant(
                new FileSystemWorkspaceStorage(Path.GetDirectoryName(path)!),
                Path.GetFileName(path),
                path);
    }
}
