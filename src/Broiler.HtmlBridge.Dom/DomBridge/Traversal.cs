using Broiler.JavaScript.BuiltIns.Null;
using System.Text;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

/// <summary>
/// DOM traversal APIs — <c>TreeWalker</c>, <c>NodeIterator</c>,
/// <c>Range</c>, and the node-filter machinery.
/// </summary>
public sealed partial class DomBridge
{
    // -------- Phase 2: TreeWalker, NodeIterator, Range builders --------

    /// <summary>
    /// Returns <c>true</c> if the node type of <paramref name="el"/> matches
    /// the <paramref name="whatToShow"/> bitmask, and the optional
    /// <paramref name="filterFn"/> accepts the node.
    /// </summary>
    private int ApplyFilter(Broiler.Dom.DomNode el, int whatToShow, JSFunction? filterFn)
    {
        var nodeType = GetNodeType(el);
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
            var result = filterFn.InvokeFunction(new Arguments(filterFn, ToJSObject(el)));
            // Handle boolean return: true → 1 (ACCEPT), false → 2 (REJECT)
            if (result.IsBoolean)
                return result.BooleanValue ? 1 : 2;
            return (int)result.DoubleValue;
        }
        return 1; // FILTER_ACCEPT
    }

    /// <summary>
    /// Builds a DOM <c>TreeWalker</c> object.
    /// </summary>
    private JSObject BuildTreeWalker(DomElement root, int whatToShow, JSFunction? filterFn)
    {
        var tw = new JSObject();
        var walker = new Broiler.Dom.DomTreeWalker(
            root,
            (Broiler.Dom.DomWhatToShow)(uint)whatToShow,
            node => (Broiler.Dom.DomFilterResult)ApplyFilter(node, whatToShow, filterFn));

        tw.FastAddValue(
            (KeyString)"root",
            ToJSObject(root),
            JSPropertyAttributes.EnumerableConfigurableValue);

        tw.FastAddProperty(
            (KeyString)"currentNode",
            new JSFunction((in Arguments a) => ToJSObject(walker.CurrentNode), "get currentNode"),
            new JSFunction((in Arguments a) =>
            {
                if (a.Length > 0 && a[0] is JSObject nodeObject &&
                    FindDomNodeByJSObject(nodeObject) is { } node)
                {
                    walker.CurrentNode = node;
                }
                return JSUndefined.Value;
            }, "set currentNode"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        tw.FastAddValue(
            (KeyString)"whatToShow",
            new JSNumber(whatToShow),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // parentNode()
        tw.FastAddValue(
            (KeyString)"parentNode",
            new JSFunction((in Arguments a) => ToTraversalJsValue(walker.ParentNode()), "parentNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // firstChild()
        tw.FastAddValue(
            (KeyString)"firstChild",
            new JSFunction((in Arguments a) => ToTraversalJsValue(walker.FirstChild()), "firstChild", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // lastChild()
        tw.FastAddValue(
            (KeyString)"lastChild",
            new JSFunction((in Arguments a) => ToTraversalJsValue(walker.LastChild()), "lastChild", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // nextSibling()
        tw.FastAddValue(
            (KeyString)"nextSibling",
            new JSFunction((in Arguments a) => ToTraversalJsValue(walker.NextSibling()), "nextSibling", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // previousSibling()
        tw.FastAddValue(
            (KeyString)"previousSibling",
            new JSFunction((in Arguments a) => ToTraversalJsValue(walker.PreviousSibling()), "previousSibling", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // nextNode() — depth-first pre-order traversal forward
        tw.FastAddValue(
            (KeyString)"nextNode",
            new JSFunction((in Arguments a) => ToTraversalJsValue(walker.NextNode()), "nextNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // previousNode() — depth-first pre-order traversal backward
        tw.FastAddValue(
            (KeyString)"previousNode",
            new JSFunction((in Arguments a) => ToTraversalJsValue(walker.PreviousNode()), "previousNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return tw;
    }

    // RF-BRIDGE-1c Phase F (F3c part 2c): a TreeWalker/NodeIterator result may be a text/comment
    // node (SHOW_TEXT/SHOW_COMMENT), so convert any non-null node — not just elements — to its JS
    // wrapper. Behaviour-preserving today: walker results over the homogeneous facade tree are all
    // DomElement (text/comment are facade elements); forward-correct once they flip to canonical.
    private JSValue ToTraversalJsValue(Broiler.Dom.DomNode? node) =>
        node is not null ? ToJSObject(node) : JSNull.Value;

    /// <summary>Helper: get next sibling or ancestor's next sibling, skipping subtree.</summary>
    private static Broiler.Dom.DomNode? GetNextSkippingChildren(Broiler.Dom.DomNode node, Broiler.Dom.DomNode root)
    {
        Broiler.Dom.DomNode? current = node;
        while (current != null && !ReferenceEquals(current, root))
        {
            var parent = current.ParentNode;
            if (parent != null)
            {
                var idx = ChildIndexOf(parent, current);
                if (idx >= 0 && idx + 1 < parent.ChildNodes.Count)
                    return parent.ChildNodes[idx + 1];
                current = parent;
            }
            else
                return null;
        }
        return null;
    }

    /// <summary>
    /// TreeWalker helper: traverse to first/last child.
    /// </summary>
    private JSValue TreeWalkerTraverseChildren(Broiler.Dom.DomNode node, bool first, Broiler.Dom.DomNode root, int whatToShow, JSFunction? filterFn, ref Broiler.Dom.DomNode currentNode)
    {
        if (node.ChildNodes.Count == 0) return JSNull.Value;
        Broiler.Dom.DomNode? child = first ? ChildAt(node, 0) : ChildAt(node, ^1);
        while (child != null)
        {
            var result = ApplyFilter(child, whatToShow, filterFn);
            if (result == 1) { currentNode = child; return ToJSObject(child); }
            if (result == 3 && child.ChildNodes.Count > 0) // SKIP — descend
            {
                child = first ? ChildAt(child, 0) : ChildAt(child, ^1);
                continue;
            }
            // REJECT or SKIP with no children — next/previous sibling
            child = GetSiblingInDirection(child, first, node);
        }
        return JSNull.Value;
    }

    /// <summary>
    /// TreeWalker helper: traverse to next/previous sibling.
    /// </summary>
    private JSValue TreeWalkerTraverseSiblings(Broiler.Dom.DomNode node, bool next, Broiler.Dom.DomNode root, int whatToShow, JSFunction? filterFn, ref Broiler.Dom.DomNode currentNode)
    {
        var sibling = node;
        while (true)
        {
            var parent = sibling.ParentNode;
            if (parent == null || ReferenceEquals(sibling, root)) return JSNull.Value;
            var idx = ChildIndexOf(parent, sibling);
            Broiler.Dom.DomNode? target = next ? (idx + 1 < parent.ChildNodes.Count ? parent.ChildNodes[idx + 1] : null) : (idx > 0 ? parent.ChildNodes[idx - 1] : null);
            if (target != null)
            {
                var result = ApplyFilter(target, whatToShow, filterFn);
                if (result == 1) { currentNode = target; return ToJSObject(target); }
                if (result == 3 && target.ChildNodes.Count > 0) // SKIP — try children
                {
                    var child = TreeWalkerTraverseChildren(target, next, root, whatToShow, filterFn, ref currentNode);
                    if (!child.IsNull) return child;
                }
                sibling = target;
                continue;
            }
            // No more siblings — move up (DOM spec steps 3.3-3.5)
            if (parent == null || ReferenceEquals(parent, root)) return JSNull.Value;
            sibling = parent;
            // Per spec: if filter accepts parent, return null
            // (the parent is a "real" node, so don't skip over it)
            var parentResult = ApplyFilter(sibling, whatToShow, filterFn);
            if (parentResult == 1) return JSNull.Value; // FILTER_ACCEPT
        }
    }

    /// <summary>Helper: get next/previous sibling, or null if past boundaries.</summary>
    private static Broiler.Dom.DomNode? GetSiblingInDirection(Broiler.Dom.DomNode node, bool forward, Broiler.Dom.DomNode boundary)
    {
        var parent = node.ParentNode;
        if (parent == null || ReferenceEquals(node, boundary)) return null;
        var idx = ChildIndexOf(parent, node);
        if (forward && idx + 1 < parent.ChildNodes.Count) return parent.ChildNodes[idx + 1];
        if (!forward && idx > 0) return parent.ChildNodes[idx - 1];
        return null;
    }

    /// <summary>
    /// Builds a DOM <c>NodeIterator</c> object.
    /// </summary>
    private JSObject BuildNodeIterator(DomElement root, int whatToShow, JSFunction? filterFn)
    {
        var iter = new JSObject();
        var iterator = new Broiler.Dom.DomNodeIterator(
            root,
            (Broiler.Dom.DomWhatToShow)(uint)whatToShow,
            node => (Broiler.Dom.DomFilterResult)ApplyFilter(node, whatToShow, filterFn));
        _activeNodeIterators.Add(new WeakReference<Broiler.Dom.DomNodeIterator>(iterator));

        iter.FastAddValue(
            (KeyString)"root",
            ToJSObject(root),
            JSPropertyAttributes.EnumerableConfigurableValue);

        iter.FastAddValue(
            (KeyString)"whatToShow",
            new JSNumber(whatToShow),
            JSPropertyAttributes.EnumerableConfigurableValue);

        iter.FastAddProperty(
            (KeyString)"referenceNode",
            new JSFunction((in Arguments a) => ToTraversalJsValue(iterator.ReferenceNode), "get referenceNode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        iter.FastAddProperty(
            (KeyString)"pointerBeforeReferenceNode",
            new JSFunction((in Arguments a) => iterator.PointerBeforeReferenceNode ? JSBoolean.True : JSBoolean.False, "get pointerBeforeReferenceNode"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextNode()
        iter.FastAddValue(
            (KeyString)"nextNode",
            new JSFunction((in Arguments a) => ToTraversalJsValue(iterator.NextNode()), "nextNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // previousNode()
        iter.FastAddValue(
            (KeyString)"previousNode",
            new JSFunction((in Arguments a) => ToTraversalJsValue(iterator.PreviousNode()), "previousNode", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // detach()
        iter.FastAddValue(
            (KeyString)"detach",
            new JSFunction((in Arguments a) =>
            {
                iterator.Dispose();
                return JSUndefined.Value;
            }, "detach", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return iter;
    }

    /// <summary>
    /// Builds a DOM <c>Range</c> object. The <paramref name="documentRoot"/>
    /// is the document node that owns this range (main or sub-document).
    /// </summary>
    private JSObject BuildRange(DomElement? documentRoot = null)
    {
        var range = new JSObject();
        var docRoot = documentRoot ?? _documentNode;
        var state = new RangeState(docRoot);
        var bridge = this;

        // Register this range for mutation tracking
        _activeRanges.Add(new WeakReference<RangeState>(state));

        // startContainer
        range.FastAddProperty(
            (KeyString)"startContainer",
            new JSFunction((in Arguments a) => ToJSObject(state.StartContainer), "get startContainer"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // startOffset
        range.FastAddProperty(
            (KeyString)"startOffset",
            new JSFunction((in Arguments a) => new JSNumber(state.StartOffset), "get startOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // endContainer
        range.FastAddProperty(
            (KeyString)"endContainer",
            new JSFunction((in Arguments a) => ToJSObject(state.EndContainer), "get endContainer"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // endOffset
        range.FastAddProperty(
            (KeyString)"endOffset",
            new JSFunction((in Arguments a) => new JSNumber(state.EndOffset), "get endOffset"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // collapsed
        range.FastAddProperty(
            (KeyString)"collapsed",
            new JSFunction((in Arguments a) => state.Collapsed ? JSBoolean.True : JSBoolean.False, "get collapsed"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // commonAncestorContainer
        range.FastAddProperty(
            (KeyString)"commonAncestorContainer",
            new JSFunction((in Arguments a) => JsTraversalGetCommonAncestorContainer020Core(state, in a), "get commonAncestorContainer"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // getBoundingClientRect()
        range.FastAddValue(
            (KeyString)"getBoundingClientRect",
            new JSFunction((in Arguments _) => JsTraversalGetBoundingClientRect021Core(bridge, state, in _), "getBoundingClientRect", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getClientRects()
        range.FastAddValue(
            (KeyString)"getClientRects",
            new JSFunction((in Arguments _) => JsTraversalGetClientRects022Core(bridge, state, in _), "getClientRects", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setStart(node, offset)
        range.FastAddValue(
            (KeyString)"setStart",
            new JSFunction((in Arguments a) => JsTraversalSetStart023Core(bridge, state, in a), "setStart", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setEnd(node, offset)
        range.FastAddValue(
            (KeyString)"setEnd",
            new JSFunction((in Arguments a) => JsTraversalSetEnd024Core(bridge, state, in a), "setEnd", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setStartBefore(node)
        range.FastAddValue(
            (KeyString)"setStartBefore",
            new JSFunction((in Arguments a) => JsTraversalSetStartBefore025Core(bridge, state, in a), "setStartBefore", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setStartAfter(node)
        range.FastAddValue(
            (KeyString)"setStartAfter",
            new JSFunction((in Arguments a) => JsTraversalSetStartAfter026Core(bridge, state, in a), "setStartAfter", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setEndBefore(node)
        range.FastAddValue(
            (KeyString)"setEndBefore",
            new JSFunction((in Arguments a) => JsTraversalSetEndBefore027Core(bridge, state, in a), "setEndBefore", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // setEndAfter(node)
        range.FastAddValue(
            (KeyString)"setEndAfter",
            new JSFunction((in Arguments a) => JsTraversalSetEndAfter028Core(bridge, state, in a), "setEndAfter", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // collapse(toStart)
        range.FastAddValue(
            (KeyString)"collapse",
            new JSFunction((in Arguments a) => JsTraversalCollapse029Core(state, in a), "collapse", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // selectNode(node)
        range.FastAddValue(
            (KeyString)"selectNode",
            new JSFunction((in Arguments a) => JsTraversalSelectNode030Core(bridge, state, in a), "selectNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // selectNodeContents(node)
        range.FastAddValue(
            (KeyString)"selectNodeContents",
            new JSFunction((in Arguments a) => JsTraversalSelectNodeContents031Core(bridge, state, in a), "selectNodeContents", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneContents() — returns a document fragment with cloned nodes
        range.FastAddValue(
            (KeyString)"cloneContents",
            new JSFunction((in Arguments a) => JsTraversalCloneContents032Core(bridge, state, in a), "cloneContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // extractContents() — removes nodes from DOM and returns in a fragment
        range.FastAddValue(
            (KeyString)"extractContents",
            new JSFunction((in Arguments a) => JsTraversalExtractContents033Core(bridge, state, in a), "extractContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // deleteContents() — removes all nodes in the range
        range.FastAddValue(
            (KeyString)"deleteContents",
            new JSFunction((in Arguments a) => JsTraversalDeleteContents034Core(state, in a), "deleteContents", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // insertNode(node)
        range.FastAddValue(
            (KeyString)"insertNode",
            new JSFunction((in Arguments a) => JsTraversalInsertNode035Core(bridge, state, in a), "insertNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // surroundContents(newParent)
        range.FastAddValue(
            (KeyString)"surroundContents",
            new JSFunction((in Arguments a) => JsTraversalSurroundContents036Core(bridge, state, in a), "surroundContents", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneRange()
        range.FastAddValue(
            (KeyString)"cloneRange",
            new JSFunction((in Arguments a) => JsTraversalCloneRange037Core(bridge, state, in a), "cloneRange", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // compareBoundaryPoints(how, sourceRange)
        range.FastAddValue(
            (KeyString)"compareBoundaryPoints",
            new JSFunction((in Arguments a) => JsTraversalCompareBoundaryPoints038Core(bridge, state, in a), "compareBoundaryPoints", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // toString() — returns text content of the range
        range.FastAddValue(
            (KeyString)"toString",
            new JSFunction((in Arguments a) => JsTraversalToString039Core(state, in a), "toString", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Range comparison constants
        range.FastAddValue((KeyString)"START_TO_START", new JSNumber(0), JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"START_TO_END", new JSNumber(1), JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"END_TO_END", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);
        range.FastAddValue((KeyString)"END_TO_START", new JSNumber(3), JSPropertyAttributes.EnumerableConfigurableValue);

        return range;
    }

    /// <summary>
    /// Finds the common ancestor of two nodes.
    /// </summary>
    private static Broiler.Dom.DomNode? FindCommonAncestor(Broiler.Dom.DomNode a, Broiler.Dom.DomNode b)
    {
        var ancestors = new HashSet<Broiler.Dom.DomNode>(ReferenceEqualityComparer.Instance);
        Broiler.Dom.DomNode? current = a;
        while (current != null)
        {
            ancestors.Add(current);
            current = current.ParentNode;
        }
        current = b;
        while (current != null)
        {
            if (ancestors.Contains(current)) return current;
            current = current.ParentNode;
        }
        return null;
    }

    /// <summary>
    /// Returns the list of top-level nodes fully contained within the specified range boundaries.
    /// For element containers, this returns children between the start and end offsets.
    /// </summary>
    private static List<Broiler.Dom.DomNode> GetNodesInRange(Broiler.Dom.DomNode startContainer, int startOffset, Broiler.Dom.DomNode endContainer, int endOffset)
    {
        var result = new List<Broiler.Dom.DomNode>();
        if (ReferenceEquals(startContainer, endContainer))
        {
            // Same container — return children between offsets
            for (var i = startOffset; i < Math.Min(endOffset, startContainer.ChildNodes.Count); i++)
                result.Add(ChildAt(startContainer, i));
            return result;
        }

        // Different containers — collect nodes between start and end
        var ancestor = FindCommonAncestor(startContainer, endContainer);
        if (ancestor == null) return result;

        var allNodes = GetDocumentOrderNodes(ancestor);
        var startIdx = allNodes.IndexOf(startContainer);
        var endIdx = allNodes.IndexOf(endContainer);
        if (startIdx < 0 || endIdx < 0) return result;

        for (var i = startIdx + 1; i < endIdx; i++)
        {
            var node = allNodes[i];
            // Only include top-level nodes (not descendants of already-included nodes)
            var isDescendantOfIncluded = result.Any(r => IsDescendant(r, node));
            if (!isDescendantOfIncluded)
                result.Add(node);
        }
        return result;
    }

    /// <summary>
    /// Collects text content from a range that spans across nodes.
    /// Handles start/end offset boundaries properly for text nodes.
    /// </summary>
    private static void CollectRangeText(StringBuilder sb, Broiler.Dom.DomNode startContainer, int startOffset, Broiler.Dom.DomNode endContainer, int endOffset)
    {
        if (ReferenceEquals(startContainer, endContainer))
        {
            // Same container
            if (IsText(startContainer))
            {
                var text = BridgeText(startContainer);
                var s = Math.Max(0, Math.Min(startOffset, text.Length));
                var e = Math.Max(s, Math.Min(endOffset, text.Length));
                sb.Append(text, s, e - s);
            }
            else
            {
                // Element container — collect text from children between offsets
                for (var i = startOffset; i < Math.Min(endOffset, startContainer.ChildNodes.Count); i++)
                    CollectTextContent(ChildAt(startContainer, i), sb);
            }
            return;
        }

        // Start container: collect from startOffset to end
        if (IsText(startContainer))
        {
            var text = BridgeText(startContainer);
            if (startOffset < text.Length)
                sb.Append(text.Substring(startOffset));
        }
        else
        {
            for (var i = startOffset; i < startContainer.ChildNodes.Count; i++)
                CollectTextContent(ChildAt(startContainer, i), sb);
        }

        // Middle nodes: collect all text from nodes between start and end paths
        var ancestor = FindCommonAncestor(startContainer, endContainer);
        if (ancestor != null)
        {
            var allNodes = GetDocumentOrderNodes(ancestor);
            var startIdx = allNodes.IndexOf(startContainer);
            var endIdx = allNodes.IndexOf(endContainer);
            if (startIdx >= 0 && endIdx >= 0)
            {
                for (var i = startIdx + 1; i < endIdx; i++)
                {
                    var node = allNodes[i];
                    // Skip descendants of start/end containers (already handled)
                    if (IsDescendant(startContainer, node) || IsDescendant(endContainer, node))
                        continue;
                    // Only collect from top-level nodes
                    if (IsText(node))
                        sb.Append(BridgeText(node));
                    else if (node.ChildNodes.Count == 0)
                        continue; // element with no text children
                    // Don't double-collect descendants
                }
            }
        }

        // End container: collect from 0 to endOffset
        if (IsText(endContainer))
        {
            // Don't include end container text for Range.toString()
            // (end boundary is exclusive for text)
        }
        else
        {
            for (var i = 0; i < Math.Min(endOffset, endContainer.ChildNodes.Count); i++)
                CollectTextContent(ChildAt(endContainer, i), sb);
        }
    }

    /// <summary>
    /// Creates a partial clone for extractContents when a boundary is in a text node.
    /// Clones the ancestor chain from the text node up to (but not including) the common ancestor.
    /// </summary>
    private static Broiler.Dom.DomNode? CreatePartialCloneForExtract(Broiler.Dom.DomNode textNode, DomElement commonAncestor, string extractedText, bool isStart, DomBridge bridge)
    {
        // Build the chain: textNode → parent → ... → child-of-commonAncestor. chain[0] is the
        // boundary text node; chain[1..] are ancestor elements (RF-BRIDGE-1c Phase F, F3c part 2d).
        var chain = new List<Broiler.Dom.DomNode>();
        Broiler.Dom.DomNode? node = textNode;
        while (node != null && !ReferenceEquals(node, commonAncestor))
        {
            chain.Add(node);
            node = node.ParentNode;
        }
        if (chain.Count == 0) return null;

        // Create text node with extracted content
        var extractedTextNode = bridge.CreateBridgeTextNode(extractedText);
        bridge._knownNodes.Add(extractedTextNode);

        if (chain.Count == 1)
        {
            // Text node is direct child of common ancestor
            return extractedTextNode;
        }

        // Clone the chain (from top to bottom)
        DomElement? topClone = null;
        DomElement? currentParent = null;
        for (var i = chain.Count - 1; i >= 1; i--)
        {
            // chain[i] for i >= 1 is an ancestor element.
            var original = (DomElement)chain[i];
            var clone = new DomElement(original.TagName, null, null, string.Empty);
            bridge._knownNodes.Add(clone);

            if (topClone == null) topClone = clone;
            if (currentParent != null)
            {
                SetParent(clone, currentParent);
                currentParent.AppendChild(clone);
            }

            // For start boundary: include siblings after the text node within this element
            // For end boundary: include siblings before the text node within this element
            if (i == 1) // direct parent of text node
            {
                var childIdx = ChildIndexOf(original, chain[0]);
                if (isStart)
                {
                    // Include the extracted text + remaining siblings
                    SetParent(extractedTextNode, clone);
                    clone.AppendChild(extractedTextNode);
                    // Move siblings after the text node into the clone
                    for (var j = childIdx + 1; j < original.ChildNodes.Count; )
                    {
                        var sibling = ChildAt(original, j);
                        RemoveNthChild(original, j);
                        SetParent(sibling, clone);
                        clone.AppendChild(sibling);
                    }
                }
                else
                {
                    // Move siblings before the text node into the clone
                    for (var j = 0; j < childIdx; )
                    {
                        var sibling = ChildAt(original, 0);
                        RemoveNthChild(original, 0);
                        childIdx--;
                        SetParent(sibling, clone);
                        clone.AppendChild(sibling);
                    }
                    SetParent(extractedTextNode, clone);
                    clone.AppendChild(extractedTextNode);
                }
            }
            currentParent = clone;
        }

        return topClone;
    }

    /// <summary>
    /// Checks if a node is fully contained between start and end containers in the range.
    /// </summary>
    private static bool IsContainedInRange(DomElement node, DomElement ancestor, DomElement startContainer, DomElement endContainer, List<DomElement> allNodes)
    {
        // A node is fully contained if it and all its descendants are between start and end
        var nodeIdx = allNodes.IndexOf(node);
        var startIdx = allNodes.IndexOf(startContainer);
        var endIdx = allNodes.IndexOf(endContainer);

        if (nodeIdx <= startIdx || nodeIdx >= endIdx) return false;

        // Check that the node is not an ancestor of start or end container
        if (IsDescendant(node, startContainer) || IsDescendant(node, endContainer))
            return false;

        return true;
    }

    private JSObject CreateDomRectObject((double Left, double Top, double Width, double Height) rectData)
    {
        var rect = new JSObject();
        rect.FastAddValue((KeyString)"x", new JSNumber(rectData.Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"y", new JSNumber(rectData.Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"top", new JSNumber(rectData.Top), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"left", new JSNumber(rectData.Left), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"right", new JSNumber(rectData.Left + rectData.Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"bottom", new JSNumber(rectData.Top + rectData.Height), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"width", new JSNumber(rectData.Width), JSPropertyAttributes.EnumerableConfigurableValue);
        rect.FastAddValue((KeyString)"height", new JSNumber(rectData.Height), JSPropertyAttributes.EnumerableConfigurableValue);
        return rect;
    }

    private List<(double Left, double Top, double Width, double Height)> GetClientRectsForRange(RangeState state)
    {
        var rects = new List<(double Left, double Top, double Width, double Height)>();
        if (state.Collapsed)
            return rects;

        foreach (var node in GetNodesInRange(state.StartContainer, state.StartOffset, state.EndContainer, state.EndOffset))
            CollectClientRectsForRangeNode(node, rects);

        return rects;
    }

    private void CollectClientRectsForRangeNode(
        Broiler.Dom.DomNode node,
        List<(double Left, double Top, double Width, double Height)> rects)
    {
        // Character-data nodes contribute no client rect here (their text runs are measured
        // elsewhere); after this guard the node is an element.
        if (IsText(node) || IsComment(node) || node is not DomElement element)
            return;

        var display = GetComputedProps(element).GetValueOrDefault("display");
        if (string.Equals(display, "contents", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var child in ChildElements(element))
                CollectClientRectsForRangeNode(child, rects);

            return;
        }

        var rect = GetBoundingClientRectForDomElement(element, isRoot: false);
        if (rect.Width > 0 || rect.Height > 0)
            rects.Add(rect);
    }

    private static (double Left, double Top, double Width, double Height) UnionClientRects(
        IReadOnlyList<(double Left, double Top, double Width, double Height)> rects)
    {
        if (rects.Count == 0)
            return (0, 0, 0, 0);

        var left = rects[0].Left;
        var top = rects[0].Top;
        var right = rects[0].Left + rects[0].Width;
        var bottom = rects[0].Top + rects[0].Height;

        for (var i = 1; i < rects.Count; i++)
        {
            var rect = rects[i];
            left = Math.Min(left, rect.Left);
            top = Math.Min(top, rect.Top);
            right = Math.Max(right, rect.Left + rect.Width);
            bottom = Math.Max(bottom, rect.Top + rect.Height);
        }

        return (left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    /// <summary>
    /// Extracts the start-side path for cross-node extractContents.
    /// Clones ancestor chain from <paramref name="topNode"/> down to <paramref name="startContainer"/>,
    /// moving content after the start boundary into the cloned structure.
    /// </summary>
    private static Broiler.Dom.DomNode ExtractStartPath(Broiler.Dom.DomNode topNode, Broiler.Dom.DomNode startContainer, int startOffset, DomBridge bridge)
    {
        // Build chain: startContainer → parent → ... → topNode. RF-BRIDGE-1c Phase F (F3c
        // part 2b): chain[0] (the boundary container) may be a text node; the rest are ancestor
        // elements, so the chain is DomNode-typed.
        var chain = new List<Broiler.Dom.DomNode>();
        Broiler.Dom.DomNode? node = startContainer;
        while (node != null)
        {
            chain.Add(node);
            if (ReferenceEquals(node, topNode)) break;
            node = node.ParentNode;
        }

        // Pass 1: Clone the ancestor chain from top to bottom, creating
        // the skeletal tree structure.  Map each chain index to its clone.
        var clones = new Broiler.Dom.DomNode[chain.Count];
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var original = chain[i];
            var clone = bridge.CloneDomElement(original, false);
            bridge._knownNodes.Add(clone);
            clones[i] = clone;
            if (i < chain.Count - 1)
            {
                SetParent(clone, clones[i + 1]);
                // Will position correctly in Pass 2
            }
        }

        // Pass 2: Process each level bottom-up so that each clone's
        // child list is built in the correct order:
        //   [next-in-chain clone, siblings moved from original]
        for (var i = 0; i < chain.Count; i++)
        {
            var original = chain[i];
            var clone = clones[i];

            if (i == 0)
            {
                // This is the startContainer level
                if (IsText(original))
                {
                    var text = BridgeText(original);
                    var extractedPart = text.Substring(startOffset);
                    SetBridgeText(original, text.Substring(0, startOffset));
                    SetBridgeText(clone, extractedPart);
                }
                else
                {
                    for (var ci = startOffset; ci < original.ChildNodes.Count; )
                    {
                        var child = ChildAt(original, ci);
                        RemoveNthChild(original, ci);
                        SetParent(child, clone);
                        clone.AppendChild(child);
                    }
                }
            }
            else
            {
                var parentClone = clones[i];
                var childClone = clones[i - 1]; // next-in-chain clone

                // First add the deeper clone (already populated from previous iterations)
                SetParent(childClone, parentClone);
                parentClone.AppendChild(childClone);

                // Then move siblings after the chain child in the original
                var nextInChain = chain[i - 1];
                var childIdx = ChildIndexOf(original, nextInChain);
                if (childIdx >= 0)
                {
                    for (var ci = childIdx + 1; ci < original.ChildNodes.Count; )
                    {
                        var child = ChildAt(original, ci);
                        RemoveNthChild(original, ci);
                        SetParent(child, parentClone);
                        parentClone.AppendChild(child);
                    }
                }
            }
        }

        return clones[chain.Count - 1];
    }

    /// <summary>
    /// Extracts the end-side path for cross-node extractContents.
    /// Clones ancestor chain from <paramref name="topNode"/> down to <paramref name="endContainer"/>,
    /// moving content before the end boundary into the cloned structure.
    /// </summary>
    private static Broiler.Dom.DomNode ExtractEndPath(Broiler.Dom.DomNode topNode, Broiler.Dom.DomNode endContainer, int endOffset, DomBridge bridge)
    {
        // Build chain: endContainer → parent → ... → topNode (chain[0] may be a text node; see
        // ExtractStartPath).
        var chain = new List<Broiler.Dom.DomNode>();
        Broiler.Dom.DomNode? node = endContainer;
        while (node != null)
        {
            chain.Add(node);
            if (ReferenceEquals(node, topNode)) break;
            node = node.ParentNode;
        }

        // Pass 1: Create clones for the chain
        var clones = new Broiler.Dom.DomNode[chain.Count];
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var original = chain[i];
            var clone = bridge.CloneDomElement(original, false);
            bridge._knownNodes.Add(clone);
            clones[i] = clone;
        }

        // Pass 2: Process bottom-up. For end-side, siblings before the
        // chain child are moved, then the deeper clone is appended.
        for (var i = 0; i < chain.Count; i++)
        {
            var original = chain[i];
            var clone = clones[i];

            if (i == 0)
            {
                // This is the endContainer level
                if (IsText(original))
                {
                    var text = BridgeText(original);
                    var extractedPart = text.Substring(0, endOffset);
                    SetBridgeText(original, text.Substring(endOffset));
                    SetBridgeText(clone, extractedPart);
                }
                else
                {
                    for (var ci = 0; ci < endOffset && original.ChildNodes.Count > 0; ci++)
                    {
                        var child = ChildAt(original, 0);
                        RemoveNthChild(original, 0);
                        SetParent(child, clone);
                        clone.AppendChild(child);
                    }
                }
            }
            else
            {
                var parentClone = clones[i];
                var childClone = clones[i - 1]; // next-in-chain clone

                // First move siblings before the chain child in the original
                var nextInChain = chain[i - 1];
                var childIdx = ChildIndexOf(original, nextInChain);
                if (childIdx >= 0)
                {
                    for (var ci = 0; ci < childIdx; )
                    {
                        var child = ChildAt(original, 0);
                        RemoveNthChild(original, 0);
                        childIdx--;
                        SetParent(child, parentClone);
                        parentClone.AppendChild(child);
                    }
                }

                // Then add the deeper clone (already populated)
                SetParent(childClone, parentClone);
                parentClone.AppendChild(childClone);
            }
        }

        return clones[chain.Count - 1];
    }

    /// <summary>
    /// Tracks the boundary-point state of a live <c>Range</c> object so that
    /// DOM mutations can adjust boundaries per the DOM Living Standard.
    /// </summary>
    private sealed class RangeState
    {
        // RF-BRIDGE-1c Phase F (F3c part 2b): range boundary points are canonical DomNode —
        // a range commonly starts/ends inside a text node. Behaviour-preserving on today's
        // homogeneous facade tree (facade text nodes are DomElement, so the DomNode fields hold
        // the same values); forward-correct once text/comment flip to canonical DomText/DomComment.
        public Broiler.Dom.DomNode StartContainer;
        public int StartOffset;
        public Broiler.Dom.DomNode EndContainer;
        public int EndOffset;
        public bool Collapsed;
        public Broiler.Dom.DomNode Root { get; }

        public RangeState(Broiler.Dom.DomNode root)
        {
            Root = root;
            StartContainer = root;
            EndContainer = root;
            Collapsed = true;
        }

        public void UpdateCollapsed()
        {
            Collapsed = ReferenceEquals(StartContainer, EndContainer) && StartOffset == EndOffset;
        }

        /// <summary>
        /// Adjusts boundary points when a child is removed from <paramref name="parent"/>
        /// at <paramref name="index"/>.  Per DOM spec §14.4 "Removing steps".
        /// </summary>
        public void AdjustForRemoval(Broiler.Dom.DomNode parent, Broiler.Dom.DomNode removedChild, int index)
        {
            // If startContainer is a descendant of removedChild (or IS removedChild),
            // set start to (parent, index).
            if (ReferenceEquals(StartContainer, removedChild) || IsDescendantOf(StartContainer, removedChild))
            {
                StartContainer = parent;
                StartOffset = index;
            }
            else if (ReferenceEquals(StartContainer, parent) && StartOffset > index)
            {
                StartOffset--;
            }

            // Same for endContainer
            if (ReferenceEquals(EndContainer, removedChild) || IsDescendantOf(EndContainer, removedChild))
            {
                EndContainer = parent;
                EndOffset = index;
            }
            else if (ReferenceEquals(EndContainer, parent) && EndOffset > index)
            {
                EndOffset--;
            }

            UpdateCollapsed();
        }

        private static bool IsDescendantOf(Broiler.Dom.DomNode node, Broiler.Dom.DomNode potentialAncestor)
        {
            var current = node.ParentNode;
            while (current != null)
            {
                if (ReferenceEquals(current, potentialAncestor)) return true;
                current = current.ParentNode;
            }
            return false;
        }
    }

    /// <summary>
    /// Notifies all active ranges that a child was removed from <paramref name="parent"/>
    /// at the given <paramref name="index"/>.
    /// </summary>
    private void NotifyChildAdded(DomElement parent, Broiler.Dom.DomNode addedChild, int index)
    {
        var previousSibling = index > 0 ? ChildAt(parent, index - 1) : null;
        var nextSibling = index + 1 < parent.ChildNodes.Count ? ChildAt(parent, index + 1) : null;
        NotifyMutationObservers(parent, addedChild, null, previousSibling, nextSibling);
    }

    private void NotifyChildRemoved(DomElement parent, Broiler.Dom.DomNode removedChild, int index, Broiler.Dom.DomNode? previousSibling = null, Broiler.Dom.DomNode? nextSibling = null)
    {
        for (var i = _activeRanges.Count - 1; i >= 0; i--)
        {
            if (_activeRanges[i].TryGetTarget(out var state))
                state.AdjustForRemoval(parent, removedChild, index);
            else
                _activeRanges.RemoveAt(i); // GC'd — prune
        }

        previousSibling ??= index > 0 ? ChildAt(parent, index - 1) : null;
        nextSibling ??= index < parent.ChildNodes.Count ? ChildAt(parent, index) : null;
        NotifyMutationObservers(parent, null, removedChild, previousSibling, nextSibling);
    }

    private void NotifyMutationObservers(
        DomElement target,
        Broiler.Dom.DomNode? addedChild,
        Broiler.Dom.DomNode? removedChild,
        Broiler.Dom.DomNode? previousSibling,
        Broiler.Dom.DomNode? nextSibling)
    {
        if (_mutationObservers.Count == 0)
            return;

        var mutation = new Broiler.Dom.DomMutationRecord(Broiler.Dom.DomMutationType.ChildList, target);
        foreach (var (observer, observedTarget, options) in _mutationObservers.ToArray())
        {
            if (!Broiler.Dom.DomMutationObserverFilter.Matches(mutation, observedTarget, options))
                continue;

            if (observer[(KeyString)"_notify"] is not JSFunction notifyFunction)
                continue;

            var record = new JSObject();
            record[(KeyString)"type"] = new JSString("childList");
            record[(KeyString)"target"] = ToJSObject(target);
            record[(KeyString)"addedNodes"] = addedChild != null
                ? new JSArray([ToJSObject(addedChild)])
                : new JSArray([]);
            record[(KeyString)"removedNodes"] = removedChild != null
                ? new JSArray([ToJSObject(removedChild)])
                : new JSArray([]);
            record[(KeyString)"previousSibling"] = previousSibling != null
                ? ToJSObject(previousSibling)
                : JSNull.Value;
            record[(KeyString)"nextSibling"] = nextSibling != null
                ? ToJSObject(nextSibling)
                : JSNull.Value;

            notifyFunction.InvokeFunction(new Arguments(observer, new JSArray([record])));
        }
    }

    private void NotifyAttributeMutationObservers(DomElement target, string attributeName, string? oldValue)
    {
        if (_mutationObservers.Count == 0)
            return;

        var mutation = new Broiler.Dom.DomMutationRecord(Broiler.Dom.DomMutationType.Attributes, target, AttributeName: attributeName);
        foreach (var (observer, observedTarget, options) in _mutationObservers.ToArray())
        {
            if (!Broiler.Dom.DomMutationObserverFilter.Matches(mutation, observedTarget, options))
                continue;

            if (observer[(KeyString)"_notify"] is not JSFunction notifyFunction)
                continue;

            var record = new JSObject();
            record[(KeyString)"type"] = new JSString("attributes");
            record[(KeyString)"target"] = ToJSObject(target);
            record[(KeyString)"attributeName"] = new JSString(attributeName);
            record[(KeyString)"oldValue"] = options.AttributeOldValue && oldValue != null
                ? new JSString(oldValue)
                : JSNull.Value;

            notifyFunction.InvokeFunction(new Arguments(observer, new JSArray([record])));
        }
    }

    private void NotifyCharacterDataMutationObservers(Broiler.Dom.DomNode target, string? oldValue)
    {
        if (_mutationObservers.Count == 0)
            return;

        var mutation = new Broiler.Dom.DomMutationRecord(Broiler.Dom.DomMutationType.CharacterData, target);
        foreach (var (observer, observedTarget, options) in _mutationObservers.ToArray())
        {
            if (!Broiler.Dom.DomMutationObserverFilter.Matches(mutation, observedTarget, options))
                continue;

            if (observer[(KeyString)"_notify"] is not JSFunction notifyFunction)
                continue;

            var record = new JSObject();
            record[(KeyString)"type"] = new JSString("characterData");
            record[(KeyString)"target"] = ToJSObject(target);
            record[(KeyString)"oldValue"] = options.CharacterDataOldValue && oldValue != null
                ? new JSString(oldValue)
                : JSNull.Value;

            notifyFunction.InvokeFunction(new Arguments(observer, new JSArray([record])));
        }
    }

    private static void UpdateCharacterData(Broiler.Dom.DomNode target, string? newValue)
    {
        SetBridgeText(target, newValue ?? string.Empty);
    }

    private void SetCharacterData(Broiler.Dom.DomNode target, string? newValue)
    {
        var previousValue = BridgeText(target);
        UpdateCharacterData(target, newValue);
        if (!string.Equals(previousValue, BridgeText(target), StringComparison.Ordinal))
            NotifyCharacterDataMutationObservers(target, previousValue);
    }

    /// <summary>
    /// Executes NodeIterator pre-removing steps per DOM §7.2.
    /// Must be called BEFORE the node is actually removed from the tree
    /// so that tree traversal can find neighboring nodes.
    /// </summary>
    private void NotifyNodeIteratorPreRemoval(Broiler.Dom.DomNode nodeToBeRemoved)
    {
        for (var i = _activeNodeIterators.Count - 1; i >= 0; i--)
        {
            if (_activeNodeIterators[i].TryGetTarget(out _))
                continue;
            else
                _activeNodeIterators.RemoveAt(i); // GC'd — prune
        }
    }

    /// <summary>
    /// Tracks the state of a live <c>NodeIterator</c> so that DOM mutations
    /// can adjust the reference node per the DOM Living Standard §7.2
    /// "NodeIterator pre-removing steps".
    /// </summary>
    private sealed class IteratorState
    {
        // RF-BRIDGE-1c Phase F (F3c part 2b): a NodeIterator's root and reference node are
        // canonical DomNode — the iterator can reference a text node. Behaviour-preserving on
        // today's homogeneous tree; forward-correct once text/comment are canonical DomText/DomComment.
        public Broiler.Dom.DomNode Root;
        public Broiler.Dom.DomNode? ReferenceNode;
        public bool PointerBeforeReferenceNode;
        public int LastKnownIndex = -1;

        public IteratorState(Broiler.Dom.DomNode root)
        {
            Root = root;
            ReferenceNode = root;
            PointerBeforeReferenceNode = true;
        }

        /// <summary>
        /// Per DOM spec §7.2: when a node is removed that is the reference node
        /// (or an inclusive descendant of it), advance the reference node to the
        /// appropriate neighboring node within the iterator's root subtree.
        /// </summary>
        public void AdjustForRemoval(Broiler.Dom.DomNode removedNode)
        {
            if (ReferenceNode == null) return;

            // Only act if the removed node IS the reference node or is an
            // ancestor of it (i.e., reference node is a descendant of removed).
            if (!ReferenceEquals(ReferenceNode, removedNode) &&
                !IsDescendantOf(ReferenceNode, removedNode))
                return;

            // Also only act if the removed node is within the iterator's root subtree.
            if (!ReferenceEquals(removedNode, Root) && !IsDescendantOf(removedNode, Root))
                return;

            if (PointerBeforeReferenceNode)
            {
                // Pointer is before reference — advance reference to the next
                // node following removedNode in document order that is an
                // inclusive descendant of root.
                var next = GetNextNodeAfter(removedNode, Root);
                if (next != null)
                {
                    ReferenceNode = next;
                    // pointerBeforeReferenceNode stays true
                    return;
                }
                // If no following node, try the first preceding node
                var prev = GetPreviousNodeBefore(removedNode, Root);
                if (prev != null)
                {
                    ReferenceNode = prev;
                    PointerBeforeReferenceNode = false;
                }
            }
            else
            {
                // Pointer is after reference — advance reference to the first
                // preceding node before removedNode in document order that is
                // an inclusive descendant of root.
                var prev = GetPreviousNodeBefore(removedNode, Root);
                if (prev != null)
                {
                    ReferenceNode = prev;
                    PointerBeforeReferenceNode = true;
                    return;
                }
                // If no preceding node, try the next following node
                var next = GetNextNodeAfter(removedNode, Root);
                if (next != null)
                {
                    ReferenceNode = next;
                    // pointerBeforeReferenceNode stays false
                }
            }
        }

        /// <summary>
        /// Returns the first node following <paramref name="node"/> in document order
        /// that is an inclusive descendant of <paramref name="root"/>, skipping the
        /// subtree of <paramref name="node"/> (since it's being removed).
        /// </summary>
        private static Broiler.Dom.DomNode? GetNextNodeAfter(Broiler.Dom.DomNode node, Broiler.Dom.DomNode root)
        {
            // Look for next sibling, then parent's next sibling, etc. RF-BRIDGE-1c Phase F (F3c
            // part 2b): walk raw ChildNodes so text/comment siblings count in document order.
            // Behaviour-preserving on today's homogeneous tree (every child is an element).
            Broiler.Dom.DomNode? current = node;
            while (current != null && !ReferenceEquals(current, root))
            {
                var parent = current.ParentNode;
                if (parent != null)
                {
                    var idx = ChildIndexOf(parent, current);
                    if (idx >= 0 && idx + 1 < parent.ChildNodes.Count)
                        return parent.ChildNodes[idx + 1];
                }
                current = current.ParentNode;
            }
            return null;
        }

        /// <summary>
        /// Returns the first node preceding <paramref name="node"/> in document order
        /// that is an inclusive descendant of <paramref name="root"/>.
        /// </summary>
        private static Broiler.Dom.DomNode? GetPreviousNodeBefore(Broiler.Dom.DomNode node, Broiler.Dom.DomNode root)
        {
            var parent = node.ParentNode;
            if (parent == null) return null;
            var idx = ChildIndexOf(parent, node);
            if (idx > 0)
            {
                // Go to the deepest last descendant of the previous sibling
                var prev = parent.ChildNodes[idx - 1];
                while (prev.ChildNodes.Count > 0)
                    prev = prev.ChildNodes[prev.ChildNodes.Count - 1];
                return prev;
            }
            // No previous sibling — parent is the previous node (unless it's root)
            if (!ReferenceEquals(parent, root))
                return parent;
            return null;
        }

        private static bool IsDescendantOf(Broiler.Dom.DomNode node, Broiler.Dom.DomNode potentialAncestor)
        {
            var current = node.ParentNode;
            while (current != null)
            {
                if (ReferenceEquals(current, potentialAncestor)) return true;
                current = current.ParentNode;
            }
            return false;
        }
    }
}
