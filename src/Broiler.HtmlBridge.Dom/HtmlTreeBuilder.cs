using Broiler.Dom.Html;

namespace Broiler.HtmlBridge;

/// <summary>
/// Compatibility adapter over the shared <see cref="HtmlDocumentParser"/>.
/// The parsing algorithm and token contract live in <c>Broiler.Dom.Html</c>;
/// this type only materializes v1 bridge facade nodes. New parser consumers
/// should use <see cref="HtmlDocumentParser"/> directly; this adapter may be
/// removed only with the v2 public boundary.
/// </summary>
public sealed class HtmlTreeBuilder
{
    public const string CompatibilitySurfaceVersion = "htmlbridge-dom-adapter/v1";
    public const string RemovalBoundaryVersion = "htmlbridge-public-surface/v2";

    // RF-BRIDGE-1c Phase F (F3c part 2d): ConvertNode returns canonical DomNode and mints canonical
    // DomText/DomComment for character data (the facade only re-materializes elements now); AllElements
    // widens to List<DomNode> so text/comment nodes are registered too. HtmlTreeBuilder itself is
    // retired in F4 (callers parse via HtmlDocumentParser directly).
    public (DomElement DocumentElement, List<Broiler.Dom.DomNode> AllElements, string Title) Build(
        string html,
        Broiler.Dom.DomDocument? document = null)
    {
        document ??= new Broiler.Dom.DomDocument();
        var parsed = new HtmlDocumentParser().ParseDocument(html);
        var parsedRoot = parsed.Document.DocumentElement ??
            throw new InvalidOperationException("The shared HTML parser did not produce a document element.");

        var allElements = new List<Broiler.Dom.DomNode>();
        // The document element is always an element node.
        var root = (DomElement)ConvertNode(parsedRoot, document, allElements, structural: true);
        return (root, allElements, parsed.Title);
    }

    public (DomElement Fragment, List<Broiler.Dom.DomNode> AllElements) BuildFragment(
        string html,
        string contextTagName,
        Broiler.Dom.DomDocument document)
    {
        var parsed = new HtmlDocumentParser().ParseFragment(html, contextTagName);
        var allElements = new List<Broiler.Dom.DomNode>();
        var fragment = new DomElement(document, "#document-fragment", null, null, string.Empty);
        foreach (var child in parsed.Fragment.ChildNodes)
            fragment.AppendChild(ConvertNode(child, document, allElements));
        return (fragment, allElements);
    }

    private static Broiler.Dom.DomNode ConvertNode(
        Broiler.Dom.DomNode source,
        Broiler.Dom.DomDocument targetDocument,
        List<Broiler.Dom.DomNode> allElements,
        bool structural = false)
    {
        Broiler.Dom.DomNode target;
        switch (source)
        {
            case Broiler.Dom.DomText text:
                target = targetDocument.CreateTextNode(text.Data);
                break;

            case Broiler.Dom.DomComment comment:
                target = targetDocument.CreateComment(comment.Data);
                break;

            case Broiler.Dom.DomElement element:
            {
                var attributes = element.Attributes.Values.ToDictionary(
                    static attribute => attribute.QualifiedName,
                    static attribute => attribute.Value,
                    StringComparer.OrdinalIgnoreCase);
                Dictionary<string, string>? style = null;
                if (attributes.TryGetValue("style", out var styleText))
                    style = ParseStyle(styleText);

                target = new DomElement(
                    targetDocument,
                    element.TagName,
                    element.Id,
                    element.ClassName,
                    string.Empty,
                    style,
                    attributes);
                break;
            }

            default:
                throw new InvalidOperationException($"Unsupported parsed node type '{source.NodeType}'.");
        }

        if (!structural)
            allElements.Add(target);

        foreach (var child in source.ChildNodes)
        {
            var childIsStructural =
                structural &&
                child is Broiler.Dom.DomElement childElement &&
                childElement.LocalName is "head" or "body";
            target.AppendChild(ConvertNode(child, targetDocument, allElements, childIsStructural));
        }

        return target;
    }

    private static Dictionary<string, string> ParseStyle(string styleValue)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var declarations = new Broiler.CSS.CssParser().ParseDeclarations(styleValue);
        foreach (var declaration in declarations.Declarations)
        {
            var value = declaration.Value.Text;
            if (declaration.Important)
                value += " !important";
            result[declaration.Name] = value;
        }
        return result;
    }
}
