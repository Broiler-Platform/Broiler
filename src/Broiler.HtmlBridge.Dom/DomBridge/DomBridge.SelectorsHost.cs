using System.Collections.Generic;
using Broiler.Dom;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

// Explicit ISelectorsHost implementation for the SelectorsBinding feature module (Phase 3): the bridge
// exposes the descendant selector search, the by-tag descendant collector and the JS-wrapper factory via
// explicit interface members (each forwards to the corresponding static/instance helper, passing the
// bridge in), so the module reaches no arbitrary bridge private field and the public surface is unchanged.
public sealed partial class DomBridge : Dom.Features.ISelectorsHost
{
    JSValue Dom.Features.ISelectorsHost.FindInDescendants(DomElement element, string selector, bool all)
        => FindInDescendants(element, selector, all, this);

    void Dom.Features.ISelectorsHost.CollectElementsByTagName(DomElement element, string tagName, List<JSValue> results)
        => CollectDescendantsByTag(element, tagName, results, this);

    JSObject Dom.Features.ISelectorsHost.ToJSObject(DomNode node) => ToJSObject(node);
}
