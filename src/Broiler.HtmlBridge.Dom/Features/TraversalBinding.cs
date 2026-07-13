using System.Text;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The DOM traversal / Range feature binding — <c>TreeWalker</c>, <c>NodeIterator</c>,
/// <c>Range</c>, the node-filter machinery and <c>document.createComment</c>. This is the first
/// co-located feature module of the HtmlBridge complexity-reduction roadmap Phase 3: the
/// registration for the feature and every handler that implements it now live together in one file
/// with semantic names, reachable and testable without loading the whole <c>DomBridge</c>
/// implementation. The module owns the traversal-scoped state (the weak active-range and
/// active-node-iterator registries) and depends only on the narrow <see cref="ITraversalHost"/>
/// contract plus the assembly's neutral static DOM-tree helpers on <c>DomBridge</c> (which Phase 4
/// promotes to <c>Broiler.Dom</c>).
/// </summary>
internal sealed partial class TraversalBinding(ITraversalHost host)
{
    private readonly ITraversalHost _host = host;

    // Live Range boundary stores and NodeIterator pointers, held weakly so a script-abandoned range
    // or iterator stays GC-collectable. The bridge drives boundary adjustment through
    // NotifyNodeRemoved (ranges are constructed non-tracking so they do not self-subscribe to the
    // document mutation event).
    private readonly List<WeakReference<DomRange>> _activeRanges = [];
    private readonly List<WeakReference<DomNodeIterator>> _activeNodeIterators = [];

    // -------- Registration --------

    /// <summary>
    /// Installs the traversal surface on a document object: the <c>NodeFilter</c> constants plus
    /// <c>createTreeWalker</c>, <c>createNodeIterator</c>, <c>createRange</c> and
    /// <c>createComment</c>.
    /// </summary>
    internal void RegisterDocumentApis(JSContext context, JSObject document)
    {
        // NodeFilter constants
        var nodeFilter = new JSObject();
        nodeFilter.FastAddValue((KeyString)"FILTER_ACCEPT", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"FILTER_REJECT", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"FILTER_SKIP", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ALL", new JSNumber(0xFFFFFFFF), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ELEMENT", new JSNumber(0x1), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ATTRIBUTE", new JSNumber(0x2), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_TEXT", new JSNumber(0x4), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_CDATA_SECTION", new JSNumber(0x8), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ENTITY_REFERENCE", new JSNumber(0x10), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_ENTITY", new JSNumber(0x20), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_PROCESSING_INSTRUCTION", new JSNumber(0x40), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_COMMENT", new JSNumber(0x80), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT", new JSNumber(0x100), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT_TYPE", new JSNumber(0x200), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_DOCUMENT_FRAGMENT", new JSNumber(0x400), JSPropertyAttributes.EnumerableConfigurableValue);
        nodeFilter.FastAddValue((KeyString)"SHOW_NOTATION", new JSNumber(0x800), JSPropertyAttributes.EnumerableConfigurableValue);
        context["NodeFilter"] = nodeFilter;

        // document.createTreeWalker(root, whatToShow, filter)
        document.FastAddValue(
            (KeyString)"createTreeWalker",
            new JSFunction((in a) => CreateTreeWalker(in a), "createTreeWalker", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.createNodeIterator(root, whatToShow, filter)
        document.FastAddValue(
            (KeyString)"createNodeIterator",
            new JSFunction((in a) => CreateNodeIterator(in a), "createNodeIterator", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.createRange()
        document.FastAddValue(
            (KeyString)"createRange",
            new JSFunction((in _) => BuildRange(), "createRange", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // document.createComment(data)
        document.FastAddValue(
            (KeyString)"createComment",
            new JSFunction((in a) => CreateComment(in a), "createComment", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
    }

    private JSValue CreateTreeWalker(in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'createTreeWalker': 1 argument required.");
        if (a[0] is not JSObject rootObj)
            throw new JSException("Failed to execute 'createTreeWalker': parameter 1 is not of type 'Node'.");
        var rootEl = _host.FindDomElementByJSObject(rootObj);
        if (rootEl == null)
            return JSNull.Value;
        var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? unchecked((int)(uint)a[1].DoubleValue) : unchecked((int)0xFFFFFFFF);
        var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);
        return BuildTreeWalker(rootEl, whatToShow, filterFn);
    }

    private JSValue CreateNodeIterator(in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'createNodeIterator': 1 argument required.");
        if (a[0] is not JSObject rootObj)
            throw new JSException("Failed to execute 'createNodeIterator': parameter 1 is not of type 'Node'.");
        var rootEl = _host.FindDomElementByJSObject(rootObj);
        if (rootEl == null)
            return JSNull.Value;
        var whatToShow = a.Length > 1 && !a[1].IsNull && !a[1].IsUndefined ? unchecked((int)(uint)a[1].DoubleValue) : unchecked((int)0xFFFFFFFF);
        var filterFn = a.Length > 2 && a[2] is JSFunction f ? f : (a.Length > 2 && a[2] is JSObject filterObj ? filterObj[(KeyString)"acceptNode"] as JSFunction : null);
        return BuildNodeIterator(rootEl, whatToShow, filterFn);
    }

    private JSValue CreateComment(in Arguments a)
    {
        var data = a.Length > 0 ? a[0].ToString() : string.Empty;
        return _host.CreateCommentNode(data);
    }

    // -------- TreeWalker / NodeIterator / Range builders --------

    /// <summary>
    /// Returns <c>1</c> (ACCEPT), <c>2</c> (REJECT) or <c>3</c> (SKIP) for <paramref name="el"/>
    /// against the <paramref name="whatToShow"/> bitmask and the optional
    /// <paramref name="filterFn"/>.
    /// </summary>
    private int ApplyFilter(DomNode el, int whatToShow, JSFunction? filterFn)
    {
        var nodeType = DomBridge.GetNodeType(el);
        var showBit = nodeType switch
        {
            1 => 0x1,    // SHOW_ELEMENT
            3 => 0x4,    // SHOW_TEXT
            8 => 0x80,   // SHOW_COMMENT
            9 => 0x100,  // SHOW_DOCUMENT
            11 => 0x400, // SHOW_DOCUMENT_FRAGMENT
            _ => 0x0
        };
        if ((whatToShow & showBit) == 0) return 3; // FILTER_SKIP

        if (filterFn != null)
        {
            // Per DOM Level 2 Traversal spec, exceptions thrown by NodeFilter
            // callbacks must propagate to the caller — they must NOT be swallowed.
            var result = filterFn.InvokeFunction(new Arguments(filterFn, _host.ToJSObject(el)));
            // Handle boolean return: true → 1 (ACCEPT), false → 2 (REJECT)
            if (result.IsBoolean)
                return result.BooleanValue ? 1 : 2;
            return (int)result.DoubleValue;
        }
        return 1; // FILTER_ACCEPT
    }

    /// <summary>Builds a DOM <c>TreeWalker</c> object.</summary>
    internal JSObject BuildTreeWalker(DomElement root, int whatToShow, JSFunction? filterFn)
    {
        var tw = new JSObject();
        var walker = new DomTreeWalker(root,
            (DomWhatToShow)(uint)whatToShow,
            node => (DomFilterResult)ApplyFilter(node, whatToShow, filterFn));

        tw.FastAddValue((KeyString)"root",
            _host.ToJSObject(root),
            JSPropertyAttributes.EnumerableConfigurableValue);

        tw.FastAddProperty((KeyString)"currentNode",
            new JSFunction((in a) => _host.ToJSObject(walker.CurrentNode), "get currentNode"),
            new JSFunction((in a) =>
            {
                if (a.Length > 0 && a[0] is JSObject nodeObject &&
                    _host.FindDomNodeByJSObject(nodeObject) is { } node)
                {
                    walker.CurrentNode = node;
                }
                return JSUndefined.Value;
            }, "set currentNode"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        tw.FastAddValue((KeyString)"whatToShow",
            new JSNumber(whatToShow),
            JSPropertyAttributes.EnumerableConfigurableValue);

        tw.FastAddValue((KeyString)"parentNode",
            new JSFunction((in a) => ToTraversalJsValue(walker.ParentNode()), "parentNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        tw.FastAddValue((KeyString)"firstChild",
            new JSFunction((in a) => ToTraversalJsValue(walker.FirstChild()), "firstChild", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        tw.FastAddValue((KeyString)"lastChild",
            new JSFunction((in a) => ToTraversalJsValue(walker.LastChild()), "lastChild", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        tw.FastAddValue((KeyString)"nextSibling",
            new JSFunction((in a) => ToTraversalJsValue(walker.NextSibling()), "nextSibling", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        tw.FastAddValue((KeyString)"previousSibling",
            new JSFunction((in a) => ToTraversalJsValue(walker.PreviousSibling()), "previousSibling", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // nextNode() — depth-first pre-order traversal forward
        tw.FastAddValue((KeyString)"nextNode",
            new JSFunction((in a) => ToTraversalJsValue(walker.NextNode()), "nextNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // previousNode() — depth-first pre-order traversal backward
        tw.FastAddValue((KeyString)"previousNode",
            new JSFunction((in a) => ToTraversalJsValue(walker.PreviousNode()), "previousNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return tw;
    }

    // RF-BRIDGE-1c Phase F (F3c part 2c): a TreeWalker/NodeIterator result may be a text/comment
    // node (SHOW_TEXT/SHOW_COMMENT), so convert any non-null node — not just elements — to its JS
    // wrapper.
    private JSValue ToTraversalJsValue(DomNode? node) => node is not null ? _host.ToJSObject(node) : JSNull.Value;

    /// <summary>Builds a DOM <c>NodeIterator</c> object.</summary>
    internal JSObject BuildNodeIterator(DomElement root, int whatToShow, JSFunction? filterFn)
    {
        var iter = new JSObject();
        var iterator = new DomNodeIterator(root,
            (DomWhatToShow)(uint)whatToShow,
            node => (DomFilterResult)ApplyFilter(node, whatToShow, filterFn));
        _activeNodeIterators.Add(new WeakReference<DomNodeIterator>(iterator));

        iter.FastAddValue((KeyString)"root",
            _host.ToJSObject(root),
            JSPropertyAttributes.EnumerableConfigurableValue);

        iter.FastAddValue((KeyString)"whatToShow",
            new JSNumber(whatToShow),
            JSPropertyAttributes.EnumerableConfigurableValue);

        iter.FastAddProperty((KeyString)"referenceNode",
            new JSFunction((in a) => ToTraversalJsValue(iterator.ReferenceNode), "get referenceNode"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        iter.FastAddProperty((KeyString)"pointerBeforeReferenceNode",
            new JSFunction((in a) => iterator.PointerBeforeReferenceNode ? JSBoolean.True : JSBoolean.False, "get pointerBeforeReferenceNode"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        iter.FastAddValue((KeyString)"nextNode",
            new JSFunction((in a) => ToTraversalJsValue(iterator.NextNode()), "nextNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        iter.FastAddValue((KeyString)"previousNode",
            new JSFunction((in a) => ToTraversalJsValue(iterator.PreviousNode()), "previousNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        iter.FastAddValue((KeyString)"detach",
            new JSFunction((in a) =>
            {
                iterator.Dispose();
                return JSUndefined.Value;
            }, "detach", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return iter;
    }

    /// <summary>
    /// Builds a DOM <c>Range</c> object. The <paramref name="documentRoot"/> is the document node
    /// that owns this range (main or sub-document); defaults to the main document root.
    /// </summary>
    internal JSObject BuildRange(DomElement? documentRoot = null)
    {
        var range = new JSObject();
        var docRoot = documentRoot ?? _host.DocumentNode;
        var state = new BridgeDomRange(_host, docRoot);

        // Register this range for mutation tracking. The range is non-tracking (it does not
        // subscribe to the document mutation event), so a script-abandoned range stays weakly held
        // here and is GC-collectable; NotifyNodeRemoved drives its adjustment.
        _activeRanges.Add(new WeakReference<DomRange>(state));

        range.FastAddProperty((KeyString)"startContainer",
            new JSFunction((in a) => _host.ToJSObject(state.StartContainer), "get startContainer"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        range.FastAddProperty((KeyString)"startOffset",
            new JSFunction((in a) => new JSNumber(state.StartOffset), "get startOffset"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        range.FastAddProperty((KeyString)"endContainer",
            new JSFunction((in a) => _host.ToJSObject(state.EndContainer), "get endContainer"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        range.FastAddProperty((KeyString)"endOffset",
            new JSFunction((in a) => new JSNumber(state.EndOffset), "get endOffset"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        range.FastAddProperty((KeyString)"collapsed",
            new JSFunction((in a) => state.Collapsed ? JSBoolean.True : JSBoolean.False, "get collapsed"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        range.FastAddProperty((KeyString)"commonAncestorContainer",
            new JSFunction((in a) => RangeGetCommonAncestorContainer(state, in a), "get commonAncestorContainer"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        range.FastAddValue((KeyString)"getBoundingClientRect",
            new JSFunction((in _) => RangeGetBoundingClientRect(state, in _), "getBoundingClientRect", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"getClientRects",
            new JSFunction((in _) => RangeGetClientRects(state, in _), "getClientRects", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"setStart",
            new JSFunction((in a) => RangeSetStart(state, in a), "setStart", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"setEnd",
            new JSFunction((in a) => RangeSetEnd(state, in a), "setEnd", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"setStartBefore",
            new JSFunction((in a) => RangeSetStartBefore(state, in a), "setStartBefore", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"setStartAfter",
            new JSFunction((in a) => RangeSetStartAfter(state, in a), "setStartAfter", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"setEndBefore",
            new JSFunction((in a) => RangeSetEndBefore(state, in a), "setEndBefore", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"setEndAfter",
            new JSFunction((in a) => RangeSetEndAfter(state, in a), "setEndAfter", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"collapse",
            new JSFunction((in a) => RangeCollapse(state, in a), "collapse", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"selectNode",
            new JSFunction((in a) => RangeSelectNode(state, in a), "selectNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"selectNodeContents",
            new JSFunction((in a) => RangeSelectNodeContents(state, in a), "selectNodeContents", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"cloneContents",
            new JSFunction((in a) => RangeCloneContents(state, in a), "cloneContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"extractContents",
            new JSFunction((in a) => RangeExtractContents(state, in a), "extractContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"deleteContents",
            new JSFunction((in a) => RangeDeleteContents(state, in a), "deleteContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"insertNode",
            new JSFunction((in a) => RangeInsertNode(state, in a), "insertNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"surroundContents",
            new JSFunction((in a) => RangeSurroundContents(state, in a), "surroundContents", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"cloneRange",
            new JSFunction((in a) => RangeCloneRange(state, in a), "cloneRange", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"compareBoundaryPoints",
            new JSFunction((in a) => RangeCompareBoundaryPoints(state, in a), "compareBoundaryPoints", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"toString",
            new JSFunction((in a) => RangeToString(state, in a), "toString", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Range comparison constants
        range.FastAddValue((KeyString)"START_TO_START", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"START_TO_END", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"END_TO_END", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"END_TO_START", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);

        return range;
    }

    // -------- Active range / iterator registry (bridge-driven mutation adjustment) --------

    /// <summary>
    /// Notifies all active ranges that a child was removed from <paramref name="parent"/> at the
    /// given <paramref name="index"/>, and prunes GC'd range references. Called by the bridge's
    /// mutation path.
    /// </summary>
    internal void NotifyNodeRemoved(DomElement parent, DomNode removedChild, int index)
    {
        for (var i = _activeRanges.Count - 1; i >= 0; i--)
        {
            if (_activeRanges[i].TryGetTarget(out var state))
                state.NotifyNodeRemoved(parent, removedChild, index);
            else
                _activeRanges.RemoveAt(i); // GC'd — prune
        }
    }

    /// <summary>
    /// Prunes GC'd node-iterator references. Executes the NodeIterator pre-removing bookkeeping per
    /// DOM §7.2; called by the bridge BEFORE a node is removed from the tree.
    /// </summary>
    internal void PruneDeadNodeIterators()
    {
        for (var i = _activeNodeIterators.Count - 1; i >= 0; i--)
        {
            if (_activeNodeIterators[i].TryGetTarget(out _))
                continue;
            else
                _activeNodeIterators.RemoveAt(i); // GC'd — prune
        }
    }

    /// <summary>Drops all active range and node-iterator registrations (session reset/dispose).</summary>
    internal void ClearActive()
    {
        _activeRanges.Clear();
        _activeNodeIterators.Clear();
    }

    /// <summary>
    /// The bridge's live <c>Range</c> boundary store and content-operation engine — the canonical
    /// <see cref="DomRange"/> with the node-creation seams overridden so content operations mint
    /// bridge nodes through <see cref="ITraversalHost"/>: <c>#document-fragment</c> result
    /// fragments and clones that carry host runtime state, all registered so the host's
    /// <c>ToJSObject</c> can wrap them. Constructed <c>trackMutations: false</c> — the bridge drives
    /// boundary adjustment from its mutation path via the weak active-range registry, so the range
    /// must not also self-subscribe to the document mutation event.
    /// </summary>
    private sealed class BridgeDomRange(ITraversalHost host, DomNode root)
        : DomRange(root, trackMutations: false)
    {
        protected override DomNode CreateResultFragment() => host.CreateRangeResultFragment();

        protected override DomNode CloneForRange(DomNode node, bool deep) => host.CloneRangeNode(node, deep);

        protected override DomText CreateTextForRange(string data) => host.CreateRangeTextNode(data);

        protected override DomRange CreateSubRange(DomNode root) => new BridgeDomRange(host, root);
    }
}
