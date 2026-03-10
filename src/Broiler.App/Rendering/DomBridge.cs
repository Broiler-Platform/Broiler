using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using YantraJS.Core;

namespace Broiler.App.Rendering;

/// <summary>
/// Registers a minimal <c>document</c> object on a <see cref="JSContext"/>
/// so that JavaScript executed via YantraJS can perform basic DOM queries
/// against the current page HTML.
/// </summary>
public sealed partial class DomBridge
{
    private const int FetchTimeoutSeconds = 30;
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(FetchTimeoutSeconds) };
    private static readonly string[] InlineEventNames = ["click", "load", "change", "input", "submit", "mousedown",
        "mouseup", "mouseover", "mouseout", "keydown", "keyup", "keypress", "focus", "blur", "error"];
    private readonly List<DomElement> _elements = [];
    private readonly List<(JSFunction Callback, DomElement Target, MutationObserverOptions Options)> _mutationObservers = [];

    // window.location fields
    private string _pageUrl = string.Empty;
    private string _pageProtocol = string.Empty;
    private string _pageHost = string.Empty;
    private string _pageHostName = string.Empty;
    private string _pagePathName = "/";
    private string _pageSearch = string.Empty;
    private string _pageHash = string.Empty;
    private string _pageOrigin = string.Empty;

    /// <summary>
    /// The current document title, kept in sync with JavaScript reads/writes.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// All elements parsed from the HTML source.
    /// </summary>
    public IReadOnlyList<DomElement> Elements => _elements;

    /// <summary>
    /// Parse the supplied <paramref name="html"/> and register a
    /// <c>document</c> global on the given <paramref name="context"/>.
    /// </summary>
    public void Attach(JSContext context, string html)
    {
        ParseHtml(html);
        RegisterDocument(context);
    }

    /// <summary>
    /// Parse the supplied <paramref name="html"/> and register a
    /// <c>document</c> global on the given <paramref name="context"/>,
    /// with the page URL available via <c>window.location</c>.
    /// </summary>
    public void Attach(JSContext context, string html, string url)
    {
        if (System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri))
        {
            _pageUrl = uri.ToString();
            _pageProtocol = uri.Scheme + ":";
            _pageHost = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            _pageHostName = uri.Host;
            _pagePathName = uri.AbsolutePath;
            _pageSearch = uri.Query;
            _pageHash = uri.Fragment;
            _pageOrigin = $"{uri.Scheme}://{(uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}")}";
        }
        else
        {
            _pageUrl = url;
        }
        ParseHtml(html);
        RegisterDocument(context);
    }

    // ------------------------------------------------------------------
    //  HTML parsing helpers
    // ------------------------------------------------------------------

    private static readonly Regex TitlePattern = new(
        @"<title[^>]*>(?<content>[\s\S]*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OpenTagPattern = new(
        @"<(?<tag>[a-zA-Z][a-zA-Z0-9]*)\b(?<attrs>[^>]*)\/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly System.Collections.Generic.HashSet<string> SkippedTags = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "html", "head", "body", "title"
    };

    private static readonly System.Collections.Generic.HashSet<string> VoidTags = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr"
    };

    private static readonly Regex IdPattern = new(
        @"\bid\s*=\s*[""'](?<id>[^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClassPattern = new(
        @"\bclass\s*=\s*[""'](?<cls>[^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttributeSelectorPattern = new(
        @"\[(?<name>[a-zA-Z][a-zA-Z0-9_:-]*)(?:(?<op>[~|^$*]?=)(?<value>[""'][^""']*[""']|[^\]]*))?\]",
        RegexOptions.Compiled);

    private static readonly Regex DocTypePattern = new(
        @"<!DOCTYPE\s+(\w+)(?:\s+PUBLIC\s+""([^""]*)""(?:\s+""([^""]*)"")?)?\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private void ParseHtml(string html)
    {
        _elements.Clear();
        _jsObjectCache.Clear();

        // Use WHATWG-aligned tokeniser & tree builder
        var builder = new HtmlTreeBuilder();
        var (docElement, allElements, title) = builder.Build(html);
        Title = title;
        DocumentElement.Children.Clear();
        foreach (var child in docElement.Children)
        {
            child.Parent = DocumentElement;
            DocumentElement.Children.Add(child);
        }
        _elements.AddRange(allElements);

        // Ensure DocumentElement is in _elements so querySelector can find it
        if (!_elements.Contains(DocumentElement))
            _elements.Insert(0, DocumentElement);

        // Extract <style> blocks and apply cascaded styles
        ExtractStyleBlocks(html);
        ApplyCascadedStyles();
    }

    /// <summary>
    /// Parses all HTML attribute name-value pairs from an attribute string.
    /// Handles quoted values (<c>"…"</c> or <c>'…'</c>), unquoted values,
    /// and boolean attributes.
    /// </summary>
    private static Dictionary<string, string> ParseAttributes(string attrs)
    {
        var result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        var i = 0;
        while (i < attrs.Length)
        {
            while (i < attrs.Length && char.IsWhiteSpace(attrs[i])) i++;
            if (i >= attrs.Length) break;

            var nameStart = i;
            while (i < attrs.Length && attrs[i] != '=' && !char.IsWhiteSpace(attrs[i]) && attrs[i] != '>') i++;
            if (i == nameStart) { i++; continue; }
            var name = attrs[nameStart..i].Trim('/');

            while (i < attrs.Length && char.IsWhiteSpace(attrs[i])) i++;

            if (i >= attrs.Length || attrs[i] != '=')
            {
                if (!string.IsNullOrEmpty(name))
                    result[name] = name;
                continue;
            }
            i++; // skip '='

            while (i < attrs.Length && char.IsWhiteSpace(attrs[i])) i++;

            string value;
            if (i < attrs.Length && (attrs[i] == '"' || attrs[i] == '\''))
            {
                var quote = attrs[i++];
                var valueStart = i;
                while (i < attrs.Length && attrs[i] != quote) i++;
                value = attrs[valueStart..i];
                if (i < attrs.Length) i++;
            }
            else
            {
                var valueStart = i;
                while (i < attrs.Length && !char.IsWhiteSpace(attrs[i]) && attrs[i] != '>') i++;
                value = attrs[valueStart..i];
            }

            if (!string.IsNullOrEmpty(name))
                result[name] = value;
        }
        return result;
    }

    /// <summary>
    /// Parses a CSS inline style string (e.g. <c>"color: red; font-size: 12px"</c>)
    /// into a property→value dictionary.
    /// </summary>
    private static Dictionary<string, string> ParseStyle(string styleValue)
    {
        var result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in styleValue.Split(';'))
        {
            var colonIdx = declaration.IndexOf(':');
            if (colonIdx > 0)
            {
                var prop = declaration[..colonIdx].Trim();
                var val = declaration[(colonIdx + 1)..].Trim();
                if (!string.IsNullOrEmpty(prop))
                    result[prop] = val;
            }
        }
        return result;
    }

}

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
    public Dictionary<string, string> Style { get; } = style ?? new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>All HTML attributes of the element, keyed case-insensitively by attribute name.</summary>
    public Dictionary<string, string> Attributes { get; } = attributes ?? new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Parent element in the DOM tree.</summary>
    public DomElement? Parent { get; set; }

    /// <summary>Ordered child elements in the DOM tree.</summary>
    public List<DomElement> Children { get; } = [];

    /// <summary>Whether this element represents a text node created via <c>document.createTextNode</c>.</summary>
    public bool IsTextNode { get; } = isTextNode;

    /// <summary>Text content for text nodes.</summary>
    public string? TextContent { get; set; }

    /// <summary>IDL-level properties that are NOT reflected as content attributes
    /// (e.g. input.value, option.defaultSelected). Keyed by property name.</summary>
    public Dictionary<string, object?> DomProperties { get; } = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Registered event listeners keyed by event type (e.g. "click", "input", "submit").
    /// Each entry stores the listener and whether it was registered for the capture phase.</summary>
    public Dictionary<string, List<(JSValue Listener, bool Capture)>> EventListeners { get; } = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Inline event handler properties keyed by event type (e.g. "click" for onclick).</summary>
    public Dictionary<string, JSValue> InlineEventHandlers { get; } = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Namespace URI for elements created with createElementNS.</summary>
    public string? NamespaceURI { get; set; }
}

/// <summary>Options for MutationObserver.observe().</summary>
public sealed class MutationObserverOptions
{
    /// <summary>Whether to observe child list changes.</summary>
    public bool ChildList { get; set; }
    /// <summary>Whether to observe attribute changes.</summary>
    public bool Attributes { get; set; }
    /// <summary>Whether to observe character data changes.</summary>
    public bool CharacterData { get; set; }
    /// <summary>Whether to observe the subtree.</summary>
    public bool Subtree { get; set; }
}

/// <summary>
/// Custom JSObject subclass for HTMLFormControlsCollection that returns null
/// (not undefined) for named property lookups that don't match any control.
/// </summary>
internal sealed class FormElementsCollection : JSObject
{
    private readonly DomElement _form;
    private readonly DomBridge _bridge;

    public FormElementsCollection(DomElement form, DomBridge bridge) : base()
    {
        _form = form;
        _bridge = bridge;
    }

    protected override JSValue GetValue(KeyString key, JSValue receiver, bool throwError = true)
    {
        // First check own properties (length, numeric indices, known names)
        var result = base.GetValue(key, receiver, false);
        if (result != null && !result.IsUndefined)
            return result;

        // Dynamic named lookup in form controls
        var prop = key.Value.ToString();
        if (!string.IsNullOrEmpty(prop))
        {
            var controls = DomBridge.CollectFormControls(_form);
            foreach (var ctrl in controls)
            {
                if (ctrl.Attributes.TryGetValue("name", out var name) &&
                    string.Equals(name, prop, System.StringComparison.Ordinal))
                    return _bridge.ToJSObject(ctrl);
            }
        }

        return JSNull.Value; // HTMLFormControlsCollection returns null for missing names
    }
}
