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
    private readonly DomElement _documentNode = new("#document", null, null, string.Empty);
    private JSObject? _documentJSObject;
    private JSContext? _jsContext;

    // Timer & async execution queues
    private int _timerIdCounter;
    private readonly Dictionary<int, JSFunction> _timeoutCallbacks = new();
    private readonly Dictionary<int, JSFunction> _intervalCallbacks = new();
    private readonly HashSet<int> _clearedTimerIds = new();
    private int _rafIdCounter;
    private readonly Dictionary<int, JSFunction> _rafCallbacks = new();

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

    /// <summary>
    /// Executes all pending <c>setTimeout</c>, <c>setInterval</c>, and
    /// <c>requestAnimationFrame</c> callbacks. Repeats until no new
    /// callbacks are queued, up to a maximum of 100 iterations to prevent
    /// infinite loops. Call this before DOM capture/serialisation.
    /// </summary>
    public void FlushTimers()
    {
        const int maxIterations = 100;
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var pending = new List<(int Id, JSFunction Fn)>();

            // Collect timeout callbacks
            foreach (var kv in _timeoutCallbacks)
            {
                if (!_clearedTimerIds.Contains(kv.Key))
                    pending.Add((kv.Key, kv.Value));
            }
            _timeoutCallbacks.Clear();

            // Collect interval callbacks (execute once per flush, keep registered)
            var intervalSnapshot = new List<(int Id, JSFunction Fn)>();
            foreach (var kv in _intervalCallbacks)
            {
                if (!_clearedTimerIds.Contains(kv.Key))
                    intervalSnapshot.Add((kv.Key, kv.Value));
            }

            // Collect rAF callbacks
            var rafSnapshot = new List<(int Id, JSFunction Fn)>();
            foreach (var kv in _rafCallbacks)
                rafSnapshot.Add((kv.Key, kv.Value));
            _rafCallbacks.Clear();

            if (pending.Count == 0 && intervalSnapshot.Count == 0 && rafSnapshot.Count == 0)
                break;

            // Execute timeout callbacks
            foreach (var (id, fn) in pending)
            {
                if (_clearedTimerIds.Contains(id)) continue;
                try { fn.InvokeFunction(new Arguments(JSUndefined.Value)); }
                catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.FlushTimers", $"setTimeout callback error: {ex.Message}", ex); }
            }

            // Execute interval callbacks (one tick per flush)
            foreach (var (id, fn) in intervalSnapshot)
            {
                if (_clearedTimerIds.Contains(id)) continue;
                try { fn.InvokeFunction(new Arguments(JSUndefined.Value)); }
                catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.FlushTimers", $"setInterval callback error: {ex.Message}", ex); }
            }

            // Execute rAF callbacks
            foreach (var (id, fn) in rafSnapshot)
            {
                try { fn.InvokeFunction(new Arguments(JSUndefined.Value, new JSNumber(0))); }
                catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.FlushTimers", $"rAF callback error: {ex.Message}", ex); }
            }
        }

        // Clear all processed timer IDs after flush loop completes
        _clearedTimerIds.Clear();
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
    /// into a property→value dictionary. Implements CSS error recovery: when the
    /// same property is declared multiple times, invalid values are discarded so
    /// the last <em>valid</em> value wins (per CSS 2.1 §4.2 / CSS Syntax §5).
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
                if (!string.IsNullOrEmpty(prop) && IsAcceptableCssValue(prop, val))
                    result[prop] = val;
            }
        }
        return result;
    }

    /// <summary>
    /// CSS error recovery: returns <c>false</c> for values that are clearly
    /// invalid for the given property.  Only properties with a closed set of
    /// keyword values are validated; all other properties accept any non-empty value.
    /// </summary>
    private static bool IsAcceptableCssValue(string property, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        // CSS-wide keywords are always valid
        var v = value.ToLowerInvariant();
        if (v is "inherit" or "initial" or "unset" or "revert") return true;

        switch (property.ToLowerInvariant())
        {
            case "white-space":
                return v is "normal" or "nowrap" or "pre" or "pre-wrap"
                    or "pre-line" or "break-spaces";

            case "display":
                return v is "block" or "inline" or "inline-block" or "none"
                    or "flex" or "inline-flex" or "grid" or "inline-grid"
                    or "table" or "table-row" or "table-cell" or "table-column"
                    or "table-row-group" or "table-header-group"
                    or "table-footer-group" or "table-column-group"
                    or "table-caption" or "list-item" or "contents"
                    or "run-in" or "flow-root";

            case "position":
                return v is "static" or "relative" or "absolute" or "fixed" or "sticky";

            case "float":
            case "css-float":
                return v is "none" or "left" or "right" or "inline-start" or "inline-end";

            case "clear":
                return v is "none" or "left" or "right" or "both" or "inline-start" or "inline-end";

            case "visibility":
                return v is "visible" or "hidden" or "collapse";

            case "overflow":
            case "overflow-x":
            case "overflow-y":
                return v is "visible" or "hidden" or "scroll" or "auto" or "clip";

            case "text-align":
                return v is "left" or "right" or "center" or "justify"
                    or "start" or "end";

            case "text-decoration-style":
                return v is "solid" or "double" or "dotted" or "dashed" or "wavy";

            case "text-transform":
                return v is "none" or "capitalize" or "uppercase" or "lowercase" or "full-width";

            case "vertical-align":
                // Also accepts lengths/percentages, which won't match these keywords
                return v is "baseline" or "sub" or "super" or "text-top"
                    or "text-bottom" or "middle" or "top" or "bottom"
                    || IsLengthOrPercentage(v);

            case "box-sizing":
                return v is "content-box" or "border-box";

            case "cursor":
                return v is "auto" or "default" or "none" or "context-menu"
                    or "help" or "pointer" or "progress" or "wait"
                    or "cell" or "crosshair" or "text" or "vertical-text"
                    or "alias" or "copy" or "move" or "no-drop"
                    or "not-allowed" or "grab" or "grabbing"
                    or "e-resize" or "n-resize" or "ne-resize" or "nw-resize"
                    or "s-resize" or "se-resize" or "sw-resize" or "w-resize"
                    or "ew-resize" or "ns-resize" or "nesw-resize" or "nwse-resize"
                    or "col-resize" or "row-resize" or "all-scroll" or "zoom-in" or "zoom-out"
                    || v.StartsWith("url(", System.StringComparison.Ordinal);

            case "list-style-type":
                return v is "disc" or "circle" or "square" or "decimal"
                    or "decimal-leading-zero" or "lower-roman" or "upper-roman"
                    or "lower-greek" or "lower-latin" or "upper-latin"
                    or "armenian" or "georgian" or "lower-alpha" or "upper-alpha"
                    or "none";

            case "border-style":
            case "border-top-style":
            case "border-right-style":
            case "border-bottom-style":
            case "border-left-style":
            case "outline-style":
                return v is "none" or "hidden" or "dotted" or "dashed"
                    or "solid" or "double" or "groove" or "ridge"
                    or "inset" or "outset";

            case "font-style":
                return v is "normal" or "italic" or "oblique";

            case "font-weight":
                return v is "normal" or "bold" or "bolder" or "lighter"
                    || (int.TryParse(v, out var w) && w >= 1 && w <= 1000);

            default:
                // For all other properties accept any non-empty value
                return true;
        }
    }

    /// <summary>Checks whether <paramref name="v"/> looks like a CSS length or percentage.</summary>
    private static bool IsLengthOrPercentage(string v)
    {
        if (v == "0") return true;

        // Known CSS length/percentage units and their suffix lengths
        ReadOnlySpan<string> units = ["vmin", "vmax", "rem", "px", "em", "vh", "vw", "pt", "cm", "mm", "in", "ex", "ch", "%"];

        foreach (var unit in units)
        {
            if (v.EndsWith(unit, System.StringComparison.Ordinal) && v.Length > unit.Length)
            {
                var numPart = v[..^unit.Length];
                if (double.TryParse(numPart, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                    return true;
            }
        }

        return false;
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

    /// <summary>Maps (namespace, localName) → qualifiedName for namespace-aware attribute methods.</summary>
    public Dictionary<(string? Namespace, string LocalName), string> NsAttrMap { get; } = new();
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
