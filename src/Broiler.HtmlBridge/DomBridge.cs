using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

/// <summary>
/// Registers a minimal <c>document</c> object on a <see cref="JSContext"/>
/// so that JavaScript executed via YantraJS can perform basic DOM queries
/// against the current page HTML.
/// </summary>
public sealed partial class DomBridge
{
    /// <summary>
    /// Safety cap for draining bridge-backed microtask/timer work so promise/timer
    /// chains can settle without risking an infinite loop in test and capture paths.
    /// </summary>
    public const int AsyncDrainIterationLimit = 1000;

    private const int FetchTimeoutSeconds = 30;
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(FetchTimeoutSeconds) };
    private static readonly string[] InlineEventNames = ["click", "load", "change", "input", "submit", "mousedown",
        "mouseup", "mouseover", "mouseout", "keydown", "keyup", "keypress", "focus", "blur", "error", "scroll",
        "scrollend"];
    private readonly List<DomElement> _elements = [];
    private readonly List<(JSFunction Callback, DomElement Target, MutationObserverOptions Options)> _mutationObservers = [];
    private readonly List<WeakReference<RangeState>> _activeRanges = [];
    private readonly List<WeakReference<IteratorState>> _activeNodeIterators = [];
    private readonly DomElement _documentNode = new("#document", null, null, string.Empty);
    private JSObject? _documentJSObject;
    private JSObject? _windowJSObject;
    private JSObject? _visualViewportJSObject;
    private readonly Dictionary<DomElement, JSObject> _docRootToDocJSObject = [];
    private JSContext? _jsContext;

    // Timer & async execution queues
    private int _timerIdCounter;
    private readonly Dictionary<int, JSFunction> _timeoutCallbacks = new();
    private readonly Dictionary<int, JSFunction> _intervalCallbacks = new();
    private readonly HashSet<int> _clearedTimerIds = new();
    private int _rafIdCounter;
    private readonly Dictionary<int, JSFunction> _rafCallbacks = new();
    private int _frameActionIdCounter;
    private readonly Dictionary<int, Action> _frameActions = new();
    private readonly Dictionary<DomElement, int> _smoothScrollTokens = [];
    private readonly List<JSFunction> _visualViewportScrollListeners = [];
    private readonly Dictionary<string, List<EventListenerRegistration>> _windowEventListeners =
        new(StringComparer.OrdinalIgnoreCase);
    private double _visualViewportScale = 1.0;
    private double _visualViewportPageLeftOffset;
    private double _visualViewportPageTopOffset;

    /// <summary>
    /// Index into <see cref="_elements"/> of the <c>&lt;script&gt;</c> element
    /// that is currently executing.  Used by <c>document.write()</c> to insert
    /// content at the correct DOM position.  Set to &lt;0 when no script is
    /// running.
    /// </summary>
    internal int CurrentScriptIndex { get; set; } = -1;

    /// <summary>
    /// Optional local base path for resolving relative sub-resource URLs to local files.
    /// When set, relative URLs are first checked against this directory before attempting HTTP.
    /// </summary>
    private string? _localBasePath;

    // viewport dimensions for window.innerWidth/innerHeight and element box-model properties
    private int _viewportWidth = 1024;
    private int _viewportHeight = 768;
    private bool _serializationTransformsApplied;

    /// <summary>
    /// Optional callback invoked after each queued timer, interval, animation-frame,
    /// or frame action task. Callers use this to run spec-like microtask checkpoints.
    /// </summary>
    public Action? TaskCheckpointCallback { get; set; }

    /// <summary>
    /// Optional Content Security Policy applied while compiling inline script-bearing
    /// bridge surfaces such as <c>on*</c> attributes.
    /// </summary>
    public ContentSecurityPolicy? Csp { get; set; }

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
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
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
    /// Registers all DOM elements with an <c>id</c> attribute as globals
    /// on the JS context, matching the HTML5 "named access on the Window
    /// object" behaviour (e.g. <c>window.myId</c> → element with id="myId").
    /// </summary>
    public void RegisterNamedElementGlobals(JSContext context)
    {
        foreach (var el in _elements)
        {
            if (el.IsTextNode || string.IsNullOrEmpty(el.Id))
                continue;
            // Only register if the global doesn't already exist
            // (user-defined globals take precedence).
            try
            {
                var existing = context.Eval(
                    $"typeof {el.Id} !== 'undefined'");
                if (existing?.ToString() == "True")
                    continue;
            }
            catch
            {
                // If the id isn't a valid JS identifier, skip it.
                continue;
            }

            var jsObj = ToJSObject(el);
            context[el.Id] = jsObj;
        }
    }

    /// <summary>
    /// Sets the viewport dimensions used by <c>window.innerWidth</c>,
    /// <c>window.innerHeight</c>, and element box-model properties
    /// (<c>clientWidth</c>, <c>clientHeight</c>, etc.) on <c>&lt;html&gt;</c>
    /// and <c>&lt;body&gt;</c> elements.
    /// </summary>
    public void SetViewportSize(int width, int height)
    {
        _viewportWidth = width;
        _viewportHeight = height;
    }

    /// <summary>
    /// Sets a local base directory for resolving relative sub-resource URLs.
    /// When set, sub-resource URLs (e.g. iframe src) are first looked up as files
    /// relative to this directory before falling back to HTTP fetch.
    /// </summary>
    public void SetLocalBasePath(string basePath)
    {
        _localBasePath = basePath;
    }

    /// <summary>
    /// Executes all pending <c>setTimeout</c>, <c>setInterval</c>, and
    /// <c>requestAnimationFrame</c> callbacks. Repeats until no new
    /// callbacks are queued, up to a maximum of 500 iterations to prevent
    /// infinite loops. The higher limit supports test harnesses like Acid3
    /// that chain 100+ tests via <c>setTimeout</c>. Call this before DOM
    /// capture/serialisation.
    /// </summary>
    public void FlushTimers()
    {
        const int maxIterations = 1000;
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            if (!FlushTimerStep())
                break;
        }

        // Clear all processed timer IDs after flush loop completes
        _clearedTimerIds.Clear();
    }

    /// <summary>
    /// Returns <c>true</c> when there are queued <c>setTimeout</c>,
    /// <c>setInterval</c>, or <c>requestAnimationFrame</c> callbacks
    /// waiting to execute.
    /// </summary>
    public bool HasPendingTimers =>
        _timeoutCallbacks.Count > 0 || _intervalCallbacks.Count > 0 || _rafCallbacks.Count > 0 || _frameActions.Count > 0;

    /// <summary>
    /// Executes one batch of pending timer and animation-frame callbacks.
    /// Returns <c>true</c> if callbacks were executed (more may be pending);
    /// <c>false</c> if there was nothing to run.
    /// Used by interactive rendering to step through animations one frame at
    /// a time so that intermediate visual states are displayed.
    /// </summary>
    public bool FlushTimerStep()
    {
        void RunTaskCheckpoint()
        {
            try
            {
                TaskCheckpointCallback?.Invoke();
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.FlushTimerStep", $"Task checkpoint error: {ex.Message}", ex);
            }
        }

        var pending = new List<(int Id, JSFunction Fn)>();

        // Collect timeout callbacks
        foreach (var kv in _timeoutCallbacks)
        {
            if (!_clearedTimerIds.Contains(kv.Key))
                pending.Add((kv.Key, kv.Value));
        }
        _timeoutCallbacks.Clear();

        // Collect interval callbacks (execute once per step, keep registered)
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

        var frameActionSnapshot = _frameActions.Values.ToList();
        _frameActions.Clear();

        if (pending.Count == 0 && intervalSnapshot.Count == 0 && rafSnapshot.Count == 0 && frameActionSnapshot.Count == 0)
            return false;

        // Execute timeout callbacks
        foreach (var (id, fn) in pending)
        {
            if (_clearedTimerIds.Contains(id)) continue;
            try { fn.InvokeFunction(new Arguments(JSUndefined.Value)); }
            catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.FlushTimerStep", $"setTimeout callback error: {ex.Message}", ex); }
            finally { RunTaskCheckpoint(); }
        }

        // Execute interval callbacks (one tick per step)
        foreach (var (id, fn) in intervalSnapshot)
        {
            if (_clearedTimerIds.Contains(id)) continue;
            try { fn.InvokeFunction(new Arguments(JSUndefined.Value)); }
            catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.FlushTimerStep", $"setInterval callback error: {ex.Message}", ex); }
            finally { RunTaskCheckpoint(); }
        }

        // Execute rAF callbacks
        foreach (var (id, fn) in rafSnapshot)
        {
            try { fn.InvokeFunction(new Arguments(JSUndefined.Value, new JSNumber(0))); }
            catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.FlushTimerStep", $"rAF callback error: {ex.Message}", ex); }
            finally { RunTaskCheckpoint(); }
        }

        foreach (var action in frameActionSnapshot)
        {
            try { action(); }
            catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.FlushTimerStep", $"frame action error: {ex.Message}", ex); }
            finally { RunTaskCheckpoint(); }
        }

        return true;
    }

    /// <summary>
    /// Fires the <c>load</c> event on the <c>&lt;body&gt;</c> element, which
    /// triggers the inline <c>onload</c> attribute handler as well as any
    /// <c>addEventListener('load', …)</c> listeners registered on the body.
    /// In browsers, the body's <c>onload</c> fires after all synchronous
    /// scripts have executed. This is critical for test harnesses like Acid3,
    /// which use <c>&lt;body onload="update()"&gt;</c> to bootstrap the
    /// test runner.
    /// </summary>
    public void FireWindowLoadEvent()
    {
        if (_jsContext == null) return;

        _jsContext["frames"] = BuildWindowFramesArray();

        var htmlEl = _elements.FirstOrDefault(e =>
            string.Equals(e.TagName, "html", StringComparison.OrdinalIgnoreCase));
        if (htmlEl != null)
            FireDescendantOnloads(htmlEl);

        // 1. Fire window.onload if it was set by script.
        //    In browsers, setting `window.onload = fn` registers a handler
        //    that fires when the page finishes loading.  This is distinct
        //    from the <body onload="…"> inline attribute handler.
        try
        {
            _jsContext.Eval(@"
(function() {
  if (typeof window.onload === 'function') {
    try { window.onload(); } catch(e) {}
  }
})();");
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.FireWindowLoadEvent",
                $"Error firing window.onload: {ex.Message}", ex);
        }

        try
        {
            DispatchWindowEvent("load");
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.FireWindowLoadEvent",
                $"Error firing window load listeners: {ex.Message}", ex);
        }

        // 2. Fire <body onload="…"> attribute handler and any load event
        //    listeners registered on the body element.
        // Find the <body> element by traversing the document tree.
        // The body is a child of <html> (documentElement), which is a
        // child of the document node. It may not appear in the flat
        // _elements list because html/head/body are structural elements
        // pre-created by HtmlTreeBuilder.
        DomElement? body = null;
        if (htmlEl != null)
        {
            body = htmlEl.Children.FirstOrDefault(c =>
                string.Equals(c.TagName, "body", StringComparison.OrdinalIgnoreCase));
        }
        if (body == null) return;

        // Ensure the body's JS object is created so inline event attributes are compiled
        ToJSObject(body);

        // Dispatch a 'load' event on the body element. This covers inline
        // attributes, property-assigned handlers (document.body.onload = fn),
        // and addEventListener registrations using the same event path.
        try
        {
            var evt = _jsContext.Eval("(function() { var e = document.createEvent('Event'); e.initEvent('load', false, false); return e; })()") as JSObject;
            if (evt != null)
                DispatchEventOnElement(body, evt);
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.FireWindowLoadEvent",
                $"Error firing window load event: {ex.Message}", ex);
        }
    }

    private JSValue DispatchWindowEvent(string eventType, bool bubbles = false)
    {
        var evt = new JSObject();
        evt.FastAddValue((KeyString)"type", new JSString(eventType), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"bubbles", bubbles ? JSBoolean.True : JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        return DispatchWindowEvent(evt);
    }

    private JSValue DispatchWindowEvent(JSObject evt)
    {
        if (_jsContext == null || _windowJSObject == null)
            return JSBoolean.True;

        var eventType = evt[(KeyString)"type"]?.ToString() ?? "unknown";
        evt.FastAddValue((KeyString)"target", _windowJSObject, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"currentTarget", _windowJSObject, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"eventPhase", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);

        var stopped = false;
        var immediateStopped = false;
        var prevented = evt[(KeyString)"defaultPrevented"] is JSValue defaultPreventedValue &&
                        defaultPreventedValue.BooleanValue;
        var currentListenerPassive = false;
        var legacyCancelBubble = false;
        evt[(KeyString)"defaultPrevented"] = prevented ? JSBoolean.True : JSBoolean.False;
        evt.FastAddValue((KeyString)"stopPropagation",
            new JSFunction((in Arguments _) =>
            {
                stopped = true;
                legacyCancelBubble = true;
                return JSUndefined.Value;
            }, "stopPropagation", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"stopImmediatePropagation",
            new JSFunction((in Arguments _) =>
            {
                stopped = true;
                immediateStopped = true;
                legacyCancelBubble = true;
                return JSUndefined.Value;
            }, "stopImmediatePropagation", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"preventDefault",
            new JSFunction((in Arguments _) =>
            {
                var cancelable = evt[(KeyString)"cancelable"];
                if (!currentListenerPassive && cancelable != null && cancelable.BooleanValue)
                {
                    prevented = true;
                    evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
                }
                return JSUndefined.Value;
            }, "preventDefault", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddProperty(
            (KeyString)"cancelBubble",
            new JSFunction((in Arguments _) => legacyCancelBubble ? JSBoolean.True : JSBoolean.False, "get cancelBubble"),
            new JSFunction((in Arguments setArgs) =>
            {
                if (setArgs.Length > 0 && setArgs[0].BooleanValue)
                {
                    legacyCancelBubble = true;
                    stopped = true;
                }
                return JSUndefined.Value;
            }, "set cancelBubble"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
        evt.FastAddProperty(
            (KeyString)"returnValue",
            new JSFunction((in Arguments _) => prevented ? JSBoolean.False : JSBoolean.True, "get returnValue"),
            new JSFunction((in Arguments setArgs) =>
            {
                var cancelable = evt[(KeyString)"cancelable"];
                if (setArgs.Length > 0 &&
                    !setArgs[0].BooleanValue &&
                    !currentListenerPassive &&
                    cancelable != null &&
                    cancelable.BooleanValue)
                {
                    prevented = true;
                    evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
                }
                return JSUndefined.Value;
            }, "set returnValue"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
        evt.FastAddValue((KeyString)"composedPath",
            new JSFunction((in Arguments _) => new JSArray(_windowJSObject), "composedPath", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        if (_windowEventListeners.TryGetValue(eventType, out var listeners))
        {
            foreach (var registration in listeners.ToList())
            {
                if (stopped || immediateStopped)
                    break;

                currentListenerPassive = registration.Passive;
                InvokeEventListener(registration.Listener, evt, "DomBridge.window.dispatchEvent");
                currentListenerPassive = false;

                if (registration.Once)
                    listeners.Remove(registration);
            }
        }

        return prevented ? JSBoolean.False : JSBoolean.True;
    }

    private JSArray BuildWindowFramesArray()
    {
        var frames = new List<JSValue>();
        CollectWindowFrames(DocumentElement, frames);
        return new JSArray(frames.ToArray());
    }

    private void CollectWindowFrames(DomElement element, List<JSValue> frames)
    {
        foreach (var child in element.Children)
        {
            if (child.IsTextNode)
                continue;

            if (string.Equals(child.TagName, "iframe", StringComparison.OrdinalIgnoreCase))
            {
                var src = child.Attributes.TryGetValue("src", out var srcValue) ? srcValue : string.Empty;
                if (!IsCrossOrigin(src, _pageUrl))
                    frames.Add(GetOrCreateSubWindow(child));
            }

            if (!string.Equals(child.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase))
                CollectWindowFrames(child, frames);
        }
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

    private static readonly HashSet<string> SkippedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "head", "body", "title"
    };

    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase)
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
        _documentNode.Children.Clear();
        _serializationTransformsApplied = false;

        // Parse DOCTYPE from the HTML and add it as first child of _documentNode
        var doctype = ParseDocType(html);
        if (doctype != null)
        {
            doctype.Parent = _documentNode;
            _documentNode.Children.Add(doctype);
            _elements.Add(doctype);
        }

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

        // Copy attributes from the parsed <html> element to DocumentElement
        // so that attributes like lang="en", dir="rtl", etc. are preserved
        // during serialization.
        if (!string.IsNullOrEmpty(docElement.Id))
            DocumentElement.Id = docElement.Id;
        if (!string.IsNullOrEmpty(docElement.ClassName))
            DocumentElement.ClassName = docElement.ClassName;
        foreach (var kv in docElement.Attributes)
            DocumentElement.Attributes[kv.Key] = kv.Value;
        foreach (var kv in docElement.Style)
            DocumentElement.Style[kv.Key] = kv.Value;

        _elements.AddRange(allElements);

        // Connect DocumentElement to _documentNode so that document.firstChild works
        // and structural pseudo-classes correctly detect the document root boundary
        DocumentElement.Parent = _documentNode;
        if (!_documentNode.Children.Contains(DocumentElement))
            _documentNode.Children.Add(DocumentElement);

        // Ensure DocumentElement is in _elements so querySelector can find it
        if (!_elements.Contains(DocumentElement))
            _elements.Insert(0, DocumentElement);

        // Extract <style> blocks for getComputedStyle() resolution.
        // Note: We intentionally do NOT call ApplyCascadedStyles() here.
        // Merging CSS rules into element.Style would bake them into inline styles,
        // which persist even after JS changes element classes (e.g. Acid3 bucket
        // elements change from class="z" to "zPPPP..." but the .z { visibility:
        // hidden } would remain in their inline style). HtmlRenderer has its own
        // CSS engine that applies stylesheet rules at render time, so cascaded
        // styles are correctly resolved without pre-merging.
        ExtractStyleBlocks(html);
    }

    /// <summary>
    /// Parses all HTML attribute name-value pairs from an attribute string.
    /// Handles quoted values (<c>"…"</c> or <c>'…'</c>), unquoted values,
    /// and boolean attributes.
    /// </summary>
    private static Dictionary<string, string> ParseAttributes(string attrs)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
                    result.TryAdd(name, name);
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

            // HTML parsing keeps the first attribute with a given name and
            // ignores later duplicates on the same start tag.
            if (!string.IsNullOrEmpty(name))
                result.TryAdd(name, WebUtility.HtmlDecode(value));
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
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in SplitCssDeclarations(styleValue))
        {
            var colonIdx = declaration.IndexOf(':');
            if (colonIdx > 0)
            {
                var prop = declaration[..colonIdx].Trim();
                var val = declaration[(colonIdx + 1)..].Trim();
                if (!string.IsNullOrEmpty(prop) && IsAcceptableCssValue(prop, val))
                {
                    result[prop] = val;
                    // Map vendor-prefixed property to unprefixed equivalent (TODO-G9)
                    var unprefixed = StripVendorPrefix(prop);
                    if (unprefixed != prop && !result.ContainsKey(unprefixed))
                        result[unprefixed] = val;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Strips vendor prefixes (<c>-webkit-</c>, <c>-moz-</c>, <c>-ms-</c>, <c>-o-</c>)
    /// from a CSS property name, returning the unprefixed equivalent.
    /// Returns the original name unchanged if it has no vendor prefix.
    /// </summary>
    private static string StripVendorPrefix(string property)
    {
        if (property.StartsWith("-webkit-", StringComparison.OrdinalIgnoreCase))
            return property[8..];
        if (property.StartsWith("-moz-", StringComparison.OrdinalIgnoreCase))
            return property[5..];
        if (property.StartsWith("-ms-", StringComparison.OrdinalIgnoreCase))
            return property[4..];
        if (property.StartsWith("-o-", StringComparison.OrdinalIgnoreCase))
            return property[3..];
        return property;
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

        // Custom-property references are validated after cascade, not during
        // raw declaration parsing. Keep them so the later resolution step can
        // substitute them into closed-keyword properties too.
        if (v.IndexOf("var(", StringComparison.OrdinalIgnoreCase) >= 0) return true;

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
                // CSS Overflow Level 3: overflow can be one or two keywords
                // (e.g. "hidden scroll" sets overflow-x:hidden and overflow-y:scroll).
                foreach (var part in v.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (part is not ("visible" or "hidden" or "scroll" or "auto" or "clip"))
                        return false;
                }
                return true;

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
                    || v.StartsWith("url(", StringComparison.Ordinal);

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

            case "color":
            case "background-color":
            case "border-color":
            case "border-top-color":
            case "border-right-color":
            case "border-bottom-color":
            case "border-left-color":
            case "outline-color":
                // CSS color values: named colors, #hex, rgb(), rgba(),
                // hsl(), hsla(), transparent, currentcolor.
                // Reject unknown vendor-prefixed values (e.g. -acid3-bogus).
                return !v.StartsWith('-')
                    || v.StartsWith("-webkit-", StringComparison.Ordinal)
                    || v.StartsWith("-moz-", StringComparison.Ordinal)
                    || v.StartsWith("-ms-", StringComparison.Ordinal)
                    || v.StartsWith("-o-", StringComparison.Ordinal);

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
            if (v.EndsWith(unit, StringComparison.Ordinal) && v.Length > unit.Length)
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
    /// These must be preserved by <see cref="DomBridge.InvalidateElementStyles"/>
    /// when the cascade is recalculated after class changes.</summary>
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

    /// <summary>Maps (namespace, localName) → qualifiedName for namespace-aware attribute methods.</summary>
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
                    string.Equals(name, prop, StringComparison.Ordinal))
                    return _bridge.ToJSObject(ctrl);
            }
        }

        return JSNull.Value; // HTMLFormControlsCollection returns null for missing names
    }
}
