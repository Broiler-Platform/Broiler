using System;
using System.Globalization;

namespace Broiler.Code.Workspaces.Model;

/// <summary>
/// A stable runtime identity for a workspace item, independent of its current
/// path.
///
/// Paths are not identity. A rename inside the IDE must keep a document's open
/// tab, undo history, and per-user state attached to it, and none of that
/// survives keying on a path. The ID is minted once when the item enters the
/// workspace and never changes while it is there.
/// </summary>
public readonly record struct WorkspaceItemId(long Value)
{
    public static WorkspaceItemId None => default;

    public bool IsNone => Value == 0;

    public override string ToString() =>
        Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Mints identities. Values are unique within one workspace instance and
/// monotonic, so an ID is never reused for a different item after a remove.
/// </summary>
public sealed class WorkspaceIdFactory
{
    private long _next;

    /// <summary>
    /// Starts minting above <paramref name="highestSeen"/>, used when restoring
    /// persisted per-user state so restored IDs cannot collide with new ones.
    /// </summary>
    public WorkspaceIdFactory(long highestSeen = 0) => _next = Math.Max(0, highestSeen);

    public WorkspaceItemId Next() => new(System.Threading.Interlocked.Increment(ref _next));
}

/// <summary>
/// How an external rename is matched back to an existing identity.
///
/// The IDE can only claim identity across an external rename when the storage
/// provider gives it something durable to match on. Guessing from content
/// similarity would silently merge two files, so an unmatched rename is
/// reported rather than resolved.
/// </summary>
public enum ExternalIdentityMatch
{
    /// <summary>The provider exposes a durable file ID and it matched.</summary>
    DurableFileId,

    /// <summary>The user confirmed the match.</summary>
    UserConfirmed,

    /// <summary>No durable ID and no confirmation; the item is treated as new.</summary>
    Unmatched,
}
