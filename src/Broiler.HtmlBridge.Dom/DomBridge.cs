using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;
using CanonicalDocument = Broiler.Dom.DomDocument;
using Broiler.HtmlBridge.Dom;
using Broiler.HtmlBridge.Logging;
using Broiler.HtmlBridge.Scripting;

namespace Broiler.HtmlBridge;

/// <summary>
/// Registers a minimal <c>document</c> object on a <see cref="JSContext"/>
/// so that JavaScript executed via YantraJS can perform basic DOM queries
/// against the current page HTML.
/// </summary>
public sealed partial class DomBridge : IDomBridgeRuntime
{
    /// <summary>
    /// Safety cap for draining bridge-backed microtask/timer work so promise/timer
    /// chains can settle without risking an infinite loop in test and capture paths.
    /// </summary>
    public const int AsyncDrainIterationLimit = 1000;

    // External resource fetches (stylesheets, iframe documents, JS fetch) block
    // the synchronous render/script pipeline.  In the sandboxed WPT/headless
    // environment external http(s) hosts are unreachable, so the only question is
    // how long each blocked fetch waits before failing — and the per-test budget
    // (Broiler.Wpt's default 30 s) equals this client's old 30 s timeout, so a
    // single unreachable stylesheet consumed the entire budget and timed the test
    // out (WPT #1147 Timeout cluster, e.g. CSS2/cascade-import-* which @link three
    // delayed-file CGI URLs).  A short timeout fails fast: even several sequential
    // external fetches stay well under the test budget, converting a 30 s hang
    // (which also risks aborting the whole shard) into a quick, deterministic miss.
    private const int FetchTimeoutSeconds = 5;
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(FetchTimeoutSeconds) };
    private static readonly string[] InlineEventNames = ["click", "load", "change", "input", "submit", "mousedown",
        "mouseup", "mouseover", "mouseout", "keydown", "keyup", "keypress", "focus", "blur", "error", "scroll",
        "scrollend"];
    private readonly HashSet<DomElement> _knownNodes =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<(JSObject Observer, DomElement Target, Broiler.Dom.DomMutationObserverOptions Options)> _mutationObservers = [];
    private readonly List<WeakReference<RangeState>> _activeRanges = [];
    private readonly List<WeakReference<Broiler.Dom.DomNodeIterator>> _activeNodeIterators = [];
    private readonly CanonicalDocument _document;
    private readonly DomElement _documentNode;
    private static readonly ConditionalWeakTable<DomElement, ElementRuntimeState> ElementRuntimeStates = [];
    private JSObject? _documentJSObject;
    private JSObject? _windowJSObject;
    private JSObject? _visualViewportJSObject;
    private readonly Dictionary<DomElement, JSObject> _docRootToDocJSObject = [];
    private JSContext? _jsContext;

    // Timer & async execution queues.
    //
    // These are read and cleared by the main-thread timer drain (FlushTimerStep)
    // but written by setTimeout/setInterval/requestAnimationFrame and scroll
    // callbacks that can run on ThreadPool threads — the JS engine dispatches
    // Promise/async/generator continuations via ThreadPool.QueueUserWorkItem, so
    // a continuation may register a timer concurrently with a flush. Plain
    // Dictionary/HashSet are not safe under that race (it surfaced as
    // "Collection was modified" / "Destination array is not long enough" aborting
    // FlushTimerStep), so use concurrent collections and atomic id counters.
    private int _timerIdCounter;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, JSFunction> _timeoutCallbacks = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, JSFunction> _intervalCallbacks = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, byte> _clearedTimerIds = new();
    private int _rafIdCounter;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, JSFunction> _rafCallbacks = new();
    private int _frameActionIdCounter;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, Action> _frameActions = new();
    // Touched by the same scroll/frame-action callbacks that can run on
    // ThreadPool threads (see the timer-map note above), so keep it concurrent.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<DomElement, int> _smoothScrollTokens = new();
    private readonly List<JSFunction> _visualViewportScrollListeners = [];
    private readonly Dictionary<string, List<EventListenerRegistration>> _windowEventListeners =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<JSObject, Dictionary<string, List<EventListenerRegistration>>> _eventTargetListeners =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<JSObject, JSObject> _eventTargetOwnerWindows =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<JSObject, DomElement> _subWindowContainers =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<JSObject, JSObject> _messagePortPeers =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<JSObject> _closedMessagePorts =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<JSObject> _startedMessagePorts =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<JSObject, List<JSObject>> _queuedMessagePortEvents =
        new(ReferenceEqualityComparer.Instance);
    private JSObject? _currentWindowOverride;
    private double _visualViewportScale = 1.0;
    private double _visualViewportPageLeftOffset;
    private double _visualViewportPageTopOffset;

    /// <summary>
    /// Index into the tree-derived <see cref="Elements"/> view of the
    /// <c>&lt;script&gt;</c> element
    /// that is currently executing.  Used by <c>document.write()</c> to insert
    /// content at the correct DOM position.  Set to &lt;0 when no script is
    /// running.
    /// </summary>
    internal int CurrentScriptIndex { get; set; } = -1;

    int IDomBridgeRuntime.CurrentScriptIndex
    {
        get => CurrentScriptIndex;
        set => CurrentScriptIndex = value;
    }

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

    public DomBridge()
    {
        _document = new CanonicalDocument();
        _documentNode = new DomElement(_document, "#document", null, null, string.Empty);
        DocumentElement = new DomElement(_document, "html", null, null, string.Empty);
        _document.AppendChild(_documentNode);
        _documentNode.Children.Add(DocumentElement);
    }

    /// <summary>
    /// The canonical document that owns every bridge-visible DOM node.
    /// </summary>
    public CanonicalDocument Document => _document;

    /// <summary>
    /// The current document title, kept in sync with JavaScript reads/writes.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// All elements parsed from the HTML source.
    /// </summary>
    public IReadOnlyList<DomElement> Elements =>
        [.. _documentNode
            .InclusiveDescendants()
            .OfType<DomElement>()
            .Where(element => !ReferenceEquals(element, _documentNode))];

    private static ElementRuntimeState GetElementRuntimeState(DomElement element) =>
        ElementRuntimeStates.GetValue(element, static _ => new ElementRuntimeState());

    /// <summary>
    /// The element's authoritative in-memory inline style dictionary (CSS kebab-case),
    /// relocated off the <c>DomElement</c> facade into <see cref="ElementRuntimeState"/>
    /// (RF-BRIDGE-1c Phase B). Lazily seeded once from the element's <c>style=</c>
    /// attribute; thereafter it is the source of truth (JS <c>element.style</c> writes,
    /// anchor/form-control styling), synced back to the attribute at serialization.
    /// </summary>
    private static Dictionary<string, string> InlineStyle(DomElement element)
    {
        var state = GetElementRuntimeState(element);
        if (!state.StyleSeeded)
        {
            state.StyleSeeded = true;
            var styleAttr = element.GetAttribute("style");
            if (!string.IsNullOrEmpty(styleAttr))
            {
                foreach (var kv in ParseStyle(styleAttr))
                    state.Style[kv.Key] = kv.Value;
            }
        }
        return state.Style;
    }

    private static Dictionary<string, List<EventListenerRegistration>> GetEventListeners(DomElement element) =>
        GetElementRuntimeState(element).EventListeners;

    private static Dictionary<string, JSValue> GetInlineEventHandlers(DomElement element) =>
        GetElementRuntimeState(element).InlineEventHandlers;

    internal bool TryGetStoredScrollOffset(DomElement element, bool vertical, out double offset)
    {
        var slot = vertical
            ? GetElementRuntimeState(element).Scroll.Top
            : GetElementRuntimeState(element).Scroll.Left;
        if (slot.TryGet(out var value) && value is double storedOffset)
        {
            offset = storedOffset;
            return true;
        }

        offset = 0;
        return false;
    }

    internal double? GetStoredScrollOffsetOrDefault(DomElement element, bool vertical) =>
        TryGetStoredScrollOffset(element, vertical, out var offset) ? offset : null;

    internal bool TryGetResolvedLayout(
        DomElement element,
        out double left,
        out double top,
        out double width,
        out double height)
    {
        // RF-BRIDGE-1b (Milestone 2.5): reads the memoized position-area resolution,
        // relocated from ElementRuntimeState.Layout to the bridge-level
        // PositionAreaResolutions cache. The four values are always set (and cleared)
        // together, so a cached entry is equivalent to the old "all four slots present".
        if (TryGetPositionAreaResolution(element, out var rect))
        {
            (left, top, width, height) = rect;
            return true;
        }

        (left, top, width, height) = (0, 0, 0, 0);
        return false;
    }

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
        foreach (var el in Elements)
        {
            if (el.IsTextNode || string.IsNullOrEmpty(el.Id))
                continue;
            // Only register if the global doesn't already exist
            // (user-defined globals take precedence — a `var`/`function` or a
            // lexical `const`/`let`/`class` with the same name shadows the named
            // element, per HTML "named access on the Window object"). The JS
            // engine reports the result as a JSBoolean whose ToString() is the
            // lowercase "true"/"false"; comparing against C#'s "True" never
            // matched, so this skip was a no-op and a same-named read-only
            // global lexical made the assignment below throw
            // "Cannot assign to read only variable", crashing the whole render.
            try
            {
                var existing = context.Eval(
                    $"typeof {el.Id} !== 'undefined'");
                if (existing != null && existing.BooleanValue)
                    continue;
            }
            catch
            {
                // If the id isn't a valid JS identifier, or resolving it throws
                // (e.g. a lexical binding still in its temporal dead zone), leave
                // the existing binding untouched.
                continue;
            }

            // Defensive: even past the guard, assigning could hit a read-only
            // binding in an edge case; a named-element convenience global must
            // never crash script execution.
            try
            {
                var jsObj = ToJSObject(el);
                context[el.Id] = jsObj;
            }
            catch (Exception ex)
            {
                RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.RegisterNamedElementGlobals",
                    $"Could not register named element global '{el.Id}': {ex.Message}", ex);
            }
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
    public bool HasPendingTimers => !_timeoutCallbacks.IsEmpty || !_intervalCallbacks.IsEmpty || !_rafCallbacks.IsEmpty || !_frameActions.IsEmpty;

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

        // ConcurrentDictionary.ToArray() takes a consistent snapshot even while
        // other threads register callbacks, and TryRemove drains only the entries
        // we actually collected — so a timer registered concurrently (or by a
        // callback that runs during this flush) is carried to the next step
        // instead of being wiped by a blanket Clear().

        // Collect timeout callbacks (one-shot: remove as collected)
        foreach (var kv in _timeoutCallbacks.ToArray())
        {
            if (_timeoutCallbacks.TryRemove(kv.Key, out var fn) && !_clearedTimerIds.ContainsKey(kv.Key))
                pending.Add((kv.Key, fn));
        }

        // Collect interval callbacks (execute once per step, keep registered)
        var intervalSnapshot = new List<(int Id, JSFunction Fn)>();
        foreach (var kv in _intervalCallbacks.ToArray())
        {
            if (!_clearedTimerIds.ContainsKey(kv.Key))
                intervalSnapshot.Add((kv.Key, kv.Value));
        }

        // Collect rAF callbacks (one-shot: remove as collected)
        var rafSnapshot = new List<(int Id, JSFunction Fn)>();
        foreach (var kv in _rafCallbacks.ToArray())
        {
            if (_rafCallbacks.TryRemove(kv.Key, out var fn))
                rafSnapshot.Add((kv.Key, fn));
        }

        var frameActionSnapshot = new List<Action>();
        foreach (var kv in _frameActions.ToArray())
        {
            if (_frameActions.TryRemove(kv.Key, out var action))
                frameActionSnapshot.Add(action);
        }

        if (pending.Count == 0 && intervalSnapshot.Count == 0 && rafSnapshot.Count == 0 && frameActionSnapshot.Count == 0)
            return false;

        // Execute timeout callbacks
        foreach (var (id, fn) in pending)
        {
            if (_clearedTimerIds.ContainsKey(id)) continue;
            try { fn.InvokeFunction(new Arguments(JSUndefined.Value)); }
            catch (Exception ex) { RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.FlushTimerStep", $"setTimeout callback error: {ex.Message}", ex); }
            finally { RunTaskCheckpoint(); }
        }

        // Execute interval callbacks (one tick per step)
        foreach (var (id, fn) in intervalSnapshot)
        {
            if (_clearedTimerIds.ContainsKey(id)) continue;
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

        var htmlEl = Elements.FirstOrDefault(e =>
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
  // A page may register the load handler either as `window.onload = fn`
  // or as a bare `onload = fn` assignment. In a browser `window` IS the
  // global object so both are the same property; in this engine the global
  // object and `window` are distinct, so a bare `onload = fn` lands on the
  // global (globalThis.onload) and never on window.onload. Check both, with
  // window.onload winning when present.
  var h = null;
  if (typeof window.onload === 'function') h = window.onload;
  else if (typeof onload === 'function') h = onload;
  if (h) {
    try { h(); } catch(e) {}
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
        // _knownNodes list because html/head/body are structural elements
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
            if (_jsContext.Eval("(function() { var e = document.createEvent('Event'); e.initEvent('load', false, false); return e; })()") is JSObject evt)
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
        evt[(KeyString)"srcElement"] = _windowJSObject;
        evt.FastAddValue((KeyString)"currentTarget", _windowJSObject, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"eventPhase", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);

        var immediateStopped = false;
        var prevented = evt[(KeyString)"defaultPrevented"] is JSValue defaultPreventedValue &&
                        defaultPreventedValue.BooleanValue;
        var currentListenerPassive = false;
        var legacyCancelBubble = false;
        evt[(KeyString)"defaultPrevented"] = prevented ? JSBoolean.True : JSBoolean.False;
        evt.FastAddValue((KeyString)"stopPropagation",
            new JSFunction((in _) => JsCallbackStopPropagation001Core(ref legacyCancelBubble, in _), "stopPropagation", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"stopImmediatePropagation",
            new JSFunction((in _) => JsCallbackStopImmediatePropagation002Core(ref immediateStopped, ref legacyCancelBubble, in _), "stopImmediatePropagation", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"preventDefault",
            new JSFunction((in _) => JsCallbackPreventDefault003Core(currentListenerPassive, evt, ref prevented, in _), "preventDefault", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddProperty(
            (KeyString)"cancelBubble",
            new JSFunction((in _) => legacyCancelBubble ? JSBoolean.True : JSBoolean.False, "get cancelBubble"),
            new JSFunction((in setArgs) => JsCallbackSetCancelBubble005Core(ref legacyCancelBubble, in setArgs), "set cancelBubble"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
        evt.FastAddProperty(
            (KeyString)"returnValue",
            new JSFunction((in _) => prevented ? JSBoolean.False : JSBoolean.True, "get returnValue"),
            new JSFunction((in setArgs) => JsCallbackSetReturnValue007Core(currentListenerPassive, evt, ref prevented, in setArgs), "set returnValue"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
        evt.FastAddValue((KeyString)"composedPath",
            new JSFunction((in _) => new JSArray(_windowJSObject), "composedPath", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        if (_windowEventListeners.TryGetValue(eventType, out var listeners))
        {
            foreach (var registration in listeners.ToList())
            {
                if (immediateStopped)
                    break;

                currentListenerPassive = registration.Passive;
                InvokeEventListener(registration.Listener, evt, "DomBridge.window.dispatchEvent");
                currentListenerPassive = false;

                if (registration.Once)
                    listeners.Remove(registration);
            }
        }

        evt[(KeyString)"currentTarget"] = JSNull.Value;
        evt[(KeyString)"eventPhase"] = new JSNumber(0);
        return prevented ? JSBoolean.False : JSBoolean.True;
    }

    private JSArray BuildWindowFramesArray()
    {
        var frames = new List<JSValue>();
        CollectWindowFrames(DocumentElement, frames);
        return new JSArray([.. frames]);
    }

    private void CollectWindowFrames(DomElement element, List<JSValue> frames)
    {
        foreach (var child in element.Children)
        {
            if (child.IsTextNode)
                continue;

            if (string.Equals(child.TagName, "iframe", StringComparison.OrdinalIgnoreCase))
            {
                var src = TryGetAttribute(child, "src", out var srcValue) ? srcValue : string.Empty;
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

    private static readonly Regex TitlePattern = TitlePatternRegex();

    private static readonly Regex OpenTagPattern = OpenTagPatternRegex();

    private static readonly HashSet<string> SkippedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "head", "body", "title"
    };

    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr"
    };

    private static readonly Regex IdPattern = IdPatternRegex();

    private static readonly Regex ClassPattern = ClassPatternRegex();

    private static readonly Regex AttributeSelectorPattern = AttributeSelectorPatternRegex();

    private static readonly Regex DocTypePattern = DocTypePatternRegex();

    private void ParseHtml(string html)
    {
        _knownNodes.Clear();
        _jsObjectCache.Clear();
        _computedPropsCache.Clear();
        _documentNode.Children.Clear();
        _serializationTransformsApplied = false;

        // Parse DOCTYPE from the HTML and add it as first child of _documentNode
        var doctype = ParseDocType(html);
        if (doctype != null)
        {
            doctype.Parent = _documentNode;
            _documentNode.Children.Add(doctype);
            _knownNodes.Add(doctype);
        }

        // Publish the document's quirks mode for the render that follows on this
        // thread. Layout (which on the HTML-string path holds no back-reference to
        // the document) reads it while sizing the root/body boxes for the
        // quirks-mode fill-viewport behaviour. Every WPT render runs through this
        // parse before laying out, so the flag is set for the render that matters.
        Layout.DocumentModeContext.CurrentQuirksMode =
            Layout.DocumentModeContext.IsQuirksHtml(html);

        // Use WHATWG-aligned tokeniser & tree builder
        var builder = new HtmlTreeBuilder();
        var (docElement, allElements, title) = builder.Build(html, _document);
        Title = title;
        DocumentElement.Children.Clear();
        foreach (var child in docElement.Children.ToArray())
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
        foreach (var attribute in docElement.Attributes.Values)
            SetAttr(DocumentElement, attribute.QualifiedName, attribute.Value);
        foreach (var kv in InlineStyle(docElement))
            InlineStyle(DocumentElement)[kv.Key] = kv.Value;

        _knownNodes.UnionWith(allElements);

        // Connect DocumentElement to _documentNode so that document.firstChild works
        // and structural pseudo-classes correctly detect the document root boundary
        DocumentElement.Parent = _documentNode;
        if (!_documentNode.Children.Contains(DocumentElement))
            _documentNode.Children.Add(DocumentElement);

        _knownNodes.Add(DocumentElement);

        // Stylesheet discovery is document-scoped and lazy through the shared
        // CssStyleEngine. A rebuilt document must not retain the prior engines.
        ResetComputedStyleEngines();
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
    /// <param name="reportDrops">
    /// When <c>true</c>, declarations rejected by
    /// <see cref="CSS.Dom.CssDeclarationValidator.IsAcceptableDeclarationValue"/>
    /// are surfaced through
    /// <see cref="CSS.Dom.CssEngineDiagnostics.DeclarationRejected"/>
    /// (diagnostic #1b). The bridge rewrites the serialized <c>style</c> attribute
    /// from the survivors of this filter (see <c>PrepareCanonicalDocumentForRendering</c>),
    /// so a dropped inline declaration vanishes before the renderer's own style engine
    /// can report it — this is the only place such drops are observable. Set it only at
    /// inline-style <em>ingestion</em> sites that write <c>InlineStyle(element)</c> (so the drop
    /// reaches the rendered output); leave it off for query/bookkeeping re-parses and for
    /// stylesheet-rule / descriptor parsing (cascade drops the style engine already reports).
    /// </param>
    private static Dictionary<string, string> ParseStyle(string styleValue, bool reportDrops = false)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var declarations = new CSS.CssParser().ParseDeclarations(styleValue);
        foreach (var declaration in declarations.Declarations)
        {
            var prop = declaration.Name;
            // Validate the importance-stripped value against the shared CSS.Dom
            // declaration table (the same closed-keyword error-recovery the cascade
            // uses), then re-attach the "!important" suffix the bridge-owned
            // declaration map carries as part of the string value.
            var rawValue = declaration.Value.Text;

            if (CSS.Dom.CssDeclarationValidator.IsAcceptableDeclarationValue(prop, rawValue))
            {
                var val = declaration.Important ? rawValue + " !important" : rawValue;
                result[prop] = val;
                // Map vendor-prefixed property to unprefixed equivalent (TODO-G9)
                var unprefixed = StripVendorPrefix(prop);
                if (unprefixed != prop && !result.ContainsKey(unprefixed))
                    result[unprefixed] = val;
            }
            else if (reportDrops)
            {
                // Report the raw value (without any synthetic " !important" suffix) so
                // inline drops aggregate identically to the engine's stylesheet drops.
                CSS.Dom.CssEngineDiagnostics.DeclarationRejected?.Invoke(prop, rawValue);
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

    [GeneratedRegex(@"<title[^>]*>(?<content>[\s\S]*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Compiled, "de-DE")]
    private static partial Regex TitlePatternRegex();
    [GeneratedRegex(@"<(?<tag>[a-zA-Z][a-zA-Z0-9]*)\b(?<attrs>[^>]*)\/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled, "de-DE")]
    private static partial Regex OpenTagPatternRegex();
    [GeneratedRegex(@"\bid\s*=\s*[""'](?<id>[^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled, "de-DE")]
    private static partial Regex IdPatternRegex();
    [GeneratedRegex(@"\bclass\s*=\s*[""'](?<cls>[^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled, "de-DE")]
    private static partial Regex ClassPatternRegex();
    [GeneratedRegex(@"\[(?<name>[a-zA-Z][a-zA-Z0-9_:-]*)(?:(?<op>[~|^$*]?=)(?<value>[""'][^""']*[""']|[^\]]*))?\]", RegexOptions.Compiled)]
    private static partial Regex AttributeSelectorPatternRegex();
    [GeneratedRegex(@"<!DOCTYPE\s+(\w+)(?:\s+PUBLIC\s+""([^""]*)""(?:\s+""([^""]*)"")?)?\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled, "de-DE")]
    private static partial Regex DocTypePatternRegex();
}

/// <summary>
/// Custom JSObject subclass for HTMLFormControlsCollection that returns null
/// (not undefined) for named property lookups that don't match any control.
/// </summary>
internal sealed class FormElementsCollection(DomElement form, DomBridge bridge) : JSObject()
{
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
            var controls = DomBridge.CollectFormControls(form);
            foreach (var ctrl in controls)
            {
                if (ctrl.GetAttribute("name") is { } name &&
                    string.Equals(name, prop, StringComparison.Ordinal))
                    return bridge.ToJSObject(ctrl);
            }
        }

        return JSNull.Value; // HTMLFormControlsCollection returns null for missing names
    }
}
