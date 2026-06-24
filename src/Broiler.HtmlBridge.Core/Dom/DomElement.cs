using System;
using System.Collections.Generic;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

/// <summary>
/// Lightweight representation of an HTML element for the DOM bridge.
/// </summary>
public sealed class DomElement(
    string tagName,
    string? id,
    string? className,
    string innerHtml,
    Dictionary<string, string>? style = null,
    Dictionary<string, string>? attributes = null,
    bool isTextNode = false)
{
    public string TagName { get; } = tagName;
    public string? Id { get; set; } = id;

    /// <summary>The element's CSS class string; mutable via <c>classList</c> or <c>className</c>.</summary>
    public string? ClassName { get; set; } = className;

    /// <summary>The element's inner HTML content; mutable via the <c>innerHTML</c> setter.</summary>
    public string InnerHtml { get; set; } = innerHtml;

    /// <summary>Parsed inline CSS style declarations, keyed case-insensitively by property name.</summary>
    public Dictionary<string, string> Style { get; } = style ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>All HTML attributes of the element, keyed case-insensitively by attribute name.</summary>
    public Dictionary<string, string> Attributes { get; } = attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parent element in the DOM tree.</summary>
    public DomElement? Parent { get; set; }

    /// <summary>Ordered child elements in the DOM tree.</summary>
    public List<DomElement> Children { get; } = [];

    /// <summary>Whether this element represents a text node created via <c>document.createTextNode</c>.</summary>
    public bool IsTextNode { get; } = isTextNode;

    /// <summary>Text content for text nodes.</summary>
    public string? TextContent { get; set; }

    /// <summary>Tracks CSS property names that were set via JavaScript
    /// (<c>element.style.prop = value</c>, <c>setProperty</c>, or <c>cssText</c>).
    /// These must be preserved when the cascade is recalculated after class changes.</summary>
    public HashSet<string> JsSetStyleProps { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>IDL-level properties that are NOT reflected as content attributes
    /// (e.g. input.value, option.defaultSelected). Keyed by property name.</summary>
    public Dictionary<string, object?> DomProperties { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registered event listeners keyed by event type (e.g. "click", "input", "submit").</summary>
    public Dictionary<string, List<EventListenerRegistration>> EventListeners { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Inline event handler properties keyed by event type (e.g. "click" for onclick).</summary>
    public Dictionary<string, JSValue> InlineEventHandlers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Namespace URI for elements created with createElementNS.</summary>
    public string? NamespaceURI { get; set; }

    /// <summary>The document root element (<c>#subdoc-root</c>) that owns this element,
    /// used to resolve <c>ownerDocument</c> for sub-document elements.</summary>
    public DomElement? OwnerDocRoot { get; set; }

    /// <summary>Maps (namespace, localName) to qualifiedName for namespace-aware attribute methods.</summary>
    public Dictionary<(string? Namespace, string LocalName), string> NsAttrMap { get; } = new();
}

public readonly record struct EventListenerRegistration(JSValue Listener, bool Capture, bool Once = false, bool Passive = false);

/// <summary>Options for MutationObserver.observe().</summary>
public sealed class MutationObserverOptions
{
    /// <summary>Whether to observe child list changes.</summary>
    public bool ChildList { get; set; }

    /// <summary>Whether to observe attribute changes.</summary>
    public bool Attributes { get; set; }

    /// <summary>Whether to expose the previous attribute value in mutation records.</summary>
    public bool AttributeOldValue { get; set; }

    /// <summary>Whether to observe character data changes.</summary>
    public bool CharacterData { get; set; }

    /// <summary>Whether to expose the previous character data value in mutation records.</summary>
    public bool CharacterDataOldValue { get; set; }

    /// <summary>Whether to observe the subtree.</summary>
    public bool Subtree { get; set; }
}
