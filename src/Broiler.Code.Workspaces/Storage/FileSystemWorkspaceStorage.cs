using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Code.Workspaces.Model;

namespace Broiler.Code.Workspaces.Storage;

/// <summary>
/// The desktop provider: a granted directory on the local filesystem.
///
/// Writes are atomic where the platform allows it — a temporary file in the
/// same directory, then a replace — so a crash or a full disk during a save
/// leaves the previous file intact rather than a truncated one. That is the
/// difference between losing an edit and losing the file.
/// </summary>
public sealed class FileSystemWorkspaceStorage : IWorkspaceStorage
{
    private readonly string _root;

    public FileSystemWorkspaceStorage(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        GrantedRoots = [_root];

        Capabilities =
            StorageCapabilities.Read |
            StorageCapabilities.Write |
            StorageCapabilities.Modify |
            StorageCapabilities.Rename |
            StorageCapabilities.AtomicWrite |
            StorageCapabilities.ChangeNotification;

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            Capabilities |= StorageCapabilities.CaseInsensitivePaths;
    }

    public StorageCapabilities Capabilities { get; }

    public IReadOnlyList<string> GrantedRoots { get; }

    public ValueTask<StorageResult<StorageTextContent>> ReadTextAsync(
        string relativePath, CancellationToken cancellationToken = default)
    {
        if (!TryResolve(relativePath, out string full, out StorageFailure? failure))
            return Fail<StorageTextContent>(failure!);
        if (!File.Exists(full))
            return Fail<StorageTextContent>(StorageFailureKind.NotFound, relativePath);

        try
        {
            byte[] bytes = File.ReadAllBytes(full);
            (string text, TextEncodingInfo encoding) = Decode(bytes);
            return ValueTask.FromResult(StorageResult<StorageTextContent>.Ok(
                new StorageTextContent(text, encoding, Revision(full), DurableId(full))));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Fail<StorageTextContent>(StorageFailureKind.PermissionDenied, exception.Message);
        }
        catch (IOException exception)
        {
            return Fail<StorageTextContent>(StorageFailureKind.Unknown, exception.Message);
        }
    }

    public ValueTask<StorageResult<string>> WriteTextAsync(
        string relativePath,
        string text,
        TextEncodingInfo encoding,
        string? expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryResolve(relativePath, out string full, out StorageFailure? failure))
            return Fail<string>(failure!);

        // The conflict check and the write are not one operation. A provider
        // that could make them one would be better; this one narrows the window
        // rather than pretending to close it, and reports what it saw.
        if (expectedRevision is not null && File.Exists(full))
        {
            string current = Revision(full);
            if (!string.Equals(current, expectedRevision, StringComparison.Ordinal))
            {
                return Fail<string>(
                    StorageFailureKind.Conflict,
                    $"'{relativePath}' changed on disk since it was read.");
            }
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            byte[] bytes = Encode(text, encoding);

            // A temporary sibling then a replace: same volume, so the final
            // step is a rename rather than a copy.
            string temporary = full + ".broiler-tmp";
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(full))
                File.Replace(temporary, full, destinationBackupFileName: null, ignoreMetadataErrors: true);
            else
                File.Move(temporary, full);

            return ValueTask.FromResult(StorageResult<string>.Ok(Revision(full)));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Fail<string>(StorageFailureKind.PermissionDenied, exception.Message);
        }
        catch (IOException exception)
        {
            return Fail<string>(StorageFailureKind.Unknown, exception.Message);
        }
    }

    public ValueTask<StorageResult<IReadOnlyList<StorageEntry>>> ListAsync(
        string relativeDirectory, CancellationToken cancellationToken = default)
    {
        if (!TryResolve(relativeDirectory, out string full, out StorageFailure? failure))
            return Fail<IReadOnlyList<StorageEntry>>(failure!);
        if (!Directory.Exists(full))
            return Fail<IReadOnlyList<StorageEntry>>(StorageFailureKind.NotFound, relativeDirectory);

        var entries = new List<StorageEntry>();
        foreach (string path in Directory.EnumerateFileSystemEntries(full))
        {
            var info = new FileInfo(path);

            // A reparse point can leave the granted root while looking like an
            // ordinary entry, so it is not listed at all.
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            bool isDirectory = info.Attributes.HasFlag(FileAttributes.Directory);
            entries.Add(new StorageEntry(
                Relative(path),
                isDirectory,
                isDirectory ? 0 : info.Length,
                isDirectory ? null : Revision(path),
                DurableId(path)));
        }

        entries.Sort(static (left, right) =>
            string.CompareOrdinal(left.RelativePath, right.RelativePath));
        return ValueTask.FromResult(
            StorageResult<IReadOnlyList<StorageEntry>>.Ok(entries));
    }

    public ValueTask<StorageResult<StorageEntry>> StatAsync(
        string relativePath, CancellationToken cancellationToken = default)
    {
        if (!TryResolve(relativePath, out string full, out StorageFailure? failure))
            return Fail<StorageEntry>(failure!);

        bool isDirectory = Directory.Exists(full);
        if (!isDirectory && !File.Exists(full))
            return Fail<StorageEntry>(StorageFailureKind.NotFound, relativePath);

        var info = new FileInfo(full);
        return ValueTask.FromResult(StorageResult<StorageEntry>.Ok(new StorageEntry(
            Relative(full),
            isDirectory,
            isDirectory ? 0 : info.Length,
            isDirectory ? null : Revision(full),
            DurableId(full))));
    }

    public ValueTask<StorageResult<bool>> DeleteAsync(
        string relativePath, CancellationToken cancellationToken = default)
    {
        if (!TryResolve(relativePath, out string full, out StorageFailure? failure))
            return Fail<bool>(failure!);

        try
        {
            if (Directory.Exists(full))
                Directory.Delete(full, recursive: true);
            else if (File.Exists(full))
                File.Delete(full);
            else
                return Fail<bool>(StorageFailureKind.NotFound, relativePath);
            return ValueTask.FromResult(StorageResult<bool>.Ok(true));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Fail<bool>(StorageFailureKind.PermissionDenied, exception.Message);
        }
        catch (IOException exception)
        {
            return Fail<bool>(StorageFailureKind.Unknown, exception.Message);
        }
    }

    public ValueTask<StorageResult<StorageEntry>> RenameAsync(
        string fromRelativePath, string toRelativePath, CancellationToken cancellationToken = default)
    {
        if (!TryResolve(fromRelativePath, out string from, out StorageFailure? fromFailure))
            return Fail<StorageEntry>(fromFailure!);
        if (!TryResolve(toRelativePath, out string to, out StorageFailure? toFailure))
            return Fail<StorageEntry>(toFailure!);
        if (!File.Exists(from) && !Directory.Exists(from))
            return Fail<StorageEntry>(StorageFailureKind.NotFound, fromRelativePath);

        // On a case-insensitive volume, "a.cs" and "A.cs" are the same file, so
        // a case-only rename must not be treated as a collision with itself.
        bool caseOnly = Capabilities.HasFlag(StorageCapabilities.CaseInsensitivePaths) &&
            string.Equals(from, to, StringComparison.OrdinalIgnoreCase);
        if (!caseOnly && (File.Exists(to) || Directory.Exists(to)))
        {
            return Fail<StorageEntry>(
                StorageFailureKind.Conflict, $"'{toRelativePath}' already exists.");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            if (Directory.Exists(from))
                Directory.Move(from, to);
            else
                File.Move(from, to, overwrite: caseOnly);

            var info = new FileInfo(to);
            return ValueTask.FromResult(StorageResult<StorageEntry>.Ok(new StorageEntry(
                Relative(to), Directory.Exists(to), info.Exists ? info.Length : 0,
                Directory.Exists(to) ? null : Revision(to), DurableId(to))));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Fail<StorageEntry>(StorageFailureKind.PermissionDenied, exception.Message);
        }
        catch (IOException exception)
        {
            return Fail<StorageEntry>(StorageFailureKind.Unknown, exception.Message);
        }
    }

    private bool TryResolve(string relativePath, out string full, out StorageFailure? failure)
    {
        full = string.Empty;
        failure = null;

        string? normalized = WorkspacePath.Normalize(relativePath);
        if (normalized is null)
        {
            failure = new StorageFailure(
                StorageFailureKind.OutsideGrant,
                $"'{relativePath}' resolves outside the granted root.");
            return false;
        }

        string candidate = Path.GetFullPath(Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate, _root, StringComparison.OrdinalIgnoreCase))
        {
            failure = new StorageFailure(
                StorageFailureKind.OutsideGrant,
                $"'{relativePath}' resolves outside the granted root.");
            return false;
        }

        full = candidate;
        return true;
    }

    private string Relative(string full) =>
        Path.GetRelativePath(_root, full).Replace('\\', '/');

    /// <summary>
    /// Last-write time and length. Not a hash: hashing every file on every stat
    /// would make opening a large workspace slow enough to matter, and this is
    /// enough to notice that something changed.
    /// </summary>
    private static string Revision(string full)
    {
        var info = new FileInfo(full);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{info.LastWriteTimeUtc.Ticks:x}-{info.Length:x}");
    }

    private static string? DurableId(string full)
    {
        // The local filesystem exposes no identity that survives a rename
        // portably, so the workspace is told there is none rather than being
        // given the path and left to believe it is durable.
        _ = full;
        return null;
    }

    private static (string Text, TextEncodingInfo Encoding) Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), new TextEncodingInfo("utf-8", true));
        return (Encoding.UTF8.GetString(bytes), TextEncodingInfo.Utf8NoBom);
    }

    private static byte[] Encode(string text, TextEncodingInfo encoding)
    {
        byte[] body = new UTF8Encoding(false).GetBytes(text);
        if (!encoding.HasByteOrderMark)
            return body;

        var result = new byte[body.Length + 3];
        result[0] = 0xEF;
        result[1] = 0xBB;
        result[2] = 0xBF;
        body.CopyTo(result, 3);
        return result;
    }

    private static ValueTask<StorageResult<T>> Fail<T>(StorageFailureKind kind, string message) =>
        ValueTask.FromResult(StorageResult<T>.Fail(kind, message));

    private static ValueTask<StorageResult<T>> Fail<T>(StorageFailure failure) =>
        ValueTask.FromResult(StorageResult<T>.Fail(failure.Kind, failure.Message));
}
