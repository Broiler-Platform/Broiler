using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

/// <summary>
/// Resolves CSS animation snapshots — for elements with <c>animation</c> and a
/// negative <c>animation-delay</c>, computes the animated property values at the
/// implied time offset and writes them directly into the element's inline style,
/// replacing the <c>animation</c>/<c>animation-delay</c> properties.  This allows
/// the static Broiler renderer to produce the correct visual output for tests that
/// rely on CSS animations (e.g. WPT <c>animation-delay-008.html</c>).
/// </summary>
public sealed partial class DomBridge
{
    /// <summary>
    /// Walks the DOM tree and resolves any CSS animations that have a negative
    /// <c>animation-delay</c> to their computed property values at <c>t=0</c>.
    /// Must be called after script execution and before serialization.
    /// </summary>
    public void ResolveAnimationSnapshots()
    {
        // 1. Collect @keyframes definitions from <style> elements.
        var keyframesMap = new Dictionary<string, List<KeyframeEntry>>(StringComparer.Ordinal);
        CollectKeyframes(DocumentElement, keyframesMap);

        if (keyframesMap.Count == 0) return;

        // 2. Walk all elements and resolve animations.
        ResolveAnimationsOnTree(DocumentElement, keyframesMap);
    }

    // -----------------------------------------------------------------
    // Keyframe parsing
    // -----------------------------------------------------------------

    private sealed record KeyframeEntry(float Position, Dictionary<string, string> Properties);

    private static void CollectKeyframes(DomElement root, Dictionary<string, List<KeyframeEntry>> map)
    {
        if (string.Equals(root.TagName, "style", StringComparison.OrdinalIgnoreCase))
        {
            var css = root.TextContent;
            // Fall back to concatenating child text nodes when TextContent is not set.
            if (string.IsNullOrEmpty(css))
            {
                css = string.Concat(root.Children
                    .Where(c => c.IsTextNode)
                    .Select(c => c.TextContent ?? string.Empty));
            }
            var styleSheet = new Broiler.CSS.CssParser().ParseStyleSheet(css);
            foreach (var atRule in styleSheet.Rules.OfType<Broiler.CSS.CssAtRule>())
            {
                if (!atRule.Name.Equals("keyframes", StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = atRule.Prelude.Trim().Trim('"', '\'');
                var entries = ParseKeyframeEntries(atRule);
                if (entries.Count > 0)
                    map[name] = entries;
            }
        }

        foreach (var child in root.Children)
            CollectKeyframes(child, map);
    }

    private static List<KeyframeEntry> ParseKeyframeEntries(Broiler.CSS.CssAtRule keyframesRule)
    {
        var entries = new List<KeyframeEntry>();

        foreach (var styleRule in keyframesRule.Rules.OfType<Broiler.CSS.CssStyleRule>())
        {
            var declarations = ParseDeclarations(
                Broiler.CSS.CssSerializer.Serialize(styleRule.Declarations));

            foreach (var selector in styleRule.Selectors.Selectors)
            {
                var s = selector.Text.Trim().ToLowerInvariant();
                float? pos = s switch
                {
                    "from" => 0f,
                    "to" => 1f,
                    _ when s.EndsWith('%') && float.TryParse(s.TrimEnd('%'),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out var pct) => pct / 100f,
                    _ => null,
                };

                if (pos.HasValue)
                    entries.Add(new KeyframeEntry(pos.Value, declarations));
            }
        }

        return entries.OrderBy(e => e.Position).ToList();
    }

    private static Dictionary<string, string> ParseDeclarations(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var declarations = new Broiler.CSS.CssParser().ParseDeclarations(text);
        foreach (var declaration in declarations.Declarations)
        {
            var value = declaration.Value.Text;
            if (declaration.Important)
                value += " !important";
            result[declaration.Name] = value;
        }
        return result;
    }

    // -----------------------------------------------------------------
    // Animation resolution
    // -----------------------------------------------------------------

    private void ResolveAnimationsOnTree(
        DomElement element,
        Dictionary<string, List<KeyframeEntry>> keyframesMap)
    {
        // Check if this element has animation properties set (inline styles).
        string? animValue = null, delayValue = null, nameValue = null;
        bool hasAnimation = false, hasDelay = false, hasName = false;

        if (element.Style.Count > 0)
        {
            hasAnimation = element.Style.TryGetValue("animation", out animValue);
            hasDelay = element.Style.TryGetValue("animation-delay", out delayValue);
            hasName = element.Style.TryGetValue("animation-name", out nameValue);
        }

        // Also check stylesheet rules that may apply to this element.
        if (!hasAnimation && !hasName)
        {
            var sheetProps = CollectStylesheetAnimationProperties(element);
            if (sheetProps != null)
            {
                if (!hasAnimation && sheetProps.TryGetValue("animation", out var sv))
                    { hasAnimation = true; animValue = sv; }
                if (!hasDelay && sheetProps.TryGetValue("animation-delay", out var dv))
                    { hasDelay = true; delayValue = dv; }
                if (!hasName && sheetProps.TryGetValue("animation-name", out var nv))
                    { hasName = true; nameValue = nv; }
            }
        }

        if (hasAnimation || hasName)
        {
            TryResolveAnimation(element, keyframesMap,
                animValue, delayValue, nameValue);
        }

        foreach (var child in element.Children)
            ResolveAnimationsOnTree(child, keyframesMap);
    }

    // -----------------------------------------------------------------
    // Stylesheet animation property matching
    // -----------------------------------------------------------------

    /// <summary>
    /// Collects animation-related properties from <c>&lt;style&gt;</c> elements
    /// whose selectors match the given element.  This is a simplified matcher
    /// that handles tag selectors (e.g. <c>body</c>, <c>html</c>).
    /// </summary>
    private static Dictionary<string, string>? CollectStylesheetAnimationProperties(DomElement element)
    {
        // Walk up to find <style> elements.
        var root = element;
        while (root.Parent != null) root = root.Parent;

        Dictionary<string, string>? result = null;
        CollectAnimPropsFromStyleElements(root, element, ref result);
        return result;
    }

    private static void CollectAnimPropsFromStyleElements(
        DomElement node, DomElement target, ref Dictionary<string, string>? result)
    {
        if (string.Equals(node.TagName, "style", StringComparison.OrdinalIgnoreCase))
        {
            var css = node.TextContent;
            if (string.IsNullOrEmpty(css))
            {
                css = string.Concat(node.Children
                    .Where(c => c.IsTextNode)
                    .Select(c => c.TextContent ?? string.Empty));
            }

            var styleSheet = new Broiler.CSS.CssParser().ParseStyleSheet(css);
            foreach (var styleRule in styleSheet.Rules.OfType<Broiler.CSS.CssStyleRule>())
            {
                foreach (var selector in styleRule.Selectors.Selectors)
                {
                    if (!SimpleMatchesElement(selector.Text, target))
                        continue;

                    var declarations = ParseDeclarations(
                        Broiler.CSS.CssSerializer.Serialize(styleRule.Declarations));
                    foreach (var kv in declarations)
                    {
                        if (kv.Key.StartsWith("animation", StringComparison.OrdinalIgnoreCase))
                        {
                            result ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            result[kv.Key] = kv.Value;
                        }
                    }
                }
            }
        }

        foreach (var child in node.Children)
            CollectAnimPropsFromStyleElements(child, target, ref result);
    }

    /// <summary>
    /// Very simple CSS selector matcher — handles tag names, classes, IDs,
    /// and <c>:root</c> pseudo-class.  Sufficient for WPT body/html selectors.
    /// </summary>
    private static bool SimpleMatchesElement(string selector, DomElement element)
    {
        var selTrimmed = selector.Trim().ToLowerInvariant();

        // Tag name selector (e.g. "body", "html")
        if (selTrimmed == element.TagName?.ToLowerInvariant())
            return true;

        // :root matches the html element
        if (selTrimmed == ":root" &&
            string.Equals(element.TagName, "html", StringComparison.OrdinalIgnoreCase))
            return true;

        // ID selector (e.g. "#myid")
        if (selTrimmed.StartsWith('#'))
        {
            var id = selTrimmed.Substring(1);
            return string.Equals(element.Id, id, StringComparison.OrdinalIgnoreCase);
        }

        // Class selector (e.g. ".myclass")
        if (selTrimmed.StartsWith('.'))
        {
            var cls = selTrimmed.Substring(1);
            return element.ClassName?.Split(' ')
                .Any(c => string.Equals(c, cls, StringComparison.OrdinalIgnoreCase)) == true;
        }

        return false;
    }

    private void TryResolveAnimation(
        DomElement element,
        Dictionary<string, List<KeyframeEntry>> keyframesMap,
        string? animationShorthand,
        string? animationDelay,
        string? animationName)
    {
        // Parse animation parameters from the shorthand.
        string? name = null;
        double durationSec = 0;
        double delaySec = 0;
        string timingFunction = "ease";
        string fillMode = "none";

        if (!string.IsNullOrWhiteSpace(animationShorthand))
        {
            var parts = TokenizeAnimationShorthand(animationShorthand!);
            var durations = new List<double>();

            foreach (var part in parts)
            {
                if (TryParseCssTime(part, out var sec))
                    durations.Add(sec);
                else if (IsTimingFunction(part))
                    timingFunction = part;
                else if (part is "none" or "forwards" or "backwards" or "both")
                    fillMode = part;
                else if (name == null && !IsKnownAnimationKeyword(part))
                    name = part;
            }

            if (durations.Count >= 1) durationSec = durations[0];
            if (durations.Count >= 2) delaySec = durations[1];
        }

        // Override with individual longhand properties.
        if (!string.IsNullOrWhiteSpace(animationName))
            name = animationName;
        if (!string.IsNullOrWhiteSpace(animationDelay) &&
            TryParseCssTime(animationDelay!, out var delayOverride))
            delaySec = delayOverride;

        if (string.IsNullOrEmpty(name) || durationSec <= 0)
            return;

        if (!keyframesMap.TryGetValue(name!, out var keyframes) || keyframes.Count == 0)
            return;

        double currentTimeMs = 0;
        var hasCurrentTimeOverride = false;
        if (GetElementRuntimeState(element).Animation.CurrentTimeMilliseconds.TryGet(out var currentTimeValue) &&
            currentTimeValue is double currentTimeMsValue)
        {
            currentTimeMs = currentTimeMsValue;
            hasCurrentTimeOverride = true;
        }

        double elapsed;
        if (hasCurrentTimeOverride)
        {
            var currentTimeSec = currentTimeMs / 1000.0;
            if (delaySec >= 0)
            {
                if (currentTimeSec < delaySec)
                {
                    if (fillMode is "backwards" or "both")
                    {
                        foreach (var kv in keyframes[0].Properties)
                            element.Style[kv.Key] = kv.Value;
                    }
                    return;
                }

                elapsed = currentTimeSec - delaySec;
            }
            else
            {
                elapsed = currentTimeSec;
            }
        }
        else
        {
            // Only resolve for negative delays (animation already running at t=0).
            if (delaySec >= 0)
                return;

            elapsed = Math.Abs(delaySec);
        }

        // Compute progress: elapsed / duration.
        double rawProgress = elapsed / durationSec;

        // Clamp progress to [0, 1] for a single iteration.
        rawProgress = Math.Min(rawProgress, 1.0);

        // Find the two surrounding keyframes and interpolate.
        // NOTE: The timing function is applied per-interval, not globally.
        var resolvedProps = ResolveKeyframeProperties(element, keyframes, (float)rawProgress, timingFunction);

        // Apply resolved values as inline styles and remove animation properties.
        foreach (var kv in resolvedProps)
            element.Style[kv.Key] = kv.Value;

        element.Style.Remove("animation");
        element.Style.Remove("animation-delay");
        element.Style.Remove("animation-name");
        element.Style.Remove("animation-duration");
        element.Style.Remove("animation-timing-function");
    }

    private Dictionary<string, string> ResolveKeyframeProperties(
        DomElement element,
        List<KeyframeEntry> keyframes, float progress, string timingFunction)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Collect all unique property names from keyframes.
        var allProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kf in keyframes)
            foreach (var prop in kf.Properties.Keys)
                allProps.Add(prop);

        foreach (var prop in allProps)
        {
            // Find the keyframes that define this property.
            var relevant = keyframes
                .Where(k => k.Properties.ContainsKey(prop))
                .ToList();

            if (relevant.Count == 0) continue;

            // Find the surrounding keyframes.
            KeyframeEntry? before = null;
            KeyframeEntry? after = null;

            for (int i = 0; i < relevant.Count; i++)
            {
                if (relevant[i].Position <= progress)
                    before = relevant[i];
                if (relevant[i].Position >= progress && after == null)
                    after = relevant[i];
            }

            if (before == null && after != null)
            {
                result[prop] = after.Properties[prop];
            }
            else if (before != null && after == null)
            {
                result[prop] = before.Properties[prop];
            }
            else if (before != null && after != null)
            {
                if (before == after || before.Position == after.Position)
                {
                    result[prop] = before.Properties[prop];
                }
                else
                {
                    // Compute local progress within this interval.
                    float intervalStart = before.Position;
                    float intervalEnd = after.Position;
                    float localProgress = (progress - intervalStart) / (intervalEnd - intervalStart);

                    // Apply per-interval timing function (steps, cubic-bezier, etc.).
                    localProgress = (float)ApplyTimingFunction(localProgress, timingFunction);

                    // Try color interpolation for background-color, color, etc.
                    var interpolated = TryInterpolateValue(
                        element, prop, before.Properties[prop], after.Properties[prop], localProgress);
                    result[prop] = interpolated;
                }
            }
        }

        return result;
    }

    private static readonly Regex StepsPattern = new(
        @"steps\(\s*(\d+)\s*(?:,\s*(start|end|jump-start|jump-end|jump-none|jump-both))?\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex CubicBezierPattern = new(
        @"cubic-bezier\(\s*([0-9.eE+-]+)\s*,\s*([0-9.eE+-]+)\s*,\s*([0-9.eE+-]+)\s*,\s*([0-9.eE+-]+)\s*\)",
        RegexOptions.Compiled);

    private static double ApplyTimingFunction(double progress, string timingFunction)
    {
        // Handle steps() timing functions.
        var stepsMatch = StepsPattern.Match(timingFunction);
        if (stepsMatch.Success)
        {
            int steps = int.Parse(stepsMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            string position = stepsMatch.Groups[2].Success ? stepsMatch.Groups[2].Value : "end";
            return ApplySteps(progress, steps, position);
        }

        if (timingFunction == "step-start")
            return ApplySteps(progress, 1, "start");
        if (timingFunction == "step-end")
            return ApplySteps(progress, 1, "end");

        // Handle cubic-bezier() timing functions.
        var bezierMatch = CubicBezierPattern.Match(timingFunction);
        if (bezierMatch.Success)
        {
            double x1 = double.Parse(bezierMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            double y1 = double.Parse(bezierMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            double x2 = double.Parse(bezierMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            double y2 = double.Parse(bezierMatch.Groups[4].Value, CultureInfo.InvariantCulture);
            return SolveCubicBezier(progress, x1, y1, x2, y2);
        }

        // Named easing keywords.
        return timingFunction switch
        {
            "ease" => SolveCubicBezier(progress, 0.25, 0.1, 0.25, 1.0),
            "ease-in" => SolveCubicBezier(progress, 0.42, 0.0, 1.0, 1.0),
            "ease-out" => SolveCubicBezier(progress, 0.0, 0.0, 0.58, 1.0),
            "ease-in-out" => SolveCubicBezier(progress, 0.42, 0.0, 0.58, 1.0),
            _ => progress, // linear
        };
    }

    /// <summary>
    /// Evaluates a cubic-bezier timing function: given an x-progress value
    /// in [0,1], finds the corresponding y-output by Newton-Raphson iteration
    /// on the x-coordinate polynomial.
    /// </summary>
    private static double SolveCubicBezier(double x, double x1, double y1, double x2, double y2)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;

        // Solve for t where B_x(t) = x using Newton-Raphson.
        double t = x; // initial guess
        for (int i = 0; i < 20; i++)
        {
            double bx = BezierCoord(t, x1, x2);
            double dx = bx - x;
            if (Math.Abs(dx) < 1e-7) break;
            double dbx = BezierDerivative(t, x1, x2);
            if (Math.Abs(dbx) < 1e-12) break;
            t -= dx / dbx;
            t = Math.Clamp(t, 0, 1);
        }

        return BezierCoord(t, y1, y2);
    }

    // B(t) = 3(1-t)^2 * t * p1 + 3(1-t) * t^2 * p2 + t^3
    private static double BezierCoord(double t, double p1, double p2)
    {
        double omt = 1 - t;
        return 3 * omt * omt * t * p1 + 3 * omt * t * t * p2 + t * t * t;
    }

    // B'(t) = 3(1-t)^2 * p1 + 6(1-t)*t*(p2-p1) + 3t^2*(1-p2)
    private static double BezierDerivative(double t, double p1, double p2)
    {
        double omt = 1 - t;
        return 3 * omt * omt * p1 + 6 * omt * t * (p2 - p1) + 3 * t * t * (1 - p2);
    }

    private static double ApplySteps(double progress, int steps, string position)
    {
        if (steps <= 0) steps = 1;

        // CSS steps() function: divides the animation into N equal intervals.
        double currentStep;
        switch (position)
        {
            case "start":
            case "jump-start":
                currentStep = Math.Ceiling(progress * steps);
                break;
            case "end":
            case "jump-end":
            default:
                currentStep = Math.Floor(progress * steps);
                break;
            case "jump-none":
                currentStep = Math.Floor(progress * steps);
                steps = Math.Max(steps - 1, 1);
                break;
            case "jump-both":
                currentStep = Math.Floor(progress * (steps + 1));
                steps = steps + 1;
                break;
        }

        return Math.Min(currentStep / steps, 1.0);
    }

    private static bool TryParseCssTime(string text, out double seconds)
    {
        seconds = 0;
        var lower = text.Trim().ToLowerInvariant();

        if (lower.EndsWith("ms"))
        {
            if (double.TryParse(lower.AsSpan(0, lower.Length - 2),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var ms))
            {
                seconds = ms / 1000.0;
                return true;
            }
        }
        else if (lower.EndsWith("s"))
        {
            if (double.TryParse(lower.AsSpan(0, lower.Length - 1),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
            {
                seconds = s;
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> TokenizeAnimationShorthand(string shorthand)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var depth = 0;

        foreach (var ch in shorthand.Trim())
        {
            if (char.IsWhiteSpace(ch) && depth == 0)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            if (ch == '(')
                depth++;
            else if (ch == ')' && depth > 0)
                depth--;

            current.Append(ch);
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }

    private static bool IsTimingFunction(string text) => text switch
    {
        "ease" or "linear" or "ease-in" or "ease-out" or "ease-in-out"
            or "step-start" or "step-end" => true,
        _ when text.StartsWith("steps(", StringComparison.OrdinalIgnoreCase) => true,
        _ when text.StartsWith("cubic-bezier(", StringComparison.OrdinalIgnoreCase) => true,
        _ => false,
    };

    private static bool IsKnownAnimationKeyword(string text) => text switch
    {
        "normal" or "reverse" or "alternate" or "alternate-reverse"
            or "none" or "forwards" or "backwards" or "both"
            or "running" or "paused" or "infinite" => true,
        _ => false,
    };

    // -----------------------------------------------------------------
    // Value interpolation
    // -----------------------------------------------------------------

    private static readonly Regex RgbPattern = new(
        @"rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RgbaPattern = new(
        @"rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*([0-9.]+)\s*)?\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Attempts to interpolate between two CSS values at the given progress.
    /// Supports color values (rgb, rgba, named colors) and numeric values.
    /// Falls back to discrete stepping for unsupported value types.
    /// </summary>
    private string TryInterpolateValue(DomElement element, string prop, string fromValue, string toValue, float progress)
    {
        // Try color interpolation for color-related properties.
        if (IsColorProperty(prop))
        {
            if (TryParseRgbColor(fromValue, out int fr, out int fg, out int fb, out double fa) &&
                TryParseRgbColor(toValue, out int tr, out int tg, out int tb, out double ta))
            {
                int r = (int)Math.Round(fr + (tr - fr) * progress);
                int g = (int)Math.Round(fg + (tg - fg) * progress);
                int b = (int)Math.Round(fb + (tb - fb) * progress);
                r = Math.Clamp(r, 0, 255);
                g = Math.Clamp(g, 0, 255);
                b = Math.Clamp(b, 0, 255);

                if (Math.Abs(fa - 1.0) < 0.001 && Math.Abs(ta - 1.0) < 0.001)
                    return $"rgb({r}, {g}, {b})";

                double a = fa + (ta - fa) * progress;
                return $"rgba({r}, {g}, {b}, {a.ToString("F2", CultureInfo.InvariantCulture)})";
            }
        }

        if (TryInterpolateLengthValue(element, prop, fromValue, toValue, progress, out var interpolatedLength))
            return interpolatedLength;

        // Fallback: discrete stepping for non-interpolatable values.
        return progress >= 1.0f ? toValue : fromValue;
    }

    private bool TryInterpolateLengthValue(
        DomElement element,
        string prop,
        string fromValue,
        string toValue,
        float progress,
        out string result)
    {
        result = string.Empty;
        if (!IsLengthInterpolableProperty(prop))
            return false;

        var percentageBasis = GetInterpolationPercentageBasis(element, prop);
        if (!TryEvaluateCssLengthWithViewport(fromValue, element, forLineHeight: false, percentageBasis, out var fromPx) ||
            !TryEvaluateCssLengthWithViewport(toValue, element, forLineHeight: false, percentageBasis, out var toPx))
        {
            return false;
        }

        var interpolated = fromPx + ((toPx - fromPx) * progress);
        result = interpolated.ToString("0.###", CultureInfo.InvariantCulture) + "px";
        return true;
    }

    private double? GetInterpolationPercentageBasis(DomElement element, string prop)
    {
        return prop switch
        {
            "width" or "min-width" or "max-width" or "left" or "right" => ResolveContainingBlockReferenceLength(element, vertical: false),
            "height" or "min-height" or "max-height" or "top" or "bottom" => ResolveContainingBlockReferenceLength(element, vertical: true),
            "margin-left" or "margin-right" or "margin-top" or "margin-bottom" or
            "padding-left" or "padding-right" or "padding-top" or "padding-bottom" =>
                ResolveContainingBlockReferenceLength(element, vertical: false),
            _ => null,
        };
    }

    private static bool IsLengthInterpolableProperty(string prop) => prop switch
    {
        "width" or "height" or "min-width" or "min-height" or "max-width" or "max-height" or
        "top" or "right" or "bottom" or "left" or
        "margin-left" or "margin-right" or "margin-top" or "margin-bottom" or
        "padding-left" or "padding-right" or "padding-top" or "padding-bottom" or
        "font-size" => true,
        _ => false,
    };

    private static bool IsColorProperty(string prop) => prop switch
    {
        "background-color" or "color" or "border-color"
            or "border-top-color" or "border-right-color"
            or "border-bottom-color" or "border-left-color"
            or "outline-color" or "text-decoration-color"
            or "fill" or "stroke" => true,
        _ => false,
    };

    private static bool TryParseRgbColor(string value, out int r, out int g, out int b, out double a)
    {
        r = g = b = 0;
        a = 1.0;

        var m = RgbaPattern.Match(value);
        if (m.Success)
        {
            r = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            g = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            b = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (m.Groups[4].Success)
                a = double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
            return true;
        }

        // Try named colors
        return TryParseNamedColor(value, out r, out g, out b);
    }

    private static bool TryParseNamedColor(string name, out int r, out int g, out int b)
    {
        r = g = b = 0;
        switch (name.Trim().ToLowerInvariant())
        {
            case "red": r = 255; return true;
            case "green": g = 128; return true;
            case "blue": b = 255; return true;
            case "white": r = g = b = 255; return true;
            case "black": return true;
            case "yellow": r = 255; g = 255; return true;
            case "cyan" or "aqua": g = 255; b = 255; return true;
            case "magenta" or "fuchsia": r = 255; b = 255; return true;
            case "lime": g = 255; return true;
            case "orange": r = 255; g = 165; return true;
            case "transparent": return true;
            default: return false;
        }
    }
}
