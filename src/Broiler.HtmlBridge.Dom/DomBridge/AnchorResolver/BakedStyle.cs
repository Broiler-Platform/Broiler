using System.Diagnostics.CodeAnalysis;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    /// <summary>
    /// The seam onto an element's inline-style working store for the bridge's <b>serialize-time bake
    /// writers</b> — the anchor resolver (position/insets/margins/borders/size), the animation-snapshot
    /// resolver (interpolated keyframe values), synthetic form-control styling (meter/progress) and the
    /// zoom serialization bake. Each reads the element's effective inline style and writes resolved/baked
    /// values, as the passes build on one another.
    ///
    /// HtmlBridge complexity-reduction roadmap Phase 4 item 2 (inline-style single authority). Increment 1
    /// routed the anchor-resolver cluster (~98 sites) here; increment 2 routed the remaining bridge-internal
    /// bake writers (AnimationResolver, and the synthetic-form-control + zoom bake writes in
    /// DomBridge.Serialization.cs), so <b>every</b> serialize-time bake write now goes through this one
    /// chokepoint while the script path (<c>element.style</c>/<c>setAttribute</c>, which write-throughs to
    /// the <c>style=</c> attribute) and the cascade cleanup stay on <see cref="InlineStyle"/>. Today it
    /// wraps that same per-element dict, so behaviour is byte-identical. The seam exists so a later
    /// increment can split the baked writes into a store distinct from the script-observable inline style —
    /// those writes must NOT leak into live <c>getAttribute("style")</c> mid-resolution, which is exactly
    /// why the dict is currently a dual-purpose parallel store — by changing only this method, the struct
    /// below, and the serializer's effective-style read (the merge point). The remaining
    /// <see cref="InlineStyle"/> reads in the serializer (<c>SyncStyleAttributeFromInlineStyle</c>, the zoom
    /// source reads, and the final property emit) are exactly those merge points.
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

    public int Count => _store.Count;

    public Dictionary<string, string>.KeyCollection Keys => _store.Keys;

    public Dictionary<string, string>.Enumerator GetEnumerator() => _store.GetEnumerator();
}
