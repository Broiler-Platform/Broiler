using System.Diagnostics.CodeAnalysis;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    /// <summary>
    /// The anchor resolver's seam onto an element's inline-style working store: it reads the
    /// element's effective inline style and writes resolved/baked geometry (position, insets,
    /// margins, borders, width/height) as the passes build on one another.
    ///
    /// HtmlBridge complexity-reduction roadmap Phase 4 item 2 (inline-style single authority),
    /// first increment: the anchor-resolver cluster reaches the store ONLY through this accessor and
    /// the <see cref="BakedStyleMap"/> it returns, instead of indexing the raw <see cref="InlineStyle"/>
    /// dictionary at ~90 scattered sites. Today it wraps that same per-element dict, so behaviour is
    /// byte-identical. The seam exists so a later increment can split the resolver's baked-geometry
    /// writes into a store distinct from the script-observable inline style — those writes must NOT
    /// leak into live <c>getAttribute("style")</c> mid-resolution (unlike the <c>element.style</c> JS
    /// path, which write-throughs to the attribute), which is exactly why the dict is currently a
    /// dual-purpose parallel store. That split then changes only this method and the struct below.
    /// </summary>
    internal BakedStyleMap BakedInlineStyle(DomElement element) => new(InlineStyle(element));
}

/// <summary>
/// Thin value wrapper over an element's inline-style working dictionary used by the anchor resolver
/// (returned by <see cref="DomBridge.BakedInlineStyle"/>). It mirrors the exact
/// <see cref="Dictionary{TKey,TValue}"/> surface the cluster uses — indexer get/set, <c>Remove</c>,
/// <c>ContainsKey</c>, <c>TryGetValue</c>, <c>GetValueOrDefault</c> and enumeration — so migrating the
/// cluster off raw dict indexing is behaviour-preserving, and it is the single point at which a future
/// baked-vs-script store split is made. Reads currently surface the same dict the writes mutate; when
/// the stores split, reads will surface the merged (script ∪ baked) view and writes the baked overlay.
/// </summary>
internal readonly struct BakedStyleMap
{
    private readonly Dictionary<string, string> _store;

    public BakedStyleMap(Dictionary<string, string> store) => _store = store;

    public string this[string key]
    {
        get => _store[key];
        set => _store[key] = value;
    }

    public bool Remove(string key) => _store.Remove(key);

    public bool ContainsKey(string key) => _store.ContainsKey(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) =>
        _store.TryGetValue(key, out value);

    public string? GetValueOrDefault(string key) => _store.GetValueOrDefault(key);

    public Dictionary<string, string>.KeyCollection Keys => _store.Keys;

    public Dictionary<string, string>.Enumerator GetEnumerator() => _store.GetEnumerator();
}
