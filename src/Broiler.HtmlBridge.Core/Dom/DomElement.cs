using System.Collections;
using CanonicalDocument = Broiler.Dom.DomDocument;
using CanonicalElement = Broiler.Dom.DomElement;
using CanonicalNodeType = Broiler.Dom.DomNodeType;
using CanonicalName = Broiler.Dom.DomName;
using CanonicalNamespaces = Broiler.Dom.DomNamespaces;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Broiler.HtmlBridge;

/// <summary>
/// Versioned v1 bridge adapter over the canonical <see cref="CanonicalElement"/>.
/// Existing bridge algorithms retain their legacy surface while every tree and attribute
/// mutation is performed by <c>Broiler.Dom</c>. New engine code should use the canonical
/// node types; this facade may be removed only with the v2 public boundary.
/// </summary>
public sealed class DomElement : CanonicalElement
{
    public const string CompatibilitySurfaceVersion = "htmlbridge-dom-adapter/v1";
    public const string RemovalBoundaryVersion = "htmlbridge-public-surface/v2";

    private readonly LegacyChildList _children;
    private string? _textContent;

    public DomElement(
        string tagName,
        string? id,
        string? className,
        string innerHtml,
        Dictionary<string, string>? style = null,
        Dictionary<string, string>? attributes = null,
        bool isTextNode = false)
        : this(new CanonicalDocument(), tagName, id, className, innerHtml, style, attributes, isTextNode)
    {
    }

    internal DomElement(
        CanonicalDocument document,
        string tagName,
        string? id,
        string? className,
        string innerHtml,
        Dictionary<string, string>? style = null,
        Dictionary<string, string>? attributes = null,
        bool isTextNode = false)
        : base(
            document,
            new CanonicalName(
                IsSpecialNode(tagName) ? null : CanonicalNamespaces.Html,
                tagName),
            ResolveNodeType(tagName, isTextNode))
    {
        _children = new LegacyChildList(this);
        InnerHtml = innerHtml;
        // RF-BRIDGE-1c Phase B: inline style is no longer stored on the node. It lives in
        // ElementRuntimeState and is reached via DomBridge.InlineStyle(element), which lazily
        // seeds it from the `style=` attribute (already applied from `attributes` below). The
        // `style` parameter is retained only for call-site compatibility until construction
        // flips to canonical factories (Phase F); it is intentionally not read here.
        _ = style;
        IsTextNode = isTextNode || string.Equals(tagName, "#text", StringComparison.Ordinal);

        if (attributes is not null)
        {
            // Fresh node: no existing attributes to update in place, so a plain
            // canonical SetAttribute (no-namespace) matches the old legacy-dictionary
            // seeding exactly.
            foreach (var (name, value) in attributes)
                SetAttribute(name, value);
        }

        if (id is not null)
            Id = id;
        if (className is not null)
            ClassName = className;
    }

    public new string? Id
    {
        get => base.Id;
        set => base.Id = value;
    }

    public new string? ClassName
    {
        get => base.ClassName;
        set => base.ClassName = value;
    }

    public string InnerHtml { get; set; }

    public LegacyChildList Children => _children;

    public bool IsTextNode { get; }

    public string? TextContent
    {
        get => _textContent;
        set
        {
            if (string.Equals(_textContent, value, StringComparison.Ordinal))
                return;

            _textContent = value;
            MarkChanged();
        }
    }

    /// <summary>
    /// Exposes a text node's content through the canonical <see cref="CanonicalElement.NodeValue"/>
    /// so engine-neutral consumers (notably the renderer's typed DOM-to-box builder) can read
    /// bridge text without depending on this facade type. The bridge models text nodes as a
    /// <see cref="DomElement"/> with <see cref="IsTextNode"/> set rather than a canonical
    /// <c>DomText</c>, so <c>NodeValue</c> is the only canonical bridge for that text.
    /// </summary>
    public override string? NodeValue => IsTextNode ? _textContent : base.NodeValue;

    public string? NamespaceURI
    {
        get => NamespaceUri;
        set => SetName(new CanonicalName(value, TagName));
    }

    public Dictionary<(string? Namespace, string LocalName), string> NsAttrMap { get; } = [];

    private static bool IsSpecialNode(string tagName) => tagName.StartsWith('#');

    private static CanonicalNodeType ResolveNodeType(string tagName, bool isTextNode) =>
        isTextNode || string.Equals(tagName, "#text", StringComparison.Ordinal)
            ? CanonicalNodeType.Text
            : string.Equals(tagName, "#comment", StringComparison.Ordinal)
                ? CanonicalNodeType.Comment
                : string.Equals(tagName, "#doctype", StringComparison.Ordinal)
                    ? CanonicalNodeType.DocumentType
                    : CanonicalNodeType.Element;

    public sealed class LegacyChildList(DomElement owner) : IList<DomElement>
    {
        public DomElement this[int index]
        {
            get => (DomElement)owner.ChildNodes[index];
            set => owner.ReplaceChild(value, owner.ChildNodes[index]);
        }

        public int Count => owner.ChildNodes.Count;
        public bool IsReadOnly => false;

        public void Add(DomElement item) => owner.AppendChild(item);
        public void Clear()
        {
            foreach (var child in owner.ChildNodes.ToArray())
                owner.RemoveChild(child);
        }

        public bool Contains(DomElement item) => owner.ChildNodes.Contains(item);
        public void CopyTo(DomElement[] array, int arrayIndex) =>
            owner.ChildNodes.Cast<DomElement>().ToArray().CopyTo(array, arrayIndex);
        public IEnumerator<DomElement> GetEnumerator() => owner.ChildNodes.Cast<DomElement>().GetEnumerator();
        public int IndexOf(DomElement item)
        {
            for (var index = 0; index < owner.ChildNodes.Count; index++)
            {
                if (ReferenceEquals(owner.ChildNodes[index], item))
                    return index;
            }

            return -1;
        }
        public void Insert(int index, DomElement item)
        {
            var reference = index < Count ? owner.ChildNodes[index] : null;
            owner.InsertBefore(item, reference);
        }

        public bool Remove(DomElement item)
        {
            if (!ReferenceEquals(item.ParentNode, owner))
                return false;
            owner.RemoveChild(item);
            return true;
        }

        public void RemoveAt(int index) => owner.RemoveChild(owner.ChildNodes[index]);
        public DomElement? Find(Predicate<DomElement> match) => owner.ChildNodes.Cast<DomElement>().FirstOrDefault(item => match(item));
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
