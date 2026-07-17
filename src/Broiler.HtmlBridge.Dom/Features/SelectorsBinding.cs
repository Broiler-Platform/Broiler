using System.Collections.Generic;
using Broiler.Dom;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Array;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// Phase 3 feature module for the DOM <c>Element</c> selector API — <c>querySelector</c>,
/// <c>querySelectorAll</c>, <c>matches</c>, <c>closest</c> and <c>getElementsByTagName</c>. These were the
/// bridge's <c>JsJsObjectsQuerySelector126Core</c>..<c>Closest129Core</c> and
/// <c>GetElementsByTagName133Core</c> callbacks; the descendant selector search, the by-tag collector and
/// the JS-wrapper factory reach the bridge through <see cref="ISelectorsHost"/>, while selector matching
/// (<c>MatchesSelector</c>) and the element-parent walk (<c>ParentEl</c>) are the bridge's
/// <c>internal static</c> helpers, called directly.
/// </summary>
internal static class SelectorsBinding
{
    public static JSValue QuerySelector(ISelectorsHost host, DomElement element, in Arguments a)
    {
        var sel = a.Length > 0 ? a[0].ToString() : string.Empty;
        return host.FindInDescendants(element, sel, false);
    }

    public static JSValue QuerySelectorAll(ISelectorsHost host, DomElement element, in Arguments a)
    {
        var sel = a.Length > 0 ? a[0].ToString() : string.Empty;
        return host.FindInDescendants(element, sel, true);
    }

    public static JSValue Matches(DomElement element, in Arguments a)
    {
        var sel = a.Length > 0 ? a[0].ToString() : string.Empty;
        return DomBridge.MatchesSelector(element, sel, element) ? JSBoolean.True : JSBoolean.False;
    }

    public static JSValue Closest(ISelectorsHost host, DomElement element, in Arguments a)
    {
        var sel = a.Length > 0 ? a[0].ToString() : string.Empty;
        for (DomElement? current = element; current != null && !current.TagName.StartsWith('#'); current = DomBridge.ParentEl(current))
        {
            if (DomBridge.MatchesSelector(current, sel, element))
                return host.ToJSObject(current);
        }

        return JSNull.Value;
    }

    public static JSValue GetElementsByTagName(ISelectorsHost host, DomElement element, in Arguments a)
    {
        var tagSearch = a.Length > 0 ? a[0].ToString().ToLowerInvariant() : string.Empty;
        var results = new List<JSValue>();
        host.CollectElementsByTagName(element, tagSearch, results);
        return new JSArray(results);
    }
}
