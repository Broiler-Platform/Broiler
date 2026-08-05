using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Code.Workspaces.Storage;

namespace Broiler.Code.Core.Shell;

/// <summary>
/// One entry in a dialog's type list. Extensions are bare, without a dot or a
/// wildcard, so a host can render them in whatever form its platform wants.
/// </summary>
public sealed record FileDialogFilter(string Label, IReadOnlyList<string> Extensions)
{
    public static FileDialogFilter Sources { get; } = new("C# source", ["cs"]);

    public static FileDialogFilter Solutions { get; } = new("Solutions", ["slnx", "sln"]);

    public static FileDialogFilter All { get; } = new("All files", ["*"]);
}

public sealed record FileDialogRequest
{
    public string Title { get; init; } = string.Empty;

    /// <summary>The name offered in a save dialog, ignored by an open dialog.</summary>
    public string? SuggestedName { get; init; }

    public IReadOnlyList<FileDialogFilter> Filters { get; init; } = [];
}

/// <summary>
/// A location the user chose, and the storage grant that choosing it created.
///
/// The dialog <em>is</em> the grant: a host hands back storage already scoped to
/// what the user picked, so this assembly never names a filesystem type and the
/// same shell works over a sandboxed provider that can only see what was
/// granted. It is also what lets a document live outside the workspace root
/// without the workspace having to widen its own.
/// </summary>
public sealed record FileGrant(IWorkspaceStorage Storage, string RelativePath, string DisplayPath);

/// <summary>
/// Asks the user for a file. A host that has no way to ask supplies nothing and
/// the affected commands report themselves unavailable, rather than appearing
/// to work and doing nothing.
/// </summary>
public interface IFileDialogService
{
    ValueTask<FileGrant?> RequestOpenAsync(
        FileDialogRequest request, CancellationToken cancellationToken = default);

    ValueTask<FileGrant?> RequestSaveAsync(
        FileDialogRequest request, CancellationToken cancellationToken = default);
}
