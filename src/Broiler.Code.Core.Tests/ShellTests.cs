using Broiler.Code.Core.Shell;
using Broiler.Code.Core.Templates;
using Broiler.Code.Workspaces;
using Broiler.Code.Workspaces.Model;
using Broiler.Code.Workspaces.Storage;
using Broiler.Code.Workspaces.Text;
using Broiler.Graphics;
using Broiler.UI.CodeEditor;
using Broiler.UI.CodeEditor.Standard;
using Broiler.UI.TabView;
using Broiler.UI.TreeView;

namespace Broiler.Code.Core.Tests;

public sealed class ShellTests : IDisposable
{
    private sealed class TestTabView : UiTabView;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "broiler-shell-tests", Guid.NewGuid().ToString("n"));

    public ShellTests() => Directory.CreateDirectory(_root);

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

    private void Write(string relativePath, string content)
    {
        string full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private CodeWorkspace CreateWorkspace() => new(new FileSystemWorkspaceStorage(_root));

    [Fact(Timeout = 600000)]
    public async Task Per_Document_View_State_Survives_A_Tab_Switch()
    {
        Write("A.cs", string.Join('\n', Enumerable.Range(0, 400).Select(i => $"// line {i}")));
        Write("B.cs", string.Join('\n', Enumerable.Range(0, 400).Select(i => $"// other {i}")));

        CodeWorkspace workspace = CreateWorkspace();
        WorkspaceItemId a = workspace.AddItem("A.cs", WorkspaceItemKind.SourceDocument).Id;
        WorkspaceItemId b = workspace.AddItem("B.cs", WorkspaceItemKind.SourceDocument).Id;

        var editor = new StandardCodeEditor { PreferredSize = new BSize(800, 600) };
        editor.Arrange(new BRect(0, 0, 800, 600));
        using var tabs = new TestTabView();
        using var coordinator = new DocumentCoordinator(workspace, editor, tabs);

        Assert.True(await coordinator.OpenAsync(a));
        editor.Selection = CodeSelection.Caret(editor.Snapshot.GetLineStart(120) + 3);
        editor.Viewport = editor.Viewport with { FirstVisibleLine = 100 };

        Assert.True(await coordinator.OpenAsync(b));
        editor.Selection = CodeSelection.Caret(0);
        editor.Viewport = editor.Viewport with { FirstVisibleLine = 0 };

        // Back to the first document: the user expects the line they left.
        Assert.True(tabs.SelectTab($"doc:{a.Value}"));

        Assert.Equal(a, coordinator.ActiveDocument);
        Assert.Equal(100, editor.Viewport.FirstVisibleLine);
        Assert.Equal(editor.Snapshot.GetLineStart(120) + 3, editor.CaretPosition);
    }

    [Fact(Timeout = 600000)]
    public async Task Opening_An_Already_Open_Document_Activates_Its_Tab()
    {
        Write("A.cs", "class A { }\n");
        CodeWorkspace workspace = CreateWorkspace();
        WorkspaceItemId a = workspace.AddItem("A.cs", WorkspaceItemKind.SourceDocument).Id;

        var editor = new StandardCodeEditor();
        using var tabs = new TestTabView();
        using var coordinator = new DocumentCoordinator(workspace, editor, tabs);

        Assert.True(await coordinator.OpenAsync(a));
        Assert.True(await coordinator.OpenAsync(a));

        // One tab, one buffer. A second of either would give the user two views
        // that disagree.
        Assert.Single(tabs.Tabs);
        Assert.Equal(a, coordinator.ActiveDocument);
    }

    [Fact(Timeout = 600000)]
    public async Task Cancelling_A_Dirty_Close_Keeps_The_Tab_And_The_Text()
    {
        Write("A.cs", "class A { }\n");
        CodeWorkspace workspace = CreateWorkspace();
        WorkspaceItemId a = workspace.AddItem("A.cs", WorkspaceItemKind.SourceDocument).Id;

        var editor = new StandardCodeEditor();
        using var tabs = new TestTabView();
        using var coordinator = new DocumentCoordinator(workspace, editor, tabs)
        {
            DirtyClosePrompt = (_, _) => ValueTask.FromResult(DirtyCloseChoice.Cancel),
        };

        await coordinator.OpenAsync(a);
        editor.Selection = CodeSelection.Caret(0);
        Assert.True(editor.InsertText("// edit\n"));
        Assert.True(tabs.FindTab($"doc:{a.Value}")!.IsDirty);

        Assert.False(await coordinator.CloseAsync(a));

        Assert.Single(tabs.Tabs);
        Assert.NotNull(workspace.FindOpenDocument(a));
        Assert.StartsWith("// edit", workspace.FindOpenDocument(a)!.Buffer.Current.ToString(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public async Task Choosing_Save_On_Close_Writes_Then_Closes()
    {
        Write("A.cs", "class A { }\n");
        CodeWorkspace workspace = CreateWorkspace();
        WorkspaceItemId a = workspace.AddItem("A.cs", WorkspaceItemKind.SourceDocument).Id;

        var editor = new StandardCodeEditor();
        using var tabs = new TestTabView();
        using var coordinator = new DocumentCoordinator(workspace, editor, tabs)
        {
            DirtyClosePrompt = (_, _) => ValueTask.FromResult(DirtyCloseChoice.Save),
        };

        await coordinator.OpenAsync(a);
        editor.Selection = CodeSelection.Caret(0);
        editor.InsertText("// saved\n");

        Assert.True(await coordinator.CloseAsync(a));

        Assert.Empty(tabs.Tabs);
        Assert.StartsWith("// saved", await File.ReadAllTextAsync(Path.Combine(_root, "A.cs")), StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public async Task Discarding_Closes_Without_Writing()
    {
        Write("A.cs", "original\n");
        CodeWorkspace workspace = CreateWorkspace();
        WorkspaceItemId a = workspace.AddItem("A.cs", WorkspaceItemKind.SourceDocument).Id;

        var editor = new StandardCodeEditor();
        using var tabs = new TestTabView();
        using var coordinator = new DocumentCoordinator(workspace, editor, tabs)
        {
            DirtyClosePrompt = (_, _) => ValueTask.FromResult(DirtyCloseChoice.Discard),
        };

        await coordinator.OpenAsync(a);
        editor.Selection = CodeSelection.Caret(0);
        editor.InsertText("// discarded\n");

        Assert.True(await coordinator.CloseAsync(a));
        Assert.Equal("original\n", await File.ReadAllTextAsync(Path.Combine(_root, "A.cs")));
    }

    [Fact(Timeout = 600000)]
    public async Task Closing_Everything_Stops_At_The_First_Cancellation()
    {
        Write("A.cs", "a\n");
        Write("B.cs", "b\n");
        CodeWorkspace workspace = CreateWorkspace();
        WorkspaceItemId a = workspace.AddItem("A.cs", WorkspaceItemKind.SourceDocument).Id;
        WorkspaceItemId b = workspace.AddItem("B.cs", WorkspaceItemKind.SourceDocument).Id;

        var editor = new StandardCodeEditor();
        using var tabs = new TestTabView();
        using var coordinator = new DocumentCoordinator(workspace, editor, tabs)
        {
            DirtyClosePrompt = (_, _) => ValueTask.FromResult(DirtyCloseChoice.Cancel),
        };

        await coordinator.OpenAsync(a);
        await coordinator.OpenAsync(b);
        editor.Selection = CodeSelection.Caret(0);
        editor.InsertText("dirty");

        Assert.False(await coordinator.CloseAllAsync());

        // The clean document closed; the dirty one the user declined to lose
        // is still there.
        Assert.NotEmpty(tabs.Tabs);
        Assert.True(workspace.HasUnsavedChanges);
    }

    [Fact(Timeout = 600000)]
    public async Task The_Solution_Explorer_Reflects_The_Workspace_And_Its_Dirty_State()
    {
        Write("src/P/P.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><Compile Include="A.cs" /></ItemGroup>
            </Project>
            """);
        Write("src/P/A.cs", "class A { }\n");
        Write("S.slnx", """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/P/P.csproj" />
              </Folder>
            </Solution>
            """);

        var storage = new FileSystemWorkspaceStorage(_root);
        CodeWorkspace workspace = (await WorkspaceLoader.LoadSolutionAsync(storage, "S.slnx")).Value!;
        using var source = new SolutionExplorerSource(workspace);

        // Root, solution, project, source file.
        TreeNodeId solution = source.GetChild(source.Root, 0);
        TreeNodeId project = source.GetChild(solution, 0);
        TreeNodeId file = source.GetChild(project, 0);

        Assert.Equal("S", source.GetPresentation(solution).Label);
        Assert.Equal("P", source.GetPresentation(project).Label);
        Assert.Equal("A.cs", source.GetPresentation(file).Label);
        Assert.Equal(TreeNodeDecoration.None, source.GetPresentation(file).Decoration);

        WorkspaceItemId id = source.ItemFor(file);
        SourceDocument document = (await workspace.OpenDocumentAsync(id)).Value!;
        document.Buffer.Apply(EditTransaction.Insert(document.Buffer.Current, 0, "x", "edit"));

        // The explorer and the tab strip agree about what is unsaved.
        Assert.Equal(TreeNodeDecoration.Dirty, source.GetPresentation(file).Decoration);

        // And the ancestors are what RevealNode needs to open.
        Assert.Equal([solution, project], source.AncestorsOf(file));
    }

    [Fact(Timeout = 600000)]
    public async Task A_Templated_Solution_Contains_Only_Standard_Sdk_Files()
    {
        var storage = new FileSystemWorkspaceStorage(_root);
        var templates = new CodeTemplateService(storage);

        TemplateResult library = CodeTemplateService.PlanProject("Sample.Core", ProjectTemplateKind.ClassLibrary);
        TemplateResult app = CodeTemplateService.PlanProject(
            "Sample.App", ProjectTemplateKind.ConsoleApplication,
            projectReferences: ["../Sample.Core/Sample.Core.csproj"]);
        TemplateResult solution = CodeTemplateService.PlanSolution(
            "Sample", ["src/Sample.Core/Sample.Core.csproj", "src/Sample.App/Sample.App.csproj"]);

        Assert.True((await templates.WriteAsync(library)).Succeeded);
        Assert.True((await templates.WriteAsync(app)).Succeeded);
        Assert.True((await templates.WriteAsync(solution)).Succeeded);

        // Nothing Broiler-specific anywhere: the workspace has to stay openable
        // by any tool, including after this IDE is uninstalled.
        foreach (string path in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            string text = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("Broiler", text, StringComparison.OrdinalIgnoreCase);
        }

        // And it loads back as a declared workspace with the reference intact.
        CodeWorkspace workspace = (await WorkspaceLoader.LoadSolutionAsync(storage, "Sample.slnx")).Value!;
        Assert.Equal(2, workspace.Projects.Count);
        Assert.Contains(
            workspace.Projects.Single(p => p.Name == "Sample.App").ProjectReferences,
            r => r.EndsWith("Sample.Core.csproj", StringComparison.Ordinal));
    }

    [Fact(Timeout = 600000)]
    public async Task A_Template_Refuses_To_Overwrite()
    {
        Write("src/Sample.Core/Sample.Core.csproj", "<Project />");
        var templates = new CodeTemplateService(new FileSystemWorkspaceStorage(_root));

        TemplateResult result = await templates.WriteAsync(
            CodeTemplateService.PlanProject("Sample.Core", ProjectTemplateKind.ClassLibrary));

        Assert.False(result.Succeeded);
        Assert.Contains("already exists", result.Message!, StringComparison.Ordinal);
        Assert.Equal("<Project />", await File.ReadAllTextAsync(
            Path.Combine(_root, "src", "Sample.Core", "Sample.Core.csproj")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("1Leading")]
    [InlineData("../escape")]
    public void A_Template_Refuses_An_Unusable_Name(string name) =>
        Assert.False(CodeTemplateService.PlanProject(name, ProjectTemplateKind.ClassLibrary).Succeeded);
}
