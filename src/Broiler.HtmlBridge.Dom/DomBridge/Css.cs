using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HtmlBridge.Logging;
using Broiler.HtmlBridge.Scripting;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.Dom;
using Broiler.CSS;
using Broiler.CSS.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// CSS specificity calculation, style-block extraction, rule cascading,
/// computed-style building, and media-query evaluation.
/// </summary>
public sealed partial class DomBridge
{
    // Phase 2 item 4 (de-globalization, 2026-07-17): the per-style-element stylesheet state (fetched
    // CSS text, the live CSSOM rule list and its parse-source / mutated flags) was the StyleSheet slot
    // of the process-static ElementRuntimeState table; it is now a per-bridge instance table, owned by
    // the session's bridge. Still an element-keyed ConditionalWeakTable, so it GCs with the style
    // element and the cloneNode copy (see CloneDomElement) is preserved. All access is on the bridge
    // instance, so this needed no static-helper cascade.
    private readonly ConditionalWeakTable<DomElement, StyleSheetRuntimeState> _styleSheetRuntimeStates = [];

    private StyleSheetRuntimeState StyleSheetStateFor(DomElement element) =>
        _styleSheetRuntimeStates.GetValue(element, static _ => new StyleSheetRuntimeState());

    // P2.3: computed-style state (the GetComputedProps memo and the style-invalidation batch depth /
    // pending roots) moved to DocumentStyleContext (see _styleContext). The memo maps are concurrent
    // there for the same reason they were here — JS continuations on ThreadPool threads re-enter
    // computed-style/geometry work concurrently with the main-thread layout pass, and a plain
    // dictionary corrupts under that race and aborts the process/WPT shard (issue #1143).

    /// <summary>
    /// Clears the bridge's <c>GetComputedProps</c> memo <em>and</em> the per-document engines'
    /// cascade/computed-style caches together — the single computed-style invalidation route
    /// (see <see cref="DocumentStyleContext.InvalidateComputedStyle"/>). The two must invalidate as
    /// one because <c>GetComputedProps</c> routes through the engine's sparse projection, which reads
    /// inline style from the live ElementRuntimeState map (an ERS mutation is invisible to the
    /// engine's own DOM-mutation subscription).
    /// </summary>
    private void ClearComputedPropsCache() => _styleContext.InvalidateComputedStyle();

    // ------------------------------------------------------------------
    //  CSS specificity (Level 3) and <style> / <link> cascading
    // ------------------------------------------------------------------


    // htmlbridge-public-surface/v2 (declared 2026-07-10): the compatibility
    // `CssRules` tuple view and the `CalculateSpecificity` static delegation shim
    // were removed here (Milestone 1.1). They had no production callers; consumers
    // use the shared Broiler.CSS parser (`CssParser` / `CssStyleRule` /
    // `CssDeclarationBlock.GetPropertyValue`) and `CssSelectorParser.CalculateSpecificity`
    // directly. See docs/architecture/htmlbridge.md#canonical-owners-and-bridge-responsibilities.

    /// <summary>
    /// Clears any CSS-derived compatibility values left in the element's inline style
    /// (<see cref="ElementRuntimeState.Style"/>, reached via <c>InlineStyle</c>)
    /// after a selector-affecting mutation. Stylesheet declarations are resolved lazily
    /// by the shared style engine; only inline declarations and JavaScript-set values
    /// remain in the bridge-owned declaration map.
    /// </summary>
    internal void InvalidateElementStyles(DomElement element)
    {
        // 1. Collect property names that come from the inline style attribute.
        //    These must never be cleared or overwritten by the cascade.
        var inlineStyleProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (TryGetAttribute(element, "style", out var inlineStyle) &&
            !string.IsNullOrEmpty(inlineStyle))
        {
            foreach (var kv in ParseStyle(inlineStyle))
                inlineStyleProps.Add(kv.Key);
        }

        // Remove all CSS-derived properties (keep inline ones AND JS-set ones).
        var keysToRemove = InlineStyle(element).Keys
            .Where(k => !inlineStyleProps.Contains(k) && !InlineStyleStateFor(element).JsSetStyleProps.Contains(k))
            .ToList();
        foreach (var key in keysToRemove)
            InlineStyle(element).Remove(key);
    }

    /// <summary>
    /// Recalculates CSS-derived inline styles for every element in the current
    /// document scope after a selector-affecting mutation such as a class,
    /// attribute, or sibling structure change.
    /// </summary>
    internal void BeginStyleInvalidationBatch() => _styleContext.BeginBatch();

    internal void EndStyleInvalidationBatch()
    {
        if (_styleContext.EndBatchShouldFlush())
            FlushPendingStyleInvalidations();
    }

    internal void InvalidateStyleScope(DomElement anchor)
    {
        ClearComputedPropsCache();
        var docRoot = GetDocumentRootFor(anchor);
        if (_styleContext.TryDeferRoot(docRoot))
            return;

        InvalidateStyleScopeRecursive(docRoot);
    }

    private void FlushPendingStyleInvalidations()
    {
        foreach (var root in _styleContext.DrainPendingRoots())
            InvalidateStyleScopeRecursive(root);
    }

    private void InvalidateStyleScopeRecursive(DomElement element)
    {
        if (!IsText(element) && !element.TagName.StartsWith('#'))
            InvalidateElementStyles(element);

        // Sub-documents keep their own style scope, but since P4.4b severed the #subdoc-root element
        // they are no longer in-tree children, so this walk never crosses a sub-document boundary.
        foreach (var child in ChildElements(element))
        {
            if (!IsText(child))
                InvalidateStyleScopeRecursive(child);
        }
    }

    /// <summary>
    /// Finds the style-scope root ancestor for the given element by walking up the
    /// parent chain. Stops at a <c>#</c>-prefixed boundary element (a <c>#shadow-root</c>;
    /// the document/sub-document sentinels are gone — the canonical <c>DomDocument</c> parent
    /// is not a <c>DomElement</c>, so <see cref="ParentEl"/> already stops the walk there).
    /// Returns the topmost element within the element's scope.
    /// </summary>
    private static DomElement GetDocumentRootFor(DomElement el)
    {
        var root = el;
        while (ParentEl(root) != null)
        {
            // If we've reached a scope root (shadow root), stop here
            if (root.TagName.StartsWith('#'))
                return root;
            root = ParentEl(root);
        }
        return root;
    }

    /// <summary>
    /// Collects all <c>&lt;style&gt;</c> (and external-stylesheet <c>&lt;link&gt;</c>) elements from a
    /// document tree. Sub-documents keep their own style scope, but since P4.4b severed the
    /// <c>#subdoc-root</c> element they are no longer in-tree children, so this walk never crosses a
    /// sub-document boundary. Phase 4 item 4/5: reuses canonical <see cref="DomNode.Descendants"/>
    /// (document-order, level-snapshotted against the concurrent-mutation race the per-level
    /// <see cref="SnapshotChildren"/> walk guarded — Descendants snapshots the real child list, so the
    /// LegacyChildList projection overflow cannot occur here) instead of the hand-rolled recursion.
    /// </summary>
    private void CollectStyleElementsInTree(DomElement root, List<DomElement> styleElements)
    {
        foreach (var element in root.Descendants().OfType<DomElement>())
        {
            if (!(string.Equals(element.TagName, "style", StringComparison.OrdinalIgnoreCase) || IsExternalStylesheet(element)))
                continue;

            // A disabled sheet (CSSOM CSSStyleSheet.disabled, or a <link disabled> content
            // attribute) does not contribute to the cascade — CSSOM §2.3.
            if (IsStyleSheetDisabled(element))
                continue;

            styleElements.Add(element);
        }
    }

    /// <summary>
    /// Snapshots an element's children in a way that tolerates concurrent DOM
    /// mutation (parallel WPT rendering, JS-driven tree edits, or a lazy
    /// sub-document root materialising during the walk).
    /// </summary>
    /// <remarks>
    /// A plain <c>ChildElements(root).ToList()</c> is NOT thread-safe here.
    /// <see cref="DomElement.LegacyChildList"/> projects the live
    /// <c>ChildNodes</c> collection: <see cref="Enumerable.ToList{T}"/> reads
    /// <c>Count</c>, allocates a destination array of that size, then calls
    /// <c>CopyTo</c>, which materialises the <em>current</em> (possibly larger)
    /// child array. If another thread appends between those two steps the copy
    /// overflows and throws <see cref="ArgumentException"/> ("Destination array
    /// was not long enough" — signature
    /// <c>DomBridge.CollectStyleElementsInTree</c>); a mutation during plain
    /// enumeration instead throws <see cref="InvalidOperationException"/>
    /// ("Collection was modified"). Either previously aborted style collection
    /// for the whole tree, leaving the document unstyled. Retry a bounded number
    /// of times, then fall back to a tolerant index walk.
    /// </remarks>
    private static List<DomElement> SnapshotChildren(DomElement root)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                return ChildElements(root).ToList();
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                // Concurrent structural mutation raced the snapshot; retry with a
                // fresh copy. Transient contention almost always clears in a
                // couple of attempts.
            }
        }

        // Sustained contention: copy element-by-element, re-checking bounds each
        // step so a shrinking list can only truncate the snapshot, never throw.
        var snapshot = new List<DomElement>();
        for (var i = 0; ; i++)
        {
            DomElement? child;
            try
            {
                if (i >= root.ChildNodes.Count)
                    break;
                // Element snapshot: a char-data child (post-flip) is skipped (null).
                child = ChildAt(root, i) as DomElement;
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException)
            {
                break;
            }

            if (child is not null)
                snapshot.Add(child);
        }

        return snapshot;
    }

    // The getComputedStyle result object is built by the Phase 3 (P3.14) StyleDeclarationBinding
    // feature module; the bridge still produces the engine-cascaded computed map here.
    private JSObject BuildComputedStyleObject(DomElement? element, string? pseudoElement = null)
    {
        var map = BuildComputedStyleMap(element, pseudoElement);
        // `overlay` (CSS Position 4) is UA-controlled — it is not in the author cascade, so the
        // engine map never carries it. Surface its computed value for getComputedStyle here (copying
        // first so a memoised engine map is never mutated). Pseudo-elements never enter the top layer.
        if (element != null && pseudoElement == null)
        {
            map = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase)
            {
                ["overlay"] = ComputeOverlayValue(element),
            };
        }

        return Dom.Features.StyleDeclarationBinding.BuildComputedDeclaration(map);
    }

    private Dictionary<string, string> BuildComputedStyleMap(DomElement? element, string? pseudoElement = null)
    {
        // getComputedStyle() resolves through the shared Broiler.CSS.Dom.CssStyleEngine
        // (BuildComputedStyleMapViaEngine, see DomBridge.ComputedStyleEngine.cs). The legacy
        // bridge computed-style cascade was retired in Phase 7 cleanup (RF-CSS-1); the engine
        // has been the sole getComputedStyle authority since the 2026-06-26 cutover, after it
        // gained the bridge's per-declaration value validation / error recovery and
        // border-shorthand reset semantics.
        if (element == null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return BuildComputedStyleMapViaEngine(element, pseudoElement);
    }

    private Dictionary<string, string> BuildSpecifiedStyleMap(DomElement element, string? pseudoElement = null)
    {
        pseudoElement = CssStyleEngine.NormalizePseudoElement(pseudoElement);
        var specified = new Dictionary<string, string>(
            GetSyncedScopedEngine(element).GetCascadedDeclaredValues(element, pseudoElement),
            StringComparer.OrdinalIgnoreCase);

        if (pseudoElement == null &&
            TryGetAttribute(element, "style", out var inlineStyleAttr) &&
            !string.IsNullOrEmpty(inlineStyleAttr))
        {
            foreach (var kv in ParseStyle(inlineStyleAttr))
                specified[kv.Key] = kv.Value;
        }

        return specified;
    }


    private static bool IsSelectListBox(DomElement element) => GetSelectVisibleRowCount(element) > 1;

    private static int GetSelectVisibleRowCount(DomElement element)
    {
        bool isMultiple = HasAttr(element, "multiple");
        if (TryGetAttribute(element, "size", out var rawSize) &&
            int.TryParse(rawSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSize) &&
            parsedSize > 0)
        {
            return parsedSize;
        }

        return isMultiple ? 4 : 1;
    }

    /// <summary>
    /// Raw author <em>source</em> text for a style element — its canonical text-node
    /// children, or a cached/fetched linked stylesheet — <em>without</em> any CSSOM
    /// <c>insertRule</c>/<c>deleteRule</c>
    /// mutations applied. This is the input from which the shared rule model is
    /// (re)parsed; <see cref="GetStyleElementCssText"/> applies mutations on top.
    /// </summary>
    private string GetStyleElementSourceText(DomElement styleEl)
    {
        var cssText = new StringBuilder();
        // RF-BRIDGE-1c Phase F (F3c part 2d): iterate raw ChildNodes — the <style> text is a
        // canonical DomText child, which ChildElements (OfType) would skip.
        foreach (var child in styleEl.ChildNodes)
        {
            if (IsText(child))
                cssText.Append(BridgeText(child));
        }

        if (string.Equals(styleEl.TagName, "link", StringComparison.OrdinalIgnoreCase) &&
            cssText.Length == 0 &&
            TryGetAttribute(styleEl, "href", out var href) &&
            !string.IsNullOrEmpty(href) &&
            IsExternalStyleAllowedByCsp(styleEl, href))
        {
            if (StyleSheetStateFor(styleEl).FetchedCss.TryGet(out var cachedCss) && cachedCss is string cachedStr)
            {
                cssText.Append(cachedStr);
            }
            else
            {
                try
                {
                    var fetchedCss = FetchExternalStylesheet(href);
                    if (!string.IsNullOrEmpty(fetchedCss))
                    {
                        StyleSheetStateFor(styleEl).FetchedCss.Set(fetchedCss);
                        cssText.Append(fetchedCss);
                    }
                }
                catch
                {
                    // Ignore stylesheet fetch failures in computed-style building.
                }
            }
        }

        return cssText.ToString();
    }

    /// <summary>
    /// Ensures the style element's live rule model
    /// (<see cref="StyleSheetRuntimeState.Rules"/>) reflects its current source text,
    /// reparsing when the source changed. Returns the shared mutable rule list — the
    /// single store behind the CSSOM (<c>cssRules</c>/<c>insertRule</c>/<c>deleteRule</c>),
    /// the renderer/legacy-cascade text, and the <c>getComputedStyle</c> engine sheet
    /// (Phase 6 store unification). Replacing the element's <c>textContent</c> changes
    /// the source text and thus discards prior <c>insertRule</c>/<c>deleteRule</c>
    /// mutations, matching CSSOM semantics.
    /// </summary>
    private List<CssRule> EnsureStyleSheetRulesCurrent(DomElement styleEl)
    {
        var state = StyleSheetStateFor(styleEl);
        var sourceText = GetStyleElementSourceText(styleEl);
        if (state.Rules is null ||
            !string.Equals(state.RulesSourceText, sourceText, StringComparison.Ordinal))
        {
            state.Rules = [.. new CssParser().ParseStyleSheet(sourceText).Rules];
            state.RulesSourceText = sourceText;
            state.RulesMutated = false;
        }

        return state.Rules;
    }

    /// <summary>
    /// The effective CSS text for a style element, as seen by the renderer/legacy
    /// cascade and the <c>getComputedStyle</c> engine. Returns the raw author source
    /// byte-for-byte while unmutated (so unchanged stylesheets are identical to
    /// pre-Phase-6), and the serialized live model once <c>insertRule</c>/<c>deleteRule</c>
    /// has mutated it — so script CSSOM mutations are observed downstream.
    /// </summary>
    /// <summary>
    /// Enforces the Content Security Policy <c>style-src</c> family on the parsed
    /// DOM so blocked inline styles do not render: an inline <c>style="…"</c>
    /// attribute blocked by <c>style-src-attr</c> (→ <c>style-src</c> →
    /// <c>default-src</c>) is stripped, and a <c>&lt;style&gt;</c> element blocked
    /// by <c>style-src-elem</c> (same fallback chain) is removed. Only the style
    /// directives are consulted — script/event-handler enforcement is intentionally
    /// left to the script pipeline — so this is safe to call on any parsed document.
    /// </summary>
    public void ApplyStyleContentSecurityPolicy(ContentSecurityPolicy? csp)
    {
        if (csp == null || DocumentElement == null)
            return;

        // CSP §"Processing a `meta` element": a policy delivered by
        // <meta http-equiv="Content-Security-Policy"> is enforced from the point the parser reaches
        // the meta — markup already parsed is *not* retroactively blocked. Enforcing document-wide
        // stripped a style attribute that precedes the meta, including on the ancestors that
        // *contain* it: WPT content-security-policy/style-src/inline-style-attribute-on-html has
        // <html style="background-color: blue"> before a `style-src 'none'` meta, and rendered white
        // instead of blue.
        //
        // A pre-order walk visits an element's start tag in parse order, and visits ancestors before
        // descendants, so "not yet reached the meta" is exactly "this start tag was parsed first".
        // A policy with no meta in the document came from a header and applies document-wide.
        var policyMeta = FindCspMetaElement(DocumentElement);
        ApplyStyleCsp(
            DocumentElement, csp,
            blockStyleAttribute: !csp.AllowsInlineStyleAttribute(),
            policyMeta,
            enforcing: policyMeta == null);
    }

    /// <summary>
    /// The <c>&lt;meta http-equiv="Content-Security-Policy"&gt;</c> element that delivered the
    /// document's policy, in document order, or <c>null</c> when none is present (a header-delivered
    /// policy). Mirrors the acceptance rules of <c>CspMetaDiscovery.FindPolicyContent</c>, which
    /// parses the same meta out of the source text.
    /// </summary>
    private DomElement? FindCspMetaElement(DomElement element)
    {
        if (!IsText(element) &&
            element.TagName.Equals("meta", StringComparison.OrdinalIgnoreCase) &&
            TryGetAttribute(element, "http-equiv", out var httpEquiv) &&
            string.Equals(httpEquiv?.Trim(), "Content-Security-Policy", StringComparison.OrdinalIgnoreCase) &&
            TryGetAttribute(element, "content", out var content) &&
            !string.IsNullOrWhiteSpace(content))
        {
            return element;
        }

        foreach (var child in ChildElements(element))
        {
            var found = FindCspMetaElement(child);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Walks the document in parse order applying the style-src family. <paramref name="enforcing"/>
    /// starts <c>false</c> for a meta-delivered policy and flips to <c>true</c> at
    /// <paramref name="policyMeta"/>; it is threaded through the walk (rather than being recomputed
    /// per element) so the flip is observed by every subsequent element in document order.
    /// </summary>
    private bool ApplyStyleCsp(
        DomElement element, ContentSecurityPolicy csp, bool blockStyleAttribute,
        DomElement? policyMeta, bool enforcing)
    {
        if (ReferenceEquals(element, policyMeta))
            enforcing = true;

        if (enforcing && !IsText(element))
        {
            if (element.TagName.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                var nonce = TryGetAttribute(element, "nonce", out var n) ? n : null;
                if (!csp.AllowsInlineStyleElement(nonce, GetStyleElementCssText(element)))
                {
                    element.Remove();
                    return enforcing;
                }
            }

            if (blockStyleAttribute && HasAttr(element, "style"))
            {
                RemoveAttr(element, "style");
                InlineStyle(element).Clear();
                InvalidateStyleScope(element);
            }
        }

        // Snapshot: a blocked <style> child removes itself from this collection.
        foreach (var child in ChildElements(element).ToArray())
            enforcing = ApplyStyleCsp(child, csp, blockStyleAttribute, policyMeta, enforcing);

        return enforcing;
    }

    private string GetStyleElementCssText(DomElement styleEl)
    {
        var rules = EnsureStyleSheetRulesCurrent(styleEl);
        var state = StyleSheetStateFor(styleEl);
        return state.RulesMutated
            ? string.Join("\n", rules.Select(CssSerializer.Serialize))
            : state.RulesSourceText ?? string.Empty;
    }

    private static void ApplyUserAgentDisplayDefaults(Dictionary<string, string> computed, DomElement element)
    {
        if (computed.ContainsKey("display"))
            return;

        if (HasAttr(element, "hidden"))
        {
            computed["display"] = "none";
            return;
        }

        if (CssUserAgentDefaults.DisplayValues.TryGetValue(element.TagName, out var display))
            computed["display"] = display;
    }

    /// <summary>
    /// Expands CSS shorthand properties into individual longhand properties (e.g.
    /// <c>margin: 10px 5px</c> → <c>margin-top/right/bottom/left</c>), only setting longhands
    /// not already present. DOM/CSS promotion Phase 2: this now delegates to the single canonical
    /// <see cref="CssStyleEngine.ExpandShorthands"/> — the bridge's own copy (which
    /// had drifted to a narrower subset: no <c>outline</c>, no <c>font</c> slash line-height, and a
    /// single-layer <c>background</c> parser) is deleted so it can no longer drift from the engine.
    /// </summary>
    internal static void ExpandCssShorthands(Dictionary<string, string> computed)
        => CssStyleEngine.ExpandShorthands(computed);

    /// <summary>
    /// Parses a CSS length value (e.g. "0", "100px", "1em") to pixels, returning
    /// <see cref="double.NaN"/> when it cannot be parsed. Font-free approximation
    /// (1em = 16px default); the algorithm is owned by the canonical
    /// <see cref="CssLengthParser.ParseToPixels"/>.
    /// </summary>
    internal static double ParseCssLengthToPixels(string value, int viewportWidth = 0, int viewportHeight = 0) =>
        CssLengthParser.ParseToPixels(value, viewportWidth, viewportHeight);

    /// <summary>
    /// Determines the viewport width and height for media query evaluation
    /// based on the element's document root. For sub-documents inside iframes,
    /// the viewport is the iframe container's CSS dimensions. For the main
    /// document, the viewport is 0×0 (headless).
    /// </summary>
    private (int Width, int Height) GetViewportForDocRoot(DomElement docRoot)
    {
        if (ReferenceEquals(docRoot, DocumentElement) ||
            string.Equals(docRoot.TagName, "#document", StringComparison.OrdinalIgnoreCase))
            return (_viewportWidth, _viewportHeight);

        // docRoot is a severed sub-document's documentElement (<html>, post-P4.4b); its parent is
        // the content DomDocument. Recover the containing iframe/object via the reverse map to read
        // its CSS dimensions as the sub-viewport size (was ParentEl(#subdoc-root)).
        var parent = GetFrameForContentDocument(docRoot?.ParentNode);
        if (parent != null && !parent.TagName.StartsWith("#", StringComparison.Ordinal))
        {
            // parent is the iframe/object element — check its style for dimensions
            if (TryGetAttribute(parent, "style", out var style) && !string.IsNullOrEmpty(style))
            {
                var w = ExtractCssDimension(style, "width");
                var h = ExtractCssDimension(style, "height");
                if (w > 0 || h > 0)
                    return (w, h);
            }

            var attributeWidth = ParseViewportDimensionAttribute(GetAttr(parent, "width"));
            var attributeHeight = ParseViewportDimensionAttribute(GetAttr(parent, "height"));
            if (attributeWidth > 0 || attributeHeight > 0)
                return (attributeWidth, attributeHeight);
        }
        return (0, 0); // Default: headless 0×0 viewport
    }

    /// <summary>
    /// Extracts a pixel dimension from a CSS style string for a given property name.
    /// </summary>
    private static int ExtractCssDimension(string style, string property)
    {
        var propIdx = style.IndexOf(property, StringComparison.OrdinalIgnoreCase);
        if (propIdx < 0) return 0;
        var colonIdx = style.IndexOf(':', propIdx);
        if (colonIdx < 0) return 0;
        var semiIdx = style.IndexOf(';', colonIdx);
        var valueStr = semiIdx >= 0 ? style[(colonIdx + 1)..semiIdx].Trim() : style[(colonIdx + 1)..].Trim();
        var px = ParseCssLengthToPixels(valueStr);
        return !double.IsNaN(px) ? (int)px : 0;
    }

    private static int ParseViewportDimensionAttribute(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        var px = ParseCssLengthToPixels(value.Trim());
        return !double.IsNaN(px) && px > 0 ? (int)px : 0;
    }

    /// <summary>
    /// Whether the document's Content Security Policy permits fetching and applying an external stylesheet
    /// from <paramref name="href"/> (a <c>&lt;link rel="stylesheet"&gt;</c>). When no policy is configured
    /// every stylesheet is allowed. Mirrors the script-side <see cref="ContentSecurityPolicy.AllowsExternalScript"/>
    /// gate so DOM/CSS never fetch or apply a <c>style-src</c>-blocked external stylesheet (Phase 7 item 5:
    /// CSP stays in the host layer; DOM and CSS receive already-authorised content).
    /// </summary>
    private bool IsExternalStyleAllowedByCsp(DomElement linkEl, string href)
    {
        if (Csp == null)
            return true;

        var nonce = TryGetAttribute(linkEl, "nonce", out var nonceValue) ? nonceValue : null;
        return Csp.AllowsExternalStyle(href, _pageUrl, nonce);
    }

    /// <summary>
    /// Fetches an external CSS stylesheet from an HTTP/HTTPS URL.
    /// Returns the CSS text content, or <c>null</c> on failure.
    /// </summary>
    private string? FetchExternalStylesheet(string url)
    {
        try
        {
            // The file/http dispatch policy lives in the loader (Phase 7 item 4), not here.
            return _resources.LoadText(url);
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.HtmlRenderer, "DomBridge.FetchExternalStylesheet", $"Failed to fetch stylesheet '{url}': {ex.Message}", ex);
            return null;
        }
    }
}
