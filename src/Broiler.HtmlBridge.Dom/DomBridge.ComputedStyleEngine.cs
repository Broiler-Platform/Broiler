using Broiler.Dom;
using Broiler.CSS.Dom;
using Broiler.HtmlBridge.Dom.Runtime;

namespace Broiler.HtmlBridge;

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
    // P2.3: the per-document engine scopes, the GetComputedProps memo and the style-invalidation
    // batch state now live in DocumentStyleContext, the single computed-style authority (was the
    // scattered _computedStyleEngines/_computedPropsCache/_computedPropsInProgress/
    // _styleInvalidationBatchDepth/_pendingStyleInvalidationRoots fields).
    private readonly DocumentStyleContext _styleContext = new();

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
    private void ResetComputedStyleEngines() => _styleContext.ResetEngines();

    /// <summary>
    /// Returns the shared <see cref="CssStyleEngine"/> for
    /// <paramref name="element"/>'s document root, creating it on first use and
    /// re-syncing its scoped stylesheet set (the same <c>&lt;style&gt;</c>/<c>&lt;link&gt;</c>/
    /// inserted-CSSOM text the legacy cascade saw) whenever that text changes.
    /// </summary>
    private CssStyleEngine GetSyncedScopedEngine(DomElement element)
    {
        var docRoot = GetDocumentRootFor(element);
        var scope = _styleContext.GetOrCreateEngineScope(docRoot, () =>
        {
            // Non-static so the `:checked` state provider can read this bridge's per-instance
            // FormControl table (Phase 2 item 4 de-globalization).
            var engine = new CssStyleEngine(new BridgeSelectorStateProvider(this));
            // Feed the bridge's live ElementRuntimeState inline map as the cascade's inline
            // source (see SerializeInlineStyleForEngine) so the engine sees JS-set and
            // anchor-resolver-written inline that never reaches the DOM style attribute.
            engine.SetInlineStyleSource(SerializeInlineStyleForEngine);
            return new ComputedStyleEngineScope(new CssStyleScopeBuilder(engine), engine);
        });

        var styleElements = new List<DomElement>();
        CollectStyleElementsInTree(docRoot, styleElements);

        // Hand the collected sheets to the canonical scope builder in document order; it
        // gates each on the element's `media` attribute against the viewport and re-syncs the
        // engine only when the effective set changes. Text extraction (canonical DomText children /
        // CSSOM rule text / external-sheet runtime state) stays here because it needs the DOM and loading.
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
