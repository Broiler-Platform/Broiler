using System;
using Broiler.JavaScript.Engine;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The narrow bridge services <see cref="WorkerBinding"/> needs — the same pattern as
/// <c>IMessagingHost</c>: the module reaches the few bridge operations it requires through named
/// seams rather than holding the bridge.
/// </summary>
internal interface IWorkerHost
{
    /// <summary>The page's JS execution context (<c>null</c> before attach).</summary>
    JSContext? JsContext { get; }

    /// <summary>
    /// Queues <paramref name="callback"/> on the page's event loop. Called from worker threads, so
    /// the implementation must be safe to call from a thread other than the page's.
    /// </summary>
    void QueueFrameAction(Action callback);

    /// <summary>
    /// Resolves a worker script specifier to its source text, or <see langword="null"/> when it
    /// cannot be found.
    /// </summary>
    /// <remarks>
    /// A seam rather than a <c>File.ReadAllText</c> inside the binding: worker scripts resolve
    /// exactly like the document's other sub-resources — relative to the page's local base path when
    /// the host set one — and that policy belongs to the host, not to this feature.
    /// </remarks>
    string? ResolveWorkerScript(string specifier);
}
