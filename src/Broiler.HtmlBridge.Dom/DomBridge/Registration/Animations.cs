using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Json;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private JSArray BuildAnimationList(DomElement? target)
    {
        var animations = new List<JSValue>();
        foreach (var element in Elements)
        {
            if (element.IsTextNode || string.Equals(element.TagName, "#comment", StringComparison.OrdinalIgnoreCase))
                continue;
            if (target != null && !ReferenceEquals(element, target))
                continue;

            if (!TryGetAnimationProperties(element, out var animationShorthand, out var animationDelay))
                continue;

            EnsureAnimationCurrentTime(element, animationShorthand, animationDelay);
            animations.Add(BuildAnimationObject(element));
        }

        return new JSArray(animations);
    }

    private bool TryGetAnimationProperties(
        DomElement element,
        out string? animationShorthand,
        out string? animationDelay)
    {
        animationShorthand = null;
        animationDelay = null;

        if (element.Style.TryGetValue("animation", out animationShorthand))
        {
            element.Style.TryGetValue("animation-delay", out animationDelay);
            return true;
        }

        var stylesheetProps = CollectStylesheetAnimationProperties(element);
        if (stylesheetProps == null)
            return false;

        var hasAnimation = stylesheetProps.TryGetValue("animation", out animationShorthand);
        stylesheetProps.TryGetValue("animation-delay", out animationDelay);
        return hasAnimation || stylesheetProps.ContainsKey("animation-name");
    }

    private void EnsureAnimationCurrentTime(
        DomElement element,
        string? animationShorthand,
        string? animationDelay)
    {
        if (GetElementRuntimeState(element).Animation.CurrentTimeMilliseconds.IsSet)
            return;

        double delaySec = 0;
        if (!string.IsNullOrWhiteSpace(animationDelay) &&
            TryParseCssTime(animationDelay, out var delayOverride))
        {
            delaySec = delayOverride;
        }
        else if (!string.IsNullOrWhiteSpace(animationShorthand))
        {
            var durations = new List<double>();
            foreach (var part in TokenizeAnimationShorthand(animationShorthand))
            {
                if (TryParseCssTime(part, out var seconds))
                    durations.Add(seconds);
            }

            if (durations.Count >= 2)
                delaySec = durations[1];
        }

        var currentTimeMs = delaySec > 0 ? (delaySec * 1000.0) + 1.0 : Math.Abs(delaySec) * 1000.0;
        GetElementRuntimeState(element).Animation.CurrentTimeMilliseconds.Set(currentTimeMs);
    }

    private static JSObject BuildAnimationObject(DomElement element)
    {
        var animation = new JSObject();
        animation.FastAddProperty(
            (KeyString)"currentTime",
            new JSFunction((in Arguments _) => JsRegistrationGetCurrentTime152Core(element, in _), "get currentTime"),
            new JSFunction((in Arguments a) => JsRegistrationSetCurrentTime153Core(element, in a), "set currentTime"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        var ready = new JSObject();
        ready.FastAddValue(
            (KeyString)"then",
            new JSFunction((in Arguments a) => JsRegistrationThen154Core(ready, in a), "then", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        ready.FastAddValue(
            (KeyString)"catch",
            new JSFunction((in Arguments _) => ready, "catch", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        animation.FastAddValue((KeyString)"ready", ready, JSPropertyAttributes.EnumerableConfigurableValue);
        return animation;
    }

}
