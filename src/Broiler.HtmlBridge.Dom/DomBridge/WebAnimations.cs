using System.Globalization;
using System.Text;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.Runtime;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// Web Animations API — <c>element.animate(keyframes, options)</c>. The render is a single
/// snapshot, so this evaluates the animation's effect at the snapshot time (the animation has
/// just started; a negative <c>delay</c> pre-advances it — the pattern WPT interpolation tests
/// and <c>interpolation-testcommon.js</c> use to sample a mid-point instantly) and bakes the
/// interpolated property values into the element's render style. Previously
/// <c>element.animate</c> was a no-op stub, so animation-driven property values never rendered
/// (a scaled/faded/animated element drew at its base value).
/// </summary>
public sealed partial class DomBridge
{
    /// <summary>Web-Animations keyframe keys (camelCase) mapped to their CSS property names,
    /// probed on each keyframe object (the engine's own-key enumeration is internal, and this
    /// covers the animatable properties the interpolator supports).</summary>
    private static readonly (string Keyframe, string Css)[] AnimatableProperties =
    [
        ("transform", "transform"),
        ("opacity", "opacity"),
        ("color", "color"),
        ("background", "background"),
        ("backgroundColor", "background-color"),
        ("borderColor", "border-color"),
        ("outlineColor", "outline-color"),
        ("width", "width"),
        ("height", "height"),
        ("left", "left"),
        ("top", "top"),
        ("right", "right"),
        ("bottom", "bottom"),
        ("marginTop", "margin-top"),
        ("marginRight", "margin-right"),
        ("marginBottom", "margin-bottom"),
        ("marginLeft", "margin-left"),
        ("paddingTop", "padding-top"),
        ("paddingRight", "padding-right"),
        ("paddingBottom", "padding-bottom"),
        ("paddingLeft", "padding-left"),
        ("borderRadius", "border-radius"),
        ("fontSize", "font-size"),
        ("lineHeight", "line-height"),
        ("letterSpacing", "letter-spacing"),
        ("filter", "filter"),
    ];

    /// <summary>
    /// <c>element.animate(keyframes, options)</c> — bakes the animation's snapshot-time value and
    /// returns a minimal Animation object. Never throws into the caller: a malformed keyframe or
    /// option leaves the element unbaked rather than aborting the script.
    /// </summary>
    private JSValue ElementAnimate(DomElement element, in Arguments a)
    {
        try
        {
            var keyframes = ParseAnimationKeyframes(a.Length > 0 ? a[0] : JSUndefined.Value);
            var timing = ParseAnimationTiming(a.Length > 1 ? a[1] : JSUndefined.Value);
            if (keyframes.Count >= 1 && timing.DurationMs > 0 &&
                TryComputeSnapshotProgress(timing, out var progress))
            {
                var resolved = ResolveKeyframeProperties(element, keyframes, progress, timing.Easing);
                foreach (var kv in resolved)
                    BakedInlineStyle(element)[kv.Key] = kv.Value;
                InvalidateStyleScope(element);
            }
        }
        catch
        {
            // Web Animations must not break the page: a bad animate() call is inert.
        }

        return BuildAnimationObject(element);
    }

    private List<KeyframeEntry> ParseAnimationKeyframes(JSValue keyframesValue)
    {
        var entries = new List<KeyframeEntry>();
        if (keyframesValue is not JSArray array)
            return entries;

        var items = array.GetArrayElements(withHoles: false).Select(t => t.value).ToList();
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is not JSObject keyframe)
                continue;

            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (keyframeKey, cssName) in AnimatableProperties)
            {
                var value = keyframe[(KeyString)keyframeKey];
                if (value is null || value.IsNullOrUndefined)
                    continue;
                var text = value.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    properties[cssName] = text;
            }

            if (properties.Count == 0)
                continue;

            // Explicit offset, else distribute evenly across the keyframe list (spec default).
            float position;
            var offset = keyframe[(KeyString)"offset"];
            if (offset is not null && offset.IsNumber)
                position = (float)offset.DoubleValue;
            else
                position = items.Count <= 1 ? 0f : (float)i / (items.Count - 1);

            entries.Add(new KeyframeEntry(position, properties));
        }

        entries.Sort((x, y) => x.Position.CompareTo(y.Position));
        return entries;
    }

    private readonly record struct AnimationTiming(
        double DurationMs, double DelayMs, string Easing, string Fill,
        double Iterations, double IterationStart);

    private static AnimationTiming ParseAnimationTiming(JSValue optionsValue)
    {
        double duration = 0, delay = 0, iterations = 1, iterationStart = 0;
        var easing = "linear";
        var fill = "none";

        if (optionsValue.IsNumber)
        {
            duration = optionsValue.DoubleValue;
        }
        else if (optionsValue is JSObject options)
        {
            if (options[(KeyString)"duration"] is { IsNumber: true } d)
                duration = d.DoubleValue;
            if (options[(KeyString)"delay"] is { IsNumber: true } dl)
                delay = dl.DoubleValue;
            if (options[(KeyString)"iterations"] is { IsNumber: true } it)
                iterations = it.DoubleValue;
            if (options[(KeyString)"iterationStart"] is { IsNumber: true } iterStart)
                iterationStart = iterStart.DoubleValue;
            if (options[(KeyString)"easing"] is { } e && !e.IsNullOrUndefined)
                easing = e.ToString();
            if (options[(KeyString)"fill"] is { } f && !f.IsNullOrUndefined)
                fill = f.ToString();
        }

        return new AnimationTiming(duration, delay, easing, fill, iterations, iterationStart);
    }

    /// <summary>
    /// Iteration progress at the snapshot. The animation has just started (current time 0), so
    /// active time is <c>-delay</c>; a negative delay lands it mid-animation. Returns false when
    /// the animation has no effect at the snapshot (before/after its active phase and not filled).
    /// </summary>
    private static bool TryComputeSnapshotProgress(AnimationTiming timing, out float progress)
    {
        progress = 0f;
        var iterations = timing.Iterations <= 0 ? 1 : timing.Iterations;
        var activeDuration = timing.DurationMs * iterations;
        var activeTime = 0.0 - timing.DelayMs;

        var fillsBackwards = timing.Fill is "backwards" or "both";
        var fillsForwards = timing.Fill is "forwards" or "both";

        if (activeTime < 0)
        {
            if (!fillsBackwards)
                return false;
            activeTime = 0;
        }
        else if (activeTime >= activeDuration)
        {
            if (!fillsForwards)
                return false;
            activeTime = activeDuration;
        }

        var overallProgress = (timing.DurationMs > 0 ? activeTime / timing.DurationMs : 0) + timing.IterationStart;
        // Iteration progress in [0,1]; a whole-number boundary at the end of the active phase
        // resolves to 1 (the animation's final value), not 0.
        var iterationProgress = overallProgress - Math.Floor(overallProgress);
        if (iterationProgress == 0 && overallProgress > 0)
            iterationProgress = 1;

        progress = (float)Math.Clamp(iterationProgress, 0.0, 1.0);
        return true;
    }

    // ------------------------------------------------------------------
    //  Transform interpolation (component-wise between matching lists)
    // ------------------------------------------------------------------

    private sealed record TransformFunction(string Name, List<string> Args);

    private static bool TryInterpolateTransform(string fromValue, string toValue, float progress, out string result)
    {
        result = string.Empty;
        var from = ParseTransformFunctions(fromValue);
        var to = ParseTransformFunctions(toValue);
        if (from is null || to is null)
            return false;

        if (from.Count == 0 && to.Count == 0)
        {
            result = "none";
            return true;
        }

        // `none` interpolates as the identity of the other side's function list.
        if (from.Count == 0)
            from = to.Select(IdentityTransform).ToList();
        else if (to.Count == 0)
            to = from.Select(IdentityTransform).ToList();

        if (from.Count != to.Count)
            return false;

        var builder = new StringBuilder();
        for (var i = 0; i < from.Count; i++)
        {
            if (!string.Equals(from[i].Name, to[i].Name, StringComparison.OrdinalIgnoreCase) ||
                from[i].Args.Count != to[i].Args.Count)
                return false;

            var args = new List<string>(from[i].Args.Count);
            for (var j = 0; j < from[i].Args.Count; j++)
            {
                if (!TryInterpolateNumericToken(from[i].Args[j], to[i].Args[j], progress, out var arg))
                    return false;
                args.Add(arg);
            }

            if (builder.Length > 0)
                builder.Append(' ');
            builder.Append(from[i].Name).Append('(').Append(string.Join(", ", args)).Append(')');
        }

        result = builder.ToString();
        return true;
    }

    private static List<TransformFunction>? ParseTransformFunctions(string value)
    {
        value = value?.Trim() ?? string.Empty;
        var functions = new List<TransformFunction>();
        if (value.Length == 0 || value.Equals("none", StringComparison.OrdinalIgnoreCase))
            return functions;

        var i = 0;
        var n = value.Length;
        while (i < n)
        {
            while (i < n && (char.IsWhiteSpace(value[i]) || value[i] == ','))
                i++;
            if (i >= n)
                break;

            var nameStart = i;
            while (i < n && value[i] != '(')
                i++;
            if (i >= n)
                return null;
            var name = value[nameStart..i].Trim();
            i++; // '('

            var argStart = i;
            while (i < n && value[i] != ')')
                i++;
            if (i >= n || name.Length == 0)
                return null;
            var args = value[argStart..i]
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            i++; // ')'

            functions.Add(new TransformFunction(name, args));
        }

        return functions;
    }

    private static TransformFunction IdentityTransform(TransformFunction function)
    {
        var lower = function.Name.ToLowerInvariant();
        string identity =
            lower.StartsWith("scale", StringComparison.Ordinal) ? "1"
            : lower.StartsWith("rotate", StringComparison.Ordinal) || lower.StartsWith("skew", StringComparison.Ordinal) ? "0deg"
            : "0";
        return new TransformFunction(function.Name, function.Args.Select(_ => identity).ToList());
    }

    private static bool TryInterpolateNumericToken(string from, string to, float progress, out string result)
    {
        result = string.Empty;
        if (!TrySplitNumberUnit(from, out var fromNumber, out var fromUnit) ||
            !TrySplitNumberUnit(to, out var toNumber, out var toUnit))
            return false;

        string unit;
        if (string.Equals(fromUnit, toUnit, StringComparison.OrdinalIgnoreCase))
            unit = fromUnit;
        else if (fromNumber == 0 && fromUnit.Length == 0)
            unit = toUnit;
        else if (toNumber == 0 && toUnit.Length == 0)
            unit = fromUnit;
        else
            return false; // incompatible units (e.g. px vs %); no conversion here

        var value = fromNumber + (toNumber - fromNumber) * progress;
        result = value.ToString("0.#####", CultureInfo.InvariantCulture) + unit;
        return true;
    }

    private static bool TrySplitNumberUnit(string token, out double number, out string unit)
    {
        number = 0;
        unit = string.Empty;
        token = token.Trim();
        if (token.Length == 0)
            return false;

        var i = 0;
        if (token[i] is '+' or '-')
            i++;
        var digitsStart = i;
        while (i < token.Length && (char.IsDigit(token[i]) || token[i] == '.'))
            i++;
        if (i == digitsStart)
            return false;

        if (!double.TryParse(token[..i], NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            return false;

        unit = token[i..].Trim();
        return true;
    }
}
