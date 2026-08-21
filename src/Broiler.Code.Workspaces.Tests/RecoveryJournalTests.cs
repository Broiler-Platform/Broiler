using Broiler.Code.Workspaces.Model;
using Broiler.Code.Workspaces.Recovery;
using Broiler.Code.Workspaces.Storage;
using Broiler.Code.Workspaces.Text;

namespace Broiler.Code.Workspaces.Tests;

public sealed class RecoveryJournalTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "broiler-recovery-tests", Guid.NewGuid().ToString("n"));

    public RecoveryJournalTests() => Directory.CreateDirectory(_root);

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

    [Fact(Timeout = 600000)]
    public async Task Unsaved_Text_Survives_A_Process_That_Never_Saved()
    {
        string workspaceRoot = Path.Combine(_root, "workspace");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "src"));
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "src", "A.cs"), "class A { }\n");

        var storage = new FileSystemWorkspaceStorage(workspaceRoot);
        var workspace = new CodeWorkspace(storage);
        WorkspaceItemId id = workspace.AddItem("src/A.cs", WorkspaceItemKind.SourceDocument).Id;
        SourceDocument document = (await workspace.OpenDocumentAsync(id)).Value!;

        document.Buffer.Apply(EditTransaction.Insert(document.Buffer.Current, 0, "// unsaved\n", "edit"));

        RecoveryJournal journal = RecoveryJournal.ForWorkspace(
            Path.Combine(_root, "app-private"), workspaceRoot);
        await journal.RecordAsync(id, document.Item, document.Buffer.Current.ToString());

        // The process dies here. The file on disk never received the edit.
        Assert.Equal("class A { }\n", await File.ReadAllTextAsync(
            Path.Combine(workspaceRoot, "src", "A.cs")));

        RecoveryJournal reopened = RecoveryJournal.ForWorkspace(
            Path.Combine(_root, "app-private"), workspaceRoot);
        RecoveredDocument entry = Assert.Single(await reopened.ReadAllAsync());

        Assert.Equal(id, entry.Id);
        Assert.Equal("src/A.cs", entry.RelativePath);
        Assert.StartsWith("// unsaved", entry.Text, StringComparison.Ordinal);
        Assert.False(entry.ConflictsWith(document.Item.ExternalRevision));
    }

    [Fact(Timeout = 600000)]
    public async Task Recovery_Data_Never_Lands_Inside_The_Workspace()
    {
        string workspaceRoot = Path.Combine(_root, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "A.cs"), "class A { }\n");

        RecoveryJournal journal = RecoveryJournal.ForWorkspace(
            Path.Combine(_root, "app-private"), workspaceRoot);
        await journal.RecordAsync(
            new WorkspaceItemId(1),
            new WorkspaceItem
            {
                Id = new WorkspaceItemId(1),
                Kind = WorkspaceItemKind.SourceDocument,
                RelativePath = "A.cs",
            },
            "recovered text");

        // Nothing new in the source tree: it would otherwise reach version
        // control, the build's input set, and the worker's granted roots.
        Assert.Equal(
            ["A.cs"],
            Directory.EnumerateFileSystemEntries(workspaceRoot).Select(Path.GetFileName).ToArray());
        Assert.NotEmpty(await journal.ReadAllAsync());
    }

    [Fact(Timeout = 600000)]
    public async Task A_Stale_Entry_Reports_A_Conflict_Rather_Than_Restoring_Over_A_Newer_File()
    {
        RecoveryJournal journal = RecoveryJournal.ForWorkspace(
            Path.Combine(_root, "app-private"), Path.Combine(_root, "workspace"));

        await journal.RecordAsync(
            new WorkspaceItemId(7),
            new WorkspaceItem
            {
                Id = new WorkspaceItemId(7),
                Kind = WorkspaceItemKind.SourceDocument,
                RelativePath = "A.cs",
                ExternalRevision = "revision-when-journaled",
            },
            "unsaved text");

        RecoveredDocument entry = Assert.Single(await journal.ReadAllAsync());

        Assert.False(entry.ConflictsWith("revision-when-journaled"));
        Assert.True(entry.ConflictsWith("someone-else-saved-since"));
    }

    [Fact(Timeout = 600000)]
    public async Task A_Malformed_Entry_Does_Not_Cost_The_Others()
    {
        RecoveryJournal journal = RecoveryJournal.ForWorkspace(
            Path.Combine(_root, "app-private"), Path.Combine(_root, "workspace"));
        await journal.RecordAsync(
            new WorkspaceItemId(1),
            new WorkspaceItem
            {
                Id = new WorkspaceItemId(1),
                Kind = WorkspaceItemKind.SourceDocument,
                RelativePath = "Good.cs",
            },
            "good");

        // A truncated entry, the shape a crash mid-write would leave if the
        // journal did not replace atomically.
        string directory = Directory.EnumerateDirectories(
            Path.Combine(_root, "app-private", "recovery")).Single();
        await File.WriteAllTextAsync(Path.Combine(directory, "999.json"), "{\"id\": 999, \"rel");

        IReadOnlyList<RecoveredDocument> recovered = await journal.ReadAllAsync();

        RecoveredDocument entry = Assert.Single(recovered);
        Assert.Equal("Good.cs", entry.RelativePath);
    }

    [Fact(Timeout = 600000)]
    public async Task Saving_Forgets_The_Entry()
    {
        RecoveryJournal journal = RecoveryJournal.ForWorkspace(
            Path.Combine(_root, "app-private"), Path.Combine(_root, "workspace"));
        var id = new WorkspaceItemId(3);
        await journal.RecordAsync(
            id,
            new WorkspaceItem
            {
                Id = id,
                Kind = WorkspaceItemKind.SourceDocument,
                RelativePath = "A.cs",
            },
            "pending");

        Assert.NotEmpty(await journal.ReadAllAsync());
        journal.Forget(id);
        Assert.Empty(await journal.ReadAllAsync());
    }
}
