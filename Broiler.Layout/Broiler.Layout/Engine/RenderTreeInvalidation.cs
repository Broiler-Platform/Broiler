using System;
using System.Threading;
using Broiler.Dom;

namespace Broiler.Layout.Engine;

/// <summary>
/// Decides whether a mutated <see cref="DomDocument"/> actually requires the render tree to be
/// rebuilt — the first increment of multithreading roadmap item #14, "layout dirty bits".
/// </summary>
/// <remarks>
/// <para>
/// <b>What this replaces.</b> <c>HtmlContainerInt</c> holds a bound document and a copy of its
/// <see cref="DomDocument.Version"/>, and rebuilds whenever the two differ:
/// <c>BuildBoundDocument</c> disposes the render tree and regenerates it — box tree and full
/// cascade — before the layout pass runs at all. The relayout profile measured that rebuild at
/// <b>60–97% of a relayout</b>, and 97.1% on the rule-heavy corpus page, so the version counter is
/// the most expensive boolean in the engine: it says "something changed" and nothing else.
/// </para>
/// <para>
/// <b>The signal it needed was already there.</b> The roadmap recorded item #14 as blocked on
/// giving <c>Broiler.DOM</c> "a way to say <em>what</em> changed", and therefore as starting with a
/// submodule change. It does not: <see cref="DomDocument.Mutated"/> has published a typed
/// <see cref="DomMutationRecord"/> — type, target node, added and removed nodes, attribute name,
/// old and new value — for as long as <c>MutationObserver</c> has worked, which is to say since
/// before the item was written. What was missing is a consumer: the container subscribed to none of
/// it and read the counter that the same call increments. This type is that consumer.
/// </para>
/// <para>
/// <b>What it elides today, and why that is provable rather than heuristic.</b> One rule: a record
/// whose target does not hang off the bound document cannot have changed anything the render tree
/// shows, because the box tree is built from that document's tree and nothing else. Building a
/// subtree with <c>createElement</c> and <c>appendChild</c> before inserting it, populating a
/// <c>DocumentFragment</c>, filling a <c>&lt;template&gt;</c>'s inert contents, editing a subtree
/// that has been detached for the duration — every one of those bumps the version once per node
/// touched today, and every one of them costs a full rebuild and re-cascade at the next layout.
/// The insertion that follows is a mutation on a <em>connected</em> parent and still rebuilds, so
/// nothing about the visible result changes; what goes away is the rebuild for the offscreen half.
/// </para>
/// <para>
/// <b>What it deliberately does not elide.</b> Anything connected — including mutations to elements
/// that produce no boxes (a <c>&lt;meta&gt;</c>, a <c>&lt;title&gt;</c>) and attribute writes no
/// selector can reach. Both are real and both would pay, and both need the cascade to be asked a
/// question it cannot answer yet: whether any rule's subject could match differently. Answering it
/// is the rest of item #14 (invalidation sets over the rule index, then a scoped rebuild), and
/// guessing at it here would be trading a measured 34× ceiling for a wrong render.
/// </para>
/// <para>
/// <b>The conservative direction is the safe one, and there is a backstop for the case this cannot
/// see.</b> Every classification failure has to fall towards "rebuild". So the ledger also records
/// the document version it observed each record at: if the version has moved further than the
/// records account for — a publish path that bumps the counter without reaching a subscriber, an
/// earlier subscriber that threw before this one ran, a document mutated before it was bound — the
/// answer is unconditionally "rebuild", which is exactly the behaviour that was there before. An
/// elision is therefore only possible when the ledger can account for every version bump since the
/// last build.
/// </para>
/// <para>
/// <b>Why it lives here and is called from there.</b> The only caller is <c>HtmlContainerInt</c>,
/// in the <c>Broiler.HTML</c> submodule. The type is on this side of the line for the reason
/// <c>CLAUDE.md</c> gives and <see cref="CssStyleRecalc"/> and
/// <c>Broiler.Layout.IR.TileParallelReplay</c> already follow: a submodule patch carrying a whole
/// feature breaks the main repository's build the moment the submodule tree is reverted, and the
/// tests and harness below name this type. The submodule half is a field and three call sites.
/// </para>
/// <para>
/// <b>Thread safety.</b> Records arrive on whatever thread mutated the document and the decision is
/// read on the layout thread, so the state is behind a lock. It is a handful of field writes per
/// mutation against a rebuild measured in hundreds of milliseconds. Item #15 makes the DOM
/// single-threaded in the hosts this repository ships, which makes the lock uncontended rather than
/// unnecessary — a <c>MutationObserver</c> callback is not the only thing that can publish.
/// </para>
/// </remarks>
public sealed class RenderTreeInvalidation : IDisposable
{
    private readonly DomDocument _document;
    private readonly object _gate = new();

    /// <summary>The document version as of the last record this ledger observed.</summary>
    private ulong _accountedVersion;

    /// <summary>Whether any record since the last build could have changed the render tree.</summary>
    private bool _renderAffected;

    private long _observedMutations;
    private long _elidedMutations;
    private bool _disposed;

    /// <summary>
    /// Starts observing <paramref name="document"/>. The caller is expected to build the render
    /// tree immediately afterwards; the ledger takes the document's current version as the state
    /// that build reflects.
    /// </summary>
    public RenderTreeInvalidation(DomDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        _accountedVersion = document.Version;
        document.Mutated += OnMutated;
    }

    /// <summary>Records classified as reaching the render tree, since construction.</summary>
    public long RenderAffectingMutations
    {
        get { lock (_gate) return _observedMutations - _elidedMutations; }
    }

    /// <summary>Records classified as unable to reach the render tree, since construction.</summary>
    public long ElidedMutations
    {
        get { lock (_gate) return _elidedMutations; }
    }

    /// <summary>Every record this ledger has seen, since construction.</summary>
    public long ObservedMutations
    {
        get { lock (_gate) return _observedMutations; }
    }

    /// <summary>
    /// Whether the render tree has to be rebuilt to reflect everything published since the last
    /// <see cref="MarkRebuilt"/>. <see langword="false"/> only when every version bump since then is
    /// accounted for by a record that cannot have changed what the tree shows.
    /// </summary>
    /// <remarks>
    /// A pure query, for tests and diagnostics. The render path calls
    /// <see cref="TrySkipRebuild"/> instead, which cannot be interleaved with an arriving mutation.
    /// </remarks>
    public bool RequiresRebuild()
    {
        lock (_gate)
            return RequiresRebuildLocked();
    }

    /// <summary>
    /// The render path's decision: <see langword="true"/> when the rebuild can be skipped, and in
    /// that case the ledger is marked current as part of the same operation.
    /// </summary>
    /// <remarks>
    /// Check and mark have to be one step. Split into
    /// <c>if (!RequiresRebuild()) { …; MarkRebuilt(); }</c> they leave a window in which a mutation
    /// arriving between the two sets the flag and has it cleared unread — a dropped invalidation,
    /// which is a stale render rather than a slow one. Item #15's single-threaded event loop makes
    /// that window unreachable in the hosts this repository ships; it is closed here anyway,
    /// because "unreachable today" is not a property the render path should depend on and the fix
    /// costs one lock instead of two.
    /// </remarks>
    public bool TrySkipRebuild()
    {
        bool skip;
        lock (_gate)
        {
            skip = !RequiresRebuildLocked();
            if (skip)
                MarkRebuiltLocked();
        }

        if (skip)
            Interlocked.Increment(ref _rebuildsElided);
        else
            Interlocked.Increment(ref _rebuildsRequired);

        return skip;
    }

    /// <summary>
    /// Declares the render tree current with the document as it stands. Called after a rebuild;
    /// <see cref="TrySkipRebuild"/> does it for itself when it skips one.
    /// </summary>
    public void MarkRebuilt()
    {
        lock (_gate)
            MarkRebuiltLocked();
    }

    private bool RequiresRebuildLocked() =>
        // The version moving further than the records account for is the one case that has to
        // rebuild regardless of what the records said: something changed that this ledger did not
        // see, and "did not see" is not "cannot matter".
        _renderAffected || _accountedVersion != _document.Version;

    private void MarkRebuiltLocked()
    {
        _renderAffected = false;
        _accountedVersion = _document.Version;
    }

    /// <summary>Stops observing the document. Idempotent.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        _document.Mutated -= OnMutated;
    }

    private void OnMutated(DomMutationRecord record)
    {
        var reaches = ReachesRenderTree(record, _document);

        lock (_gate)
        {
            _observedMutations++;
            if (reaches)
                _renderAffected = true;
            else
                _elidedMutations++;

            _accountedVersion = _document.Version;
        }
    }

    /// <summary>
    /// Whether <paramref name="record"/> could have changed what a render tree built from
    /// <paramref name="document"/> shows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The test is connectivity, and it is the whole classification: the box tree is generated by
    /// walking <paramref name="document"/>, so a node whose root is anything else — a
    /// <c>DocumentFragment</c>, a <c>&lt;template&gt;</c>'s contents, an orphaned subtree, another
    /// document — contributes no box, and neither do its descendants.
    /// </para>
    /// <para>
    /// A <see cref="DomMutationType.ChildList"/> record names the <em>parent</em> as its target,
    /// which is what makes the test sound for insertions and removals alike: nodes added to a
    /// detached parent are themselves detached, and a node removed from a connected parent is
    /// reported against that connected parent, not against the node that just left the tree. The
    /// same holds for a node moved out of the document — the removal record on the old, connected
    /// parent is what forces the rebuild, and the <see cref="DomMutationType.Adoption"/> record that
    /// follows it names a node that is by then detached.
    /// </para>
    /// <para>
    /// Records from a sub-document — an <c>&lt;iframe&gt;</c>'s content document, reached through
    /// the container's content-document resolver — never arrive here at all: they are published on
    /// their own <see cref="DomDocument"/>, which is not the one this ledger observes and not the
    /// one whose version the container compares. That is unchanged by this type, and it is the same
    /// gap it was before.
    /// </para>
    /// </remarks>
    private static bool ReachesRenderTree(DomMutationRecord record, DomDocument document)
    {
        var target = record?.Target;
        if (target is null)
            return true;

        return ReferenceEquals(target.GetRootNode(), document);
    }

    private static long _rebuildsRequired;
    private static long _rebuildsElided;

    /// <summary>
    /// Process-wide decision counts, for the relayout harness and for tests that need to assert a
    /// rebuild did <em>not</em> happen — which is otherwise invisible, being the absence of work.
    /// </summary>
    /// <remarks>
    /// Counted per decision rather than per record: the container's own version fast-path means
    /// <see cref="TrySkipRebuild"/> is consulted once per batch of mutations, not once per layout
    /// call and not once per mutation, so these are counts of "relayouts that would have rebuilt".
    /// <see cref="RequiresRebuild"/> deliberately does not count — it is a query, and a diagnostic
    /// that a test can move by observing it is not a diagnostic.
    /// </remarks>
    public static class Decisions
    {
        /// <summary>Decisions that required a rebuild.</summary>
        public static long Required => Interlocked.Read(ref _rebuildsRequired);

        /// <summary>Decisions that skipped one.</summary>
        public static long Elided => Interlocked.Read(ref _rebuildsElided);

        /// <summary>Zeroes both counts.</summary>
        public static void Reset()
        {
            Interlocked.Exchange(ref _rebuildsRequired, 0);
            Interlocked.Exchange(ref _rebuildsElided, 0);
        }
    }
}
