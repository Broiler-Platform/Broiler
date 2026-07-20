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
/// One entry in a document's <see cref="ModuleMap"/> (Phase 7 item 6): a recognised
/// <c>&lt;script type="module"&gt;</c> keyed for the browser module system. Inline modules are keyed by a
/// synthetic <c>inline:{order}</c> id and carry their authorised body in <see cref="Source"/>; module
/// scripts with a <c>src</c> are recorded with their URL but not yet fetched (a later slice loads them),
/// so their <see cref="Source"/> is <c>null</c> and <see cref="IsExecutable"/> is <c>false</c>.
/// </summary>
public sealed record ModuleMapEntry(
    int DocumentOrder,
    ScriptSourceKind Kind,
    string Key,
    string? Url,
    string? Source,
    bool IsExecutable);

/// <summary>
/// The document's module map (Phase 7 item 6, first slice): the registry of recognised
/// <c>&lt;script type="module"&gt;</c> elements in document order, so a module is no longer silently
/// dropped. Inline modules are executable now; external/data-URI module loading (fetch + import graph) is
/// a later slice. Keyed by <see cref="ModuleMapEntry.Key"/> (an inline module's synthetic id or an
/// external module's URL).
/// </summary>
public sealed class ModuleMap
{
    private readonly List<ModuleMapEntry> _entries = [];
    private readonly Dictionary<string, ModuleMapEntry> _byKey = new(System.StringComparer.Ordinal);

    /// <summary>The module entries in document order.</summary>
    public IReadOnlyList<ModuleMapEntry> Entries => _entries;

    /// <summary>Number of recognised module scripts.</summary>
    public int Count => _entries.Count;

    /// <summary>Looks up a module entry by its <see cref="ModuleMapEntry.Key"/>.</summary>
    public bool TryGet(string key, out ModuleMapEntry? entry) => _byKey.TryGetValue(key, out entry);

    internal void Add(ModuleMapEntry entry)
    {
        _entries.Add(entry);
        _byKey[entry.Key] = entry;
    }
}

/// <summary>
/// Holds the result of extracting all scripts from an HTML page,
/// separated into regular (inline / data-URI / external), deferred,
/// and async scripts so that the engine can execute them in the correct order.
/// </summary>
public sealed class ScriptExtractionResult(
    IReadOnlyList<string> scripts,
    IReadOnlyList<string> deferredScripts,
    IReadOnlyList<string> asyncScripts,
    IReadOnlyList<ScriptDescriptor>? descriptors = null,
    IReadOnlyList<string>? moduleScripts = null,
    ModuleMap? moduleMap = null)
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

    /// <summary>
    /// Executable inline module bodies (Phase 7 item 6, first slice), in document order, each already
    /// wrapped for module top-level semantics (strict mode, own scope) by
    /// <see cref="ModuleScriptWrapper.WrapInlineModule"/>. Module scripts are deferred, so a consumer runs
    /// these after the classic <see cref="DeferredScripts"/>. External/data-URI module loading is a later
    /// slice, so those modules appear only in <see cref="ModuleMap"/>, not here.
    /// </summary>
    public IReadOnlyList<string> ModuleScripts { get; } = moduleScripts ?? [];

    /// <summary>The document's module map (Phase 7 item 6): every recognised module in document order.</summary>
    public ModuleMap ModuleMap { get; } = moduleMap ?? new ModuleMap();
}
