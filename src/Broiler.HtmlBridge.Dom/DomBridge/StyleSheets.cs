using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
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

    /// <summary>Collects all style elements in the sub-tree. Phase 4 item 4/5: reuses canonical
    /// <see cref="DomNode.Descendants"/> (document-order, level-snapshotted against concurrent mutation)
    /// instead of a hand-rolled depth-first recursion over the live child list.</summary>
    private void CollectStyleElements(DomNode root, List<DomElement> results)
    {
        foreach (var element in root.Descendants().OfType<DomElement>())
        {
            if (!(string.Equals(element.TagName, "style", StringComparison.OrdinalIgnoreCase) || IsExternalStylesheet(element)))
                continue;

            // A disabled <link> has no associated CSS style sheet, so it is absent from
            // document.styleSheets (HTML §4.2.4 <link disabled>). A <style> whose sheet was
            // disabled via CSSOM (CSSStyleSheet.disabled) still appears in the collection —
            // only its rules stop applying — so it is not filtered here.
            if (IsStyleSheetDisabled(element) &&
                string.Equals(element.TagName, "link", StringComparison.OrdinalIgnoreCase))
                continue;

            results.Add(element);
        }
    }

    /// <summary>
    /// The effective CSSOM <c>disabled</c> state of a <c>&lt;style&gt;</c>/<c>&lt;link&gt;</c>
    /// stylesheet: the script-set <c>CSSStyleSheet.disabled</c> flag when present, otherwise
    /// the element's <c>disabled</c> content attribute (only a <c>&lt;link&gt;</c> carries one —
    /// <c>HTMLLinkElement.disabled</c> reflects it). A disabled sheet does not apply to the
    /// cascade (CSSOM §2.3).
    /// </summary>
    private bool IsStyleSheetDisabled(DomElement element)
    {
        var state = StyleSheetStateFor(element);
        if (state.DisabledOverride is bool overridden)
            return overridden;

        return string.Equals(element.TagName, "link", StringComparison.OrdinalIgnoreCase)
            && HasAttr(element, "disabled");
    }

    /// <summary>
    /// Sets the script-driven <c>CSSStyleSheet.disabled</c> flag on a stylesheet element and
    /// re-runs the cascade so a newly (un)disabled sheet (dis)appears from computed style.
    /// Does not touch the <c>disabled</c> content attribute — that is only reflected by
    /// <c>HTMLLinkElement.disabled</c>, not by <c>CSSStyleSheet.disabled</c>.
    /// </summary>
    private void SetStyleSheetDisabledFlag(DomElement element, bool value)
    {
        StyleSheetStateFor(element).DisabledOverride = value;
        InvalidateStyleScope(element);
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

        // disabled — CSSOM StyleSheet.disabled. A true value prevents the sheet from
        // applying (CSSOM §2.3). Getting reads the effective state (script flag, else the
        // <link disabled> content attribute); setting stores the script flag and re-cascades.
        sheet.FastAddProperty((KeyString)"disabled",
            new JSFunction((in _) => IsStyleSheetDisabled(styleElement) ? JSBoolean.True : JSBoolean.False, "get disabled"),
            new JSFunction((in a) => { SetStyleSheetDisabledFlag(styleElement, a.Length > 0 && a[0].BooleanValue); return JSUndefined.Value; }, "set disabled"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

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
