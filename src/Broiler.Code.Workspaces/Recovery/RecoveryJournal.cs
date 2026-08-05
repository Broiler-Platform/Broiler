using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Code.Workspaces.Model;

namespace Broiler.Code.Workspaces.Recovery;

public sealed record RecoveredDocument(
    WorkspaceItemId Id,
    string RelativePath,
    string Text,
    string? ExternalRevisionWhenJournaled)
{
    /// <summary>
    /// True when the file on disk has moved on since the journal was written,
    /// so restoring is a merge decision rather than a restore.
    /// </summary>
    public bool ConflictsWith(string? currentRevision) =>
        ExternalRevisionWhenJournaled is not null &&
        !string.Equals(ExternalRevisionWhenJournaled, currentRevision, StringComparison.Ordinal);
}

/// <summary>
/// An app-private record of unsaved text, so a crash or a killed process does
/// not lose work.
///
/// It lives outside the workspace on purpose. Writing recovery data into the
/// user's source tree would put it under version control, inside the build's
/// input set, and inside the granted roots a build worker materializes — three
/// places it has no business being. On Android the same reasoning makes it the
/// app-private mirror rather than the SAF tree.
///
/// Entries are written whole and replaced atomically. A half-written journal is
/// worse than none: it looks recoverable and restores corrupted text.
/// </summary>
public sealed class RecoveryJournal
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly string _directory;

    public RecoveryJournal(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <summary>
    /// The journal for one workspace, keyed by a hash of its root so two open
    /// workspaces cannot overwrite each other's recovery data.
    /// </summary>
    public static RecoveryJournal ForWorkspace(string appPrivateRoot, string workspaceRoot)
    {
        string key = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(workspaceRoot.ToUpperInvariant())))[..16];
        return new RecoveryJournal(Path.Combine(appPrivateRoot, "recovery", key));
    }

    public async ValueTask RecordAsync(
        WorkspaceItemId id,
        WorkspaceItem item,
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(text);
        Directory.CreateDirectory(_directory);

        var entry = new JournalEntry(
            id.Value, item.RelativePath, text, item.ExternalRevision);

        string path = EntryPath(id);
        string temporary = path + ".tmp";
        await File.WriteAllTextAsync(
            temporary, JsonSerializer.Serialize(entry, Options), cancellationToken)
            .ConfigureAwait(false);

        // Replace rather than write in place, so a crash mid-write leaves the
        // previous entry rather than a truncated one.
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Drops an entry once its document is saved or discarded.</summary>
    public void Forget(WorkspaceItemId id)
    {
        string path = EntryPath(id);
        if (File.Exists(path))
            File.Delete(path);
    }

    public void Clear()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    /// <summary>
    /// Everything the journal holds. A malformed entry is skipped rather than
    /// failing the whole recovery: one unreadable file must not cost the user
    /// the other nineteen.
    /// </summary>
    public async ValueTask<IReadOnlyList<RecoveredDocument>> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        var recovered = new List<RecoveredDocument>();
        if (!Directory.Exists(_directory))
            return recovered;

        foreach (string path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                JournalEntry? entry = JsonSerializer.Deserialize<JournalEntry>(json, Options);
                if (entry is null)
                    continue;

                recovered.Add(new RecoveredDocument(
                    new WorkspaceItemId(entry.Id),
                    entry.RelativePath,
                    entry.Text,
                    entry.ExternalRevision));
            }
            catch (Exception exception) when (exception is JsonException or IOException)
            {
                // Skipped deliberately; see the summary above.
            }
        }

        recovered.Sort(static (left, right) =>
            string.CompareOrdinal(left.RelativePath, right.RelativePath));
        return recovered;
    }

    private string EntryPath(WorkspaceItemId id) => Path.Combine(
        _directory,
        string.Create(CultureInfo.InvariantCulture, $"{id.Value}.json"));

    private sealed record JournalEntry(
        long Id, string RelativePath, string Text, string? ExternalRevision);
}
