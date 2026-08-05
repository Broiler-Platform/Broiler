using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Code.Workspaces.Model;
using Broiler.Code.Workspaces.Projects;
using Broiler.Code.Workspaces.Storage;

namespace Broiler.Code.Workspaces;

/// <summary>
/// Opens a solution into a workspace, reading only what is declared.
///
/// Nothing here evaluates MSBuild, runs a target, restores, or loads an
/// analyzer. Opening a workspace does not imply trust, so a freshly opened
/// solution shows its structure and lets the user edit, and everything that
/// needs evaluation is marked as such until the user makes a scoped trust
/// decision.
/// </summary>
public static class WorkspaceLoader
{
    public static async ValueTask<StorageResult<CodeWorkspace>> LoadSolutionAsync(
        IWorkspaceStorage storage,
        string solutionRelativePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var workspace = new CodeWorkspace(storage);
        StorageResult<StorageTextContent> read = await storage
            .ReadTextAsync(solutionRelativePath, cancellationToken).ConfigureAwait(false);
        if (!read.Succeeded)
            return StorageResult<CodeWorkspace>.Fail(read.Failure!.Kind, read.Failure.Message);

        DeclaredSolutionFile solutionFile;
        try
        {
            solutionFile = DeclaredSolutionFile.Parse(solutionRelativePath, read.Value!.Text);
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or FormatException)
        {
            return StorageResult<CodeWorkspace>.Fail(
                StorageFailureKind.Unknown,
                $"'{solutionRelativePath}' could not be parsed: {exception.Message}");
        }

        WorkspaceItem solutionItem = workspace.AddItem(
            solutionRelativePath, WorkspaceItemKind.Configuration);

        string solutionDirectory = DirectoryOf(solutionRelativePath);
        var projectIds = new List<WorkspaceItemId>();

        foreach (SolutionProjectEntry entry in solutionFile.InDisplayOrder())
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkspaceItemId id = await LoadProjectAsync(
                workspace, storage, Combine(solutionDirectory, entry.RelativePath), cancellationToken)
                .ConfigureAwait(false);
            if (!id.IsNone)
                projectIds.Add(id);
        }

        workspace.AddSolution(new CodeSolution
        {
            Id = solutionItem.Id,
            Name = NameWithoutExtension(solutionRelativePath),
            RelativePath = solutionItem.RelativePath,
            Projects = projectIds,
            Folders = solutionFile.Folders,
            SolutionItems = [],
        });

        return StorageResult<CodeWorkspace>.Ok(workspace);
    }

    private static async ValueTask<WorkspaceItemId> LoadProjectAsync(
        CodeWorkspace workspace,
        IWorkspaceStorage storage,
        string projectRelativePath,
        CancellationToken cancellationToken)
    {
        StorageResult<StorageTextContent> read = await storage
            .ReadTextAsync(projectRelativePath, cancellationToken).ConfigureAwait(false);
        if (!read.Succeeded)
        {
            workspace.AddDiagnostic(new WorkspaceDiagnostic(
                "BRW0200",
                $"'{projectRelativePath}' could not be read: {read.Failure!.Message}",
                RelativePath: projectRelativePath));
            return WorkspaceItemId.None;
        }

        DeclaredProjectFile projectFile;
        try
        {
            projectFile = DeclaredProjectFile.Parse(projectRelativePath, read.Value!.Text);
        }
        catch (System.Xml.XmlException exception)
        {
            workspace.AddDiagnostic(new WorkspaceDiagnostic(
                "BRW0201",
                $"'{projectRelativePath}' is not valid XML: {exception.Message}",
                RelativePath: projectRelativePath));
            return WorkspaceItemId.None;
        }

        WorkspaceItem projectItem = workspace.AddItem(
            projectRelativePath, WorkspaceItemKind.ProjectFile);
        string projectDirectory = DirectoryOf(projectItem.RelativePath);
        var items = new List<WorkspaceItemId>();

        // Explicit compile items only. The SDK's implicit globs are
        // evaluated-only; enumerating the directory here would produce an item
        // set that looks authoritative and disagrees with the real build the
        // moment a project excludes something.
        foreach ((string include, string? link) in projectFile.GetExplicitCompileItems())
        {
            // Registered at the file's real location, not at the path the link
            // displays it under. The same file linked into two projects is one
            // item with one buffer; keying on the display path would give it
            // two, and they would disagree about whether it is dirty.
            WorkspaceItem item = workspace.AddItem(
                Combine(projectDirectory, include),
                WorkspaceItemKind.SourceDocument,
                projectItem.Id,
                isLinked: link is not null,
                linkedAs: link is not null ? Combine(projectDirectory, link) : null);
            if (!item.Id.IsNone)
                items.Add(item.Id);
        }

        var project = new CodeProject
        {
            Id = projectItem.Id,
            Name = NameWithoutExtension(projectItem.RelativePath),
            RelativePath = projectItem.RelativePath,
            TargetFrameworks = projectFile.GetTargetFrameworks(),
            ProjectReferences = projectFile.GetProjectReferences(),
            Items = items,
            HasUnsupportedConstructs = projectFile.HasUnsupportedConstructs,
        };

        workspace.AddProject(project);
        return projectItem.Id;
    }

    private static string DirectoryOf(string relativePath)
    {
        int slash = relativePath.LastIndexOf('/');
        return slash < 0 ? string.Empty : relativePath[..slash];
    }

    private static string Combine(string directory, string relative)
    {
        string normalizedRelative = relative.Replace('\\', '/');
        string combined = directory.Length == 0
            ? normalizedRelative
            : $"{directory}/{normalizedRelative}";

        // Normalize here so a project's "../shared/File.cs" becomes a path the
        // workspace can key on, and an escape becomes visible.
        return WorkspacePath.Normalize(combined) ?? combined;
    }

    private static string NameWithoutExtension(string relativePath)
    {
        int slash = relativePath.LastIndexOf('/');
        string file = slash < 0 ? relativePath : relativePath[(slash + 1)..];
        int dot = file.LastIndexOf('.');
        return dot < 0 ? file : file[..dot];
    }
}
