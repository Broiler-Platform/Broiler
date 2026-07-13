using Broiler.JavaScript.Runtime;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// <see cref="DomBridge"/>'s implementation of <see cref="IMutationObserverHost"/>, the narrow
/// contract the extracted <see cref="Broiler.HtmlBridge.Dom.Features.MutationObserverBinding"/>
/// feature module consumes (HtmlBridge complexity-reduction roadmap Phase 3). Explicit interface
/// members, so these seams do not widen the public <c>DomBridge</c> surface.
/// </summary>
public sealed partial class DomBridge : IMutationObserverHost
{
    JSObject IMutationObserverHost.ToJSObject(DomNode node) => ToJSObject(node);

    DomNode? IMutationObserverHost.FindDomNodeByJSObject(JSObject? jsObj) =>
        jsObj is null ? null : FindDomNodeByJSObject(jsObj);
}
