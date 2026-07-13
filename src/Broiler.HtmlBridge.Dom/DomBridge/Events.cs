using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HtmlBridge.Logging;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.Dom;

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
        // Callers pass the registrations already scoped to a single event type,
        // so the DOM duplicate-registration check only needs listener/capture.
        => listeners.Any(existing =>
            existing.Listener == candidate.Listener &&
            existing.Capture == candidate.Capture);

    private static bool GetBooleanOption(JSObject options, string name)
    {
        var value = options[(KeyString)name];
        return value != null && !value.IsNullOrUndefined && value.BooleanValue;
    }

    internal static void InvokeEventListener(JSValue listener, JSObject evt, string logContext)
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

    private static bool CheckElementValidity(DomElement element)
    {
        if (string.Equals(element.TagName, "form", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateFormChildren(element);
        }

        // Individual element validation
        if (!HasAttr(element, "required")) return true;

        var tag = element.TagName;
        if (string.Equals(tag, "input", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "textarea", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "select", StringComparison.OrdinalIgnoreCase))
        {
            TryGetAttribute(element, "value", out var val);
            return !string.IsNullOrEmpty(val);
        }
        return true;
    }

    private static bool ValidateFormChildren(DomElement form)
    {
        foreach (var child in ChildElements(form))
        {
            if (!IsText(child) && !CheckElementValidity(child)) return false;
            if (!ValidateFormChildren(child)) return false;
        }
        return true;
    }

    /// <summary>
    /// Dispatches a DOM event on the given element with full capture → target → bubble propagation.
    /// The engine lives in the Phase 3 EventDispatchBinding feature module; this thin delegator keeps
    /// the historical call sites (element/document dispatchEvent, form submit, XHR/layout-driven
    /// synthetic events) source-compatible.
    /// </summary>
    private JSValue DispatchEventOnElement(DomNode target, JSObject evt) =>
        _eventDispatch.DispatchEventOnElement(target, evt);

    /// <summary>
    /// Compiles all <c>on*</c> HTML attributes (e.g. <c>onclick="code"</c>) on the given
    /// element into <see cref="JSFunction"/> instances stored in <see cref="bridge-owned inline event handler state"/>.
    /// Only compiles attributes that have not already been compiled.
    /// </summary>
    private void CompileInlineEventAttributes(DomElement element)
    {
        foreach (var eventName in InlineEventNames)
        {
            var attrName = $"on{eventName}";
            if (TryGetAttribute(element, attrName, out var code) &&
                !string.IsNullOrEmpty(code) &&
                !GetInlineEventHandlers(element).ContainsKey(eventName))
            {
                CompileInlineEventAttribute(element, attrName, code);
            }
        }
    }

    /// <summary>
    /// Compiles a single <c>on*</c> attribute value into a <see cref="JSFunction"/>
    /// and stores it in <see cref="bridge-owned inline event handler state"/>.
    /// </summary>
    internal void CompileInlineEventAttribute(DomElement element, string attrName, string code)
    {
        if (_jsContext == null || string.IsNullOrEmpty(code) || attrName.Length <= 2) return;
        var eventName = attrName[2..].ToLowerInvariant();
        if (Csp != null && !Csp.AllowsInlineEventHandler(code))
        {
            GetInlineEventHandlers(element).Remove(eventName);
            return;
        }

        try
        {
            var fn = _jsContext.Eval($"(function(event) {{ {code} }})") as JSFunction;
            if (fn != null)
                GetInlineEventHandlers(element)[eventName] = fn;
        }
        catch (Exception ex)
        {
            RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.CompileInlineEventAttribute",
                $"Failed to compile on{eventName} handler: {ex.Message}", ex);
        }
    }
}
