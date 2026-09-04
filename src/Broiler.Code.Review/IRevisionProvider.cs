using System.Threading;
using System.Threading.Tasks;

namespace Broiler.Code.Review;

/// <summary>
/// Supplies the source revision a review was recorded at.
///
/// This is an interface rather than a call to git because nothing in the review
/// model depends on the answer. Staleness is decided by content
/// (<see cref="ReviewContentHash"/>); the revision is provenance — it answers
/// "which commit was this read at?" for an auditor, and lets a reviewer pull up
/// the exact diff. A host that cannot answer returns null and loses nothing but
/// that convenience.
///
/// Keeping it out of the model is also what lets this assembly run where git
/// does not: a browser or Android host has a workspace and no repository, and a
/// review recorded there is worth exactly as much as one recorded on a desktop.
/// </summary>
public interface IRevisionProvider
{
    /// <summary>
    /// The current revision, or null when there is none to report. Null is an
    /// ordinary answer — an untracked file, a workspace that is not a
    /// repository, a host with no git — and never an error.
    /// </summary>
    ValueTask<string?> GetCurrentRevisionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The provider for a host that has no revision to report. Used by default, so
/// a review can always be recorded and the revision field is simply absent.
/// </summary>
public sealed class NoRevisionProvider : IRevisionProvider
{
    public static NoRevisionProvider Instance { get; } = new();

    public ValueTask<string?> GetCurrentRevisionAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<string?>(null);
}
