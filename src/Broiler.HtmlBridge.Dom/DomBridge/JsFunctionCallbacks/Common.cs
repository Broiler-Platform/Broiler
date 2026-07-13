using System.Text;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom;

public sealed partial class DomBridge
{
    private delegate string? JsPropertyStringGetter(JSObject obj, params string[] names);


    private delegate IEnumerable<(string Key, string Value)> ObjectStringEntriesEnumerator(JSObject obj);


    private delegate (int status, string statusText, string url, string type, bool redirected, Dictionary<string, string> headers) ResponseInitParser(JSValue? initValue);


    private delegate JSValue ResponseFactory(string body, int statusCode, string statusText,
        string responseUrl, string type, bool redirected, Dictionary<string, string> headers);


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


    private static bool GetMutationObserverOption(JSObject optionsObject, string propertyName)
    {
        var optionValue = optionsObject[(KeyString)propertyName];
        return optionValue != null &&
               !optionValue.IsUndefined &&
               !optionValue.IsNull &&
               optionValue.BooleanValue;
    }


    private static DomMutationObserverOptions CreateMutationObserverOptions(JSValue? value)
    {
        if (value is not JSObject optionsObject)
            return new DomMutationObserverOptions();

        return new DomMutationObserverOptions
        {
            ChildList = GetMutationObserverOption(optionsObject, "childList"),
            Attributes = GetMutationObserverOption(optionsObject, "attributes"),
            AttributeOldValue = GetMutationObserverOption(optionsObject, "attributeOldValue"),
            CharacterData = GetMutationObserverOption(optionsObject, "characterData"),
            CharacterDataOldValue = GetMutationObserverOption(optionsObject, "characterDataOldValue"),
            Subtree = GetMutationObserverOption(optionsObject, "subtree")
        };
    }


    private void RegisterMutationObserver(JSObject observerObject, DomNode target, DomMutationObserverOptions options)
    {
        _mutationObservers.RemoveAll(entry =>
            ReferenceEquals(entry.Observer, observerObject) &&
            ReferenceEquals(entry.Target, target));
        _mutationObservers.Add((observerObject, target, options));
    }


    private void UnregisterMutationObserver(JSObject observerObject) => _mutationObservers.RemoveAll(entry => ReferenceEquals(entry.Observer, observerObject));


    private static DomElement GetDocumentElement(DomElement docRoot) => ChildElements(docRoot).FirstOrDefault(c => !IsText(c) && !c.TagName.StartsWith('#')) ?? docRoot;


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
