using System;
using System.IO;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.HtmlBridge.Logging;
using Broiler.JavaScript.Engine;

namespace Broiler.HtmlBridge;

/// <summary>
/// <see cref="DomBridge"/>'s implementation of <see cref="IWorkerHost"/> — the narrow contract the
/// <see cref="Broiler.HtmlBridge.Dom.Features.WorkerBinding"/> feature module consumes, in the same
/// shape as <c>DomBridge.MessagingHost.cs</c>. Explicit interface implementations, so the seams do
/// not widen the public <c>DomBridge</c> surface.
/// </summary>
public sealed partial class DomBridge : IWorkerHost
{
    JSContext? IWorkerHost.JsContext => _jsContext;

    /// <summary>
    /// Queued on the page's <c>BrowserEventLoop</c>, whose frame-action store is a
    /// <c>ConcurrentDictionary</c> — so this is safe to call from a worker thread, which is the
    /// whole point of the seam. Deliberately does <em>not</em> go through the disposed guard: a
    /// worker thread can be mid-post while the bridge tears down, and throwing
    /// <see cref="ObjectDisposedException"/> onto that thread would surface as a worker crash rather
    /// than the no-op it should be.
    /// </summary>
    void IWorkerHost.QueueFrameAction(Action callback)
    {
        if (_disposed)
            return;

        try
        {
            QueueFrameAction(callback);
        }
        catch (ObjectDisposedException)
        {
            // Raced with disposal; the message simply does not arrive, which is correct.
        }
    }

    /// <summary>
    /// Resolves a worker script the way the document's other sub-resources resolve: against the
    /// local base path the host set, then as given.
    /// </summary>
    /// <remarks>
    /// Only <c>file</c>-shaped specifiers are resolved. A worker whose script would have to be
    /// fetched over the network returns <see langword="null"/> here and surfaces as an <c>error</c>
    /// event on the Worker object, rather than blocking a render on a request this host has no
    /// policy for.
    /// </remarks>
    string? IWorkerHost.ResolveWorkerScript(string specifier)
    {
        try
        {
            if (Uri.TryCreate(specifier, UriKind.Absolute, out var absolute))
            {
                if (!absolute.IsFile)
                    return null;

                return File.Exists(absolute.LocalPath) ? File.ReadAllText(absolute.LocalPath) : null;
            }

            var basePath = _resources.LocalBasePath;
            if (!string.IsNullOrEmpty(basePath))
            {
                var candidate = Path.Combine(basePath, specifier);
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
            }

            return File.Exists(specifier) ? File.ReadAllText(specifier) : null;
        }
        catch (Exception ex)
        {
            RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.ResolveWorkerScript",
                $"Could not read worker script '{specifier}': {ex.Message}", ex);
            return null;
        }
    }
}
