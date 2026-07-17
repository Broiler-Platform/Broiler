using Broiler.Dom;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The narrow host surface <see cref="CharacterDataBinding"/> needs from the bridge: the
/// notifying character-data setter (mutation-observer aware), the text-node factory (for
/// <c>splitText</c>), the JS-wrapper factory, and wrapper-cache invalidation. Read-side text access,
/// node-type tests and the neutral tree helpers are the bridge's <c>internal static</c> helpers,
/// called directly.
/// </summary>
internal interface ICharacterDataHost
{
    void SetCharacterData(DomNode node, string? value);
    DomText CreateBridgeTextNode(string data);
    JSObject ToJSObject(DomNode node);
    void RemoveJsObject(DomNode node);
}
