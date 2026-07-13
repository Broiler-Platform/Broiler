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
using Broiler.HtmlBridge.Dom;
using Broiler.HtmlBridge.Logging;
using Broiler.HtmlBridge.Scripting;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.Dom;
using Broiler.CSS.Dom;
using Broiler.CSS;

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

    // P2.6: sub-resource HTTP and the local base path now live in ResourceLoader, the single host
    // resource loader (was the static SharedHttpClient plus the _localBasePath field). Feature
    // callbacks ask the loader instead of reaching a static HttpClient.
    private readonly Dom.Runtime.ResourceLoader _resources = new();
    private static readonly string[] InlineEventNames = ["click", "load", "change", "input", "submit", "mousedown",
        "mouseup", "mouseover", "mouseout", "keydown", "keyup", "keypress", "focus", "blur", "error", "scroll",
        "scrollend"];
    // RF-BRIDGE-1c Phase F (F3b): the registration set is keyed by canonical DomNode so
    // it can hold text/comment nodes once they flip to canonical DomText/DomComment (which
    // are not Broiler.Dom.DomElement). A facade node IS-A DomNode, so this is a behaviour-preserving
    // widen on the current homogeneous tree.
    private readonly HashSet<DomNode> _knownNodes =
        new(ReferenceEqualityComparer.Instance);
    // Phase 3 (P3.2): the whole MutationObserver feature — the observer registry (P2.5
    // MutationObserverHub), the observe()/disconnect() registration and childList/attribute/
    // characterData record delivery — lives in the MutationObserverBinding module, reached through
    // the narrow IMutationObserverHost contract (see DomBridge.MutationObserverHost.cs).
    private readonly Dom.Features.MutationObserverBinding _mutations;
    // Phase 3 (P3.3): the DOM event dispatch engine — capture/target/bubble propagation, the event
    // object's propagation-control methods and composedPath() — lives in EventDispatchBinding,
    // reached through the narrow IEventDispatchHost contract (see DomBridge.EventDispatchHost.cs).
    private readonly Dom.Features.EventDispatchBinding _eventDispatch;
    // Phase 3 (P3.5): the HTML table DOM interfaces (HTMLTableElement / HTMLTableSectionElement /
    // HTMLTableRowElement) live in TableBinding, reached through the narrow ITableHost contract
    // (see DomBridge.TableHost.cs).
    private readonly Dom.Features.TableBinding _tables;
    // Phase 3 (first feature-module slice): TreeWalker/NodeIterator/Range construction, every Range
    // callback and the traversal-scoped active-range / active-node-iterator registries live in the
    // co-located TraversalBinding module. The bridge holds the module through the narrow
    // ITraversalHost contract it implements (see DomBridge.TraversalHost.cs).
    private readonly Dom.Features.TraversalBinding _traversal;
    private readonly DomDocument _document;
    private readonly DomElement _documentNode;
    private static readonly ConditionalWeakTable<DomNode, ElementRuntimeState> ElementRuntimeStates = [];
    private JSObject? _documentJSObject;
    private JSObject? _windowJSObject;
    private JSObject? _visualViewportJSObject;
    private JSContext? _jsContext;

    // P2.4: the timer/interval/requestAnimationFrame/frame-action queues, their id counters and the
    // drain (FlushTimerStep/FlushTimers) now live in BrowserEventLoop, the single owner of the
    // document's task queues (was the eight scattered _timerIdCounter/_timeoutCallbacks/… fields).
    private readonly Dom.Runtime.BrowserEventLoop _eventLoop = new();
    // Smooth-scroll continuation tokens: a per-element monotonic marker so a queued smooth-scroll
    // frame action only commits if it is still the active scroll for that element. Touched by
    // scroll/frame-action callbacks that can run on ThreadPool threads, so keep it concurrent.
    private int _smoothScrollTokenCounter;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<DomElement, int> _smoothScrollTokens = new();
    // P2.5: the event-listener stores (per-node addEventListener listeners, window listeners,
    // generic JS-target listeners, target→owner-window map, and visual-viewport scroll listeners)
    // now live in EventTargetRegistry, the single listener owner (was the scattered
    // _windowEventListeners/_eventTargetListeners/_eventTargetOwnerWindows/_visualViewportScrollListeners
    // fields plus ElementRuntimeState.EventListeners for node listeners).
    private readonly Dom.Runtime.EventTargetRegistry _eventTargets = new();
    private readonly Dictionary<JSObject, DomElement> _subWindowContainers = new(ReferenceEqualityComparer.Instance);
    // P2.6: MessageChannel/MessagePort state (peers, closed/started marks, queued messages) now lives
    // in MessagePortRegistry, the single owner of the browsing-context port state (was the scattered
    // _messagePortPeers/_closedMessagePorts/_startedMessagePorts/_queuedMessagePortEvents fields).
    private readonly Dom.Runtime.MessagePortRegistry _messagePorts = new();
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
        _traversal = new Dom.Features.TraversalBinding(this);
        _mutations = new Dom.Features.MutationObserverBinding(this);
        _eventDispatch = new Dom.Features.EventDispatchBinding(this);
        _tables = new Dom.Features.TableBinding(this);
        _document = new DomDocument();
        _documentNode = CreateBridgeElement("#document");
        DocumentElement = CreateBridgeElement("html");
        _document.AppendChild(_documentNode);
        _documentNode.AppendChild(DocumentElement);
    }

    /// <summary>
    /// The canonical document that owns every bridge-visible DOM node.
    /// </summary>
    public DomDocument Document => _document;

    /// <summary>
    /// The current document title, kept in sync with JavaScript reads/writes.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// All elements parsed from the HTML source.
    /// </summary>
    public IReadOnlyList<DomElement> Elements =>
        [.. _documentNode.InclusiveDescendants().OfType<DomElement>().Where(element => !ReferenceEquals(element, _documentNode))];

    private static ElementRuntimeState GetElementRuntimeState(DomNode node) =>
        ElementRuntimeStates.GetValue(node, static _ => new ElementRuntimeState());

    /// <summary>
    /// The element's authoritative in-memory inline style dictionary (CSS kebab-case),
    /// relocated off the <c>Broiler.Dom.DomElement</c> facade into <see cref="ElementRuntimeState"/>
    /// (RF-BRIDGE-1c Phase B). Lazily seeded once from the element's <c>style=</c>
    /// attribute; thereafter it is the source of truth (JS <c>element.style</c> writes,
    /// anchor/form-control styling), synced back to the attribute at serialization.
    /// </summary>
    // -----------------------------------------------------------------
    // RF-BRIDGE-1c Phase E2: child-node access over canonical ChildNodes,
    // replacing the facade Broiler.Dom.DomElement.Children (LegacyChildList). The bridge
    // tree is homogeneous Broiler.Dom.DomElement today, so ChildElements is a drop-in for
    // the old enumeration; the Cast/ChildAt casts are safe until text/comment
    // flip to canonical DomText/DomComment (Phase F), when ChildElements narrows
    // to OfType and callers gain IsText handling.
    // -----------------------------------------------------------------

    /// <summary>The element's <see cref="DomElement"/> children. RF-BRIDGE-1c Phase F (F3c part 2c):
    /// narrowed from <c>Cast</c> to <c>OfType&lt;Broiler.Dom.DomElement&gt;()</c> so it skips canonical
    /// <c>DomText</c>/<c>DomComment</c> children once construction flips; a no-op on today's
    /// homogeneous tree. Callers that need text/comment children walk raw <c>ChildNodes</c> instead.</summary>
    internal static IEnumerable<DomElement> ChildElements(DomNode element) =>
        element.ChildNodes.OfType<DomElement>();

    /// <summary>The child node at <paramref name="index"/> (old <c>Children[index]</c>). RF-BRIDGE-1c
    /// Phase F (F3c part 2c): returns canonical <see cref="DomNode"/> — a child may be a
    /// <c>DomText</c>/<c>DomComment</c> once construction flips. Element-only callers narrow with
    /// <c>as Broiler.Dom.DomElement</c>/<c>is Broiler.Dom.DomElement</c>; on today's homogeneous tree every child is an element.</summary>
    internal static DomNode ChildAt(DomNode element, int index) => element.ChildNodes[index];

    /// <summary>The child node at <paramref name="index"/>, supporting from-end indices like <c>^1</c>
    /// (old <c>Children[^1]</c>); canonical <c>ChildNodes</c> is an <c>IReadOnlyList</c> with no
    /// from-end indexer.</summary>
    private static DomNode ChildAt(DomNode element, Index index) =>
        element.ChildNodes[index.GetOffset(element.ChildNodes.Count)];

    /// <summary>Index of <paramref name="child"/> among the element's children, or -1
    /// (old <c>Children.IndexOf</c>, reference equality).</summary>
    internal static int ChildIndexOf(DomNode element, DomNode child)
    {
        for (var i = 0; i < element.ChildNodes.Count; i++)
        {
            if (ReferenceEquals(element.ChildNodes[i], child))
                return i;
        }

        return -1;
    }

    // RF-BRIDGE-1c Phase F (F3c part 2b): the child-mutation helpers take a DomNode parent so
    // range-extract code (whose ancestor-chain clones are DomNode-typed) can reparent without
    // casts. At runtime the parent is always an element; canonical AppendChild/InsertBefore/
    // RemoveChild enforce nothing text-specific, so this is a safe widen.

    /// <summary>Old <c>Children.Insert(index, child)</c>.</summary>
    internal static void InsertChildAt(DomNode parent, int index, DomNode child)
    {
        var reference = index < parent.ChildNodes.Count ? parent.ChildNodes[index] : null;
        parent.InsertBefore(child, reference);
    }

    /// <summary>Old <c>Children.Remove(child)</c> — removes only if actually a child; returns success.</summary>
    internal static bool RemoveChildFrom(DomNode parent, DomNode child)
    {
        if (!ReferenceEquals(child.ParentNode, parent))
            return false;

        parent.RemoveChild(child);
        return true;
    }

    /// <summary>Old raw <c>Children.RemoveAt(index)</c> (no mutation notifications — matches the
    /// LegacyChildList primitive; distinct from the notifying <c>RemoveChildAt</c> helper).</summary>
    private static void RemoveNthChild(DomNode parent, int index) => parent.RemoveChild(parent.ChildNodes[index]);

    /// <summary>Old <c>Children.Clear()</c>.</summary>
    private static void ClearChildren(DomNode parent)
    {
        foreach (var child in parent.ChildNodes.ToArray())
            parent.RemoveChild(child);
    }

    /// <summary>Whether <paramref name="node"/> is a text node (RF-BRIDGE-1c Phase D: replaces
    /// the facade <c>IsText(Broiler.Dom.DomElement)</c>). NodeType-based, so it holds for the current
    /// facade text nodes and for canonical <c>DomText</c> once construction flips in the
    /// <c>Children</c>/text cutover.</summary>
    internal static bool IsText(DomNode node) => node.NodeType == DomNodeType.Text;

    /// <summary>Whether <paramref name="node"/> is a comment node (RF-BRIDGE-1c Phase F).
    /// NodeType-based, so it holds for the current facade comment nodes and for canonical
    /// <c>DomComment</c> once construction flips — the replacement for the many
    /// <c>TagName == "#comment"</c> checks, since a canonical <c>DomComment</c> has no
    /// <c>TagName</c>.</summary>
    internal static bool IsComment(DomNode node) => node.NodeType == DomNodeType.Comment;

    /// <summary>Reads a text/comment node's character data (RF-BRIDGE-1c Phase F). Canonical
    /// <c>DomText</c>/<c>DomComment</c> expose it as <c>Data</c>; the facade text/comment nodes
    /// (pre-flip) expose it as <c>TextContent</c>. The single accessor both models funnel through
    /// during the text cutover; returns <c>""</c> (never null) for character-data nodes.</summary>
    internal static string BridgeText(DomNode node) => node switch
    {
        DomCharacterData characterData => characterData.Data,
        _ => node.NodeValue ?? string.Empty,
    };

    /// <summary>Writes a text/comment node's character data (see <see cref="BridgeText"/>).</summary>
    private static void SetBridgeText(DomNode node, string value)
    {
        if (node is DomCharacterData characterData)
            characterData.Data = value;
    }

    /// <summary>Mints a canonical <see cref="DomText"/> carrying <paramref name="data"/>
    /// (RF-BRIDGE-1c Phase F, F3c part 2d — construction cutover). The single funnel for text-node
    /// construction; callers treat the result as a <see cref="DomNode"/>.</summary>
    private DomText CreateBridgeTextNode(string data) => _document.CreateTextNode(data);

    /// <summary>Mints a canonical <see cref="DomComment"/> carrying <paramref name="data"/>
    /// (see <see cref="CreateBridgeTextNode"/>).</summary>
    private DomComment CreateBridgeCommentNode(string data) => _document.CreateComment(data);

    /// <summary>
    /// The single construction funnel for bridge element nodes (RF-BRIDGE-1c Phase F, F4). Every
    /// former <c>new Broiler.Dom.DomElement(...)</c> site routes through here (or <see cref="CreateBridgeElementNS"/>)
    /// so element construction lives in exactly one place over the canonical <c>Broiler.Dom</c>
    /// document factories. The tag name may be an HTML element literal (HTML namespace) or a
    /// <c>#</c>-sentinel (<c>#document</c>, <c>#subdoc-root</c>, …), which keeps a null namespace and
    /// its preserved name — the bridge-internal document/fragment/shadow model over canonical types.
    /// </summary>
    private DomElement CreateBridgeElement(string tagName, string? id = null, string? className = null, Dictionary<string, string>? attributes = null) =>
        // A leading '#' marks a bridge sentinel (document/fragment/shadow/doctype root): null
        // namespace, name preserved verbatim. Every other tag is an HTML element. CreateElementNS
        // preserves the given name's case exactly as the old facade ctor did (no ToLowerInvariant).
        CreateBridgeElementNS(tagName.StartsWith('#') ? null : DomNamespaces.Html, tagName, id, className, attributes);

    /// <summary>
    /// Element construction with an explicit namespace used verbatim (may be <c>null</c>), for the
    /// <c>createElementNS</c> handlers, sub-document roots, and clones (which preserve the source
    /// element's namespace). See <see cref="CreateBridgeElement"/>.
    /// </summary>
    private DomElement CreateBridgeElementNS(string? namespaceUri, string tagName, string? id = null, string? className = null, Dictionary<string, string>? attributes = null)
    {
        var element = _document.CreateElementNS(namespaceUri, tagName);
        if (attributes is not null)
            foreach (var (name, value) in attributes)
                element.SetAttribute(name, value);
        if (id is not null)
            element.Id = id;
        if (className is not null)
            element.ClassName = className;
        return element;
    }

    /// <summary>Sets an element's <c>textContent</c> per DOM (RF-BRIDGE-1c Phase F, F3c part 2d):
    /// replaces all children with a single canonical <see cref="DomText"/> (or none when
    /// <paramref name="value"/> is null/empty). Replaces the former element-store
    /// <c>Broiler.Dom.DomElement.TextContent</c> scalar.</summary>
    private void SetElementTextContent(DomElement element, string? value)
    {
        ClearChildren(element);
        if (!string.IsNullOrEmpty(value))
        {
            var textNode = CreateBridgeTextNode(value);
            _knownNodes.Add(textNode);
            element.AppendChild(textNode);
        }
    }

    /// <summary>An element's <c>textContent</c> — the concatenation of its descendant text (RF-BRIDGE-1c
    /// Phase F, F3c part 2d). Replaces reads of the former element-store <c>Broiler.Dom.DomElement.TextContent</c>.</summary>
    private static string GetElementTextContent(DomElement element)
    {
        var sb = new System.Text.StringBuilder();
        CollectTextContent(element, sb);
        return sb.ToString();
    }

    /// <summary>The element's parent as a <see cref="DomElement"/> (RF-BRIDGE-1c Phase E:
    /// replaces the facade <c>ParentEl(Broiler.Dom.DomElement)</c> getter — <c>ParentNode as Broiler.Dom.DomElement</c>).
    /// A node's parent is always an element, so this is stable when text/comment nodes become
    /// canonical <c>DomText</c>/<c>DomComment</c> in Phase D.</summary>
    internal static DomElement? ParentEl(DomNode node) => node.ParentNode as DomElement;

    /// <summary>Reparents <paramref name="child"/> under <paramref name="parent"/> (RF-BRIDGE-1c
    /// Phase E: replaces the facade <c>ParentEl(Broiler.Dom.DomElement)</c> setter). A null parent detaches;
    /// otherwise the child is appended if not already there — matching the old setter exactly.
    /// RF-BRIDGE-1c Phase F (F3c part 2b): the parent widened to <c>DomNode?</c> so range-extract
    /// code can pass DomNode-typed ancestor-chain clones (always elements at runtime).</summary>
    internal static void SetParent(DomNode child, DomNode? parent)
    {
        if (parent is null)
            child.Remove();
        else if (!ReferenceEquals(child.ParentNode, parent))
            parent.AppendChild(child);
    }

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

    /// <summary>Read-only diagnostic view of an element's resolved inline-style map — the same
    /// dictionary the anchor resolver reads and writes (display:none, resolved left/top/width/height,
    /// …). RF-BRIDGE-1c Phase F4 removed the <c>Broiler.Dom.DomElement.Style</c> facade member; internal test and
    /// tooling callers that need to inspect post-resolution inline styles route through this accessor
    /// instead. Visible only to <c>InternalsVisibleTo</c> assemblies — not part of the public surface,
    /// so it does not re-open a public facade seam.</summary>
    internal static IReadOnlyDictionary<string, string> GetInlineStyleView(DomElement element) =>
        InlineStyle(element);

    /// <summary>Parity-test hook (DOM/CSS promotion §2.1): the bridge's own sparse
    /// computed-style projection. Paired with <see cref="GetSparseComputedStyleForParity"/>
    /// (the canonical engine's candidate replacement over the SAME synced engine) so a
    /// differential test can measure how close the canonical projection is before the
    /// higher-risk swap of the ~98 <c>GetComputedProps</c> call sites. Visible only to
    /// <c>InternalsVisibleTo</c> assemblies — not a public seam.</summary>
    internal Dictionary<string, string> GetComputedPropsForParity(DomElement element) =>
        GetComputedProps(element);

    /// <summary>Parity-test hook (DOM/CSS promotion §2.1): the canonical engine's
    /// <c>CssStyleEngine.GetSparseComputedStyle</c> over the element's synced scoped engine —
    /// the candidate replacement for <see cref="GetComputedPropsForParity"/>.</summary>
    internal IReadOnlyDictionary<string, string> GetSparseComputedStyleForParity(DomElement element) =>
        GetSyncedScopedEngine(element).GetSparseComputedStyle(element, sparseInheritance: true);

    private Dictionary<string, List<EventListenerRegistration>> GetEventListeners(DomNode element) =>
        _eventTargets.NodeListeners(element);

    private static Dictionary<string, JSValue> GetInlineEventHandlers(DomNode element) =>
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
        ThrowIfDisposed();
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
        ThrowIfDisposed();
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
            if (IsText(el) || string.IsNullOrEmpty(el.Id))
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
    /// Sets a local base directory for resolving relative sub-resource URLs.
    /// When set, sub-resource URLs (e.g. iframe src) are first looked up as files
    /// relative to this directory before falling back to HTTP fetch.
    /// </summary>
    public void SetLocalBasePath(string basePath) => _resources.LocalBasePath = basePath;

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
        ThrowIfDisposed();
        _eventLoop.DrainAll(TaskCheckpointCallback);
    }

    /// <summary>
    /// Returns <c>true</c> when there are queued <c>setTimeout</c>,
    /// <c>setInterval</c>, or <c>requestAnimationFrame</c> callbacks
    /// waiting to execute.
    /// </summary>
    public bool HasPendingTimers
    {
        get
        {
            ThrowIfDisposed();
            return _eventLoop.HasPendingWork;
        }
    }

    /// <summary>
    /// Executes one batch of pending timer and animation-frame callbacks.
    /// Returns <c>true</c> if callbacks were executed (more may be pending);
    /// <c>false</c> if there was nothing to run.
    /// Used by interactive rendering to step through animations one frame at
    /// a time so that intermediate visual states are displayed.
    /// </summary>
    public bool FlushTimerStep()
    {
        ThrowIfDisposed();
        return _eventLoop.DrainStep(TaskCheckpointCallback);
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
        ThrowIfDisposed();
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
            body = ChildElements(htmlEl).FirstOrDefault(c =>
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

    private JSBoolean DispatchWindowEvent(string eventType, bool bubbles = false)
    {
        var evt = new JSObject();
        evt.FastAddValue((KeyString)"type", new JSString(eventType), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"bubbles", bubbles ? JSBoolean.True : JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        return DispatchWindowEvent(evt);
    }

    private JSBoolean DispatchWindowEvent(JSObject evt)
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

        if (_eventTargets.TryGetWindowListeners(eventType, out var listeners))
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
        foreach (var child in ChildElements(element))
        {
            if (IsText(child))
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

    private static readonly Regex DocTypePattern = DocTypePatternRegex();

    private void ParseHtml(string html)
    {
        _knownNodes.Clear();
        // P2.2: one call clears both wrapper maps. Re-parse now also releases stale sub-document
        // wrappers (keyed by detached roots that no lookup can reach again) — observably
        // equivalent to before, but it stops them lingering until disposal.
        _jsObjects.Clear();
        ClearComputedPropsCache();
        ClearChildren(_documentNode);
        _serializationTransformsApplied = false;
        // A re-parse is a new document generation: drop the prior document's timers, listeners,
        // observers and message ports so re-attaching leaves no state from the previous document
        // (HtmlBridge complexity-reduction roadmap Phase 2, P2.1).
        ClearRuntimeSessionState();
        // A re-parsed document is a new generation: release the prior document's headless
        // layout view (and its renderer container) so geometry is document-scoped.
        DisposeLayoutView();

        // Parse DOCTYPE from the HTML and add it as first child of _documentNode
        var doctype = ParseDocType(html);
        if (doctype != null)
        {
            SetParent(doctype, _documentNode);
            _documentNode.AppendChild(doctype);
            _knownNodes.Add(doctype);
        }

        // Publish the document's quirks mode for the render that follows on this
        // thread. Layout (which on the HTML-string path holds no back-reference to
        // the document) reads it while sizing the root/body boxes for the
        // quirks-mode fill-viewport behaviour. Every WPT render runs through this
        // parse before laying out, so the flag is set for the render that matters.
        Layout.DocumentModeContext.CurrentQuirksMode =
            Layout.DocumentModeContext.IsQuirksHtml(html);

        // Use WHATWG-aligned tokeniser & tree builder (shared HtmlDocumentParser).
        var (docElement, allElements, title) = BuildDocumentTree(html);
        Title = title;
        ClearChildren(DocumentElement);
        // RF-BRIDGE-1c Phase F (F3c part 2d): reparent ALL children (raw ChildNodes) so any
        // text/comment nodes directly under the parsed <html> survive — no-op on the old
        // homogeneous tree where every child was an element.
        foreach (var child in docElement.ChildNodes.ToArray())
        {
            SetParent(child, DocumentElement);
            DocumentElement.AppendChild(child);
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
        SetParent(DocumentElement, _documentNode);
        if (!_documentNode.ChildNodes.Contains(DocumentElement))
            _documentNode.AppendChild(DocumentElement);

        _knownNodes.Add(DocumentElement);

        // Stylesheet discovery is document-scoped and lazy through the shared
        // CssStyleEngine. A rebuilt document must not retain the prior engines.
        ResetComputedStyleEngines();
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

            if (CssDeclarationValidator.IsAcceptableDeclarationValue(prop, rawValue))
            {
                var val = declaration.Important ? rawValue + " !important" : rawValue;
                result[prop] = val;
                // Map vendor-prefixed property to unprefixed equivalent (TODO-G9)
                var unprefixed = CssPropertyNames.StripVendorPrefix(prop);
                if (unprefixed != prop && !result.ContainsKey(unprefixed))
                    result[unprefixed] = val;
            }
            else if (reportDrops)
            {
                // Report the raw value (without any synthetic " !important" suffix) so
                // inline drops aggregate identically to the engine's stylesheet drops.
                CssEngineDiagnostics.DeclarationRejected?.Invoke(prop, rawValue);
            }
        }
        return result;
    }

    /// <summary>
    /// Whether <paramref name="value"/> is an acceptable declared value for
    /// <paramref name="property"/> per the shared <see cref="CSS.Dom.CssDeclarationValidator"/> —
    /// the same closed-keyword error-recovery the inline-style <em>attribute</em> path
    /// (<see cref="ParseStyle"/>) applies. A live <c>CSSStyleDeclaration</c> per-property setter
    /// (<c>el.style.color = …</c>, <c>setProperty(…)</c>, <c>cssFloat = …</c>) must <em>reject</em>
    /// an invalid value rather than store it, matching the attribute path (where
    /// <c>el.style = "color: bogus"</c> already drops the declaration) and CSSOM error handling.
    /// The value may carry a trailing <c>!important</c>, which is stripped before validation;
    /// unknown and custom (<c>--*</c>) properties are always accepted (the validator's default).
    /// </summary>
    private static bool IsAcceptableInlineValue(string property, string value) =>
        CssDeclarationValidator.IsAcceptableDeclarationValue(property, CssPriority.Strip(value));

    [GeneratedRegex(@"<!DOCTYPE\s+(\w+)(?:\s+PUBLIC\s+""([^""]*)""(?:\s+""([^""]*)"")?)?\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
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
