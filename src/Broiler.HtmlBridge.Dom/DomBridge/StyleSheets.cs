using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.Dom;
using Broiler.CSS;

namespace Broiler.HtmlBridge;

/// <summary>
/// CSSOM — the <c>document.styleSheets</c> collection and the individual
/// <c>CSSStyleSheet</c> objects (per-element identity cache, the live <c>cssRules</c>
/// collection, and <c>insertRule</c>/<c>deleteRule</c> mutation bookkeeping). The
/// <c>CSSRuleList</c>/<c>CSSRule</c> object model and the <c>JsStyleSheets*Core</c> callbacks
/// this builds on live in the <see cref="Dom.Features.StyleSheetBinding"/> feature module
/// (Phase 3, P3.15).
/// </summary>
public sealed partial class DomBridge
{
    /// <summary>Cache for stylesheet objects, keyed by the owning style element.</summary>
    private readonly Dictionary<DomElement, JSObject> _styleSheetCache = [];

    private JSArray BuildStyleSheetsCollection(DomNode docRoot)
    {
        var styleEls = new List<DomElement>();
        CollectStyleElements(docRoot, styleEls);

        var arr = new JSArray();
        foreach (var styleEl in styleEls)
        {
            var sheet = BuildStyleSheetObject(styleEl);
            arr.Add(sheet);
        }

        return arr;
    }

    /// <summary>Collects all style elements in the sub-tree.</summary>
    private static void CollectStyleElements(DomNode root, List<DomElement> results)
    {
        foreach (var child in ChildElements(root))
        {
            if (string.Equals(child.TagName, "style", StringComparison.OrdinalIgnoreCase))
                results.Add(child);
            else if (IsExternalStylesheet(child))
                results.Add(child);
            CollectStyleElements(child, results);
        }
    }

    /// <summary>
    /// Returns <c>true</c> if the element is a <c>&lt;link rel="stylesheet" href="..."&gt;</c>.
    /// </summary>
    private static bool IsExternalStylesheet(DomElement element)
    {
        if (!string.Equals(element.TagName, "link", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!TryGetAttribute(element, "rel", out var rel) ||
            !rel.Contains("stylesheet", StringComparison.OrdinalIgnoreCase))
            return false;
        return HasAttr(element, "href");
    }

    /// <summary>
    /// Builds a CSSStyleSheet JSObject for a style element.
    /// Cached per style element to ensure identity (the same object is returned
    /// each time, making cssRules a live collection per the CSSOM spec).
    /// </summary>
    private JSObject BuildStyleSheetObject(DomElement styleElement)
    {
        if (_styleSheetCache.TryGetValue(styleElement, out var cached))
            return cached;

        var sheet = new JSObject();

        // ownerNode
        sheet.FastAddProperty((KeyString)"ownerNode", new JSFunction((in _) => ToJSObject(styleElement), "get ownerNode"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // href — null for inline stylesheets
        sheet.FastAddProperty((KeyString)"href", NullFunction("get href"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // Internal rules storage for this stylesheet — the single shared, mutable
        // Broiler.CSS rule model held in the element's runtime state (Phase 6 store
        // unification). The same list backs the renderer text and the
        // getComputedStyle engine, so a script insertRule/deleteRule here is observed
        // by both. CurrentRules() reparses on textContent change before returning it.
        List<CssRule> CurrentRules() => EnsureStyleSheetRulesCurrent(styleElement);
        void MarkRulesMutated() => StyleSheetStateFor(styleElement).RulesMutated = true;

        // Live cssRules object — single instance that always reflects current state
        var liveCssRules = new JSObject();
        var lastSyncedRuleCount = 0;
        // length is a live getter that always reflects the current rule count
        liveCssRules.FastAddProperty((KeyString)"length",
            new JSFunction((in _) => Dom.Features.StyleSheetBinding.JsStyleSheetsGetLength002Core(CurrentRules, in _), "get length"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        liveCssRules.FastAddValue((KeyString)"item",
            new JSFunction((in a) => Dom.Features.StyleSheetBinding.JsStyleSheetsItem003Core(SyncLiveCssRulesIndices, liveCssRules, CurrentRules, in a), "item", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Syncs indexed properties on the live cssRules object with the shared model
        void SyncLiveCssRulesIndices()
        {
            var rules = CurrentRules();
            for (var i = 0; i < rules.Count; i++)
            {
                var ruleObj = Dom.Features.StyleSheetBinding.BuildCssRuleObject(rules[i], sheet);
                liveCssRules[(uint)i] = ruleObj;
            }

            for (var i = rules.Count; i < lastSyncedRuleCount; i++)
                liveCssRules.GetElements().RemoveAt((uint)i);

            lastSyncedRuleCount = rules.Count;
        }

        // cssRules — returns the live collection, syncing indices on access
        sheet.FastAddProperty((KeyString)"cssRules",
            new JSFunction((in _) => Dom.Features.StyleSheetBinding.JsStyleSheetsGetCssRules004Core(SyncLiveCssRulesIndices, liveCssRules, in _), "get cssRules"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // insertRule(rule, index) — mutates the shared model (marking it mutated so
        // the renderer/engine serialize from it) and resyncs the live collection
        sheet.FastAddValue((KeyString)"insertRule",
            new JSFunction((in a) => Dom.Features.StyleSheetBinding.JsStyleSheetsInsertRule005Core(CurrentRules, MarkRulesMutated, SyncLiveCssRulesIndices, in a), "insertRule", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // deleteRule(index) — removes a rule from the shared model
        sheet.FastAddValue((KeyString)"deleteRule",
            new JSFunction((in a) => Dom.Features.StyleSheetBinding.JsStyleSheetsDeleteRule006Core(CurrentRules, MarkRulesMutated, SyncLiveCssRulesIndices, in a), "deleteRule", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        _styleSheetCache[styleElement] = sheet;
        return sheet;
    }

}
