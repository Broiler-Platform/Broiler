using System.Text;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private JSValue GetNodeTextValue(DomNode node)
    {
        // RF-BRIDGE-1c Phase F (F3c part 2d): character-data nodes expose their data as textContent;
        // an element's textContent is the concatenation of its descendant text.
        if (node is DomCharacterData characterData)
            return new JSString(characterData.Data);

        if (node is not DomElement element)
            return new JSString(string.Empty);

        if (element.ChildNodes.Count > 0)
        {
            var sb = new StringBuilder();
            CollectTextContent(element, sb);
            return new JSString(sb.ToString());
        }

        return new JSString(GetElementRuntimeState(element).InnerHtml);
    }


    private bool IsCurrentIframeCrossOrigin(DomElement element)
    {
        if (HasAttr(element, "srcdoc"))
            return false;

        var iframeSrcValue = TryGetAttribute(element, "src", out var srcVal) ? srcVal : string.Empty;
        return IsCrossOrigin(iframeSrcValue, _pageUrl);
    }


    // MutationObserver option parsing and observe()/disconnect() registration moved to the Phase 3
    // MutationObserverBinding feature module (Broiler.HtmlBridge.Dom.Features).

    // Phase 4 item 1 (P4.4a): docRoot may be a legacy #subdoc-root element OR a canonical
    // DomDocument browsing-context root; ChildElements works over both. Returns the documentElement,
    // or — for an element root with none — the root itself (the prior `?? docRoot` fallback). A
    // canonical DomDocument with no documentElement yields null (per DOM; e.g. createDocument with an
    // empty qualifiedName), so callers must null-check.
    private static DomElement? GetDocumentElement(DomNode docRoot) =>
        ChildElements(docRoot).FirstOrDefault(c => !IsText(c) && !c.TagName.StartsWith('#')) ?? docRoot as DomElement;


    private bool IsPositionAfter(DomNode docRoot, DomNode containerA, int offsetA, DomNode containerB, int offsetB)
    {
        if (ReferenceEquals(containerA, containerB))
            return offsetA > offsetB;

        if (IsDescendant(containerA, containerB))
        {
            DomNode node = containerB;
            while (node.ParentNode != null && !ReferenceEquals(node.ParentNode, containerA))
                node = node.ParentNode;
            if (node.ParentNode != null)
            {
                var childIdx = ChildIndexOf(containerA, node);
                return offsetA > childIdx;
            }

            return false;
        }

        if (IsDescendant(containerB, containerA))
        {
            DomNode node = containerA;
            while (node.ParentNode != null && !ReferenceEquals(node.ParentNode, containerB))
                node = node.ParentNode;
            if (node.ParentNode != null)
            {
                var childIdx = ChildIndexOf(containerB, node);
                return childIdx >= offsetB;
            }

            return true;
        }

        var commonRoot = FindCommonAncestor(containerA, containerB) ?? docRoot;
        var allNodes = GetDocumentOrderNodes(commonRoot);
        var idxA = allNodes.IndexOf(containerA);
        var idxB = allNodes.IndexOf(containerB);
        if (idxA < 0 || idxB < 0)
            return false;

        return idxA > idxB || (idxA == idxB && offsetA > offsetB);
    }


    private int CompareBoundaryPosition(DomNode docRoot, DomNode containerA, int offsetA, DomNode containerB, int offsetB)
    {
        if (ReferenceEquals(containerA, containerB) && offsetA == offsetB)
            return 0;

        if (IsPositionAfter(docRoot, containerA, offsetA, containerB, offsetB))
            return 1;

        if (IsPositionAfter(docRoot, containerB, offsetB, containerA, offsetA))
            return -1;

        return 0;
    }
}
