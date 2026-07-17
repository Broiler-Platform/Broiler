using Broiler.Dom.Html;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// DOM → HTML serialisation — converts the in-memory DOM tree back to
/// an HTML string after JavaScript execution.
/// Uses shared serialization helpers from Broiler.HTML.Dom.
/// </summary>
public sealed partial class DomBridge
{
    // ------------------------------------------------------------------
    //  DOM → HTML serialisation
    // ------------------------------------------------------------------

    private const int MaxSerializationDepth = 100_000;
    private const double ZoomSerializationEpsilon = 0.0001;
    private const double DefaultProgressLikeTrackLengthPx = 120;
    private readonly Dictionary<DomElement, Dictionary<string, string>> _zoomSpecifiedStyleCache = [];

    // RF-BRIDGE-1b render-doc/live-doc separation: when non-null, ApplyZoomSerializationStyles
    // records each mutated element's pre-bake Style + Attributes here so the geometry-snapshot
    // path can restore the live document afterward (zoom baking is destructive — it scales
    // sizes and drops the `zoom` property — which would otherwise corrupt subsequent unzoomed
    // CSSOM queries, e.g. clientWidth of a zoomed element). The render/serialize paths leave
    // this null so their baking stays permanent.
    private List<(DomElement Element, Dictionary<string, string> Style, Dictionary<string, string> Attributes)>? _zoomSerializationRevertLog;

    /// <summary>
    /// Serialises the current DOM tree back to an HTML string.
    /// Call this after JavaScript execution to obtain the modified page
    /// content for re-rendering.
    /// </summary>
    public string SerializeToHtml()
    {
        ApplyZoomSerializationStyles(DocumentElement, 1.0);
        ApplySerializationTransforms();
        return HtmlSerializer.Serialize(
            DocumentElement,
            CreateSerializationAdapter(),
            new HtmlSerializationOptions(
                IncludeHtmlDoctype: true,
                MaximumDepth: MaxSerializationDepth,
                EncodeTextNodes: false,
                NewLineAfterDoctype: true));
    }

    /// <summary>
    /// Returns the canonical document prepared for direct renderer consumption.
    /// Bridge-owned style and form-control state is reflected into canonical
    /// attributes because the typed renderer intentionally depends only on
    /// <see cref="DomDocument"/>.
    /// </summary>
    public DomDocument GetRenderDocument()
    {
        // Zoom baking runs per call (not part of the run-once guarded transforms) so the
        // geometry-snapshot path can revert it afterward via _zoomSerializationRevertLog;
        // it is idempotent once baked (the `zoom` property is stripped), so repeat calls on
        // the render path are no-ops. Must run before the guarded pseudo/progress transforms,
        // which depend on the baked sizes.
        ApplyZoomSerializationStyles(DocumentElement, 1.0);
        ApplySerializationTransforms();
        ReflectRenderState(DocumentElement);
        return _document;
    }

    /// <summary>
    /// RF-BRIDGE-1b render-doc/live-doc separation: restores the live document's zoom-baked
    /// elements to their pre-bake Style/Attributes (captured in
    /// <see cref="_zoomSerializationRevertLog"/>). Called by the geometry-snapshot path after
    /// the layout is captured, so subsequent CSSOM/estimator queries see pristine (unzoomed)
    /// styles rather than the render-oriented baked sizes.
    /// </summary>
    private void RevertZoomSerialization()
    {
        var log = _zoomSerializationRevertLog;
        _zoomSerializationRevertLog = null;
        if (log == null)
            return;

        for (var i = log.Count - 1; i >= 0; i--)
        {
            var (element, style, attributes) = log[i];
            RestoreStringMap(InlineStyle(element), style);
            RestoreAttributes(element, attributes);
        }

        // Reverted styles must not read back the baked values from the computed cache.
        ClearComputedPropsCache();
    }

    private static void RestoreStringMap(IDictionary<string, string> target, Dictionary<string, string> saved)
    {
        target.Clear();
        foreach (var kv in saved)
            target[kv.Key] = kv.Value;
    }

    /// <summary>
    /// Serializes the element's authoritative inline-style dict back into its canonical
    /// <c>style=</c> attribute in CSSOM serialization form (shorthand-first, <c>"; "</c>-joined),
    /// removing the attribute when the dict is empty. This is the single inline-style write-through
    /// (Phase 4 item 2): it runs at serialization (<see cref="ReflectRenderState"/>) and after every
    /// script <c>element.style</c> mutation, so a JS style mutation and <c>getAttribute("style")</c>
    /// observe the same state. Uses the node-model <see cref="SetAttr"/>/<see cref="RemoveAttr"/> (not
    /// the JS <c>setAttribute</c> binding), so there is no reparse loop back into the dict.
    /// </summary>
    private void SyncStyleAttributeFromInlineStyle(DomElement element)
    {
        var style = InlineStyle(element);
        if (style.Count == 0)
        {
            RemoveAttr(element, "style");
            return;
        }

        var styleText = string.Join(
            "; ",
            style
                .OrderBy(kv => HtmlSerializer.IsShorthandProperty(kv.Key) ? 0 : 1)
                .Select(static kv => $"{kv.Key}: {kv.Value}"));
        if (!TryGetAttribute(element, "style", out var currentStyle) ||
            !string.Equals(currentStyle, styleText, StringComparison.Ordinal))
        {
            SetAttr(element, "style", styleText);
        }
    }

    private void ReflectRenderState(DomElement element)
    {
        if (!IsText(element) && !element.TagName.StartsWith('#'))
        {
            SyncStyleAttributeFromInlineStyle(element);

            if (element.TagName.Equals("input", StringComparison.OrdinalIgnoreCase) &&
                !HasAttr(element, "value") &&
                GetElementRuntimeState(element).FormControl.Value.TryGet(out var idlValue) &&
                idlValue is string { Length: > 0 } idlString)
            {
                SetAttr(element, "value", idlString);
            }
        }

        foreach (var child in ChildElements(element))
            ReflectRenderState(child);
    }

    private string SerializeElementToHtml(DomElement element) =>
        HtmlSerializer.Serialize(element, CreateSerializationAdapter(),
            new HtmlSerializationOptions(MaximumDepth: MaxSerializationDepth, EncodeTextNodes: false));

    private string SerializeChildrenToHtml(DomElement element) => string.Concat(ChildElements(element).Select(SerializeElementToHtml));

    private void ApplySerializationTransforms()
    {
        if (_serializationTransformsApplied)
            return;

        _serializationTransformsApplied = true;
        RemoveRenderCommentNodes(DocumentElement);
        // Zoom baking is applied by the callers (GetRenderDocument/SerializeToHtml) before this,
        // so it can be reverted on the geometry-snapshot path; pseudo/progress below depend on
        // the baked sizes and must run after it.
        ApplyZoomPseudoSerializationOverrides();
        ApplyProgressLikeSerializationPlaceholders(DocumentElement);
    }

    /// <summary>
    /// Drops comment nodes from the render-bound document. Comments never render,
    /// but the shared serializer emits them as <c>&lt;!--…--&gt;</c>, which splits
    /// an otherwise-contiguous run of text around a comment (e.g.
    /// <c>"\n&lt;!-- c --&gt;\n"</c> between block siblings) into two separate text
    /// nodes when the canonical HTML is re-parsed for layout. CSS white-space
    /// processing then collapses each run independently, yielding a spurious extra
    /// space between elements (and an uncollapsed leading space at the start of a
    /// block) that shifts all following content — a common cause of the WPT
    /// "MissingContent" pixel mismatches in comment-heavy tests. Removing the
    /// comment nodes lets the surrounding text re-parse as a single node so the run
    /// collapses as the spec requires. Runs only inside <see cref="ApplySerializationTransforms"/>,
    /// so JS-visible <c>innerHTML</c>/<c>outerHTML</c> (which serialize without it)
    /// still expose the comments.
    /// </summary>
    private static void RemoveRenderCommentNodes(DomElement element)
    {
        for (int i = element.ChildNodes.Count - 1; i >= 0; i--)
        {
            var child = element.ChildNodes[i];
            if (IsComment(child))
            {
                RemoveNthChild(element, i);
                continue;
            }

            if (child is DomElement childElement)
                RemoveRenderCommentNodes(childElement);
        }
    }

    private void ApplyZoomPseudoSerializationOverrides()
    {
        var rules = new List<string>();
        int pseudoIndex = 0;
        CollectZoomPseudoSerializationOverrides(DocumentElement, 1.0, rules, ref pseudoIndex);
        if (rules.Count == 0)
            return;

        var styleElement = CreateBridgeElement("style");
        SetElementTextContent(styleElement, string.Join(Environment.NewLine, rules));

        var head = FindFirstElementByTagName(DocumentElement, "head");
        if (head != null)
        {
            SetParent(styleElement, head);
            head.AppendChild(styleElement);
            return;
        }

        SetParent(styleElement, DocumentElement);
        InsertChildAt(DocumentElement, 0, styleElement);
    }

    private void CollectZoomPseudoSerializationOverrides(DomElement element, double parentZoom, List<string> rules, ref int pseudoIndex)
    {
        if (IsText(element))
            return;

        var props = GetComputedProps(element);
        var specifiedZoom = props.GetValueOrDefault("zoom");
        var usedZoom = ResolveSpecifiedZoom(specifiedZoom, parentZoom);

        if (Math.Abs(usedZoom - 1.0) > ZoomSerializationEpsilon)
        {
            AppendZoomPseudoSerializationOverride(element, "::before", usedZoom, rules, ref pseudoIndex);
            AppendZoomPseudoSerializationOverride(element, "::after", usedZoom, rules, ref pseudoIndex);
        }

        foreach (var child in ChildElements(element))
            CollectZoomPseudoSerializationOverrides(child, usedZoom, rules, ref pseudoIndex);
    }

    private void AppendZoomPseudoSerializationOverride(DomElement element, string pseudoElement, double usedZoom, List<string> rules, ref int pseudoIndex)
    {
        var pseudoProps = BuildComputedStyleMap(element, pseudoElement);
        var content = pseudoProps.GetValueOrDefault("content")?.Trim();
        if (string.IsNullOrEmpty(content) ||
            string.Equals(content, "none", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(content, "normal", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var declarations = new List<string>();
        foreach (var property in ZoomScaledSerializationProperties)
        {
            if (!pseudoProps.TryGetValue(property, out var value) || string.IsNullOrWhiteSpace(value))
                continue;

            if (TryScaleSerializableCssValue(value, usedZoom, out var scaled))
                declarations.Add($"{property}: {scaled} !important");
        }

        if (declarations.Count == 0)
            return;

        if (string.IsNullOrWhiteSpace(element.Id))
            element.Id = $"broiler-zoom-pseudo-{++pseudoIndex}";

        rules.Add($"#{element.Id}{pseudoElement} {{ {string.Join("; ", declarations)}; }}");
    }

    private static DomElement? FindFirstElementByTagName(DomElement root, string tagName)
    {
        if (string.Equals(root.TagName, tagName, StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (var child in ChildElements(root))
        {
            var match = FindFirstElementByTagName(child, tagName);
            if (match != null)
                return match;
        }

        return null;
    }

    private void ApplyProgressLikeSerializationPlaceholders(DomElement element)
    {
        if (IsText(element))
            return;

        foreach (var child in ChildElements(element).ToList())
            ApplyProgressLikeSerializationPlaceholders(child);

        var tag = element.TagName.ToLowerInvariant();
        if (tag is not ("progress" or "meter"))
            return;

        var props = GetComputedProps(element);
        var width = props.GetValueOrDefault("width");
        var height = props.GetValueOrDefault("height");
        var writingMode = props.GetValueOrDefault("writing-mode") ?? "horizontal-tb";
        var direction = props.GetValueOrDefault("direction") ?? "ltr";
        var vertical = IsVerticalWritingMode(writingMode);
        var reverseInline = string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase);
        var ratio = ResolveProgressLikeValueRatio(element, tag);

        InlineStyle(element)["display"] = "inline-block";
        InlineStyle(element)["box-sizing"] = "border-box";
        InlineStyle(element)["position"] = "relative";
        InlineStyle(element)["overflow"] = "hidden";
        InlineStyle(element)["padding"] = "0";
        InlineStyle(element)["border"] = "1px solid #767676";
        InlineStyle(element)["background-color"] = tag == "meter" ? "#e6e6e6" : "#f0f0f0";
        InlineStyle(element)["vertical-align"] = "middle";
        if (!string.IsNullOrWhiteSpace(width) && !string.Equals(width, "auto", StringComparison.OrdinalIgnoreCase))
            InlineStyle(element)["width"] = width;
        if (!string.IsNullOrWhiteSpace(height) && !string.Equals(height, "auto", StringComparison.OrdinalIgnoreCase))
            InlineStyle(element)["height"] = height;

        ClearChildren(element);

        var fill = CreateBridgeElement("div");
        SetParent(fill, element);
        InlineStyle(fill)["position"] = "absolute";
        InlineStyle(fill)["background-color"] = tag == "meter" ? "#4caf50" : "#0a84ff";

        var fillExtent = vertical
            ? ReadPixelLength(height, DefaultProgressLikeTrackLengthPx) * ratio
            : ReadPixelLength(width, DefaultProgressLikeTrackLengthPx) * ratio;
        var fillExtentPx = $"{fillExtent.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}px";
        if (vertical)
        {
            InlineStyle(fill)["left"] = "0";
            InlineStyle(fill)["right"] = "0";
            InlineStyle(fill)[reverseInline ? "bottom" : "top"] = "0";
            InlineStyle(fill)["height"] = fillExtentPx;
        }
        else
        {
            InlineStyle(fill)["top"] = "0";
            InlineStyle(fill)["bottom"] = "0";
            InlineStyle(fill)[reverseInline ? "right" : "left"] = "0";
            InlineStyle(fill)["width"] = fillExtentPx;
        }

        element.AppendChild(fill);
    }

    private static double ResolveProgressLikeValueRatio(DomElement element, string tag)
    {
        var min = tag == "meter" ? ReadNumericAttribute(element, "min", 0) : 0;
        var max = ReadNumericAttribute(element, "max", 1);
        if (max <= min)
            max = min + 1;

        var value = ReadNumericAttribute(element, "value", min);
        return Math.Clamp((value - min) / (max - min), 0, 1);
    }

    private static double ReadNumericAttribute(DomElement element, string attributeName, double fallback)
    {
        if (!TryGetAttribute(element, attributeName, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
            return fallback;

        return double.TryParse(
            rawValue,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;
    }

    private static double ReadPixelLength(string? rawValue, double fallback)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return fallback;

        var trimmed = rawValue.Trim();
        if (trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^2];

        return double.TryParse(
            trimmed,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;
    }

    private void ApplyZoomSerializationStyles(DomElement element, double parentZoom)
    {
        if (IsText(element))
            return;

        var props = GetComputedProps(element);
        var specifiedZoom = props.GetValueOrDefault("zoom");
        var usedZoom = ResolveSpecifiedZoom(specifiedZoom, parentZoom);

        var willScale = Math.Abs(usedZoom - 1.0) > ZoomSerializationEpsilon;
        var willSvg = ShouldApplySvgSerializationAttributes(element);
        // Record the pre-bake state for the geometry-snapshot revert (only when this element
        // is actually mutated — scaled, SVG-adjusted, or carrying a `zoom` to strip).
        if (_zoomSerializationRevertLog != null && (willScale || willSvg || InlineStyle(element).ContainsKey("zoom")))
            _zoomSerializationRevertLog.Add((
                element,
                new Dictionary<string, string>(InlineStyle(element)),
                AttributeSnapshot(element)));

        if (willScale)
        {
            foreach (var property in ZoomScaledSerializationProperties)
            {
                if (!TryGetZoomSerializableValue(element, props, property, out var value))
                    continue;

                if (TryScaleSerializableCssValue(value, usedZoom, out var scaled))
                    InlineStyle(element)[property] = scaled;
            }

        }

        if (willSvg)
            ApplyZoomSerializationSvgAttributes(element, usedZoom);

        InlineStyle(element).Remove("zoom");

        foreach (var child in ChildElements(element))
            ApplyZoomSerializationStyles(child, usedZoom);
    }

    private static bool ShouldApplySvgSerializationAttributes(DomElement element)
    {
        var tag = element.TagName.ToLowerInvariant();
        return tag is "svg" or "defs" or "path" or "rect" or "line" or "text" or "textpath" or "polygon" or "polyline";
    }

    private bool TryGetZoomSerializableValue(DomElement element, Dictionary<string, string> props, string property, out string value)
    {
        value = string.Empty;
        if (ZoomPreferSpecifiedProperties.Contains(property))
        {
            var specifiedProps = GetZoomSpecifiedStyleMap(element);
            if (specifiedProps.TryGetValue(property, out value) &&
                !string.IsNullOrWhiteSpace(value) &&
                !string.Equals(value.Trim(), "inherit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (props.TryGetValue(property, out value) && !string.IsNullOrWhiteSpace(value))
            return true;

        if (!InlineStyle(element).TryGetValue(property, out var specified) ||
            !string.Equals(specified?.Trim(), "inherit", StringComparison.OrdinalIgnoreCase) ||
            ParentEl(element) == null)
        {
            return false;
        }

        var parentProps = GetComputedProps(ParentEl(element));
        if (parentProps.TryGetValue(property, out value) && !string.IsNullOrWhiteSpace(value))
            return true;

        if (InlineStyle(ParentEl(element)!).TryGetValue(property, out value) && !string.IsNullOrWhiteSpace(value))
            return true;

        return false;
    }

    private Dictionary<string, string> GetZoomSpecifiedStyleMap(DomElement element)
    {
        if (!_zoomSpecifiedStyleCache.TryGetValue(element, out var specified))
        {
            specified = BuildSpecifiedStyleMap(element);
            _zoomSpecifiedStyleCache[element] = specified;
        }

        return specified;
    }

    private static readonly HashSet<string> ZoomPreferSpecifiedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "width",
        "height",
        "min-width",
        "min-height",
        "max-width",
        "max-height"
    };

    private static readonly string[] ZoomScaledSerializationProperties =
    [
        "width", "height", "min-width", "min-height", "max-width", "max-height",
        "top", "right", "bottom", "left",
        "margin-top", "margin-right", "margin-bottom", "margin-left",
        "padding-top", "padding-right", "padding-bottom", "padding-left",
        "scroll-margin-top", "scroll-margin-right", "scroll-margin-bottom", "scroll-margin-left",
        "scroll-padding-top", "scroll-padding-right", "scroll-padding-bottom", "scroll-padding-left",
        "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
        "stroke-width",
        "font-size", "line-height", "letter-spacing", "word-spacing", "text-indent",
        "border-radius", "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius",
        "outline-width", "outline-offset",
        "column-width", "column-height", "column-gap"
    ];

    private static bool TryScaleSerializableCssValue(string value, double factor, out string scaled)
    {
        scaled = string.Empty;
        var trimmed = value.Trim();
        if (trimmed.Length == 0 ||
            trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TryScaleLengthToken(trimmed, factor, out scaled))
            return true;

        var parts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is 2 or 3 or 4)
        {
            var scaledParts = new string[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!TryScaleLengthToken(parts[i], factor, out scaledParts[i]))
                    return false;
            }

            scaled = string.Join(" ", scaledParts);
            return true;
        }

        return false;
    }

    private static bool TryScaleLengthToken(string token, double factor, out string scaled)
    {
        scaled = string.Empty;
        var trimmed = token.Trim();
        if (trimmed.Length == 0)
            return false;

        ReadOnlySpan<string> units = ["px", "pt", "em", "rem"];
        foreach (var unit in units)
        {
            if (!trimmed.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
                continue;

            var numericPart = trimmed[..^unit.Length];
            if (!double.TryParse(numericPart, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            scaled = $"{(number * factor).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}{unit}";
            return true;
        }

        return false;
    }

    // RF-BRIDGE-1c Phase F (F3c part 2c): the serialization adapter is over canonical DomNode so
    // text/comment children serialize once construction flips to DomText/DomComment. GetKind keys
    // text/comment off NodeType (holds for facade and canonical char-data) and the doctype/fragment
    // kinds off the canonical node types; everything else is an element. GetName/GetAttributes/
    // GetStyles are only invoked for element/doctype nodes (see HtmlSerializer.Append), so their
    // Broiler.Dom.DomElement narrowing is always satisfied.
    private HtmlSerializationAdapter<DomNode> CreateSerializationAdapter() => new(
        GetKind: static node =>
            IsText(node) ? HtmlSerializationNodeKind.Text
            : IsComment(node) ? HtmlSerializationNodeKind.Comment
            : node is DomDocumentType ? HtmlSerializationNodeKind.DocumentType
            : node is DomDocumentFragment ? HtmlSerializationNodeKind.Fragment
            : HtmlSerializationNodeKind.Element,
        GetName: static node => node is DomDocumentType docType ? docType.Name
            : node is DomElement element ? element.TagName : string.Empty,
        // A materialised nested-browsing-context document is no longer an in-tree child (P4.4b
        // severed the #subdoc-root element); it is referenced off its <iframe>/<object>/<frame>
        // container and rasterised in isolation (srcdoc content round-trips via the srcdoc
        // attribute), so it can never appear in ChildNodes and needs no serialization skip.
        GetChildren: static node => node.ChildNodes,
        GetAttributes: node => node is DomElement element ? GetSerializableAttributes(element) : [],
        GetStyles: static node => node is DomElement element
            ? InlineStyle(element).OrderBy(kv => HtmlSerializer.IsShorthandProperty(kv.Key) ? 0 : 1)
            : [],
        // RF-BRIDGE-1c Phase F (F3c part 2d): text nodes serialize with the same HTML escaping the
        // former element-store textContent path applied — except inside raw-text elements
        // (script/style/…), whose character data must stay literal. The bridge serializes with
        // EncodeTextNodes:false, so GetText returns the already-escaped form. Comments stay raw.
        GetText: static node => node switch
        {
            DomText text => IsRawTextSerializationParent(text)
                ? text.Data
                : HtmlSerializer.Encode(text.Data),
            DomCharacterData other => other.Data,
            _ => BridgeText(node),
        },
        // Phase 4 item 3: the parallel InnerHtml string is gone — raw-text content is always a
        // canonical DomText child, serialized via GetChildren/GetText above. No raw fallback.
        GetRawInnerHtml: static _ => null);

    /// <summary>Whether <paramref name="node"/>'s parent is an HTML raw-text element whose text
    /// content is serialized literally (not HTML-escaped). The standard raw-text element set is
    /// owned by <see cref="SharedHtmlSerializer.RawTextElements"/> (§13.3); this bridge predicate
    /// only applies it to the node's parent. (RF-BRIDGE-1c Phase F, F3c part 2d.)</summary>
    private static bool IsRawTextSerializationParent(DomNode node) =>
        node.ParentNode is DomElement parent &&
        HtmlSerializer.IsRawTextElement(parent.TagName);

    private IEnumerable<KeyValuePair<string, string>> GetSerializableAttributes(DomElement element)
    {
        if (!string.IsNullOrEmpty(element.Id))
            yield return new("id", element.Id);
        if (!string.IsNullOrEmpty(element.ClassName))
            yield return new("class", element.ClassName);

        var serializedSrcDoc = TrySerializeCurrentSrcDoc(element);
        foreach (var attribute in element.Attributes.Values)
        {
            var name = attribute.QualifiedName;
            var value = attribute.Value;
            if (name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("class", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new(
                name,
                name.Equals("srcdoc", StringComparison.OrdinalIgnoreCase) && serializedSrcDoc is not null
                    ? serializedSrcDoc
                    : value);
        }

        if (element.TagName.Equals("input", StringComparison.OrdinalIgnoreCase) &&
            !HasAttr(element, "value") &&
            GetElementRuntimeState(element).FormControl.Value.TryGet(out var idlValue) &&
            idlValue is string { Length: > 0 } idlString)
        {
            yield return new("value", idlString);
        }
    }

    private string? TrySerializeCurrentSrcDoc(DomElement element)
    {
        if (!string.Equals(element.TagName, "iframe", StringComparison.OrdinalIgnoreCase) ||
            !HasAttr(element, "srcdoc"))
        {
            return null;
        }

        var subDocumentRoot = GetContentDocument(element);
        if (subDocumentRoot == null || subDocumentRoot.ChildNodes.Count == 0)
            return null;

        return string.Concat(ChildElements(subDocumentRoot).Select(SerializeElementToHtml));
    }

}
