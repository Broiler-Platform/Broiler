using System.Text.RegularExpressions;
using SharedHtmlSerializer = Broiler.Dom.Html.HtmlSerializer;
using Broiler.Dom.Html;

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
    private readonly Dictionary<Broiler.Dom.DomElement, Dictionary<string, string>> _zoomSpecifiedStyleCache = [];

    // RF-BRIDGE-1b render-doc/live-doc separation: when non-null, ApplyZoomSerializationStyles
    // records each mutated element's pre-bake Style + Attributes here so the geometry-snapshot
    // path can restore the live document afterward (zoom baking is destructive — it scales
    // sizes and drops the `zoom` property — which would otherwise corrupt subsequent unzoomed
    // CSSOM queries, e.g. clientWidth of a zoomed element). The render/serialize paths leave
    // this null so their baking stays permanent.
    private List<(Broiler.Dom.DomElement Element, Dictionary<string, string> Style, Dictionary<string, string> Attributes)>? _zoomSerializationRevertLog;

    /// <summary>
    /// Serialises the current DOM tree back to an HTML string.
    /// Call this after JavaScript execution to obtain the modified page
    /// content for re-rendering.
    /// </summary>
    public string SerializeToHtml()
    {
        ApplyZoomSerializationStyles(DocumentElement, 1.0);
        ApplySerializationTransforms();
        return SharedHtmlSerializer.Serialize(
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
    /// <see cref="Broiler.Dom.DomDocument"/>.
    /// </summary>
    public Broiler.Dom.DomDocument GetRenderDocument()
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

    private void ReflectRenderState(Broiler.Dom.DomElement element)
    {
        if (!IsText(element) && !element.TagName.StartsWith('#'))
        {
            if (InlineStyle(element).Count == 0)
            {
                RemoveAttr(element, "style");
            }
            else
            {
                var styleText = string.Join(
                    "; ",
                    InlineStyle(element)
                        .OrderBy(kv => SharedHtmlSerializer.IsShorthandProperty(kv.Key) ? 0 : 1)
                        .Select(static kv => $"{kv.Key}: {kv.Value}"));
                if (!TryGetAttribute(element, "style", out var currentStyle) ||
                    !string.Equals(currentStyle, styleText, StringComparison.Ordinal))
                {
                    SetAttr(element, "style", styleText);
                }
            }

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

    private string SerializeElementToHtml(Broiler.Dom.DomElement element) =>
        SharedHtmlSerializer.Serialize(
            element,
            CreateSerializationAdapter(),
            new HtmlSerializationOptions(MaximumDepth: MaxSerializationDepth, EncodeTextNodes: false));

    private string SerializeChildrenToHtml(Broiler.Dom.DomElement element) => string.Concat(ChildElements(element).Select(SerializeElementToHtml));

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
    private static void RemoveRenderCommentNodes(Broiler.Dom.DomElement element)
    {
        for (int i = element.ChildNodes.Count - 1; i >= 0; i--)
        {
            var child = element.ChildNodes[i];
            if (IsComment(child))
            {
                RemoveNthChild(element, i);
                continue;
            }

            if (child is Broiler.Dom.DomElement childElement)
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

    private void CollectZoomPseudoSerializationOverrides(
        Broiler.Dom.DomElement element,
        double parentZoom,
        List<string> rules,
        ref int pseudoIndex)
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

    private void AppendZoomPseudoSerializationOverride(
        Broiler.Dom.DomElement element,
        string pseudoElement,
        double usedZoom,
        List<string> rules,
        ref int pseudoIndex)
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

    private static Broiler.Dom.DomElement? FindFirstElementByTagName(Broiler.Dom.DomElement root, string tagName)
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

    private void ApplyProgressLikeSerializationPlaceholders(Broiler.Dom.DomElement element)
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
        GetElementRuntimeState(element).InnerHtml = string.Empty;

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

    private static double ResolveProgressLikeValueRatio(Broiler.Dom.DomElement element, string tag)
    {
        var min = tag == "meter" ? ReadNumericAttribute(element, "min", 0) : 0;
        var max = ReadNumericAttribute(element, "max", 1);
        if (max <= min)
            max = min + 1;

        var value = ReadNumericAttribute(element, "value", min);
        return Math.Clamp((value - min) / (max - min), 0, 1);
    }

    private static double ReadNumericAttribute(Broiler.Dom.DomElement element, string attributeName, double fallback)
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

    private void ApplyZoomSerializationStyles(Broiler.Dom.DomElement element, double parentZoom)
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

    private static bool ShouldApplySvgSerializationAttributes(Broiler.Dom.DomElement element)
    {
        var tag = element.TagName.ToLowerInvariant();
        return tag is "svg" or "defs" or "path" or "rect" or "line" or "text" or "textpath" or "polygon" or "polyline";
    }

    private bool TryGetZoomSerializableValue(
        Broiler.Dom.DomElement element,
        Dictionary<string, string> props,
        string property,
        out string value)
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

    private Dictionary<string, string> GetZoomSpecifiedStyleMap(Broiler.Dom.DomElement element)
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

    private void ApplyZoomSerializationSvgAttributes(Broiler.Dom.DomElement element, double usedZoom)
    {
        var tag = element.TagName.ToLowerInvariant();
        var props = GetComputedProps(element);

        ApplySvgPresentationAttribute(element, props, "fill");
        ApplySvgPresentationAttribute(element, props, "stroke");
        ApplySvgPresentationAttribute(element, props, "stroke-width", preferInlineStyle: true);

        if (tag is "text" or "textpath")
        {
            ApplySvgPresentationAttribute(element, props, "font-size", preferInlineStyle: true);
            ApplySvgPresentationAttribute(element, props, "font-family");
        }

        switch (tag)
        {
            case "svg":
                ScaleSvgLengthAttribute(element, "width", usedZoom);
                ScaleSvgLengthAttribute(element, "height", usedZoom);
                break;
            case "rect":
                ScaleSvgLengthAttribute(element, "x", usedZoom);
                ScaleSvgLengthAttribute(element, "y", usedZoom);
                ScaleSvgLengthAttribute(element, "width", usedZoom);
                ScaleSvgLengthAttribute(element, "height", usedZoom);
                break;
            case "line":
                ScaleSvgLengthAttribute(element, "x1", usedZoom);
                ScaleSvgLengthAttribute(element, "x2", usedZoom);
                ScaleSvgLengthAttribute(element, "y1", usedZoom);
                ScaleSvgLengthAttribute(element, "y2", usedZoom);
                break;
            case "text":
                ScaleSvgLengthAttribute(element, "x", usedZoom);
                ScaleSvgLengthAttribute(element, "y", usedZoom);
                break;
            case "polygon":
            case "polyline":
                ScaleSvgPointListAttribute(element, "points", usedZoom);
                break;
            case "path":
                ScaleSvgPathDataAttribute(element, "d", usedZoom);
                break;
        }
    }

    private void ApplySvgPresentationAttribute(
        Broiler.Dom.DomElement element,
        Dictionary<string, string> props,
        string propertyName,
        bool preferInlineStyle = false)
    {
        if (HasAttr(element, propertyName))
            return;

        string? value = null;
        if (preferInlineStyle && InlineStyle(element).TryGetValue(propertyName, out var inlineValue) && !string.IsNullOrWhiteSpace(inlineValue))
            value = inlineValue;
        else if (props.TryGetValue(propertyName, out var propValue) && !string.IsNullOrWhiteSpace(propValue))
            value = propValue;
        else if (preferInlineStyle && props.TryGetValue(propertyName, out var fallbackProp) && !string.IsNullOrWhiteSpace(fallbackProp))
            value = fallbackProp;

        if (string.IsNullOrWhiteSpace(value))
            return;

        SetAttr(element, propertyName, value.Trim());
    }

    private void ScaleSvgLengthAttribute(Broiler.Dom.DomElement element, string attributeName, double usedZoom)
    {
        if (!TryGetAttribute(element, attributeName, out var value) ||
            !TryScaleSvgLengthToken(element, value, usedZoom, out var scaled))
        {
            return;
        }

        SetAttr(element, attributeName, scaled);
    }

    private void ScaleSvgPointListAttribute(Broiler.Dom.DomElement element, string attributeName, double usedZoom)
    {
        if (!TryGetAttribute(element, attributeName, out var value) || string.IsNullOrWhiteSpace(value))
            return;

        SetAttr(element, attributeName, ScaleSvgPointRegex().Replace(value, match => ScaleSvgNumericMatch(match, usedZoom)));
    }

    private void ScaleSvgPathDataAttribute(Broiler.Dom.DomElement element, string attributeName, double usedZoom)
    {
        if (!TryGetAttribute(element, attributeName, out var value) || string.IsNullOrWhiteSpace(value))
            return;

        SetAttr(element, attributeName, ScaleSvgPathRegex().Replace(value, match => ScaleSvgNumericMatch(match, usedZoom)));
    }

    private static string ScaleSvgNumericMatch(Match match, double factor)
    {
        if (!double.TryParse(match.Value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var number))
        {
            return match.Value;
        }

        return (number * factor).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private bool TryScaleSvgLengthToken(Broiler.Dom.DomElement element, string value, double usedZoom, out string scaled)
    {
        scaled = string.Empty;
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.EndsWith('%'))
            return false;

        if (double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var unitlessNumber))
        {
            scaled = (unitlessNumber * usedZoom).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        foreach (var unit in SvgZoomScaledUnits)
        {
            if (!trimmed.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
                continue;

            var numericPart = trimmed[..^unit.Length];
            if (!double.TryParse(numericPart, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            if (TryResolveSvgFontRelativeUnitPixels(element, unit, out var unitPixels))
            {
                scaled = (number * unitPixels * usedZoom)
                    .ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            var factor = ResolveSvgLengthZoomFactor(element, unit, usedZoom);
            if (Math.Abs(factor - 1.0) < ZoomSerializationEpsilon)
                return false;

            scaled = $"{(number * factor).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}{unit}";
            return true;
        }

        return false;
    }

    private bool TryResolveSvgFontRelativeUnitPixels(Broiler.Dom.DomElement element, string unit, out double pixels)
    {
        pixels = 0;
        if (SvgRootFontRelativeUnits.Contains(unit))
        {
            pixels = ResolveOriginalRootSpecifiedFontSizePx() * GetSvgFontRelativeUnitRatio(unit);
            return pixels > 0;
        }

        if (!SvgFontRelativeUnits.Contains(unit))
            return false;

        pixels = ResolveOriginalNearestSpecifiedFontSizePx(element) * GetSvgFontRelativeUnitRatio(unit);
        return pixels > 0;
    }

    private double ResolveOriginalNearestSpecifiedFontSizePx(Broiler.Dom.DomElement element)
    {
        for (Broiler.Dom.DomElement? current = element; current != null; current = ParentEl(current))
        {
            if (TryGetSpecifiedFontSizePx(current, out var fontSize))
                return fontSize;
        }

        return ResolveOriginalRootSpecifiedFontSizePx();
    }

    private double ResolveOriginalRootSpecifiedFontSizePx() =>
        TryGetSpecifiedFontSizePx(DocumentElement, out var fontSize) ? fontSize : 16;

    private bool TryGetSpecifiedFontSizePx(Broiler.Dom.DomElement element, out double fontSize)
    {
        fontSize = 0;
        var specified = BuildSpecifiedStyleMap(element);
        if (TryParsePx(specified.GetValueOrDefault("font-size")) is double px)
        {
            fontSize = px;
            return true;
        }

        if (!specified.TryGetValue("font", out var fontShorthand) || string.IsNullOrWhiteSpace(fontShorthand))
            return false;

        var sizeMatch = FontShortHandRegex().Match(fontShorthand);
        if (!sizeMatch.Success ||
            !double.TryParse(sizeMatch.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out fontSize))
        {
            return false;
        }

        return true;
    }

    private static double GetSvgFontRelativeUnitRatio(string unit) => unit.ToLowerInvariant() switch
    {
        // Broiler's SVG length resolution currently uses the same deterministic
        // Ahem-like 0.8em approximation that the existing font-relative zoom
        // coverage already assumes for ex/cap units.
        "ex" or "rex" or "cap" or "rcap" => 0.8,
        _ => 1.0
    };

    private double ResolveSvgLengthZoomFactor(Broiler.Dom.DomElement element, string unit, double usedZoom)
    {
        if (SvgAbsoluteOrViewportUnits.Contains(unit))
            return usedZoom;

        if (SvgRootFontRelativeUnits.Contains(unit))
            return usedZoom / GetRootFontSizeOwnerZoom();

        if (SvgFontRelativeUnits.Contains(unit))
            return usedZoom / GetNearestExplicitFontSizeOwnerZoom(element);

        return usedZoom;
    }

    private double GetNearestExplicitFontSizeOwnerZoom(Broiler.Dom.DomElement element)
    {
        for (Broiler.Dom.DomElement? current = element; current != null; current = ParentEl(current))
        {
            var props = GetComputedProps(current);
            if (props.TryGetValue("font-size", out var fontSize) && !string.IsNullOrWhiteSpace(fontSize))
                return GetUsedZoomForElement(current);
        }

        return 1.0;
    }

    private double GetRootFontSizeOwnerZoom()
    {
        var props = GetComputedProps(DocumentElement);
        if (props.TryGetValue("font-size", out var fontSize) && !string.IsNullOrWhiteSpace(fontSize))
            return GetUsedZoomForElement(DocumentElement);

        return 1.0;
    }

    private static readonly string[] SvgZoomScaledUnits =
    [
        "rcap", "rch", "ric", "rex", "rlh", "rem",
        "vmin", "vmax",
        "cap",
        "em", "ex", "ch", "ic", "lh",
        "vw", "vh",
        "px", "pt", "pc", "cm", "mm", "in", "q"
    ];

    private static readonly HashSet<string> SvgAbsoluteOrViewportUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "vw", "vh", "vmin", "vmax",
        "px", "pt", "pc", "cm", "mm", "in", "q"
    };

    private static readonly HashSet<string> SvgFontRelativeUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "em", "ex", "cap", "ch", "ic", "lh"
    };

    private static readonly HashSet<string> SvgRootFontRelativeUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "rem", "rex", "rcap", "rch", "ric", "rlh"
    };

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
    // text/comment off NodeType (holds for facade and canonical char-data), and the remaining
    // special kinds off the facade #document-fragment/#subdoc-root/#doctype TagNames (still facade
    // elements). GetName/GetAttributes/GetStyles/GetRawInnerHtml are only invoked for element/doctype
    // nodes (see HtmlSerializer.Append), so their Broiler.Dom.DomElement narrowing is always satisfied.
    private HtmlSerializationAdapter<Broiler.Dom.DomNode> CreateSerializationAdapter() => new(
        GetKind: static node =>
            IsText(node) ? HtmlSerializationNodeKind.Text
            : IsComment(node) ? HtmlSerializationNodeKind.Comment
            : node is Broiler.Dom.DomElement element
                ? element.TagName.ToLowerInvariant() switch
                {
                    "#document-fragment" => HtmlSerializationNodeKind.Fragment,
                    "#subdoc-root" => HtmlSerializationNodeKind.DocumentRoot,
                    "#doctype" => HtmlSerializationNodeKind.DocumentType,
                    _ => HtmlSerializationNodeKind.Element
                }
                : HtmlSerializationNodeKind.Element,
        GetName: static node => node is Broiler.Dom.DomElement element ? element.TagName : string.Empty,
        // Never serialise a materialised nested-browsing-context (#subdoc-root) into
        // its container.  A sub-document is fetched and attached to its <iframe>/
        // <object>/<frame> so scripts can reach contentDocument and onload can fire,
        // but it is a *separate* document — emitting it inline leaks its <style> into
        // the parent's cascade (WPT css/CSS2/box-display/root-canvas-001, where the
        // embedded p{background:green;height:100%} painted the whole parent green).
        // The renderer rasterises each embedded document in isolation instead
        // (srcdoc content is round-tripped via the srcdoc attribute).
        GetChildren: static node => node.ChildNodes.Where(static child =>
            !(child is Broiler.Dom.DomElement childElement && string.Equals(childElement.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase))),
        GetAttributes: node => node is Broiler.Dom.DomElement element ? GetSerializableAttributes(element) : [],
        GetStyles: static node => node is Broiler.Dom.DomElement element
            ? InlineStyle(element).OrderBy(kv => SharedHtmlSerializer.IsShorthandProperty(kv.Key) ? 0 : 1)
            : [],
        // RF-BRIDGE-1c Phase F (F3c part 2d): text nodes serialize with the same HTML escaping the
        // former element-store textContent path applied — except inside raw-text elements
        // (script/style/…), whose character data must stay literal. The bridge serializes with
        // EncodeTextNodes:false, so GetText returns the already-escaped form. Comments stay raw.
        GetText: static node => node switch
        {
            Broiler.Dom.DomText text => IsRawTextSerializationParent(text)
                ? text.Data
                : SharedHtmlSerializer.Encode(text.Data),
            Broiler.Dom.DomCharacterData other => other.Data,
            _ => BridgeText(node),
        },
        GetRawInnerHtml: static node => GetElementRuntimeState(node).InnerHtml);

    /// <summary>Whether <paramref name="node"/>'s parent is an HTML raw-text element whose text
    /// content is serialized literally (not HTML-escaped) — <c>script</c>, <c>style</c>, and the
    /// other raw-text elements. (RF-BRIDGE-1c Phase F, F3c part 2d.)</summary>
    private static bool IsRawTextSerializationParent(Broiler.Dom.DomNode node) =>
        node.ParentNode is Broiler.Dom.DomElement parent &&
        parent.TagName.ToLowerInvariant() is "script" or "style" or "xmp" or "iframe"
            or "noembed" or "noframes" or "noscript" or "plaintext";

    private IEnumerable<KeyValuePair<string, string>> GetSerializableAttributes(Broiler.Dom.DomElement element)
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

    private string? TrySerializeCurrentSrcDoc(Broiler.Dom.DomElement element)
    {
        if (!string.Equals(element.TagName, "iframe", StringComparison.OrdinalIgnoreCase) ||
            !HasAttr(element, "srcdoc"))
        {
            return null;
        }

        var subDocumentRoot = ChildElements(element).FirstOrDefault(child =>
            string.Equals(child.TagName, "#subdoc-root", StringComparison.OrdinalIgnoreCase));
        if (subDocumentRoot == null || subDocumentRoot.ChildNodes.Count == 0)
            return null;

        return string.Concat(ChildElements(subDocumentRoot).Select(SerializeElementToHtml));
    }

    [GeneratedRegex(@"-?\d*\.?\d+(?:[eE][+-]?\d+)?")]
    private static partial Regex ScaleSvgPointRegex();
    
    [GeneratedRegex(@"-?\d*\.?\d+(?:[eE][+-]?\d+)?")]
    private static partial Regex ScaleSvgPathRegex();

    [GeneratedRegex(@"(?<![\w.-])(-?\d*\.?\d+)px(?:\s*/|(?=\s|$))", RegexOptions.IgnoreCase)]
    private static partial Regex FontShortHandRegex();
}
