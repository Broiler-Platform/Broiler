using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.Code.Workspaces.Storage;

/// <summary>
/// What a storage provider can actually do.
///
/// The workspace asks rather than assumes. A desktop filesystem renames
/// atomically and exposes durable file IDs; Android's Storage Access Framework
/// gives a document tree with different rename semantics; a browser has
/// directory handles on some engines and nothing but import/export on others.
/// Code that assumes the desktop shape works until it reaches a device, so the
/// capability is a value the workspace reads and honours.
/// </summary>
[Flags]
public enum StorageCapabilities
{
    None = 0,

    Read = 1,

    Write = 2,

    /// <summary>Create and delete entries.</summary>
    Modify = 4,

    /// <summary>Rename without a read-write-delete round trip.</summary>
    Rename = 8,

    /// <summary>
    /// Writes land atomically — a crash mid-save leaves the old file, never a
    /// truncated one.
    /// </summary>
    AtomicWrite = 16,

    /// <summary>Exposes an identity that survives a rename outside the IDE.</summary>
    DurableFileIds = 32,

    /// <summary>Reports external changes without polling.</summary>
    ChangeNotification = 64,

    /// <summary>Paths differing only in case denote the same entry.</summary>
    CaseInsensitivePaths = 128,
}

public enum StorageFailureKind
{
    None = 0,
    NotFound,
    PermissionDenied,

    /// <summary>The entry changed since it was read.</summary>
    Conflict,

    /// <summary>The path escapes the granted roots.</summary>
    OutsideGrant,

    /// <summary>The capability needed for the operation is not available.</summary>
    Unsupported,

    /// <summary>The provider failed for a reason it could not classify.</summary>
    Unknown,
}

public sealed record StorageFailure(StorageFailureKind Kind, string Message);

/// <summary>
/// The result of a storage operation. Failures are values rather than
/// exceptions: Save All has to report which of twenty files failed and why
/// while still saving the rest, and an exception per file makes that awkward
/// enough that callers start swallowing them.
/// </summary>
public readonly record struct StorageResult<T>(bool Succeeded, T? Value, StorageFailure? Failure)
{
    public static StorageResult<T> Ok(T value) => new(true, value, null);

    public static StorageResult<T> Fail(StorageFailureKind kind, string message) =>
        new(false, default, new StorageFailure(kind, message));
}

/// <summary>Text plus what is needed to write it back unchanged.</summary>
public sealed record StorageTextContent(
    string Text,
    Model.TextEncodingInfo Encoding,
    string? ExternalRevision,
    string? DurableFileId);

public sealed record StorageEntry(
    string RelativePath,
    bool IsDirectory,
    long SizeBytes,
    string? ExternalRevision,
    string? DurableFileId);

/// <summary>
/// Asynchronous, capability-based access to the granted workspace roots.
///
/// Asynchronous throughout because two of the four target hosts have no
/// synchronous option: Android's SAF and the browser's file handles are both
/// promise-shaped, and a synchronous contract would either block their UI
/// thread or need a second contract later.
/// </summary>
public interface IWorkspaceStorage
{
    StorageCapabilities Capabilities { get; }

    /// <summary>Roots the user granted. Nothing outside them is reachable.</summary>
    IReadOnlyList<string> GrantedRoots { get; }

    ValueTask<StorageResult<StorageTextContent>> ReadTextAsync(
        string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes text. <paramref name="expectedRevision"/> is the revision the
    /// caller last saw; a mismatch fails with
    /// <see cref="StorageFailureKind.Conflict"/> rather than overwriting.
    /// Passing null writes unconditionally, which only a caller that has
    /// already resolved the conflict should do.
    /// </summary>
    ValueTask<StorageResult<string>> WriteTextAsync(
        string relativePath,
        string text,
        Model.TextEncodingInfo encoding,
        string? expectedRevision,
        CancellationToken cancellationToken = default);

    ValueTask<StorageResult<IReadOnlyList<StorageEntry>>> ListAsync(
        string relativeDirectory, CancellationToken cancellationToken = default);

    ValueTask<StorageResult<StorageEntry>> StatAsync(
        string relativePath, CancellationToken cancellationToken = default);

    ValueTask<StorageResult<bool>> DeleteAsync(
        string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames, if <see cref="StorageCapabilities.Rename"/> is present. A
    /// provider without it fails with
    /// <see cref="StorageFailureKind.Unsupported"/> so the caller can fall back
    /// to a copy-and-delete deliberately rather than by accident.
    /// </summary>
    ValueTask<StorageResult<StorageEntry>> RenameAsync(
        string fromRelativePath, string toRelativePath, CancellationToken cancellationToken = default);
}
