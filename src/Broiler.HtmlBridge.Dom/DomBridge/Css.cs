using System.Globalization;
using System.Text;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HtmlBridge.Logging;
using Broiler.HtmlBridge.Scripting;
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
    // directly. See docs/architecture/htmlbridge-engine-boundaries.md.

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
            .Where(k => !inlineStyleProps.Contains(k) && !GetElementRuntimeState(element).JsSetStyleProps.Contains(k))
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

        foreach (var child in ChildElements(element))
        {
            if (!IsText(child) && !child.TagName.StartsWith("#subdoc", StringComparison.OrdinalIgnoreCase))
                InvalidateStyleScopeRecursive(child);
        }
    }

    /// <summary>
    /// Finds the document root ancestor for the given element by walking up the
    /// parent chain. Stops at <c>#subdoc-root</c> or <c>#document</c> boundaries.
    /// Returns the topmost node within the element's document scope.
    /// </summary>
    private static DomElement GetDocumentRootFor(DomElement el)
    {
        var root = el;
        while (ParentEl(root) != null)
        {
            // If we've reached a document root, stop here
            if (root.TagName.StartsWith('#'))
                return root;
            root = ParentEl(root);
        }
        return root;
    }

    /// <summary>
    /// Recursively collects all <c>&lt;style&gt;</c> elements from a document tree.
    /// Does not descend into sub-document boundaries (<c>#subdoc-root</c>).
    /// </summary>
    private static void CollectStyleElementsInTree(DomElement root, List<DomElement> styleElements)
    {
        foreach (var child in SnapshotChildren(root))
        {
            if (!IsText(child))
            {
                if (string.Equals(child.TagName, "style", StringComparison.OrdinalIgnoreCase))
                    styleElements.Add(child);
                else if (IsExternalStylesheet(child))
                    styleElements.Add(child);

                // Don't descend into sub-document roots (they have their own style scope)
                if (!child.TagName.StartsWith("#subdoc", StringComparison.OrdinalIgnoreCase))
                    CollectStyleElementsInTree(child, styleElements);
            }
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

    private JSObject BuildComputedStyleObject(DomElement? element, string? pseudoElement = null)
    {
        var computed = BuildComputedStyleMap(element, pseudoElement);
        var propertyNames = computed.Keys.ToList();
        var obj = new JSObject();

        // Expose all computed properties as both camelCase and kebab-case
        foreach (var kv in computed)
        {
            var camel = CssPropertyNames.ToDomPropertyName(kv.Key);
            var normalized = CssPriority.Strip(kv.Value);
            obj.FastAddValue((KeyString)kv.Key, new JSString(normalized), JSPropertyAttributes.EnumerableConfigurableValue);
            if (camel != kv.Key)
                obj.FastAddValue((KeyString)camel, new JSString(normalized), JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // getPropertyValue method (supports both kebab-case and camelCase lookups)
        obj.FastAddValue((KeyString)"getPropertyValue", new JSFunction((in a) => JsCssGetPropertyValue001Core(computed, in a), "getPropertyValue", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddProperty((KeyString)"length", new JSFunction((in _) => new JSNumber(propertyNames.Count), "get length"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddValue((KeyString)"item", new JSFunction((in a) => JsCssItem003Core(propertyNames, in a), "item", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        obj.FastAddValue((KeyString)"getPropertyPriority", new JSFunction((in _) => new JSString(string.Empty), "getPropertyPriority", 1), JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddProperty((KeyString)"parentRule", NullFunction("get parentRule"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        return obj;
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
        pseudoElement = NormalizePseudoElement(pseudoElement);
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

    private static string? NormalizePseudoElement(string? pseudoElement)
    {
        if (string.IsNullOrWhiteSpace(pseudoElement))
            return null;

        pseudoElement = pseudoElement.Trim();
        if (pseudoElement.Equals("::before", StringComparison.OrdinalIgnoreCase) ||
            pseudoElement.Equals(":before", StringComparison.OrdinalIgnoreCase))
            return "::before";
        if (pseudoElement.Equals("::after", StringComparison.OrdinalIgnoreCase) ||
            pseudoElement.Equals(":after", StringComparison.OrdinalIgnoreCase))
            return "::after";
        if (pseudoElement.Equals("::first-line", StringComparison.OrdinalIgnoreCase) ||
            pseudoElement.Equals(":first-line", StringComparison.OrdinalIgnoreCase))
            return "::first-line";
        if (pseudoElement.Equals("::first-letter", StringComparison.OrdinalIgnoreCase) ||
            pseudoElement.Equals(":first-letter", StringComparison.OrdinalIgnoreCase))
            return "::first-letter";

        return null;
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

    private static bool IsVerticalWritingMode(string? writingMode)
    {
        var normalized = writingMode?.Trim().ToLowerInvariant();
        return normalized is "vertical-rl" or "vertical-lr" or "sideways-rl" or "sideways-lr";
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
            !string.IsNullOrEmpty(href))
        {
            if (GetElementRuntimeState(styleEl).StyleSheet.FetchedCss.TryGet(out var cachedCss) && cachedCss is string cachedStr)
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
                        GetElementRuntimeState(styleEl).StyleSheet.FetchedCss.Set(fetchedCss);
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
        var state = GetElementRuntimeState(styleEl).StyleSheet;
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

        ApplyStyleCsp(DocumentElement, csp, blockStyleAttribute: !csp.AllowsInlineStyleAttribute());
    }

    private void ApplyStyleCsp(DomElement element, ContentSecurityPolicy csp, bool blockStyleAttribute)
    {
        if (!IsText(element))
        {
            if (element.TagName.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                var nonce = TryGetAttribute(element, "nonce", out var n) ? n : null;
                if (!csp.AllowsInlineStyleElement(nonce, GetStyleElementCssText(element)))
                {
                    element.Remove();
                    return;
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
            ApplyStyleCsp(child, csp, blockStyleAttribute);
    }

    private string GetStyleElementCssText(DomElement styleEl)
    {
        var rules = EnsureStyleSheetRulesCurrent(styleEl);
        var state = GetElementRuntimeState(styleEl).StyleSheet;
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
    private static void ExpandCssShorthands(Dictionary<string, string> computed)
        => CssStyleEngine.ExpandShorthands(computed);

    /// <summary>
    /// Evaluates a media query string. Supports basic queries needed for Acid3.
    /// Evaluates comma-separated media queries (any match = true).
    /// Supports <c>all</c>, <c>not all</c>, <c>only all</c>, and basic conditions
    /// like <c>(min-color: 0)</c>, <c>(min-monochrome: 0)</c>.
    /// </summary>
    private static bool EvaluateMediaQuery(string query, int viewportWidth = 0, int viewportHeight = 0)
        => CssStyleEngine.MatchesMediaQuery(query, new CssEnvironment(viewportWidth, viewportHeight));

    /// <summary>
    /// Parses a CSS length value (e.g. "0", "100px", "1em") to pixels.
    /// Returns -1 if the value cannot be parsed.
    /// Default font size for em conversion is 16px.
    /// </summary>
    private static double ParseCssLengthToPixels(string value, int viewportWidth = 0, int viewportHeight = 0)
    {
        if (string.IsNullOrWhiteSpace(value)) return double.NaN;

        var v = NormalizeSingleValueLengthFunction(value).Trim().ToLowerInvariant();
        if (viewportHeight > 0 && v.EndsWith("vh"))
        {
            if (double.TryParse(v[..^2], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var vh))
            {
                return (vh / 100.0) * viewportHeight;
            }

            return double.NaN;
        }

        if (viewportWidth > 0 && v.EndsWith("vw"))
        {
            if (double.TryParse(v[..^2], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var vw))
            {
                return (vw / 100.0) * viewportWidth;
            }

            return double.NaN;
        }

        var viewportMin = Math.Min(viewportWidth, viewportHeight);
        if (viewportMin > 0 && v.EndsWith("vmin"))
        {
            if (double.TryParse(v[..^4], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var vmin))
            {
                return (vmin / 100.0) * viewportMin;
            }

            return double.NaN;
        }

        var viewportMax = Math.Max(viewportWidth, viewportHeight);
        if (viewportMax > 0 && v.EndsWith("vmax"))
        {
            if (double.TryParse(v[..^4], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var vmax))
            {
                return (vmax / 100.0) * viewportMax;
            }

            return double.NaN;
        }

        if (v.EndsWith("px"))
        {
            if (double.TryParse(v[..^2], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var px))
                return px;
            return double.NaN;
        }
        if (v.EndsWith("em") || v.EndsWith("rem"))
        {
            var numStr = v.EndsWith("rem") ? v[..^3] : v[..^2];
            if (double.TryParse(numStr, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var em))
                return em * 16.0; // 1em = 16px default
            return double.NaN;
        }
        if (v.EndsWith("ex"))
        {
            if (double.TryParse(v[..^2], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var ex))
                return ex * 8.0; // Match the core parser's 1ex ≈ 0.5em approximation at 16px.
            return double.NaN;
        }
        if (v.EndsWith("ch"))
        {
            if (double.TryParse(v[..^2], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var ch))
                return ch * 8.0; // Approximate 1ch as 8px for a 16px monospace glyph advance.
            return double.NaN;
        }
        if (v.EndsWith("ic"))
        {
            if (double.TryParse(v[..^2], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var ic))
                return ic * 16.0; // Approximate 1ic as 1em for the current focused Phase 3 slice.
            return double.NaN;
        }
        if (v.EndsWith("rlh"))
        {
            if (double.TryParse(v[..^3], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var rlh))
                return rlh * 19.2; // Approximate 1rlh as the default 16px root line-height × 1.2.
            return double.NaN;
        }
        if (v.EndsWith("lh"))
        {
            if (double.TryParse(v[..^2], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var lh))
                return lh * 19.2; // Approximate 1lh as the default 16px line-height × 1.2.
            return double.NaN;
        }
        // Plain number (treat as pixels)
        if (double.TryParse(v, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var raw))
            return raw;
        return double.NaN;
    }

    private static string NormalizeSingleValueLengthFunction(string value)
    {
        var current = value.Trim();
        while (TryUnwrapSingleValueFunction(current, "calc", out var inner) ||
               TryUnwrapSingleValueFunction(current, "max", out inner) ||
               TryUnwrapSingleValueFunction(current, "min", out inner))
        {
            current = inner.Trim();
        }

        while (current.Length >= 2 && current[0] == '(' && current[^1] == ')' && HasBalancedParens(current[1..^1]))
            current = current[1..^1].Trim();

        return current;
    }

    private static bool TryUnwrapSingleValueFunction(string value, string functionName, out string inner)
    {
        inner = string.Empty;
        if (!value.StartsWith(functionName + "(", StringComparison.OrdinalIgnoreCase) || value[^1] != ')')
            return false;

        var content = value[(functionName.Length + 1)..^1];
        if (!HasBalancedParens(content))
            return false;

        var depth = 0;
        foreach (var ch in content)
        {
            switch (ch)
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    return false;
            }
        }

        inner = content;
        return true;
    }

    private static bool HasBalancedParens(string value)
    {
        var depth = 0;
        foreach (var ch in value)
        {
            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')')
            {
                depth--;
                if (depth < 0)
                    return false;
            }
        }

        return depth == 0;
    }

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
    /// Fetches an external CSS stylesheet from an HTTP/HTTPS URL.
    /// Returns the CSS text content, or <c>null</c> on failure.
    /// </summary>
    private string? FetchExternalStylesheet(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;
            if (uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                var path = uri.LocalPath;
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            return _resources.GetStringAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.HtmlRenderer, "DomBridge.FetchExternalStylesheet", $"Failed to fetch stylesheet '{url}': {ex.Message}", ex);
            return null;
        }
    }
}
