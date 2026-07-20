using System.Text;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HtmlBridge.Logging;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// Sibling partial peeled out of <c>SubDocuments.cs</c> (Phase 3 ratchet, 2026-07-17) to keep it
/// under the 750-line guard: the generic HTML-fragment DOM-mutation helpers that are not
/// sub-document-specific. Covers per-node cache teardown (<see cref="RemoveElementsRecursive"/>),
/// <c>normalize()</c> text coalescing, indexed child removal/insertion, and the
/// <c>innerHTML</c> / <c>outerHTML</c> / <c>insertAdjacentHTML</c> / child-node argument fragment
/// builders. Pure partial-class relocation — no signature, accessibility, or logic change.
/// </summary>
public sealed partial class DomBridge
{
    // RF-BRIDGE-1c Phase F (F3c part 2d): unregister the whole node subtree from the bridge's
    // per-node caches when a sub-document root is torn down (raw ChildNodes so canonical
    // text/comment nodes are released too). The former AddElementsRecursive counterpart is gone —
    // node membership is now read from the canonical tree, so there is nothing to register on build.
    private void RemoveElementsRecursive(DomNode node)
    {
        _jsObjects.Remove(node);

        if (node is DomElement element)
            _styleSheetCache.Remove(element);

        foreach (var child in node.ChildNodes)
            RemoveElementsRecursive(child);
    }

    private void NormalizeNode(DomElement node)
    {
        for (var index = 0; index < node.ChildNodes.Count;)
        {
            var child = ChildAt(node, index);
            if (!IsText(child))
            {
                // Recurse into element children (a comment has no text children to merge).
                if (child is DomElement childElement)
                    NormalizeNode(childElement);
                index++;
                continue;
            }

            var mergedText = BridgeText(child);
            var nextIndex = index + 1;
            while (nextIndex < node.ChildNodes.Count && IsText(ChildAt(node, nextIndex)))
            {
                mergedText += BridgeText(ChildAt(node, nextIndex));
                RemoveChildAt(node, nextIndex);
            }

            if (mergedText.Length == 0)
            {
                RemoveChildAt(node, index);
                continue;
            }

            SetCharacterData(child, mergedText);
            index++;
        }
    }

    private void RemoveChildAt(DomElement parent, int index)
    {
        if (index < 0 || index >= parent.ChildNodes.Count)
            return;

        var child = ChildAt(parent, index);
        NotifyNodeIteratorPreRemoval(child);
        RemoveNthChild(parent, index);
        SetParent(child, null);
        InvalidateStyleScope(parent);
        NotifyChildRemoved(parent, child, index);
    }

    // Phase 4 items 4/5 (P4.9 follow-up): the bridge's NodesAreEqual / CanonicalAttributesAreEqual
    // copies were deleted after their promotion to canonical Broiler.Dom.DomNode.IsEqualNode landed
    // in the pinned submodule (patches/0001, applied by the maintainer). The isEqualNode binding now
    // delegates to node.IsEqualNode(other); behaviour is pinned by IsEqualNodePromotionTests. The
    // canonical algorithm drops the bridge copy's element-level BridgeText comparison, which was a
    // no-op on the canonical tree (an element's NodeValue is null) — so it is behaviour-equivalent.

    // NormalizeInsertAdjacentPosition / GetInsertAdjacentTarget moved to the InsertAdjacentBinding feature
    // module (Phase 3 P3.56) with their only consumers, the insertAdjacent* methods.

    // Phase 4 item 1: parent widened DomElement -> DomNode so a canonical DomDocumentFragment can be
    // an insertion parent (fragment.appendChild/append/...). The style-scope invalidation and
    // child-added mutation notification are element-only concerns, guarded accordingly; the onload
    // firing below already guards on `node is DomElement`. Behaviour for element parents is identical.
    private void InsertNodeAt(DomNode parent, DomNode node, int index)
    {
        if (ReferenceEquals(node, parent) || parent.IsDescendantOf(node))
            ThrowDOMException(_jsContext!, "The new child element contains the parent.", "HierarchyRequestError");

        if (index < 0)
            index = 0;
        if (index > parent.ChildNodes.Count)
            index = parent.ChildNodes.Count;

        if (ParentEl(node) != null)
        {
            var oldParent = ParentEl(node);
            var oldIndex = ChildIndexOf(oldParent, node);
            if (oldIndex >= 0)
            {
                if (ReferenceEquals(oldParent, parent) && oldIndex < index)
                    index--;

                NotifyNodeIteratorPreRemoval(node);
                RemoveNthChild(oldParent, oldIndex);
                NotifyChildRemoved(oldParent, node, oldIndex);
            }
        }

        // Phase 4 (mutation-primitive cleanup): a single canonical insert. The prior
        // SetParent(node, parent) appended node at the end first, so the InsertChildAt below then
        // re-moved it to `index` — firing spurious canonical add-at-end + remove records that the
        // canonical NodeIterator/CSS mutation subscribers observe. The move-block above already
        // detached node from any old parent, so InsertChildAt alone lands it at `index` with one
        // canonical ChildList(added) record.
        InsertChildAt(parent, index, node);
        if (parent is DomElement parentElement)
        {
            InvalidateStyleScope(parentElement);
            NotifyChildAdded(parentElement, node, index);
        }

        // RF-BRIDGE-1c Phase F (F3c part 2b): only elements carry a TagName / fire onloads; a
        // canonical char-data node inserts with no sub-document side effects.
        if (node is DomElement insertedElement)
        {
            var insertedTag = insertedElement.TagName?.ToLowerInvariant();
            if (insertedTag == "iframe" || insertedTag == "object")
                FireSubDocumentOnload(insertedElement);
            else
                FireDescendantOnloads(insertedElement);
        }
    }

    private List<DomNode> BuildAdjacentHtmlNodes(DomElement contextElement, string html)
    {
        var nodes = new List<DomNode>();
        if (string.IsNullOrEmpty(html))
            return nodes;

        if (!TryBuildInnerHtmlFragmentContainer(contextElement, html, out var fragmentContainer))
            return nodes;

        // RF-BRIDGE-1c Phase F (F3c part 2d): move ALL children (raw ChildNodes) so text/comment
        // nodes in the parsed fragment survive.
        foreach (var child in fragmentContainer.ChildNodes.ToArray())
        {
            RemoveChildFrom(fragmentContainer, child);
            SetParent(child, null);
            nodes.Add(child);
        }

        return nodes;
    }

    private List<DomNode> BuildChildNodeArgumentNodes(in Arguments arguments)
    {
        // RF-BRIDGE-1c Phase F (F3c part 2b): returns canonical DomNode — an argument may be a
        // text node (resolved via FindDomNodeByJSObject) and string arguments mint text nodes.
        var nodes = new List<DomNode>();
        for (var i = 0; i < arguments.Length; i++)
        {
            var value = arguments[i];
            if (value is JSObject candidateObject)
            {
                var candidateNode = FindDomNodeByJSObject(candidateObject);
                if (candidateNode != null)
                {
                    // Phase 4 item 1: a canonical DomDocumentFragment argument inserts its children
                    // (per DOM), not the fragment itself. (Was a "#document-fragment" TagName check on
                    // the former sentinel element — a non-element fragment no longer matches that.)
                    if (candidateNode is DomDocumentFragment candidateFragment)
                    {
                        foreach (var fragmentChild in candidateFragment.ChildNodes.ToArray())
                            nodes.Add(fragmentChild);
                        continue;
                    }

                    nodes.Add(candidateNode);
                    continue;
                }
            }

            var textNode = CreateBridgeTextNode(value.ToString());
            nodes.Add(textNode);
        }

        return nodes;
    }

    private void SetElementInnerHtml(DomElement element, string html)
    {
        html ??= string.Empty;

        foreach (var child in element.ChildNodes.ToArray())
            RemoveElementsRecursive(child);

        ClearChildren(element);

        if (!string.IsNullOrEmpty(html) &&
            TryBuildInnerHtmlFragmentContainer(element, html, out var fragmentContainer))
        {
            // RF-BRIDGE-1c Phase F (F3c part 2d): move ALL children so parsed text/comment survive.
            foreach (var child in fragmentContainer.ChildNodes.ToArray())
            {
                // Single canonical move: AppendChild removes the child from the parsed fragment and
                // appends it to element in one op. The prior SetParent(child, element) did the same
                // move, leaving the following AppendChild a no-op — redundant, not wrong.
                element.AppendChild(child);
            }
        }

        ResetComputedStyleEngines();
        InvalidateStyleScope(element);
    }

    private void SetElementOuterHtml(DomElement element, string html)
    {
        html ??= string.Empty;

        var parent = ParentEl(element);
        if (parent == null)
            return;

        var index = ChildIndexOf(parent, element);
        if (index < 0)
            return;

        var previousSibling = index > 0 ? ChildAt(parent, index - 1) : null;
        var nextSibling = index + 1 < parent.ChildNodes.Count ? ChildAt(parent, index + 1) : null;

        DomDocumentFragment? parsedContainer = null;
        if (!string.IsNullOrEmpty(html))
        {
            var parsingContext = parent.TagName.StartsWith('#')
                ? CreateBridgeElement("body")
                : parent;
            if (TryBuildInnerHtmlFragmentContainer(parsingContext, html, out var fragmentContainer))
                parsedContainer = fragmentContainer;
        }

        NotifyNodeIteratorPreRemoval(element);
        RemoveNthChild(parent, index);
        SetParent(element, null);
        NotifyChildRemoved(parent, element, index, previousSibling, nextSibling);

        if (parsedContainer != null)
        {
            var insertIndex = index;
            foreach (var child in parsedContainer.ChildNodes.ToArray())
            {
                // Single canonical insert (InsertChildAt moves child out of the parsed fragment and
                // into parent at insertIndex); the prior SetParent-append + reposition fired spurious
                // records. The explicit NotifyChildAdded record is unchanged.
                InsertChildAt(parent, insertIndex, child);
                NotifyChildAdded(parent, child, insertIndex);
                insertIndex++;
            }
        }

        ResetComputedStyleEngines();
        InvalidateStyleScope(parent);
    }

    private bool TryBuildInnerHtmlFragmentContainer(DomElement contextElement, string html, out DomDocumentFragment container)
    {
        container = null!;

        var contextTag = contextElement.TagName.ToLowerInvariant();
        if (IsVoidHtmlElementTag(contextTag))
            return false;

        var (fragment, _) = BuildFragmentTree(html, contextTag);
        container = fragment;
        return true;
    }

    private static DomElement? FindFirstElementByTag(DomElement root, string tag)
    {
        foreach (var child in ChildElements(root))
        {
            if (!IsText(child) && string.Equals(child.TagName, tag, StringComparison.OrdinalIgnoreCase))
                return child;

            var match = FindFirstElementByTag(child, tag);
            if (match != null)
                return match;
        }

        return null;
    }

    private static bool IsVoidHtmlElementTag(string tag) => tag is
        "area" or "base" or "br" or "col" or "embed" or "hr" or "img" or
        "input" or "link" or "meta" or "param" or "source" or "track" or "wbr";
}
