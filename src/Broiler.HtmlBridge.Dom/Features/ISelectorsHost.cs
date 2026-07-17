using System.Collections.Generic;
using Broiler.Dom;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The narrow host surface <see cref="SelectorsBinding"/> needs from the bridge: the descendant selector
/// search (<c>querySelector</c>/<c>querySelectorAll</c> — it wraps every hit through the bridge's JS-object
/// cache), the by-tag descendant collector (<c>getElementsByTagName</c>) and the plain JS-wrapper factory
/// (<c>closest</c>). Selector matching itself (<c>MatchesSelector</c>) and the element-parent walk
/// (<c>ParentEl</c>) are the bridge's <c>internal static</c> helpers, called directly.
/// </summary>
internal interface ISelectorsHost
{
    JSValue FindInDescendants(DomElement element, string selector, bool all);
    void CollectElementsByTagName(DomElement element, string tagName, List<JSValue> results);
    JSObject ToJSObject(DomNode node);
}
