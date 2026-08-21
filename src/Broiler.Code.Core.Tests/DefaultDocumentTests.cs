using Broiler.Code.Core.Shell;
using Broiler.Code.Workspaces;
using Broiler.Code.Workspaces.Model;
using Broiler.Code.Workspaces.Storage;
using Broiler.Graphics;
using Broiler.UI;
using Broiler.UI.CodeEditor;
using Broiler.UI.Button.Standard;
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
/// What happens when Broiler Code opens with nothing in particular to show.
///
/// This is the path every first run takes and the one that was broken: the head
/// opened a workspace, found nothing it could bind, and left the editor with a
/// null document — read-only, refusing every keystroke, with no way to make a
/// new file or open an existing one. These drive the default path end to end.
/// </summary>
public sealed class DefaultDocumentTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "broiler-default", Guid.NewGuid().ToString("n"));

    public DefaultDocumentTests() => Directory.CreateDirectory(_root);

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
    public async Task An_Empty_Grant_Still_Opens_Something_Editable()
    {
        using Fixture fixture = Fixture.Create();

        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        // The defect, precisely: an empty folder produced a read-only surface.
        Assert.False(fixture.Editor.IsReadOnly);

        fixture.Editor.Selection = CodeSelection.Caret(0);
        Assert.True(fixture.Editor.InsertText("hello"));
        Assert.Equal("hello", fixture.Editor.Snapshot.GetText(0, 5));
    }

    [Fact(Timeout = 600000)]
    public async Task The_Default_Document_Is_Untitled_And_Named_So_In_The_Tab()
    {
        using Fixture fixture = Fixture.Create();

        CodeWorkspace workspace = await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        WorkspaceItem item = workspace.FindItem(fixture.Shell.Coordinator!.ActiveDocument)!;
        Assert.True(item.IsUntitled);
        Assert.Equal("Untitled1.cs", item.RelativePath);
        Assert.Equal("Untitled1.cs", fixture.Controls.Tabs.SelectedTab!.Header);
    }

    [Fact(Timeout = 600000)]
    public async Task A_Grant_With_Sources_Opens_One_Rather_Than_An_Untitled_Buffer()
    {
        string directory = Grant("sources");
        await File.WriteAllTextAsync(Path.Combine(directory, "Alpha.cs"), "class Alpha { }\n");

        using Fixture fixture = Fixture.Create();
        CodeWorkspace workspace = await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(directory));

        WorkspaceItem item = workspace.FindItem(fixture.Shell.Coordinator!.ActiveDocument)!;
        Assert.False(item.IsUntitled);
        Assert.Equal("Alpha.cs", item.RelativePath);
    }

    [Fact(Timeout = 600000)]
    public async Task Bin_And_Obj_Are_Not_Walked()
    {
        string directory = Grant("built");
        Directory.CreateDirectory(Path.Combine(directory, "obj"));
        await File.WriteAllTextAsync(
            Path.Combine(directory, "obj", "Generated.cs"), "class Generated { }\n");
        await File.WriteAllTextAsync(Path.Combine(directory, "Real.cs"), "class Real { }\n");

        using Fixture fixture = Fixture.Create();
        CodeWorkspace workspace = await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(directory));

        Assert.Null(workspace.FindItem("obj/Generated.cs"));
        Assert.NotNull(workspace.FindItem("Real.cs"));
    }

    [Fact(Timeout = 600000)]
    public async Task New_Adds_A_Second_Untitled_Document_Without_Reusing_The_Name()
    {
        using Fixture fixture = Fixture.Create();
        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        Assert.True(await fixture.Shell.InvokeAsync(CodeCommandNames.New));

        CodeWorkspace workspace = fixture.Shell.Workspace!;
        WorkspaceItem second = workspace.FindItem(fixture.Shell.Coordinator!.ActiveDocument)!;
        Assert.Equal("Untitled2.cs", second.RelativePath);
        Assert.Equal(2, fixture.Controls.Tabs.Tabs.Count);
        Assert.False(fixture.Editor.IsReadOnly);
    }

    [Fact(Timeout = 600000)]
    public async Task Saving_An_Untitled_Document_Asks_Where_And_Writes_There()
    {
        string elsewhere = Grant("elsewhere");
        using Fixture fixture = Fixture.Create();
        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        fixture.Editor.Selection = CodeSelection.Caret(0);
        fixture.Editor.InsertText("class Saved { }\n");

        // Save, not Save As: a document with nowhere to go has to ask, rather
        // than reporting a failure the user cannot act on.
        fixture.Dialogs.SaveTo = Path.Combine(elsewhere, "Saved.cs");
        Assert.True(await fixture.Shell.InvokeAsync(CodeCommandNames.Save));

        Assert.Equal(
            "class Saved { }\n",
            await File.ReadAllTextAsync(Path.Combine(elsewhere, "Saved.cs")));

        // The document followed its bytes: same tab, new name, no longer untitled.
        WorkspaceItem item = fixture.Shell.Workspace!
            .FindItem(fixture.Shell.Coordinator!.ActiveDocument)!;
        Assert.False(item.IsUntitled);
        Assert.Equal("Saved.cs", fixture.Controls.Tabs.SelectedTab!.Header);
        Assert.Single(fixture.Controls.Tabs.Tabs);
    }

    [Fact(Timeout = 600000)]
    public async Task Save_As_Keeps_The_Documents_Identity_And_Undo_History()
    {
        string elsewhere = Grant("elsewhere");
        using Fixture fixture = Fixture.Create();
        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        WorkspaceItemId before = fixture.Shell.Coordinator!.ActiveDocument;
        fixture.Editor.Selection = CodeSelection.Caret(0);
        fixture.Editor.InsertText("first");

        fixture.Dialogs.SaveTo = Path.Combine(elsewhere, "Kept.cs");
        Assert.True(await fixture.Shell.InvokeAsync(CodeCommandNames.SaveAs));

        Assert.Equal(before, fixture.Shell.Coordinator.ActiveDocument);
        Assert.True(fixture.Editor.Undo());
        Assert.Equal(0, fixture.Editor.Snapshot.Length);
    }

    [Fact(Timeout = 600000)]
    public async Task Open_Reads_A_File_Outside_The_Workspace_Root()
    {
        string elsewhere = Grant("elsewhere");
        string file = Path.Combine(elsewhere, "Outside.cs");
        await File.WriteAllTextAsync(file, "class Outside { }\n");

        using Fixture fixture = Fixture.Create();
        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        // The grant comes from the dialog, so a file the workspace root never
        // contained is reachable without widening that root.
        fixture.Dialogs.OpenFrom = file;
        Assert.True(await fixture.Shell.InvokeAsync(CodeCommandNames.Open));

        Assert.Equal("class Outside { }\n", fixture.Editor.Snapshot.GetText(0, 18));
        Assert.Equal("Outside.cs", fixture.Controls.Tabs.SelectedTab!.Header);
    }

    [Fact(Timeout = 600000)]
    public async Task A_File_Opened_By_Grant_Saves_Back_To_Where_It_Came_From()
    {
        string elsewhere = Grant("elsewhere");
        string file = Path.Combine(elsewhere, "Outside.cs");
        await File.WriteAllTextAsync(file, "class Outside { }\n");

        using Fixture fixture = Fixture.Create();
        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        fixture.Dialogs.OpenFrom = file;
        await fixture.Shell.InvokeAsync(CodeCommandNames.Open);

        fixture.Editor.Selection = CodeSelection.Caret(0);
        fixture.Editor.InsertText("// edited\n");

        // A plain Save, with no dialog: the grant travelled with the document,
        // so it writes back where it came from rather than under the root.
        fixture.Dialogs.SaveTo = null;
        Assert.True(await fixture.Shell.InvokeAsync(CodeCommandNames.Save));
        Assert.StartsWith("// edited", await File.ReadAllTextAsync(file), StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public async Task Two_Granted_Files_With_The_Same_Name_Stay_Two_Documents()
    {
        string first = Path.Combine(Grant("one"), "Program.cs");
        string second = Path.Combine(Grant("two"), "Program.cs");
        await File.WriteAllTextAsync(first, "// one\n");
        await File.WriteAllTextAsync(second, "// two\n");

        using Fixture fixture = Fixture.Create();
        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        fixture.Dialogs.OpenFrom = first;
        await fixture.Shell.InvokeAsync(CodeCommandNames.Open);
        fixture.Dialogs.OpenFrom = second;
        await fixture.Shell.InvokeAsync(CodeCommandNames.Open);

        // Identity is the path plus its grant. Keying on the relative path
        // alone would silently make these one document with one buffer.
        Assert.Equal(3, fixture.Controls.Tabs.Tabs.Count);
        Assert.StartsWith("// two", fixture.Editor.Snapshot.GetText(0, 6), StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public async Task Cancelling_The_Dialog_Changes_Nothing()
    {
        using Fixture fixture = Fixture.Create();
        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        fixture.Editor.Selection = CodeSelection.Caret(0);
        fixture.Editor.InsertText("kept");

        fixture.Dialogs.SaveTo = null;
        Assert.False(await fixture.Shell.InvokeAsync(CodeCommandNames.SaveAs));

        Assert.Equal("kept", fixture.Editor.Snapshot.GetText(0, 4));
        Assert.True(fixture.Shell.Workspace!.HasUnsavedChanges);
    }

    [Fact(Timeout = 600000)]
    public async Task A_Host_That_Cannot_Ask_Says_So_Rather_Than_Doing_Nothing()
    {
        using Fixture fixture = Fixture.Create();
        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        fixture.Shell.FileDialogs = null;

        CodeCommand open = fixture.Shell.Commands.Find(CodeCommandNames.Open)!;
        Assert.Equal(CommandAvailability.Unavailable, open.Availability);
        Assert.Contains("ask for a file", open.Reason!, StringComparison.Ordinal);

        // And the menu shows it as unavailable rather than hiding it.
        UiMenuItem file = fixture.Controls.Menu.Items
            .First(item => item.Text.Contains("File", StringComparison.Ordinal));
        UiMenuItem entry = file.Children.First(child => child.CommandName == CodeCommandNames.Open);
        Assert.False(entry.IsEnabled);
        Assert.Contains("unavailable", entry.Text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public async Task Save_All_Asks_For_A_Location_Instead_Of_Skipping_Untitled_Work()
    {
        string elsewhere = Grant("elsewhere");
        using Fixture fixture = Fixture.Create();
        await WorkspaceBootstrap.OpenAsync(
            fixture.Shell, new FileSystemWorkspaceStorage(Grant("empty")));

        fixture.Editor.Selection = CodeSelection.Caret(0);
        fixture.Editor.InsertText("// work\n");

        // The whole point: an untitled document is the one most likely to be
        // lost, so Save All must not quietly pass over it.
        fixture.Dialogs.SaveTo = Path.Combine(elsewhere, "Work.cs");
        Assert.True(await fixture.Shell.InvokeAsync(CodeCommandNames.SaveAll));

        Assert.Equal("// work\n", await File.ReadAllTextAsync(Path.Combine(elsewhere, "Work.cs")));
        Assert.False(fixture.Shell.Workspace!.HasUnsavedChanges);
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

    /// <summary>
    /// Stands in for the platform dialogs. Null means the user cancelled, which
    /// is a case worth testing precisely because it is the one that must leave
    /// everything untouched.
    /// </summary>
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
