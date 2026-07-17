using Broiler.Dom;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

// Explicit IDocumentCollectionHost implementation for the DocumentCollectionBinding feature module
// (Phase 3): the bridge exposes the document root, element list, JS-wrapper factory, tree-order link
// collector and stylesheet-object builder via explicit interface members, so the module never
// reaches an arbitrary bridge private field and the public surface is unchanged.
public sealed partial class DomBridge : Dom.Features.IDocumentCollectionHost
{
    JSObject Dom.Features.IDocumentCollectionHost.ToJSObject(DomNode node) => ToJSObject(node);

    DomElement Dom.Features.IDocumentCollectionHost.DocumentElement => DocumentElement;

    IReadOnlyList<DomElement> Dom.Features.IDocumentCollectionHost.Elements => Elements;

    void Dom.Features.IDocumentCollectionHost.CollectLinksInTreeOrder(DomElement root, List<JSValue> results)
        => CollectLinksInTreeOrder(root, results);

    JSObject Dom.Features.IDocumentCollectionHost.BuildStyleSheetObject(DomElement styleElement)
        => BuildStyleSheetObject(styleElement);
}
