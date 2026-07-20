using System.Collections.Generic;

namespace Broiler.HtmlBridge.Scripting;

/// <summary>Where a <c>&lt;script&gt;</c>'s program text comes from.</summary>
public enum ScriptSourceKind
{
    /// <summary>Inline script — the program is the element's text content.</summary>
    Inline,

    /// <summary>External <c>src</c> pointing at a <c>data:</c> URI.</summary>
    DataUri,

    /// <summary>External <c>src</c> pointing at a file/http(s)/relative URL.</summary>
    External,
}

/// <summary>
/// Metadata-rich descriptor of one discovered <c>&lt;script&gt;</c> element (Phase 7 item 3): its
/// document order, source kind, source URL (for external/data-URI), nonce, and the
/// <c>async</c>/<c>defer</c>/<c>type=module</c> flags — plus the resolved program text when the script
/// was authorised and its body was available. This is the neutral, host-agnostic shape the loader
/// (item 4) and the event loop (item 6) consume; it does not itself perform I/O or CSP decisions.
/// </summary>
public sealed record ScriptDescriptor(
    int DocumentOrder,
    ScriptSourceKind Kind,
    string? Url,
    string? Nonce,
    bool IsAsync,
    bool IsDefer,
    bool IsModule,
    string Content);

/// <summary>
/// Holds the result of extracting all scripts from an HTML page,
/// separated into regular (inline / data-URI / external), deferred,
/// and async scripts so that the engine can execute them in the correct order.
/// </summary>
public sealed class ScriptExtractionResult(
    IReadOnlyList<string> scripts,
    IReadOnlyList<string> deferredScripts,
    IReadOnlyList<string> asyncScripts,
    IReadOnlyList<ScriptDescriptor>? descriptors = null)
{
    /// <summary>Regular scripts to execute in document order.</summary>
    public IReadOnlyList<string> Scripts { get; } = scripts;

    /// <summary>Deferred scripts to execute after all regular scripts.</summary>
    public IReadOnlyList<string> DeferredScripts { get; } = deferredScripts;

    /// <summary>Async scripts that may execute as soon as they are available.</summary>
    public IReadOnlyList<string> AsyncScripts { get; } = asyncScripts;

    /// <summary>
    /// Every discovered <c>&lt;script&gt;</c> in document order with its metadata (Phase 7 item 3),
    /// including <c>type=module</c> scripts that the classic <see cref="Scripts"/>/<see cref="DeferredScripts"/>/
    /// <see cref="AsyncScripts"/> lists omit. The classic lists remain the authoritative execution buckets;
    /// this exposes the metadata that used to be computed and discarded.
    /// </summary>
    public IReadOnlyList<ScriptDescriptor> Descriptors { get; } = descriptors ?? [];
}
