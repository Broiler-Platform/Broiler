using System.Text;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private delegate string? JsPropertyStringGetter(JSObject obj, params string[] names);


    private delegate IEnumerable<(string Key, string Value)> ObjectStringEntriesEnumerator(JSObject obj);


    private delegate (int status, string statusText, string url, string type, bool redirected, Dictionary<string, string> headers) ResponseInitParser(JSValue? initValue);


    private delegate JSValue ResponseFactory(
        string body,
        int statusCode,
        string statusText,
        string responseUrl,
        string type,
        bool redirected,
        Dictionary<string, string> headers);


    private JSValue GetNodeTextValue(DomElement element)
    {
        if (element.IsTextNode)
            return element.TextContent != null ? new JSString(element.TextContent) : new JSString(string.Empty);

        if (element.TextContent != null && element.Children.Count == 0)
            return new JSString(element.TextContent);

        if (element.Children.Count > 0)
        {
            var sb = new StringBuilder();
            CollectTextContent(element, sb);
            return new JSString(sb.ToString());
        }

        return new JSString(element.InnerHtml);
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


    private static Broiler.Dom.DomMutationObserverOptions CreateMutationObserverOptions(JSValue? value)
    {
        if (value is not JSObject optionsObject)
            return new Broiler.Dom.DomMutationObserverOptions();

        return new Broiler.Dom.DomMutationObserverOptions
        {
            ChildList = GetMutationObserverOption(optionsObject, "childList"),
            Attributes = GetMutationObserverOption(optionsObject, "attributes"),
            AttributeOldValue = GetMutationObserverOption(optionsObject, "attributeOldValue"),
            CharacterData = GetMutationObserverOption(optionsObject, "characterData"),
            CharacterDataOldValue = GetMutationObserverOption(optionsObject, "characterDataOldValue"),
            Subtree = GetMutationObserverOption(optionsObject, "subtree")
        };
    }


    private void RegisterMutationObserver(JSObject observerObject, DomElement target, Broiler.Dom.DomMutationObserverOptions options)
    {
        _mutationObservers.RemoveAll(entry =>
            ReferenceEquals(entry.Observer, observerObject) &&
            ReferenceEquals(entry.Target, target));
        _mutationObservers.Add((observerObject, target, options));
    }


    private void UnregisterMutationObserver(JSObject observerObject)
    {
        _mutationObservers.RemoveAll(entry => ReferenceEquals(entry.Observer, observerObject));
    }


    private static DomElement GetDocumentElement(DomElement docRoot)
    {
        return docRoot.Children.FirstOrDefault(c => !c.IsTextNode && !c.TagName.StartsWith("#"))
            ?? docRoot;
    }


    private bool IsPositionAfter(DomElement docRoot, DomElement containerA, int offsetA, DomElement containerB, int offsetB)
    {
        if (ReferenceEquals(containerA, containerB))
            return offsetA > offsetB;

        if (IsDescendant(containerA, containerB))
        {
            var node = containerB;
            while (node.Parent != null && !ReferenceEquals(node.Parent, containerA))
                node = node.Parent;
            if (node.Parent != null)
            {
                var childIdx = containerA.Children.IndexOf(node);
                return offsetA > childIdx;
            }

            return false;
        }

        if (IsDescendant(containerB, containerA))
        {
            var node = containerA;
            while (node.Parent != null && !ReferenceEquals(node.Parent, containerB))
                node = node.Parent;
            if (node.Parent != null)
            {
                var childIdx = containerB.Children.IndexOf(node);
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


    private int CompareBoundaryPosition(DomElement docRoot, DomElement containerA, int offsetA, DomElement containerB, int offsetB)
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
