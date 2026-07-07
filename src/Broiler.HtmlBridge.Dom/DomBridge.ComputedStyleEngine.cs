using System.Text;

namespace Broiler.HtmlBridge;

/// <summary>
/// Phase 4 cutover: <c>getComputedStyle()</c> resolves through the shared
/// <see cref="Broiler.CSS.Dom.CssStyleEngine"/> (cascade, inheritance, custom
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
        public required Broiler.CSS.Dom.CssStyleEngine Engine { get; init; }

        public int StyleSheetHash { get; set; } = -1;
    }

    /// <summary>
    /// Resets the per-scope computed-style engines. Called when the document tree
    /// is rebuilt so stale document roots do not retain engines or subscriptions.
    /// </summary>
    private void ResetComputedStyleEngines() => _computedStyleEngines.Clear();

    /// <summary>
    /// Returns the shared <see cref="Broiler.CSS.Dom.CssStyleEngine"/> for
    /// <paramref name="element"/>'s document root, creating it on first use and
    /// re-syncing its scoped stylesheet set (the same <c>&lt;style&gt;</c>/<c>&lt;link&gt;</c>/
    /// inserted-CSSOM text the legacy cascade saw) whenever that text changes.
    /// </summary>
    private Broiler.CSS.Dom.CssStyleEngine GetSyncedScopedEngine(DomElement element)
    {
        var docRoot = GetDocumentRootFor(element);
        if (!_computedStyleEngines.TryGetValue(docRoot, out var scope))
        {
            scope = new ComputedStyleEngineScope
            {
                Engine = new Broiler.CSS.Dom.CssStyleEngine(new BridgeSelectorStateProvider()),
            };
            _computedStyleEngines[docRoot] = scope;
        }

        var styleElements = new List<DomElement>();
        CollectStyleElementsInTree(docRoot, styleElements);

        var combined = new StringBuilder();
        foreach (var styleEl in styleElements)
            combined.Append(GetStyleElementCssText(styleEl)).Append('\n');
        var combinedText = combined.ToString();

        var hash = combinedText.GetHashCode(StringComparison.Ordinal);
        if (hash != scope.StyleSheetHash)
        {
            scope.Engine.ClearStyleSheets();
            if (combinedText.Length > 0)
            {
                var sheet = new Broiler.CSS.CssParser().ParseStyleSheet(combinedText);
                scope.Engine.AddStyleSheet(sheet, CSS.Dom.CssOrigin.Author);
            }

            scope.StyleSheetHash = hash;
        }

        var (vpWidth, vpHeight) = GetViewportForDocRoot(docRoot);
        scope.Engine.UpdateEnvironment(new Broiler.CSS.Dom.CssEnvironment(vpWidth, vpHeight));
        return scope.Engine;
    }

    /// <summary>
    /// Builds the computed-style map for <paramref name="element"/> through the
    /// shared <see cref="Broiler.CSS.Dom.CssStyleEngine"/>, scoped to the
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
    /// collection loops; callers still merge <c>element.Style</c> on top as before.
    /// </summary>
    private Dictionary<string, string> CollectMatchedRuleProperties(DomElement element) =>
        new(GetSyncedScopedEngine(element).GetCascadedDeclaredValues(element), StringComparer.OrdinalIgnoreCase);
}
