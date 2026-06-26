using System;
using System.Collections.Generic;

namespace Broiler.Layout;

/// <summary>
/// Lightweight descriptor of the source HTML element a layout box was built
/// from: its tag name, self-closing flag, and attribute bag. Relocated from the
/// renderer into <c>Broiler.Layout</c> with the layout code (see
/// <c>docs/roadmap/broiler-layout-component.md</c> §2.1). The attribute bag is
/// exposed read-only to keep the component's public surface free of mutable
/// collections; the parser still constructs it from a <see cref="Dictionary{TKey,TValue}"/>.
/// </summary>
public sealed class HtmlTag
{
    public HtmlTag(string name, bool isSingle, IReadOnlyDictionary<string, string>? attributes = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
        IsSingle = isSingle;
        Attributes = attributes;
    }

    public string Name { get; }
    public IReadOnlyDictionary<string, string>? Attributes { get; }
    public bool IsSingle { get; }
    public bool HasAttributes() => Attributes != null && Attributes.Count > 0;
    public bool HasAttribute(string attribute) => Attributes != null && Attributes.ContainsKey(attribute);
    public string? TryGetAttribute(string attribute, string? defaultValue = null) =>
        Attributes != null && Attributes.TryGetValue(attribute, out string? value) ? value : defaultValue;

    public override string ToString() => $"<{Name}>";
}
