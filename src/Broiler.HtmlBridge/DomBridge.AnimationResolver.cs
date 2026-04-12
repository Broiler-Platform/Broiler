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

    private static readonly Regex KeyframesRulePattern = new(
        @"@keyframes\s+(?<name>[^\s{]+)\s*\{(?<body>(?:[^{}]|\{[^{}]*\})*)\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex KeyframeBlockPattern = new(
        @"(?<selectors>[^{]+)\{(?<declarations>[^}]*)\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static void CollectKeyframes(DomElement root, Dictionary<string, List<KeyframeEntry>> map)
    {
        if (string.Equals(root.TagName, "style", StringComparison.OrdinalIgnoreCase))
        {
            var css = root.TextContent ?? string.Empty;
            foreach (Match m in KeyframesRulePattern.Matches(css))
            {
                var name = m.Groups["name"].Value.Trim().Trim('"', '\'');
                var body = m.Groups["body"].Value;
                var entries = ParseKeyframeEntries(body);
                if (entries.Count > 0)
                    map[name] = entries;
            }
        }

        foreach (var child in root.Children)
            CollectKeyframes(child, map);
    }

    private static List<KeyframeEntry> ParseKeyframeEntries(string body)
    {
        var entries = new List<KeyframeEntry>();

        foreach (Match m in KeyframeBlockPattern.Matches(body))
        {
            var selectorText = m.Groups["selectors"].Value.Trim();
            var declarations = ParseDeclarations(m.Groups["declarations"].Value);

            foreach (var sel in selectorText.Split(','))
            {
                var s = sel.Trim().ToLowerInvariant();
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
        foreach (var decl in text.Split(';'))
        {
            var colon = decl.IndexOf(':');
            if (colon <= 0) continue;
            var prop = decl.Substring(0, colon).Trim();
            var val = decl.Substring(colon + 1).Trim();
            if (!string.IsNullOrEmpty(prop))
                result[prop] = val;
        }
        return result;
    }

    // -----------------------------------------------------------------
    // Animation resolution
    // -----------------------------------------------------------------

    private static void ResolveAnimationsOnTree(
        DomElement element,
        Dictionary<string, List<KeyframeEntry>> keyframesMap)
    {
        // Check if this element has animation properties set.
        if (element.Style.Count > 0)
        {
            var hasAnimation = element.Style.TryGetValue("animation", out var animValue);
            var hasDelay = element.Style.TryGetValue("animation-delay", out var delayValue);
            var hasName = element.Style.TryGetValue("animation-name", out var nameValue);

            if (hasAnimation || hasName)
            {
                TryResolveAnimation(element, keyframesMap,
                    animValue, delayValue, nameValue);
            }
        }

        foreach (var child in element.Children)
            ResolveAnimationsOnTree(child, keyframesMap);
    }

    private static void TryResolveAnimation(
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

        if (!string.IsNullOrWhiteSpace(animationShorthand))
        {
            var parts = animationShorthand!.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var durations = new List<double>();

            foreach (var part in parts)
            {
                if (TryParseCssTime(part, out var sec))
                    durations.Add(sec);
                else if (IsTimingFunction(part))
                    timingFunction = part;
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

        // Only resolve for negative delays (animation already running at t=0).
        if (delaySec >= 0)
            return;

        // Compute progress: elapsed = abs(delay), progress = elapsed / duration.
        double elapsed = Math.Abs(delaySec);
        double rawProgress = elapsed / durationSec;

        // Clamp progress to [0, 1] for a single iteration.
        rawProgress = Math.Min(rawProgress, 1.0);

        // Apply timing function to get the effective progress.
        double progress = ApplyTimingFunction(rawProgress, timingFunction);

        // Find the two surrounding keyframes and interpolate.
        var resolvedProps = ResolveKeyframeProperties(keyframes, (float)progress, timingFunction);

        // Apply resolved values as inline styles and remove animation properties.
        foreach (var kv in resolvedProps)
            element.Style[kv.Key] = kv.Value;

        element.Style.Remove("animation");
        element.Style.Remove("animation-delay");
        element.Style.Remove("animation-name");
        element.Style.Remove("animation-duration");
        element.Style.Remove("animation-timing-function");
    }

    private static Dictionary<string, string> ResolveKeyframeProperties(
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

                    // Apply per-interval timing function (steps, etc.).
                    localProgress = (float)ApplyTimingFunction(localProgress, timingFunction);

                    // For non-numeric values (colors, keywords), use discrete stepping.
                    result[prop] = localProgress >= 1.0f
                        ? after.Properties[prop]
                        : before.Properties[prop];
                }
            }
        }

        return result;
    }

    private static double ApplyTimingFunction(double progress, string timingFunction)
    {
        // Handle steps() timing functions.
        var stepsMatch = Regex.Match(timingFunction, @"steps\(\s*(\d+)\s*(?:,\s*(start|end|jump-start|jump-end|jump-none|jump-both))?\s*\)");
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

        // For linear/ease/etc., use the raw progress (close enough for static snapshots).
        return progress;
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
}
