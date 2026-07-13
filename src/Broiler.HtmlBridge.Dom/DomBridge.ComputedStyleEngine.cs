using Broiler.Dom;
using Broiler.CSS.Dom;

namespace Broiler.HtmlBridge.Dom;

/// <summary>
/// Phase 4 cutover: <c>getComputedStyle()</c> resolves through the shared
/// <see cref="CssStyleEngine"/> (cascade, inheritance, custom
/// properties, shorthands, initial values) instead of the bridge's legacy
/// <c>BuildComputedStyleMap</c> cascade. The bridge still owns stylesheet
/// discovery, <c>&lt;link&gt;</c> fetching, CSSOM rule text, sub-document scoping,
/// and the JavaScript <c>CSSStyleDeclaration</c> wrapper; only the cascade and
/// computed-style authority moves into <c>Broiler.CSS.Dom</c>.
/// </summary>
public sealed partial class DomBridge
{
    // One engine per document root: this keeps the engine's mutation-driven
    // computed-style cache and its single DomDocument.Mutated subscription intact
    // across calls, instead of leaking a subscription per getComputedStyle() call.
    // The scoped stylesheet set is re-synced only when the collected
    // <style>/<link>/inserted-rule text actually changes.
    private readonly Dictionary<DomElement, ComputedStyleEngineScope> _computedStyleEngines = [];

    private sealed class ComputedStyleEngineScope
    {
        public required CssStyleScopeBuilder ScopeBuilder { get; init; }
        // The wrapped engine, held directly so ERS-inline mutations (which the engine's
        // DOM-mutation subscription does not observe) can invalidate its computed caches.
        public required CssStyleEngine Engine { get; init; }
    }

    /// <summary>
    /// Invalidates every per-document engine's cascade/computed-style caches. Called
    /// alongside clearing <c>_computedPropsCache</c> because those engines now read inline
    /// style from the bridge's live ElementRuntimeState map (via
    /// <see cref="CssStyleEngine.SetInlineStyleSource"/>), whose mutations
    /// are not DOM mutations and so do not trigger the engine's own invalidation.
    /// </summary>
    private void InvalidateScopedEngineComputedCaches()
    {
        foreach (var scope in _computedStyleEngines.Values)
            scope.Engine.InvalidateComputedStyleCaches();
    }

    /// <summary>
    /// Serializes an element's live inline-style map (ElementRuntimeState) to a CSS
    /// declaration string for the canonical engine's cascade — the bridge's authoritative
    /// inline source, which includes JS <c>el.style.X=</c> writes and anchor-resolver-written
    /// geometry that never reach the DOM <c>style</c> attribute the engine would otherwise read.
    /// Returns <c>null</c> when there is no inline style.
    /// </summary>
    private static string? SerializeInlineStyleForEngine(DomElement element)
    {
        var inline = InlineStyle(element);
        if (inline.Count == 0)
            return null;
        var sb = new System.Text.StringBuilder();
        foreach (var kv in inline)
        {
            if (sb.Length > 0)
                sb.Append(';');
            sb.Append(kv.Key).Append(':').Append(kv.Value);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Resets the per-scope computed-style engines. Called when the document tree
    /// is rebuilt so stale document roots do not retain engines or subscriptions.
    /// </summary>
    private void ResetComputedStyleEngines() => _computedStyleEngines.Clear();

    /// <summary>
    /// Returns the shared <see cref="CssStyleEngine"/> for
    /// <paramref name="element"/>'s document root, creating it on first use and
    /// re-syncing its scoped stylesheet set (the same <c>&lt;style&gt;</c>/<c>&lt;link&gt;</c>/
    /// inserted-CSSOM text the legacy cascade saw) whenever that text changes.
    /// </summary>
    private CssStyleEngine GetSyncedScopedEngine(DomElement element)
    {
        var docRoot = GetDocumentRootFor(element);
        if (!_computedStyleEngines.TryGetValue(docRoot, out var scope))
        {
            var engine = new CssStyleEngine(new BridgeSelectorStateProvider());
            // Feed the bridge's live ElementRuntimeState inline map as the cascade's inline
            // source (see SerializeInlineStyleForEngine) so the engine sees JS-set and
            // anchor-resolver-written inline that never reaches the DOM style attribute.
            engine.SetInlineStyleSource(SerializeInlineStyleForEngine);
            scope = new ComputedStyleEngineScope
            {
                ScopeBuilder = new CssStyleScopeBuilder(engine),
                Engine = engine,
            };
            _computedStyleEngines[docRoot] = scope;
        }

        var styleElements = new List<DomElement>();
        CollectStyleElementsInTree(docRoot, styleElements);

        // Hand the collected sheets to the canonical scope builder in document order; it
        // gates each on the element's `media` attribute against the viewport and re-syncs the
        // engine only when the effective set changes. Text extraction (InnerHtml / CSSOM rule
        // text / external-sheet runtime state) stays here because it needs the DOM and loading.
        var sources = new List<CssStyleScopeBuilder.StyleSource>(styleElements.Count);
        foreach (var styleEl in styleElements)
            sources.Add(new CssStyleScopeBuilder.StyleSource(
                GetStyleElementCssText(styleEl),
                CSS.Dom.CssOrigin.Author,
                GetAttr(styleEl, "media")));

        var (vpWidth, vpHeight) = GetViewportForDocRoot(docRoot);
        return scope.ScopeBuilder.Sync(sources, new CssEnvironment(vpWidth, vpHeight));
    }

    /// <summary>
    /// Builds the computed-style map for <paramref name="element"/> through the
    /// shared <see cref="CssStyleEngine"/>, scoped to the
    /// element's document root.
    /// </summary>
    private Dictionary<string, string> BuildComputedStyleMapViaEngine(DomElement element, string? pseudoElement)
    {
        var computed = GetSyncedScopedEngine(element).GetComputedStyle(element, pseudoElement: pseudoElement);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in computed.Properties)
            map[pair.Key] = pair.Value;

        return map;
    }

    /// <summary>
    /// Returns the cascade-winning <em>declared</em> CSS property values for
    /// <paramref name="element"/> from matching stylesheet rules (no inline styles,
    /// inheritance, or initial-value backfill), via the shared style engine. This
    /// replaces the legacy <c>foreach (… in CssRules) if (MatchesSelector(…))</c>
    /// collection loops; callers still merge <c>InlineStyle(element)</c> on top as before.
    /// </summary>
    private Dictionary<string, string> CollectMatchedRuleProperties(DomElement element) =>
        new(GetSyncedScopedEngine(element).GetCascadedDeclaredValues(element), StringComparer.OrdinalIgnoreCase);
}
