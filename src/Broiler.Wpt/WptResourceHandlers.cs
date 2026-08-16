using System;
using Broiler.HTML.Core.Entities;

namespace Broiler.Wpt;

/// <summary>
/// The sub-resource load policy of a conformance run: a <c>&lt;link rel="stylesheet"&gt;</c> or an
/// <c>&lt;img&gt;</c> is served from the checkout when the corpus holds it, and is otherwise
/// answered rather than fetched. Nothing in a run reaches the network.
/// </summary>
/// <remarks>
/// <para>
/// A test document routinely names hosts the corpus does not serve — <c>http://example.com/…</c>,
/// IP and documentation literals, a CGI on someone's personal server. None of them can produce a
/// resource on a runner, so the only outcome a fetch has is failure; what it costs is the time the
/// network takes to say so, which on a black-holing host is the client's whole timeout. That is a
/// timeout, not a render, and it is what the per-test budget is then spent on.
/// </para>
/// <para>
/// <b>The policy has to hold for every loader a test passes through, and that is the part that was
/// missing.</b> A run lays each document out more than once — the script bridge lays it out
/// headlessly to answer geometry reads, and the runner lays it out again to paint it — and the
/// bridge fetches external stylesheets through a loader of its own besides. Only the paint
/// container had handlers, so the other two fetched everything the document named, and paid for it
/// before the guarded render started. <see cref="Install"/> covers them; <see cref="Stylesheet"/>
/// and <see cref="Image"/> remain the paint container's handlers, which do the extra work of
/// <em>serving</em> a corpus URL from disk rather than merely declining a foreign one.
/// </para>
/// </remarks>
internal static class WptResourceHandlers
{
    /// <summary>
    /// Stands in for an off-corpus stylesheet the run declines to fetch. It has to be non-empty:
    /// <c>StylesheetLoadHandler</c> reads an empty <c>SetStyleSheet</c> as "the host supplied
    /// nothing" and falls through to fetching <c>Src</c> itself — the fetch this exists to prevent.
    /// A comment is the smallest thing that parses to no rules at all.
    /// </summary>
    internal const string OffCorpusStylesheet = "/* off-corpus stylesheet: not fetched */";

    /// <summary>
    /// The checkout this process's run is rooted at, or <see langword="null"/> when the runner was
    /// given no <c>--wpt-dir</c>. Read at <em>call</em> time by the policies <see cref="Install"/>
    /// registers, so nothing depends on when they were registered relative to the first test.
    /// </summary>
    internal static string? Root { get; set; }

    /// <summary>
    /// Registers the policy with the two loaders no container event reaches: the engine's, which
    /// the script bridge's headless geometry layout goes through as much as the render does, and
    /// the bridge's own, whose speculative preload scan requests a document's external stylesheets
    /// on a worker before the parse has even started.
    /// </summary>
    /// <remarks>
    /// Both are process-wide and idempotent (<c>??=</c>), and both are inert until a run is rooted,
    /// so a host that embeds this assembly for something else is unaffected. The engine half is a
    /// no-op until <c>Broiler.HTML</c>'s stylesheet loader carries the one line that consults it —
    /// that line is a submodule patch, and this assembly names nothing new in the submodule, so it
    /// builds and runs either way (the images half is entirely in <c>Broiler.Layout</c> and needs no
    /// patch at all).
    /// </remarks>
    internal static void Install()
    {
        Broiler.Layout.Engine.OfflineSubresources.FetchPolicy ??= MayFetch;
        Broiler.HtmlBridge.Dom.Runtime.ResourceLoader.NetworkFetchPolicy ??= MayFetch;
    }

    /// <summary>
    /// False only for a URL naming a host outside the corpus, and only while a run is rooted.
    /// </summary>
    private static bool MayFetch(string? url) =>
        Root is not { Length: > 0 } || !WptTestRunner.IsOffCorpusNetworkResource(url);

    /// <summary>
    /// The policy actually registered with the engine, so a test can exercise the delegate the
    /// engine will call rather than a copy of it. Read through here rather than from
    /// <c>OfflineSubresources</c> directly, because <c>Broiler.Layout</c> keeps its internals
    /// visible to a deliberately short list of assemblies and this needs no place on it.
    /// </summary>
    internal static Func<string?, bool>? RegisteredEnginePolicy =>
        Broiler.Layout.Engine.OfflineSubresources.FetchPolicy;

    /// <summary>
    /// The paint container's stylesheet handler for a render rooted at <paramref name="wptRoot"/>.
    /// </summary>
    internal static EventHandler<HtmlStylesheetLoadEventArgs> Stylesheet(string wptRoot) =>
        (_, args) =>
        {
            var local = WptTestRunner.TryResolveWptRootRelativePath(args.Src, wptRoot);
            if (local != null)
            {
                args.SetSrc = local;
                return;
            }

            // A sheet that parses to no rules, which is what a sheet that cannot load contributes
            // to the cascade. Answering here rather than leaving it to the engine's own policy
            // keeps the decision on the path that also knows how to *serve* a corpus URL.
            if (WptTestRunner.IsOffCorpusNetworkResource(args.Src))
                args.SetStyleSheet = OffCorpusStylesheet;
        };

    /// <summary>
    /// The paint container's image handler for a render rooted at <paramref name="wptRoot"/>.
    /// </summary>
    internal static EventHandler<HtmlImageLoadEventArgs> Image(string wptRoot) =>
        (_, args) =>
        {
            var local = WptTestRunner.TryResolveWptRootRelativePath(args.Src, wptRoot);
            if (local != null)
            {
                args.Callback(local);
                return;
            }

            // Marked handled, not redirected: the image does not load, which is exactly what these
            // tests check the parser does with such a URL. `args.Callback` cannot express that — it
            // throws on an empty path.
            if (WptTestRunner.IsOffCorpusNetworkResource(args.Src))
                args.Handled = true;
        };
}
