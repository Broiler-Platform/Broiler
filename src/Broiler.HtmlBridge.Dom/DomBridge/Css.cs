using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HtmlBridge.Logging;
using Broiler.HtmlBridge.Scripting;

namespace Broiler.HtmlBridge;

/// <summary>
/// CSS specificity calculation, style-block extraction, rule cascading,
/// computed-style building, and media-query evaluation.
/// </summary>
public sealed partial class DomBridge
{
    private int _styleInvalidationBatchDepth;
    private HashSet<DomElement>? _pendingStyleInvalidationRoots;
    // These caches/reentrancy guards are read and written while computing
    // getComputedStyle / element geometry. That work is re-entered from JS
    // Promise/async/generator and scroll/timer continuations that the JS engine
    // dispatches on ThreadPool threads (see the timer-map note in DomBridge.cs),
    // so a continuation can mutate them concurrently with the main-thread layout
    // pass. A plain Dictionary/HashSet corrupts under that race and throws
    // "Operations that change non-concurrent collections must have exclusive
    // access" from GetComputedProps — unhandled on a ThreadPool thread it aborts
    // the whole process (SIGABRT/exit 134), taking out an entire WPT shard
    // (issue #1143). Use concurrent collections, the same defensive idiom as the
    // timer/raf maps.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<DomElement, Dictionary<string, string>> _computedPropsCache = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<DomElement, Dictionary<string, string>> _computedPropsInProgress = new();

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
    internal void BeginStyleInvalidationBatch()
    {
        _styleInvalidationBatchDepth++;
    }

    internal void EndStyleInvalidationBatch()
    {
        if (_styleInvalidationBatchDepth == 0)
            return;

        _styleInvalidationBatchDepth--;
        if (_styleInvalidationBatchDepth == 0)
            FlushPendingStyleInvalidations();
    }

    internal void InvalidateStyleScope(DomElement anchor)
    {
        _computedPropsCache.Clear();
        var docRoot = GetDocumentRootFor(anchor);
        if (_styleInvalidationBatchDepth > 0)
        {
            _pendingStyleInvalidationRoots ??= [];
            _pendingStyleInvalidationRoots.Add(docRoot);
            return;
        }

        InvalidateStyleScopeRecursive(docRoot);
    }

    private void FlushPendingStyleInvalidations()
    {
        if (_pendingStyleInvalidationRoots == null || _pendingStyleInvalidationRoots.Count == 0)
            return;

        foreach (var root in _pendingStyleInvalidationRoots)
            InvalidateStyleScopeRecursive(root);

        _pendingStyleInvalidationRoots.Clear();
    }

    private void InvalidateStyleScopeRecursive(DomElement element)
    {
        if (!IsText(element) && !element.TagName.StartsWith("#", StringComparison.Ordinal))
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
            if (root.TagName.StartsWith("#", StringComparison.Ordinal))
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

        // Helper to convert CSS property name to JS camelCase (e.g., "z-index" -> "zIndex")
        static string ToCamelCase(string cssName)
        {
            var sb = new StringBuilder();
            bool upper = false;
            foreach (char c in cssName)
            {
                if (c == '-') { upper = true; continue; }
                sb.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            return sb.ToString();
        }

        // Expose all computed properties as both camelCase and kebab-case
        foreach (var kv in computed)
        {
            var camel = ToCamelCase(kv.Key);
            var normalized = StripCssPriority(kv.Value);
            obj.FastAddValue((KeyString)kv.Key, new JSString(normalized), JSPropertyAttributes.EnumerableConfigurableValue);
            if (camel != kv.Key)
                obj.FastAddValue((KeyString)camel, new JSString(normalized), JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // getPropertyValue method (supports both kebab-case and camelCase lookups)
        obj.FastAddValue(
            (KeyString)"getPropertyValue",
            new JSFunction((in Arguments a) => JsCssGetPropertyValue001Core(computed, in a), "getPropertyValue", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddProperty(
            (KeyString)"length",
            new JSFunction((in Arguments _) => new JSNumber(propertyNames.Count), "get length"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddValue(
            (KeyString)"item",
            new JSFunction((in Arguments a) => JsCssItem003Core(propertyNames, in a), "item", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue(
            (KeyString)"getPropertyPriority",
            new JSFunction((in Arguments _) => new JSString(string.Empty), "getPropertyPriority", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddProperty(
            (KeyString)"parentRule",
            NullFunction("get parentRule"),
            null,
            JSPropertyAttributes.EnumerableConfigurableProperty);

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

    private void ApplyInheritedProperties(Dictionary<string, string> computed, DomElement element)
    {
        if (ParentEl(element) == null)
            return;

        var parentProps = GetComputedProps(ParentEl(element));
        foreach (var property in CSS.Dom.CssComputedDefaults.InheritedProperties)
        {
            if (computed.ContainsKey(property))
                continue;

            if (parentProps.TryGetValue(property, out var inheritedValue) &&
                !string.IsNullOrWhiteSpace(inheritedValue))
            {
                computed[property] = inheritedValue;
            }
        }
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

    private static void ApplyApproximateFormControlComputedSizes(
        Dictionary<string, string> computed,
        DomElement element)
    {
        string tag = element.TagName.ToLowerInvariant();
        if (tag is not ("input" or "button" or "select" or "textarea" or "progress" or "meter"))
            return;

        string writingMode = computed.GetValueOrDefault("writing-mode") ?? "horizontal-tb";
        bool vertical = IsVerticalWritingMode(writingMode);

        double logicalInlineSize = 60;
        double logicalBlockSize = 20;

        switch (tag)
        {
            case "input":
                string type = GetAttr(element, "type")?.ToLowerInvariant() ?? "text";
                switch (type)
                {
                    case "hidden":
                        logicalInlineSize = 0;
                        logicalBlockSize = 0;
                        break;
                    case "checkbox":
                    case "radio":
                        logicalInlineSize = 13;
                        logicalBlockSize = 13;
                        break;
                    case "submit":
                    case "button":
                    case "reset":
                        logicalInlineSize = 72;
                        logicalBlockSize = 20;
                        ApplyButtonLikeMultilineSizing(ref logicalInlineSize, ref logicalBlockSize, GetAttr(element, "value"));
                        break;
                    default:
                        logicalInlineSize = 173;
                        logicalBlockSize = 16;
                        break;
                }
                break;
            case "button":
                logicalInlineSize = 72;
                logicalBlockSize = 20;
                ApplyButtonLikeMultilineSizing(ref logicalInlineSize, ref logicalBlockSize, GetElementRenderedText(element));
                break;
            case "select":
                logicalInlineSize = 60;
                logicalBlockSize = 19;
                ApplySelectListBoxSizing(ref logicalInlineSize, ref logicalBlockSize, element);
                break;
            case "textarea":
                logicalInlineSize = 170;
                logicalBlockSize = 40;
                break;
            case "progress":
            case "meter":
                logicalInlineSize = 120;
                logicalBlockSize = 16;
                break;
        }

        double physicalWidth = vertical ? logicalBlockSize : logicalInlineSize;
        double physicalHeight = vertical ? logicalInlineSize : logicalBlockSize;

        if (!HasExplicitPhysicalOrLogicalSize(computed, "width", vertical ? "block-size" : "inline-size") && physicalWidth > 0)
            computed["width"] = FormatPx(physicalWidth);
        if (!HasExplicitPhysicalOrLogicalSize(computed, "height", vertical ? "inline-size" : "block-size") && physicalHeight > 0)
            computed["height"] = FormatPx(physicalHeight);
    }

    private static void ApplyLogicalSizeAliases(Dictionary<string, string> computed)
    {
        string writingMode = computed.GetValueOrDefault("writing-mode") ?? "horizontal-tb";
        bool vertical = IsVerticalWritingMode(writingMode);

        string width = computed.GetValueOrDefault("width") ?? "auto";
        string height = computed.GetValueOrDefault("height") ?? "auto";
        string inlineSize = computed.GetValueOrDefault("inline-size") ?? "auto";
        string blockSize = computed.GetValueOrDefault("block-size") ?? "auto";

        if (!HasExplicitSpecifiedSize(width))
            width = ResolveLogicalPhysicalFallback(width, vertical ? blockSize : inlineSize);

        if (!HasExplicitSpecifiedSize(height))
            height = ResolveLogicalPhysicalFallback(height, vertical ? inlineSize : blockSize);

        computed["width"] = width;
        computed["height"] = height;
        computed["block-size"] = HasExplicitSpecifiedSize(blockSize) ? blockSize : (vertical ? width : height);
        computed["inline-size"] = HasExplicitSpecifiedSize(inlineSize) ? inlineSize : (vertical ? height : width);
    }

    private static bool HasExplicitPhysicalOrLogicalSize(Dictionary<string, string> computed, string physicalProperty, string logicalProperty) =>
        HasExplicitSpecifiedSize(computed.GetValueOrDefault(physicalProperty)) ||
        HasExplicitSpecifiedSize(computed.GetValueOrDefault(logicalProperty));

    private static void ApplyButtonLikeMultilineSizing(ref double logicalInlineSize, ref double logicalBlockSize, string? rawText)
    {
        int lineCount = CountRenderedLines(rawText);
        if (lineCount <= 1)
            return;

        logicalBlockSize = 20 * lineCount;
    }

    private static void ApplySelectListBoxSizing(ref double logicalInlineSize, ref double logicalBlockSize, DomElement element)
    {
        int visibleRows = GetSelectVisibleRowCount(element);
        if (visibleRows <= 1)
            return;

        const double rowBlockSize = 16;
        const double chromeBlockSize = 4;
        logicalInlineSize = Math.Max(logicalInlineSize, 72);
        logicalBlockSize = (visibleRows * rowBlockSize) + chromeBlockSize;
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

    private static int CountRenderedLines(string? rawText)
    {
        if (string.IsNullOrEmpty(rawText))
            return 1;

        return WebUtility.HtmlDecode(rawText)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Length;
    }

    private static string GetElementRenderedText(DomElement element)
    {
        var builder = new StringBuilder();
        AppendRenderedText(element, builder);
        return builder.ToString();
    }

    private static void AppendRenderedText(DomElement element, StringBuilder builder)
    {
        foreach (var child in ChildElements(element))
        {
            if (IsText(child))
            {
                if (BridgeText(child).Length > 0)
                    builder.Append(BridgeText(child));
                continue;
            }

            if (string.Equals(child.TagName, "br", StringComparison.OrdinalIgnoreCase))
            {
                builder.Append('\n');
                continue;
            }

            AppendRenderedText(child, builder);
        }
    }

    private static string ResolveLogicalPhysicalFallback(string currentPhysicalValue, string mappedLogicalValue) =>
        HasExplicitSpecifiedSize(mappedLogicalValue) ? mappedLogicalValue : currentPhysicalValue;

    private static bool IsVerticalWritingMode(string? writingMode)
    {
        var normalized = writingMode?.Trim().ToLowerInvariant();
        return normalized is "vertical-rl" or "vertical-lr" or "sideways-rl" or "sideways-lr";
    }

    private static bool HasExplicitSpecifiedSize(string? value)
    {
        value = value?.Trim() ?? string.Empty;
        return value.Length > 0 &&
               !string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Raw author <em>source</em> text for a style element — its text-node children,
    /// the <c>textContent</c>/<c>InnerHtml</c> fallback, or a cached/fetched linked
    /// stylesheet — <em>without</em> any CSSOM <c>insertRule</c>/<c>deleteRule</c>
    /// mutations applied. This is the input from which the shared rule model is
    /// (re)parsed; <see cref="GetStyleElementCssText"/> applies mutations on top.
    /// </summary>
    private string GetStyleElementSourceText(DomElement styleEl)
    {
        var cssText = new StringBuilder();
        foreach (var child in ChildElements(styleEl))
        {
            if (IsText(child))
                cssText.Append(BridgeText(child));
        }

        if (cssText.Length == 0 && styleEl.TextContent != null)
            cssText.Append(styleEl.TextContent);

        if (cssText.Length == 0 && !string.IsNullOrEmpty(GetElementRuntimeState(styleEl).InnerHtml))
            cssText.Append(GetElementRuntimeState(styleEl).InnerHtml);

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
    private List<Broiler.CSS.CssRule> EnsureStyleSheetRulesCurrent(DomElement styleEl)
    {
        var state = GetElementRuntimeState(styleEl).StyleSheet;
        var sourceText = GetStyleElementSourceText(styleEl);
        if (state.Rules is null ||
            !string.Equals(state.RulesSourceText, sourceText, StringComparison.Ordinal))
        {
            state.Rules = [.. new Broiler.CSS.CssParser().ParseStyleSheet(sourceText).Rules];
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
            ? string.Join("\n", rules.Select(CSS.CssSerializer.Serialize))
            : state.RulesSourceText ?? string.Empty;
    }

    private static string FormatPx(double value) =>
        $"{Math.Round(value).ToString(CultureInfo.InvariantCulture)}px";

    private static void ApplyUserAgentDisplayDefaults(
        Dictionary<string, string> computed,
        DomElement element)
    {
        if (computed.ContainsKey("display"))
            return;

        if (HasAttr(element, "hidden"))
        {
            computed["display"] = "none";
            return;
        }

        if (CSS.Dom.CssUserAgentDefaults.DisplayValues.TryGetValue(element.TagName, out var display))
            computed["display"] = display;
    }

    private static int FindMatchingClosingParen(string value, int openParenIndex)
    {
        int depth = 0;
        for (int i = openParenIndex; i < value.Length; i++)
        {
            if (value[i] == '(')
                depth++;
            else if (value[i] == ')')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private static int FindTopLevelChar(string value, char target)
    {
        int depth = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '(')
                depth++;
            else if (value[i] == ')')
                depth--;
            else if (value[i] == target && depth == 0)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Expands CSS shorthand properties into individual longhand properties.
    /// For example, <c>margin: 10px 5px</c> expands to <c>margin-top: 10px</c>,
    /// <c>margin-right: 5px</c>, <c>margin-bottom: 10px</c>, <c>margin-left: 5px</c>.
    /// Only sets longhands that are not already explicitly set.
    /// </summary>
    private static void ExpandCssShorthands(Dictionary<string, string> computed)
    {
        if (computed.TryGetValue("font", out var fontVal))
            ExpandFontShorthand(computed, fontVal);

        // Expand margin shorthand → margin-top, margin-right, margin-bottom, margin-left
        if (computed.TryGetValue("margin", out var marginVal))
            ExpandBoxShorthand(computed, marginVal, "margin-top", "margin-right", "margin-bottom", "margin-left");

        // Expand padding shorthand → padding-top, padding-right, padding-bottom, padding-left
        if (computed.TryGetValue("padding", out var paddingVal))
            ExpandBoxShorthand(computed, paddingVal, "padding-top", "padding-right", "padding-bottom", "padding-left");

        // Expand border-width shorthand → individual sides
        if (computed.TryGetValue("border-width", out var bwVal))
            ExpandBoxShorthand(computed, bwVal, "border-top-width", "border-right-width", "border-bottom-width", "border-left-width");

        // Expand border-style shorthand → individual sides
        if (computed.TryGetValue("border-style", out var bsVal))
            ExpandBoxShorthand(computed, bsVal, "border-top-style", "border-right-style", "border-bottom-style", "border-left-style");

        // Expand border-color shorthand → individual sides
        if (computed.TryGetValue("border-color", out var bcVal))
            ExpandBoxShorthand(computed, bcVal, "border-top-color", "border-right-color", "border-bottom-color", "border-left-color");

        // Expand border shorthand (e.g. "2cm solid gray") → border-width, border-style, border-color
        if (computed.TryGetValue("border", out var borderVal))
            ExpandBorderShorthand(computed, borderVal);

        if (computed.TryGetValue("border-left", out var borderLeftVal))
            ExpandBorderSideShorthand(computed, borderLeftVal, "left");
        if (computed.TryGetValue("border-top", out var borderTopVal))
            ExpandBorderSideShorthand(computed, borderTopVal, "top");
        if (computed.TryGetValue("border-right", out var borderRightVal))
            ExpandBorderSideShorthand(computed, borderRightVal, "right");
        if (computed.TryGetValue("border-bottom", out var borderBottomVal))
            ExpandBorderSideShorthand(computed, borderBottomVal, "bottom");

        // Expand border-inline shorthand → border-left and border-right
        // CSS Logical Properties §5.1: border-inline applies to both
        // inline-start and inline-end (left and right in LTR).
        if (computed.TryGetValue("border-inline", out var biVal))
        {
            if (!computed.ContainsKey("border-left")) computed["border-left"] = biVal;
            if (!computed.ContainsKey("border-right")) computed["border-right"] = biVal;
            ExpandBorderSideShorthand(computed, biVal, "left");
            ExpandBorderSideShorthand(computed, biVal, "right");
        }

        // Expand border-block shorthand → border-top and border-bottom
        if (computed.TryGetValue("border-block", out var bbVal))
        {
            if (!computed.ContainsKey("border-top")) computed["border-top"] = bbVal;
            if (!computed.ContainsKey("border-bottom")) computed["border-bottom"] = bbVal;
            ExpandBorderSideShorthand(computed, bbVal, "top");
            ExpandBorderSideShorthand(computed, bbVal, "bottom");
        }

        // Expand margin-block shorthand → margin-top and margin-bottom
        if (computed.TryGetValue("margin-block", out var mbVal))
        {
            var parts = SplitCssValues(mbVal);
            if (parts.Length >= 1)
            {
                if (!computed.ContainsKey("margin-top")) computed["margin-top"] = parts[0];
                if (!computed.ContainsKey("margin-bottom")) computed["margin-bottom"] = parts.Length > 1 ? parts[1] : parts[0];
            }
        }

        // Expand margin-inline shorthand → margin-left and margin-right
        if (computed.TryGetValue("margin-inline", out var miVal))
        {
            var parts = SplitCssValues(miVal);
            if (parts.Length >= 1)
            {
                if (!computed.ContainsKey("margin-left")) computed["margin-left"] = parts[0];
                if (!computed.ContainsKey("margin-right")) computed["margin-right"] = parts.Length > 1 ? parts[1] : parts[0];
            }
        }

        // Expand padding-block shorthand → padding-top and padding-bottom
        if (computed.TryGetValue("padding-block", out var pbVal))
        {
            var parts = SplitCssValues(pbVal);
            if (parts.Length >= 1)
            {
                if (!computed.ContainsKey("padding-top")) computed["padding-top"] = parts[0];
                if (!computed.ContainsKey("padding-bottom")) computed["padding-bottom"] = parts.Length > 1 ? parts[1] : parts[0];
            }
        }

        // Expand padding-inline shorthand → padding-left and padding-right
        if (computed.TryGetValue("padding-inline", out var piVal))
        {
            var parts = SplitCssValues(piVal);
            if (parts.Length >= 1)
            {
                if (!computed.ContainsKey("padding-left")) computed["padding-left"] = parts[0];
                if (!computed.ContainsKey("padding-right")) computed["padding-right"] = parts.Length > 1 ? parts[1] : parts[0];
            }
        }

        // Expand inset shorthand → top, right, bottom, left (CSS Logical Properties)
        if (computed.TryGetValue("inset", out var insetVal))
        {
            // inset is a shorthand that maps to top, right, bottom, left using
            // the same 1-4 value pattern as margin/padding.  Only set longhands
            // that are not already explicitly specified to avoid clobbering
            // author-provided overrides.
            var insetParts = SplitCssValues(insetVal);
            if (insetParts.Length > 0)
            {
                string iTop = insetParts[0];
                string iRight = insetParts.Length > 1 ? insetParts[1] : iTop;
                string iBottom = insetParts.Length > 2 ? insetParts[2] : iTop;
                string iLeft = insetParts.Length > 3 ? insetParts[3] : iRight;

                if (!computed.ContainsKey("top")) computed["top"] = iTop;
                if (!computed.ContainsKey("right")) computed["right"] = iRight;
                if (!computed.ContainsKey("bottom")) computed["bottom"] = iBottom;
                if (!computed.ContainsKey("left")) computed["left"] = iLeft;
            }
        }

        // Expand background shorthand → background-color, background-image, background-repeat,
        // background-attachment, background-position (CSS2.1 §14.2.1)
        if (computed.TryGetValue("background", out var bgVal))
            ExpandBackgroundShorthand(computed, bgVal);
    }

    private static void ExpandFontShorthand(Dictionary<string, string> computed, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (value.Trim().Equals("inherit", StringComparison.OrdinalIgnoreCase))
        {
            // Keep any longhands that were already assigned by more specific
            // declarations; only use the inherited font shorthand to backfill
            // missing longhands in the computed map.
            if (!computed.ContainsKey("font-style")) computed["font-style"] = "inherit";
            if (!computed.ContainsKey("font-variant")) computed["font-variant"] = "inherit";
            if (!computed.ContainsKey("font-weight")) computed["font-weight"] = "inherit";
            if (!computed.ContainsKey("font-size")) computed["font-size"] = "inherit";
            if (!computed.ContainsKey("line-height")) computed["line-height"] = "inherit";
            if (!computed.ContainsKey("font-family")) computed["font-family"] = "inherit";
            return;
        }

        var tokens = SplitCssValues(value);
        if (tokens.Length == 0)
            return;

        string fontStyle = "normal";
        string fontVariant = "normal";
        string fontWeight = "normal";
        string? fontSize = null;
        string? lineHeight = null;
        int fontSizeIndex = -1;

        for (int i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            var lower = token.ToLowerInvariant();

            if (TryParseFontSizeAndLineHeight(lower, token, out var parsedFontSize, out var parsedLineHeight))
            {
                fontSize = parsedFontSize;
                lineHeight = parsedLineHeight;
                fontSizeIndex = i;
                break;
            }

            if (lower is "normal" or "italic" or "oblique")
                fontStyle = lower;
            else if (lower == "small-caps")
                fontVariant = lower;
            else if (lower is "bold" or "bolder" or "lighter" or "100" or "200" or "300" or "400" or "500" or "600" or "700" or "800" or "900")
                fontWeight = lower;
        }

        if (fontSizeIndex < 0 || fontSizeIndex >= tokens.Length - 1 || string.IsNullOrWhiteSpace(fontSize))
            return;

        var fontFamily = string.Join(" ", tokens[(fontSizeIndex + 1)..]).Trim();
        if (string.IsNullOrWhiteSpace(fontFamily))
            return;

        bool hasNonEmptyFamily = fontFamily
            .Split(',', StringSplitOptions.TrimEntries)
            .Any(part => !string.IsNullOrWhiteSpace(part.Trim('"', '\'', ' ')));
        if (!hasNonEmptyFamily)
            return;

        // Respect longhands that already won in the cascade; the shorthand only
        // populates values that are still absent from the computed map.
        if (!computed.ContainsKey("font-style")) computed["font-style"] = fontStyle;
        if (!computed.ContainsKey("font-variant")) computed["font-variant"] = fontVariant;
        if (!computed.ContainsKey("font-weight")) computed["font-weight"] = fontWeight;
        if (!computed.ContainsKey("font-size")) computed["font-size"] = fontSize;
        var resolvedLineHeight = !string.IsNullOrWhiteSpace(lineHeight) ? lineHeight : "normal";
        if (!computed.ContainsKey("line-height")) computed["line-height"] = resolvedLineHeight;
        if (!computed.ContainsKey("font-family")) computed["font-family"] = fontFamily;
    }

    private static bool TryParseFontSizeAndLineHeight(string lowerToken, string originalToken, out string fontSize, out string lineHeight)
    {
        fontSize = string.Empty;
        lineHeight = string.Empty;

        string sizeToken = lowerToken;
        string? lineHeightToken = null;
        int slashIndex = lowerToken.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex >= 0)
        {
            sizeToken = lowerToken[..slashIndex];
            lineHeightToken = originalToken[(slashIndex + 1)..];
        }

        if (!IsFontSizeToken(sizeToken))
            return false;

        if (lineHeightToken != null)
        {
            var trimmedLineHeight = lineHeightToken.Trim();
            if (!IsFontLineHeightToken(trimmedLineHeight))
                return false;
            lineHeight = trimmedLineHeight;
        }

        fontSize = originalToken;
        if (slashIndex >= 0)
            fontSize = originalToken[..slashIndex];

        return true;
    }

    private static bool IsFontSizeToken(string token) =>
        token is "xx-small" or "x-small" or "small" or "medium" or "large" or "x-large" or "xx-large" or "larger" or "smaller"
        || IsLengthOrPercentage(token);

    private static bool IsFontLineHeightToken(string token) =>
        token.Equals("normal", StringComparison.OrdinalIgnoreCase)
        || IsLengthOrPercentage(token)
        || double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    /// <summary>
    /// Expands a 1–4 value CSS box shorthand (margin, padding, border-width, etc.)
    /// into four individual properties using CSS box-model order: top, right, bottom, left.
    /// </summary>
    private static void ExpandBoxShorthand(Dictionary<string, string> computed, string value,
        string topProp, string rightProp, string bottomProp, string leftProp)
    {
        var parts = SplitCssValues(value);
        if (parts.Length == 0) return;

        string top, right, bottom, left;
        switch (parts.Length)
        {
            case 1:
                top = right = bottom = left = parts[0];
                break;
            case 2:
                top = bottom = parts[0];
                right = left = parts[1];
                break;
            case 3:
                top = parts[0];
                right = left = parts[1];
                bottom = parts[2];
                break;
            default: // 4+
                top = parts[0];
                right = parts[1];
                bottom = parts[2];
                left = parts[3];
                break;
        }

        if (!computed.ContainsKey(topProp)) computed[topProp] = top;
        if (!computed.ContainsKey(rightProp)) computed[rightProp] = right;
        if (!computed.ContainsKey(bottomProp)) computed[bottomProp] = bottom;
        if (!computed.ContainsKey(leftProp)) computed[leftProp] = left;
    }

    /// <summary>
    /// Expands the <c>border</c> shorthand (e.g. "2cm solid gray") into
    /// <c>border-width</c>, <c>border-style</c>, and <c>border-color</c>.
    /// </summary>
    private static void ExpandBorderShorthand(Dictionary<string, string> computed, string value)
    {
        var parts = SplitCssValues(value);

        string? width = null, style = null, color = null;

        foreach (var part in parts)
        {
            var lower = part.ToLowerInvariant();
            if (lower is "none" or "hidden" or "dotted" or "dashed" or "solid"
                or "double" or "groove" or "ridge" or "inset" or "outset")
            {
                style ??= part;
            }
            else if (lower is "thin" or "medium" or "thick" || IsLengthOrPercentage(lower))
            {
                width ??= part;
            }
            else
            {
                color ??= part;
            }
        }

        if (width != null && !computed.ContainsKey("border-width")) computed["border-width"] = width;
        if (style != null && !computed.ContainsKey("border-style")) computed["border-style"] = style;
        if (color != null && !computed.ContainsKey("border-color")) computed["border-color"] = color;

        // Further expand border-width, border-style, and border-color into individual sides
        if (width != null)
            ExpandBoxShorthand(computed, width, "border-top-width", "border-right-width", "border-bottom-width", "border-left-width");
        if (style != null)
            ExpandBoxShorthand(computed, style, "border-top-style", "border-right-style", "border-bottom-style", "border-left-style");
        if (color != null)
            ExpandBoxShorthand(computed, color, "border-top-color", "border-right-color", "border-bottom-color", "border-left-color");
    }

    /// <summary>
    /// Expands a border shorthand value (e.g. "solid black 1em") into
    /// individual longhands for a specific side (top, right, bottom, left).
    /// </summary>
    private static void ExpandBorderSideShorthand(
        Dictionary<string, string> computed, string value, string side)
    {
        var parts = SplitCssValues(value);
        string? width = null, style = null, color = null;
        foreach (var part in parts)
        {
            var lower = part.ToLowerInvariant();
            if (lower is "none" or "hidden" or "dotted" or "dashed" or "solid"
                or "double" or "groove" or "ridge" or "inset" or "outset")
                style ??= part;
            else if (lower is "thin" or "medium" or "thick" || IsLengthOrPercentage(lower))
                width ??= part;
            else
                color ??= part;
        }

        if (width != null && !computed.ContainsKey($"border-{side}-width"))
            computed[$"border-{side}-width"] = width;
        if (style != null && !computed.ContainsKey($"border-{side}-style"))
            computed[$"border-{side}-style"] = style;
        if (color != null && !computed.ContainsKey($"border-{side}-color"))
            computed[$"border-{side}-color"] = color;
    }

    /// <summary>
    /// Expands the CSS <c>background</c> shorthand into its five longhand properties:
    /// <c>background-color</c>, <c>background-image</c>, <c>background-repeat</c>,
    /// <c>background-attachment</c>, and <c>background-position</c>.
    /// CSS2.1 §14.2.1: tokens can appear in any order; unspecified longhands
    /// receive their initial values.
    /// </summary>
    /// <remarks>
    /// Implements TODO-24 (acid3-compliance.md §11.5): correctly handles data-URI
    /// images with percent-encoded characters, non-integer percentage positions
    /// (e.g. <c>99.8392283%</c>), and trailing color keywords (e.g. <c>white</c>).
    /// See also: <c>CssParser.ParseBackgroundShorthand()</c> for the rendering-path
    /// counterpart.  Tests: <c>Acid3Todo24_28Tests.cs</c>.
    /// </remarks>
    private static void ExpandBackgroundShorthand(Dictionary<string, string> computed, string value)
    {
        var tokens = SplitCssValues(value);

        string? color = null;
        string? image = null;
        string? repeat = null;
        string? attachment = null;
        var positionParts = new List<string>();

        foreach (var token in tokens)
        {
            var lower = token.ToLowerInvariant();

            // background-image: url(...) or none
            if (lower.StartsWith("url("))
            {
                image ??= token;
                continue;
            }

            // CSS gradient functions (linear-gradient, radial-gradient, etc.)
            if (lower.StartsWith("linear-gradient(") ||
                lower.StartsWith("radial-gradient(") ||
                lower.StartsWith("conic-gradient(") ||
                lower.StartsWith("repeating-linear-gradient(") ||
                lower.StartsWith("repeating-radial-gradient(") ||
                lower.StartsWith("repeating-conic-gradient("))
            {
                image ??= token;
                continue;
            }

            if (lower == "none")
            {
                image ??= "none";
                continue;
            }

            // background-attachment (CSS3 adds 'local')
            if (lower is "scroll" or "fixed" or "local")
            {
                attachment ??= lower;
                continue;
            }

            // CSS3 background-origin / background-clip box values —
            // accept but don't change rendering (not yet implemented).
            if (lower is "content-box" or "padding-box" or "border-box" or "border-area")
            {
                continue;
            }

            // CSS3 background-size separator — skip '/' and size tokens
            if (lower == "/")
                continue;

            // background-repeat (CSS3 adds 'space' and 'round')
            if (lower is "repeat" or "repeat-x" or "repeat-y" or "no-repeat" or "space" or "round")
            {
                repeat ??= lower;
                continue;
            }

            // background-position keywords
            if (lower is "left" or "right" or "top" or "bottom" or "center")
            {
                positionParts.Add(lower);
                continue;
            }

            // Length or percentage values → position
            if (IsLengthOrPercentage(lower))
            {
                positionParts.Add(token);
                continue;
            }

            // inherit — skip
            if (lower == "inherit")
                continue;

            // CSS3 background-size keywords — accept but don't render
            if (lower is "auto" or "cover" or "contain")
                continue;

            // Remaining token → treat as color (named color, hex, rgb(), etc.)
            color ??= token;
        }

        if (!computed.ContainsKey("background-color"))
            computed["background-color"] = color ?? "transparent";
        if (!computed.ContainsKey("background-image"))
            computed["background-image"] = image ?? "none";
        if (!computed.ContainsKey("background-repeat"))
            computed["background-repeat"] = repeat ?? "repeat";
        if (!computed.ContainsKey("background-attachment"))
            computed["background-attachment"] = attachment ?? "scroll";
        if (!computed.ContainsKey("background-position"))
            computed["background-position"] = positionParts.Count > 0
                ? string.Join(" ", positionParts) : "0% 0%";
    }

    /// <summary>
    /// Splits a CSS value into whitespace-separated tokens, respecting
    /// parenthesised groups (e.g. <c>rgba(0, 0, 0, 0.5)</c>).
    /// </summary>
    private static string[] SplitCssValues(string value)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        int depth = 0;
        foreach (char c in value)
        {
            if (c == '(') depth++;
            else if (c == ')' && depth > 0) depth--;

            if (char.IsWhiteSpace(c) && depth == 0 && sb.Length > 0)
            {
                parts.Add(sb.ToString());
                sb.Clear();
            }
            else if (!char.IsWhiteSpace(c) || depth > 0)
            {
                sb.Append(c);
            }
        }
        if (sb.Length > 0) parts.Add(sb.ToString());
        return parts.ToArray();
    }

    /// <summary>
    /// Evaluates a media query string. Supports basic queries needed for Acid3.
    /// Evaluates comma-separated media queries (any match = true).
    /// Supports <c>all</c>, <c>not all</c>, <c>only all</c>, and basic conditions
    /// like <c>(min-color: 0)</c>, <c>(min-monochrome: 0)</c>.
    /// </summary>
    private static bool EvaluateMediaQuery(string query, int viewportWidth = 0, int viewportHeight = 0)
        => CSS.Dom.CssStyleEngine.MatchesMediaQuery(
            query,
            new Broiler.CSS.Dom.CssEnvironment(viewportWidth, viewportHeight));

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

        // Walk up from docRoot to find the containing iframe/object element
        // The docRoot is typically a #subdoc-root child of the iframe element
        var parent = ParentEl(docRoot);
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
    private static string? FetchExternalStylesheet(string url)
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
            return SharedHttpClient.GetStringAsync(url)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.HtmlRenderer, "DomBridge.FetchExternalStylesheet",
                $"Failed to fetch stylesheet '{url}': {ex.Message}", ex);
            return null;
        }
    }
}
