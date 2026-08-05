using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Code.Workspaces;
using Broiler.Code.Workspaces.Model;
using Broiler.Code.Workspaces.Storage;

namespace Broiler.Code.Core.Shell;

/// <summary>
/// Brings a shell up over a storage grant: finds a solution if there is one,
/// registers the sources, and leaves something editable on screen.
///
/// It lives here rather than in a head, and it goes through
/// <see cref="IWorkspaceStorage"/> rather than the filesystem, for the same
/// reason: this is the path every host takes on startup, so it has to be
/// testable without a window. The Windows head shipped its own copy of this
/// logic and nothing tested it — which is how it came to open a workspace whose
/// editor had no document.
/// </summary>
public static class WorkspaceBootstrap
{
    /// <summary>Directories never worth walking; they hold build output, not sources.</summary>
    private static readonly string[] Ignored = ["bin", "obj", ".git", ".vs", "node_modules"];

    /// <summary>
    /// Opens a workspace over <paramref name="storage"/> and attaches it, then
    /// shows a document. When the grant holds a solution it is loaded; otherwise
    /// the sources under it are registered so the tree is browsable. Either way
    /// the editor ends up with a buffer, because an editor without one refuses
    /// every keystroke.
    /// </summary>
    public static async ValueTask<CodeWorkspace> OpenAsync(
        CodeShell shell,
        IWorkspaceStorage storage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(storage);

        string? solution = await FindSolutionAsync(storage, cancellationToken).ConfigureAwait(false);
        CodeWorkspace workspace;

        if (solution is not null)
        {
            StorageResult<CodeWorkspace> loaded = await WorkspaceLoader
                .LoadSolutionAsync(storage, solution, cancellationToken).ConfigureAwait(false);

            // A solution that will not load is reported, not fatal: the folder
            // is still worth opening as loose sources.
            workspace = loaded.Succeeded ? loaded.Value! : new CodeWorkspace(storage);
            if (!loaded.Succeeded)
            {
                workspace.AddDiagnostic(new WorkspaceDiagnostic(
                    "BRW0200",
                    $"'{solution}' could not be loaded: {loaded.Failure!.Message}",
                    RelativePath: solution));
            }
        }
        else
        {
            workspace = new CodeWorkspace(storage);
        }

        // Loose sources are registered too, so a folder without a solution is
        // browsable and editable rather than an empty tree.
        await AddSourcesAsync(workspace, storage, string.Empty, cancellationToken).ConfigureAwait(false);

        shell.AttachWorkspace(workspace);

        WorkspaceItemId first = FirstSource(workspace);
        if (!first.IsNone)
            await shell.OpenDocumentAsync(first, cancellationToken).ConfigureAwait(false);

        // Nothing to open — an empty grant, or every source failed to read. An
        // untitled buffer means the user can still type.
        await shell.EnsureDocumentAsync(cancellationToken).ConfigureAwait(false);
        return workspace;
    }

    private static async ValueTask<string?> FindSolutionAsync(
        IWorkspaceStorage storage, CancellationToken cancellationToken)
    {
        StorageResult<IReadOnlyList<StorageEntry>> listed =
            await storage.ListAsync(string.Empty, cancellationToken).ConfigureAwait(false);
        if (!listed.Succeeded)
            return null;

        // .slnx before .sln: where a repository carries both, the newer format
        // is the one being maintained.
        foreach (string extension in new[] { ".slnx", ".sln" })
        {
            foreach (StorageEntry entry in listed.Value!)
            {
                if (!entry.IsDirectory &&
                    entry.RelativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.RelativePath;
                }
            }
        }

        return null;
    }

    private static async ValueTask AddSourcesAsync(
        CodeWorkspace workspace,
        IWorkspaceStorage storage,
        string directory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StorageResult<IReadOnlyList<StorageEntry>> listed =
            await storage.ListAsync(directory, cancellationToken).ConfigureAwait(false);
        if (!listed.Succeeded)
            return;

        foreach (StorageEntry entry in listed.Value!)
        {
            if (entry.IsDirectory)
            {
                if (!IsIgnored(entry.RelativePath))
                {
                    await AddSourcesAsync(workspace, storage, entry.RelativePath, cancellationToken)
                        .ConfigureAwait(false);
                }

                continue;
            }

            if (entry.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                workspace.AddItem(entry.RelativePath, WorkspaceItemKind.SourceDocument);
        }
    }

    private static bool IsIgnored(string relativeDirectory)
    {
        int slash = relativeDirectory.LastIndexOf('/');
        string name = slash < 0 ? relativeDirectory : relativeDirectory[(slash + 1)..];
        return Array.Exists(Ignored, ignored =>
            string.Equals(name, ignored, StringComparison.OrdinalIgnoreCase));
    }

    private static WorkspaceItemId FirstSource(CodeWorkspace workspace)
    {
        foreach (WorkspaceItem item in workspace.Items)
        {
            if (item.Kind == WorkspaceItemKind.SourceDocument && !item.IsUntitled)
                return item.Id;
        }

        return WorkspaceItemId.None;
    }
}
