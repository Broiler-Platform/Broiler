using Broiler.Code.Workspaces.Model;
using Broiler.Code.Workspaces.Projects;
using Broiler.Code.Workspaces.Storage;
using Broiler.Code.Workspaces.Text;

namespace Broiler.Code.Workspaces.Tests;

/// <summary>
/// The workspace claims, checked against a real directory rather than a mock.
/// Storage is where the interesting failures live — case collisions, traversal,
/// conflicts, partial saves — and none of them reproduce against a dictionary.
/// </summary>
public sealed class WorkspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "broiler-workspace-tests", Guid.NewGuid().ToString("n"));

    public WorkspaceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leaked temp directory is not worth failing a test run over.
        }
    }

    private string Write(string relativePath, string content)
    {
        string full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    private CodeWorkspace CreateWorkspace() =>
        new(new FileSystemWorkspaceStorage(_root));

    [Theory]
    [InlineData("../escape.cs")]
    [InlineData("src/../../escape.cs")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/System32/drivers/etc/hosts")]
    [InlineData("src/../../../outside")]
    [InlineData("con")]
    [InlineData("src/nul.cs")]
    [InlineData("name. /x")]
    public void Paths_That_Escape_Or_Alias_Are_Refused(string path) =>
        Assert.Null(WorkspacePath.Normalize(path));

    [Theory]
    [InlineData("src/a.cs", "src/a.cs")]
    [InlineData("src\\a.cs", "src/a.cs")]
    [InlineData("./src/./a.cs", "src/a.cs")]
    [InlineData("src/sub/../a.cs", "src/a.cs")]
    public void Ordinary_Paths_Normalize(string input, string expected) =>
        Assert.Equal(expected, WorkspacePath.Normalize(input));

    [Fact(Timeout = 600000)]
    public async Task A_Traversal_Path_Never_Reaches_Storage()
    {
        // A real file outside the root, to prove the refusal is not just a
        // missing-file error dressed up as a security check.
        string outside = Path.Combine(Path.GetTempPath(), $"broiler-outside-{Guid.NewGuid():n}.txt");
        await File.WriteAllTextAsync(outside, "secret");
        try
        {
            var storage = new FileSystemWorkspaceStorage(_root);
            string relative = Path.GetRelativePath(_root, outside).Replace('\\', '/');

            StorageResult<StorageTextContent> read = await storage.ReadTextAsync(relative);

            Assert.False(read.Succeeded);
            Assert.Equal(StorageFailureKind.OutsideGrant, read.Failure!.Kind);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact(Timeout = 600000)]
    public async Task An_Item_Keeps_Its_Identity_Across_A_Rename()
    {
        Write("src/Original.cs", "class C { }\n");
        CodeWorkspace workspace = CreateWorkspace();
        WorkspaceItem item = workspace.AddItem("src/Original.cs", WorkspaceItemKind.SourceDocument);
        WorkspaceItemId id = item.Id;

        SourceDocument document = (await workspace.OpenDocumentAsync(id)).Value!;
        document.Buffer.Apply(EditTransaction.Insert(document.Buffer.Current, 0, "// edited\n", "edit"));

        StorageResult<WorkspaceItem> renamed = await workspace.RenameItemAsync(id, "src/Renamed.cs");

        Assert.True(renamed.Succeeded);
        Assert.Equal(id, renamed.Value!.Id);
        Assert.Equal("src/Renamed.cs", renamed.Value.RelativePath);

        // The open document, its buffer, and its undo history followed the file.
        SourceDocument? still = workspace.FindOpenDocument(id);
        Assert.NotNull(still);
        Assert.Equal("src/Renamed.cs", still!.Item.RelativePath);
        Assert.True(still.Buffer.CanUndo);
        Assert.StartsWith("// edited", still.Buffer.Current.ToString(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public async Task Opening_An_Already_Open_Document_Returns_The_Same_Buffer()
    {
        Write("src/A.cs", "class A { }\n");
        CodeWorkspace workspace = CreateWorkspace();
        WorkspaceItemId id = workspace.AddItem("src/A.cs", WorkspaceItemKind.SourceDocument).Id;

        SourceDocument first = (await workspace.OpenDocumentAsync(id)).Value!;
        first.Buffer.Apply(EditTransaction.Insert(first.Buffer.Current, 0, "x", "edit"));
        SourceDocument second = (await workspace.OpenDocumentAsync(id)).Value!;

        Assert.Same(first, second);
        Assert.Same(first.Buffer, second.Buffer);
    }

    [Fact(Timeout = 600000)]
    public async Task A_Dirty_Document_Refuses_To_Close_Until_Changes_Are_Handled()
    {
        Write("src/A.cs", "class A { }\n");
        CodeWorkspace workspace = CreateWorkspace();
        WorkspaceItemId id = workspace.AddItem("src/A.cs", WorkspaceItemKind.SourceDocument).Id;
        SourceDocument document = (await workspace.OpenDocumentAsync(id)).Value!;
        document.Buffer.Apply(EditTransaction.Insert(document.Buffer.Current, 0, "x", "edit"));

        Assert.True(document.IsDirty);
        Assert.False(workspace.CloseDocument(id));
        Assert.NotNull(workspace.FindOpenDocument(id));

        // Save, then it closes. Discard is the other way out and is the
        // caller's decision, never the workspace's.
        Assert.Equal(SaveOutcomeKind.Saved, (await workspace.SaveDocumentAsync(id)).Kind);
        Assert.False(document.IsDirty);
        Assert.True(workspace.CloseDocument(id));
    }

    [Fact(Timeout = 600000)]
    public async Task Save_All_Reports_Every_Failure_And_Never_Drops_Text()
    {
        Write("src/Good.cs", "class Good { }\n");
        Write("src/Conflicted.cs", "class Conflicted { }\n");
        CodeWorkspace workspace = CreateWorkspace();

        WorkspaceItemId good = workspace.AddItem("src/Good.cs", WorkspaceItemKind.SourceDocument).Id;
        WorkspaceItemId conflicted = workspace.AddItem("src/Conflicted.cs", WorkspaceItemKind.SourceDocument).Id;

        SourceDocument goodDocument = (await workspace.OpenDocumentAsync(good)).Value!;
        SourceDocument conflictedDocument = (await workspace.OpenDocumentAsync(conflicted)).Value!;
        goodDocument.Buffer.Apply(EditTransaction.Insert(goodDocument.Buffer.Current, 0, "// a\n", "edit"));
        conflictedDocument.Buffer.Apply(
            EditTransaction.Insert(conflictedDocument.Buffer.Current, 0, "// b\n", "edit"));

        // Someone else changes one of them underneath us.
        await Task.Delay(20);
        Write("src/Conflicted.cs", "class Conflicted { } // changed elsewhere\n");

        SaveAllReport report = await workspace.SaveAllAsync();

        Assert.False(report.AllSucceeded);
        Assert.Equal(1, report.SavedCount);

        SaveOutcome failure = Assert.Single(report.Failures);
        Assert.Equal(SaveOutcomeKind.Conflict, failure.Kind);
        Assert.Equal("src/Conflicted.cs", failure.RelativePath);

        // The failed document keeps its edits and stays dirty; the successful
        // one is clean. Neither lost anything.
        Assert.True(conflictedDocument.IsDirty);
        Assert.StartsWith("// b", conflictedDocument.Buffer.Current.ToString(), StringComparison.Ordinal);
        Assert.False(goodDocument.IsDirty);
    }

    [Fact(Timeout = 600000)]
    public async Task An_External_Change_Is_Detected_Before_It_Is_Overwritten()
    {
        Write("src/A.cs", "class A { }\n");
        CodeWorkspace workspace = CreateWorkspace();
        WorkspaceItemId id = workspace.AddItem("src/A.cs", WorkspaceItemKind.SourceDocument).Id;
        SourceDocument document = (await workspace.OpenDocumentAsync(id)).Value!;

        Assert.False(await workspace.HasExternalChangeAsync(id));

        await Task.Delay(20);
        Write("src/A.cs", "class A { } // elsewhere\n");

        Assert.True(await workspace.HasExternalChangeAsync(id));

        document.Buffer.Apply(EditTransaction.Insert(document.Buffer.Current, 0, "x", "edit"));
        Assert.Equal(SaveOutcomeKind.Conflict, (await workspace.SaveDocumentAsync(id)).Kind);

        // Overwriting is possible, but only when the caller says so explicitly.
        Assert.Equal(
            SaveOutcomeKind.Saved,
            (await workspace.SaveDocumentAsync(id, overwriteConflict: true)).Kind);
    }

    [Fact(Timeout = 600000)]
    public async Task A_Save_Preserves_Encoding_And_Byte_Order_Mark()
    {
        string full = Path.Combine(_root, "bom.cs");
        await File.WriteAllTextAsync(full, "class C { }\n", new System.Text.UTF8Encoding(true));

        CodeWorkspace workspace = CreateWorkspace();
        WorkspaceItemId id = workspace.AddItem("bom.cs", WorkspaceItemKind.SourceDocument).Id;
        SourceDocument document = (await workspace.OpenDocumentAsync(id)).Value!;

        Assert.True(document.Item.Encoding.HasByteOrderMark);
        document.Buffer.Apply(EditTransaction.Insert(document.Buffer.Current, 0, "// x\n", "edit"));
        Assert.Equal(SaveOutcomeKind.Saved, (await workspace.SaveDocumentAsync(id)).Kind);

        byte[] bytes = await File.ReadAllBytesAsync(full);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    [Fact(Timeout = 600000)]
    public void Paths_Differing_Only_In_Case_Are_Reported_On_A_Case_Insensitive_Provider()
    {
        var storage = new FileSystemWorkspaceStorage(_root);
        var workspace = new CodeWorkspace(storage);

        WorkspaceItem first = workspace.AddItem("src/File.cs", WorkspaceItemKind.SourceDocument);
        WorkspaceItem second = workspace.AddItem("src/FILE.cs", WorkspaceItemKind.SourceDocument);

        if (storage.Capabilities.HasFlag(StorageCapabilities.CaseInsensitivePaths))
        {
            // The same file. One identity, and the collision is reported rather
            // than resolved silently.
            Assert.Equal(first.Id, second.Id);
            Assert.Contains(workspace.Diagnostics, d => d.Code == "BRW0002");
        }
        else
        {
            Assert.NotEqual(first.Id, second.Id);
        }
    }

    [Fact(Timeout = 600000)]
    public void An_Item_Outside_The_Grant_Is_Refused_With_A_Diagnostic()
    {
        CodeWorkspace workspace = CreateWorkspace();

        WorkspaceItem item = workspace.AddItem("../outside.cs", WorkspaceItemKind.SourceDocument);

        Assert.True(item.Id.IsNone);
        Assert.True(item.IsReadOnlyDeclaration);
        Assert.Contains(workspace.Diagnostics, d => d.Code == "BRW0001");
    }
}
