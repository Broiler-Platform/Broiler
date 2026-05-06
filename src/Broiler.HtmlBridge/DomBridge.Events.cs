using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Array;
using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

/// <summary>
/// DOM event dispatch — capture → target → bubble propagation and
/// element validity checks used during form-submission events.
/// </summary>
public sealed partial class DomBridge
{
    private static EventListenerRegistration CreateEventListenerRegistration(JSValue listener, JSValue options)
    {
        if (options is JSObject optionsObject)
        {
            return new EventListenerRegistration(
                listener,
                GetBooleanOption(optionsObject, "capture"),
                GetBooleanOption(optionsObject, "once"),
                GetBooleanOption(optionsObject, "passive"));
        }

        return new EventListenerRegistration(listener, options.BooleanValue);
    }

    private static bool GetCaptureForRemoval(JSValue options)
        => options is JSObject optionsObject ? GetBooleanOption(optionsObject, "capture") : options.BooleanValue;

    private static bool HasMatchingEventListener(
        List<EventListenerRegistration> listeners,
        EventListenerRegistration candidate)
        => listeners.Any(existing =>
            existing.Listener == candidate.Listener &&
            existing.Capture == candidate.Capture);

    private static bool GetBooleanOption(JSObject options, string name)
    {
        var value = options[(KeyString)name];
        return value != null && !value.IsNullOrUndefined && value.BooleanValue;
    }

    private static void InvokeEventListener(JSValue listener, JSObject evt, string logContext)
    {
        try
        {
            if (listener is JSFunction fn)
            {
                fn.InvokeFunction(new Arguments(fn, evt));
                return;
            }

            if (listener is JSObject listenerObject &&
                listenerObject[(KeyString)"handleEvent"] is JSFunction handleEvent)
            {
                handleEvent.InvokeFunction(new Arguments(listenerObject, evt));
            }
        }
        catch (Exception ex)
        {
            RenderLogger.LogWarning(LogCategory.JavaScript, logContext, $"Event listener error: {ex.Message}", ex);
        }
    }

    private JSValue BuildComposedPathValue(DomElement target, IReadOnlyList<DomElement> path)
    {
        JSValue ToEventPathObject(DomElement node)
            => node == _documentNode ? (_documentJSObject ?? JSNull.Value) : ToJSObject(node);

        var values = new List<JSValue> { ToEventPathObject(target) };

        for (int i = path.Count - 1; i >= 0; i--)
            values.Add(ToEventPathObject(path[i]));

        if (_windowJSObject != null)
            values.Add(_windowJSObject);

        return new JSArray(values.ToArray());
    }

    private static bool CheckElementValidity(DomElement element)
    {
        if (string.Equals(element.TagName, "form", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateFormChildren(element);
        }

        // Individual element validation
        if (!element.Attributes.ContainsKey("required")) return true;

        var tag = element.TagName;
        if (string.Equals(tag, "input", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "textarea", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "select", StringComparison.OrdinalIgnoreCase))
        {
            element.Attributes.TryGetValue("value", out var val);
            return !string.IsNullOrEmpty(val);
        }
        return true;
    }

    private static bool ValidateFormChildren(DomElement form)
    {
        foreach (var child in form.Children)
        {
            if (!child.IsTextNode && !CheckElementValidity(child)) return false;
            if (!ValidateFormChildren(child)) return false;
        }
        return true;
    }

    /// <summary>
    /// Dispatches a DOM event on the given element with full capture → target → bubble
    /// propagation (DOM Events Level 3).
    /// </summary>
    private JSValue DispatchEventOnElement(DomElement target, JSObject evt)
    {
        var typeVal = evt[(KeyString)"type"];
        var eventType = typeVal != null && typeVal is JSString ? typeVal.ToString() : "unknown";

        // Build the path from the root to the target
        var path = new List<DomElement>();
        var visited = new HashSet<DomElement>();
        var node = target.Parent;
        while (node != null && visited.Add(node)) { path.Add(node); node = node.Parent; }
        path.Reverse();

        // Include the document node at the very beginning of the path
        // (first for capture, last for bubble) unless the target IS the document node.
        if (target != _documentNode && !path.Contains(_documentNode))
            path.Insert(0, _documentNode);

        var stopped = false;
        var immediateStopped = false;
        var prevented = evt[(KeyString)"defaultPrevented"] is JSValue defaultPreventedValue &&
                        defaultPreventedValue.BooleanValue;
        var currentListenerPassive = false;
        var legacyCancelBubble = false;

        // Set up event object properties
        evt[(KeyString)"target"] = target == _documentNode
            ? (_documentJSObject ?? JSNull.Value)
            : ToJSObject(target);
        evt[(KeyString)"srcElement"] = evt[(KeyString)"target"];
        evt[(KeyString)"eventPhase"] = new JSNumber(0);
        evt.FastAddValue((KeyString)"stopPropagation",
            new JSFunction((in Arguments _) =>
            {
                stopped = true;
                legacyCancelBubble = true;
                return JSUndefined.Value;
            }, "stopPropagation", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"stopImmediatePropagation",
            new JSFunction((in Arguments _) =>
            {
                stopped = true;
                immediateStopped = true;
                legacyCancelBubble = true;
                return JSUndefined.Value;
            }, "stopImmediatePropagation", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"preventDefault",
            new JSFunction((in Arguments _) =>
            {
                var cancelable = evt[(KeyString)"cancelable"];
                if (!currentListenerPassive && cancelable != null && cancelable.BooleanValue)
                {
                    prevented = true;
                    evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
                }
                return JSUndefined.Value;
            }, "preventDefault", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddProperty(
            (KeyString)"cancelBubble",
            new JSFunction((in Arguments _) => legacyCancelBubble ? JSBoolean.True : JSBoolean.False, "get cancelBubble"),
            new JSFunction((in Arguments setArgs) =>
            {
                if (setArgs.Length > 0 && setArgs[0].BooleanValue)
                {
                    legacyCancelBubble = true;
                    stopped = true;
                }
                return JSUndefined.Value;
            }, "set cancelBubble"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
        evt.FastAddProperty(
            (KeyString)"returnValue",
            new JSFunction((in Arguments _) => prevented ? JSBoolean.False : JSBoolean.True, "get returnValue"),
            new JSFunction((in Arguments setArgs) =>
            {
                var cancelable = evt[(KeyString)"cancelable"];
                if (setArgs.Length > 0 &&
                    !setArgs[0].BooleanValue &&
                    !currentListenerPassive &&
                    cancelable != null &&
                    cancelable.BooleanValue)
                {
                    prevented = true;
                    evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
                }
                return JSUndefined.Value;
            }, "set returnValue"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
        evt.FastAddValue((KeyString)"composedPath",
            new JSFunction((in Arguments _) => BuildComposedPathValue(target, path), "composedPath", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // Phase 1: Capture (root → parent of target)
        evt[(KeyString)"eventPhase"] = new JSNumber(1);
        foreach (var ancestor in path)
        {
            if (stopped) break;
            evt[(KeyString)"currentTarget"] = ancestor == _documentNode
                ? (_documentJSObject ?? JSNull.Value)
                : ToJSObject(ancestor);
            FireListeners(ancestor, eventType, evt, capturePhase: true, ref stopped, ref immediateStopped, ref currentListenerPassive);
        }

        // Phase 2: Target — fire capture listeners first, then non-capture listeners.
        if (!stopped)
        {
            evt[(KeyString)"eventPhase"] = new JSNumber(2);
            evt[(KeyString)"currentTarget"] = target == _documentNode
                ? (_documentJSObject ?? JSNull.Value)
                : ToJSObject(target);
            FireListeners(target, eventType, evt, capturePhase: true, ref stopped, ref immediateStopped, ref currentListenerPassive);
            FireListeners(target, eventType, evt, capturePhase: false, ref stopped, ref immediateStopped, ref currentListenerPassive);
        }

        // Phase 3: Bubble (parent of target → root) — only if event.bubbles is true
        var bubblesVal = evt[(KeyString)"bubbles"];
        var eventBubbles = bubblesVal != null && bubblesVal.BooleanValue;
        if (!stopped && eventBubbles)
        {
            evt[(KeyString)"eventPhase"] = new JSNumber(3);
            for (int i = path.Count - 1; i >= 0; i--)
            {
                if (stopped) break;
                evt[(KeyString)"currentTarget"] = path[i] == _documentNode
                    ? (_documentJSObject ?? JSNull.Value)
                    : ToJSObject(path[i]);
                FireListeners(path[i], eventType, evt, capturePhase: false, ref stopped, ref immediateStopped, ref currentListenerPassive);
            }
        }

        evt[(KeyString)"currentTarget"] = JSNull.Value;
        evt[(KeyString)"eventPhase"] = new JSNumber(0);
        return prevented ? JSBoolean.False : JSBoolean.True;
    }

    /// <summary>
    /// Fires registered listeners for the given event type on a single element.
    /// When <paramref name="capturePhase"/> is <c>true</c>, only capture listeners fire.
    /// When <c>false</c>, only bubble listeners fire.
    /// When <c>null</c> (unused), all listeners fire in registration order plus the inline handler.
    /// </summary>
    private static void FireListeners(DomElement el, string eventType, JSObject evt,
        bool? capturePhase, ref bool stopped, ref bool immediateStopped, ref bool currentListenerPassive)
    {
        if (el.EventListeners.TryGetValue(eventType, out var listeners))
        {
            foreach (var registration in listeners.ToList())
            {
                if (immediateStopped) break;
                // In capture/bubble phases, only fire matching listeners.
                // In target phase (capturePhase == null), fire all listeners.
                if (capturePhase.HasValue && registration.Capture != capturePhase.Value) continue;
                currentListenerPassive = registration.Passive;
                InvokeEventListener(registration.Listener, evt, "DomBridge.dispatchEvent");
                currentListenerPassive = false;

                if (registration.Once)
                    listeners.Remove(registration);
            }
        }

        // Fire inline event handler (on* property) — fires after addEventListener listeners on the target,
        // and during bubble phase on ancestors (like a bubble listener).
        if (!immediateStopped && (capturePhase == null || capturePhase == false))
        {
            if (el.InlineEventHandlers.TryGetValue(eventType, out var inlineHandler) && inlineHandler is JSFunction inlineFn)
            {
                // Inline on* handlers behave like regular non-passive listeners.
                currentListenerPassive = false;
                try { inlineFn.InvokeFunction(new Arguments(inlineFn, evt)); }
                catch (Exception ex) { RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.dispatchEvent", $"Inline handler error: {ex.Message}", ex); }
            }
        }
    }

    /// <summary>
    /// Compiles all <c>on*</c> HTML attributes (e.g. <c>onclick="code"</c>) on the given
    /// element into <see cref="JSFunction"/> instances stored in <see cref="DomElement.InlineEventHandlers"/>.
    /// Only compiles attributes that have not already been compiled.
    /// </summary>
    private void CompileInlineEventAttributes(DomElement element)
    {
        foreach (var eventName in InlineEventNames)
        {
            var attrName = $"on{eventName}";
            if (element.Attributes.TryGetValue(attrName, out var code) &&
                !string.IsNullOrEmpty(code) &&
                !element.InlineEventHandlers.ContainsKey(eventName))
            {
                CompileInlineEventAttribute(element, attrName, code);
            }
        }
    }

    /// <summary>
    /// Compiles a single <c>on*</c> attribute value into a <see cref="JSFunction"/>
    /// and stores it in <see cref="DomElement.InlineEventHandlers"/>.
    /// </summary>
    internal void CompileInlineEventAttribute(DomElement element, string attrName, string code)
    {
        if (_jsContext == null || string.IsNullOrEmpty(code) || attrName.Length <= 2) return;
        var eventName = attrName[2..].ToLowerInvariant();
        if (Csp != null && !Csp.AllowsInlineEventHandler(code))
        {
            element.InlineEventHandlers.Remove(eventName);
            return;
        }

        try
        {
            var fn = _jsContext.Eval($"(function(event) {{ {code} }})") as JSFunction;
            if (fn != null)
                element.InlineEventHandlers[eventName] = fn;
        }
        catch (Exception ex)
        {
            RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.CompileInlineEventAttribute",
                $"Failed to compile on{eventName} handler: {ex.Message}", ex);
        }
    }
}
