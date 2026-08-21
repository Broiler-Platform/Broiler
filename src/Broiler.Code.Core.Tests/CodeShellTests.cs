using Broiler.Code.Core.Diagnostics;
using Broiler.Code.Core.Shell;
using Broiler.Code.Workspaces;
using Broiler.Code.Workspaces.Model;
using Broiler.Code.Workspaces.Storage;
using Broiler.Graphics;
using Broiler.UI;
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
/// The shell as a composed thing, not as its parts.
///
/// Every earlier test drove the coordinator and the tree directly, so a shell
/// that composed nothing still passed all of them — which is exactly what
/// happened: the heads shipped a bare editor with no menu and no document, and
/// nothing failed. These assert the assembly itself.
/// </summary>
public sealed class CodeShellTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "broiler-shell", Guid.NewGuid().ToString("n"));

    public CodeShellTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        File.WriteAllText(Path.Combine(_root, "src", "Alpha.cs"), "class Alpha { }\n");
        File.WriteAllText(Path.Combine(_root, "src", "Beta.cs"), "class Beta { }\n");
    }

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

    private static (CodeShell Shell, StandardCodeEditor Editor, CodeShellControls Controls) CreateShell()
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

        return (new CodeShell(controls), editor, controls);
    }

    private CodeWorkspace CreateWorkspace()
    {
        var workspace = new CodeWorkspace(new FileSystemWorkspaceStorage(_root));
        workspace.AddItem("src/Alpha.cs", WorkspaceItemKind.SourceDocument);
        workspace.AddItem("src/Beta.cs", WorkspaceItemKind.SourceDocument);
        return workspace;
    }

    [Fact(Timeout = 600000)]
    public void The_Shell_Puts_Every_Pane_In_The_Tree()
    {
        (CodeShell shell, _, CodeShellControls controls) = CreateShell();

        // A bare editor as the only root was the defect. The root must actually
        // contain the chrome.
        List<UiElement> descendants = Flatten(shell.RootElement).ToList();

        Assert.Contains(descendants, e => e is UiMenu);
        Assert.Contains(descendants, e => e is Broiler.UI.Toolbar.UiToolbar);
        Assert.Contains(descendants, e => e is Broiler.UI.TreeView.UiTreeView);
        Assert.Contains(descendants, e => e is Broiler.UI.TabView.UiTabView);
        Assert.Contains(descendants, e => e is UiCodeEditor);
        Assert.Contains(descendants, e => e is Broiler.UI.Splitter.UiSplitter);
        Assert.Contains(descendants, e => e is Broiler.UI.Label.UiLabel);
        shell.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Menu_Has_File_And_Build_Commands()
    {
        (CodeShell shell, _, CodeShellControls controls) = CreateShell();

        IReadOnlyList<UiMenuItem> items = ((UiMenu)Flatten(shell.RootElement).First(e => e is UiMenu)).Items;

        Assert.Contains(items, item => item.Text.Contains("File", StringComparison.Ordinal));
        Assert.Contains(items, item => item.Text.Contains("Build", StringComparison.Ordinal));

        UiMenuItem file = items.First(item => item.Text.Contains("File", StringComparison.Ordinal));
        Assert.Contains(file.Children, child => child.CommandName == CodeCommandNames.Save);
        Assert.Contains(file.Children, child => child.CommandName == CodeCommandNames.SaveAll);
        shell.Dispose();
    }

    [Fact(Timeout = 600000)]
    public async Task Attaching_A_Workspace_Gives_The_Editor_A_Document_So_Input_Works()
    {
        (CodeShell shell, StandardCodeEditor editor, CodeShellControls controls) = CreateShell();
        CodeWorkspace workspace = CreateWorkspace();

        // Before: no document, so every edit path refuses. This is precisely
        // the state the shipped head was left in.
        Assert.True(editor.IsReadOnly);
        Assert.False(editor.InsertText("x"));

        shell.AttachWorkspace(workspace);
        WorkspaceItem alpha = workspace.FindItem("src/Alpha.cs")!;
        Assert.True(await shell.OpenDocumentAsync(alpha.Id));

        Assert.False(editor.IsReadOnly);
        editor.Selection = CodeSelection.Caret(0);
        Assert.True(editor.InsertText("// typed\n"));
        Assert.StartsWith("// typed", editor.Snapshot.GetText(0, 8), StringComparison.Ordinal);
        shell.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void An_Unavailable_Build_Command_Says_So_Rather_Than_Looking_Broken()
    {
        (CodeShell shell, _, CodeShellControls controls) = CreateShell();

        CodeCommand build = shell.Commands.Find(CodeCommandNames.Build)!;

        // No build service until Phase 4. Unavailable with a reason, rather
        // than a menu entry that does nothing when clicked.
        Assert.Equal(CommandAvailability.Unavailable, build.Availability);
        Assert.Contains("build service", build.Reason!, StringComparison.OrdinalIgnoreCase);
        shell.Dispose();
    }

    [Fact(Timeout = 600000)]
    public async Task Save_All_Names_The_Files_It_Could_Not_Write()
    {
        (CodeShell shell, StandardCodeEditor editor, CodeShellControls controls) = CreateShell();
        CodeWorkspace workspace = CreateWorkspace();
        shell.AttachWorkspace(workspace);

        WorkspaceItem alpha = workspace.FindItem("src/Alpha.cs")!;
        await shell.OpenDocumentAsync(alpha.Id);
        editor.Selection = CodeSelection.Caret(0);
        editor.InsertText("// edit\n");

        Assert.True(await shell.InvokeAsync(CodeCommandNames.SaveAll));
        Assert.StartsWith(
            "// edit",
            await File.ReadAllTextAsync(Path.Combine(_root, "src", "Alpha.cs")),
            StringComparison.Ordinal);
        shell.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Problems_Group_By_Document_And_Carry_A_Severity_Decoration()
    {
        (CodeShell shell, _, CodeShellControls controls) = CreateShell();
        var snapshot = new SourceBufferDocument(
            new Workspaces.Text.SourceBuffer("one\ntwo\nthree\n")).Snapshot;

        shell.SetDocumentProblems("src/Alpha.cs",
        [
            new MergedDiagnostic(DiagnosticOrigin.Live, "src/Alpha.cs",
                new CodeDiagnosticAdornment(0, 3, CodeDiagnosticSeverity.Error, "boom", "CS0001"), 1),
        ], snapshot);

        Assert.Equal(1, shell.Problems.Counts.Errors);
        Assert.Single(shell.Problems.GetGroupedByDocument());
        shell.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Status_Line_Says_When_Nothing_Has_Analysed()
    {
        (CodeShell shell, _, CodeShellControls controls) = CreateShell();
        shell.SetAnalysisMode(CodeAnalysisMode.ClassificationOnly);

        // Zero errors means something different when nothing has looked. The
        // status control is named rather than found by type: Output is a label
        // too, and picking by type silently read the wrong one.
        Assert.Contains("classification only", controls.Status.Text, StringComparison.Ordinal);
        shell.Dispose();
    }

    private static IEnumerable<UiElement> Flatten(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Flatten(child))
                yield return descendant;
        }
    }
}
