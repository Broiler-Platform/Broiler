using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private void InstallEventTargetApi(JSObject target, string logContext)
    {
        target.FastAddValue(
            (KeyString)"addEventListener",
            new JSFunction((in Arguments a) => JsMessagingAddEventListener001Core(target, in a), "addEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        target.FastAddValue(
            (KeyString)"removeEventListener",
            new JSFunction((in Arguments a) => JsMessagingRemoveEventListener002Core(target, in a), "removeEventListener", 3),
            JSPropertyAttributes.EnumerableConfigurableValue);

        target.FastAddValue(
            (KeyString)"dispatchEvent",
            new JSFunction((in Arguments a) => JsMessagingDispatchEvent003Core(logContext, target, in a), "dispatchEvent", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
    }

    private List<EventListenerRegistration> GetOrCreateEventTargetListeners(JSObject target, string type)
    {
        if (!_eventTargetListeners.TryGetValue(target, out var listenersByType))
        {
            listenersByType = new Dictionary<string, List<EventListenerRegistration>>(StringComparer.OrdinalIgnoreCase);
            _eventTargetListeners[target] = listenersByType;
        }

        if (!listenersByType.TryGetValue(type, out var listeners))
        {
            listeners = [];
            listenersByType[type] = listeners;
        }

        return listeners;
    }

    private JSValue DispatchEventTarget(JSObject target, JSObject evt, string logContext)
    {
        var eventType = evt[(KeyString)"type"]?.ToString() ?? "unknown";
        evt.FastAddValue((KeyString)"target", target, JSPropertyAttributes.EnumerableConfigurableValue);
        evt[(KeyString)"srcElement"] = target;
        evt.FastAddValue((KeyString)"currentTarget", target, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"eventPhase", new JSNumber(2), JSPropertyAttributes.EnumerableConfigurableValue);

        var immediateStopped = false;
        var prevented = evt[(KeyString)"defaultPrevented"] is JSValue defaultPreventedValue &&
                        defaultPreventedValue.BooleanValue;
        var currentListenerPassive = false;
        var legacyCancelBubble = false;
        evt[(KeyString)"defaultPrevented"] = prevented ? JSBoolean.True : JSBoolean.False;
        evt.FastAddValue((KeyString)"stopPropagation",
            new JSFunction((in Arguments _) => JsMessagingStopPropagation004Core(ref legacyCancelBubble, in _), "stopPropagation", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"stopImmediatePropagation",
            new JSFunction((in Arguments _) => JsMessagingStopImmediatePropagation005Core(ref immediateStopped, ref legacyCancelBubble, in _), "stopImmediatePropagation", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"preventDefault",
            new JSFunction((in Arguments _) => JsMessagingPreventDefault006Core(currentListenerPassive, evt, ref prevented, in _), "preventDefault", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddProperty(
            (KeyString)"cancelBubble",
            new JSFunction((in Arguments _) => legacyCancelBubble ? JSBoolean.True : JSBoolean.False, "get cancelBubble"),
            new JSFunction((in Arguments setArgs) => JsMessagingSetCancelBubble008Core(ref legacyCancelBubble, in setArgs), "set cancelBubble"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
        evt.FastAddProperty(
            (KeyString)"returnValue",
            new JSFunction((in Arguments _) => prevented ? JSBoolean.False : JSBoolean.True, "get returnValue"),
            new JSFunction((in Arguments setArgs) => JsMessagingSetReturnValue010Core(currentListenerPassive, evt, ref prevented, in setArgs), "set returnValue"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
        evt.FastAddValue((KeyString)"composedPath",
            new JSFunction((in Arguments _) => new JSArray(target), "composedPath", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        InvokeEventTargetHandler(target, eventType, evt, logContext);

        if (_eventTargetListeners.TryGetValue(target, out var listenersByType) &&
            listenersByType.TryGetValue(eventType, out var listeners))
        {
            foreach (var registration in listeners.ToList())
            {
                if (immediateStopped)
                    break;

                currentListenerPassive = registration.Passive;
                InvokeEventListenerWithOwner(target, registration.Listener, evt, logContext);
                currentListenerPassive = false;

                if (registration.Once)
                    listeners.Remove(registration);
            }
        }

        evt[(KeyString)"currentTarget"] = JSNull.Value;
        evt[(KeyString)"eventPhase"] = new JSNumber(0);
        return prevented ? JSBoolean.False : JSBoolean.True;
    }

    private void InvokeEventTargetHandler(JSObject target, string eventType, JSObject evt, string logContext)
    {
        if (target[(KeyString)$"on{eventType}"] is not JSValue handler ||
            handler.IsNullOrUndefined)
        {
            return;
        }

        InvokeEventListenerWithOwner(target, handler, evt, logContext);
    }

    private void InvokeEventListenerWithOwner(JSObject target, JSValue listener, JSObject evt, string logContext)
    {
        var ownerWindow = ResolveOwnerWindow(target);
        if (ownerWindow == null)
        {
            InvokeEventListener(listener, evt, logContext);
            return;
        }

        RunWithWindowContext(ownerWindow, () => InvokeEventListener(listener, evt, logContext));
    }

    private JSObject? ResolveCurrentWindow()
        => GetCanonicalWindow(_currentWindowOverride ?? _jsContext?["window"] as JSObject ?? _windowJSObject);

    private JSObject? ResolveOwnerWindow(JSObject target)
        => _eventTargetOwnerWindows.TryGetValue(target, out var ownerWindow) ? GetCanonicalWindow(ownerWindow) : ResolveCurrentWindow();

    private JSObject? GetCanonicalWindow(JSObject? candidate)
    {
        if (candidate == null || ReferenceEquals(candidate, _windowJSObject))
            return candidate;

        if (_subWindowContainers.ContainsKey(candidate))
            return candidate;

        foreach (var subWindow in _subWindowCache.Values)
        {
            if (ReferenceEquals(candidate, subWindow))
                return subWindow;

            var candidateHref = (candidate[(KeyString)"location"] as JSObject)?[(KeyString)"href"]?.ToString();
            var subWindowHref = (subWindow[(KeyString)"location"] as JSObject)?[(KeyString)"href"]?.ToString();
            if (!string.Equals(candidateHref, subWindowHref, StringComparison.Ordinal))
                continue;

            var candidateParent = candidate[(KeyString)"parent"] as JSObject;
            var subWindowParent = subWindow[(KeyString)"parent"] as JSObject;
            if (ReferenceEquals(candidateParent, subWindowParent))
                return subWindow;
        }

        return candidate;
    }

    private void RunWithWindowContext(JSObject targetWindow, Action callback)
    {
        if (_jsContext == null)
        {
            callback();
            return;
        }

        JSValue? previousWindow = null;
        JSValue? previousDocument = null;
        JSValue? previousLocation = null;
        JSValue? previousParent = null;
        JSValue? previousPostMessage = null;
        JSValue? previousSelf = null;
        JSValue? previousTop = null;
        var previousCurrentWindow = _currentWindowOverride;

        try
        {
            previousWindow = _jsContext.Eval("typeof window === 'undefined' ? undefined : window");
            previousDocument = _jsContext.Eval("typeof document === 'undefined' ? undefined : document");
            previousLocation = _jsContext.Eval("typeof location === 'undefined' ? undefined : location");
            previousParent = _jsContext.Eval("typeof parent === 'undefined' ? undefined : parent");
            previousPostMessage = _jsContext.Eval("typeof postMessage === 'undefined' ? undefined : postMessage");
            previousSelf = _jsContext.Eval("typeof self === 'undefined' ? undefined : self");
            previousTop = _jsContext.Eval("typeof top === 'undefined' ? undefined : top");

            _jsContext["window"] = targetWindow;
            _jsContext["document"] = GetWindowDocument(targetWindow);
            _jsContext["location"] = targetWindow[(KeyString)"location"] ?? JSUndefined.Value;
            _jsContext["parent"] = GetWindowParent(targetWindow);
            _jsContext["postMessage"] = targetWindow[(KeyString)"postMessage"] ?? JSUndefined.Value;
            _jsContext["self"] = targetWindow;
            _jsContext["top"] = _windowJSObject ?? targetWindow;
            _currentWindowOverride = targetWindow;

            callback();
        }
        finally
        {
            _jsContext["window"] = previousWindow ?? JSUndefined.Value;
            _jsContext["document"] = previousDocument ?? JSUndefined.Value;
            _jsContext["location"] = previousLocation ?? JSUndefined.Value;
            _jsContext["parent"] = previousParent ?? JSUndefined.Value;
            _jsContext["postMessage"] = previousPostMessage ?? JSUndefined.Value;
            _jsContext["self"] = previousSelf ?? JSUndefined.Value;
            _jsContext["top"] = previousTop ?? JSUndefined.Value;
            _currentWindowOverride = previousCurrentWindow;
        }
    }

    private JSValue GetWindowDocument(JSObject targetWindow)
    {
        if (ReferenceEquals(targetWindow, _windowJSObject))
            return _documentJSObject ?? JSUndefined.Value;

        return _subWindowContainers.TryGetValue(targetWindow, out var containerElement)
            ? GetOrCreateSubDocument(containerElement)
            : JSUndefined.Value;
    }

    private JSValue GetWindowParent(JSObject targetWindow)
    {
        if (ReferenceEquals(targetWindow, _windowJSObject))
            return _jsContext?.Eval("this") ?? targetWindow;

        return targetWindow[(KeyString)"parent"] ?? (_windowJSObject ?? targetWindow);
    }

    private void RegisterWindowMessaging(JSObject window)
    {
        window.FastAddValue(
            (KeyString)"postMessage",
            new JSFunction((in Arguments a) => JsMessagingPostMessage012Core(window, in a), "postMessage", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
    }

    private (string TargetOrigin, JSArray Ports, JSValue CloneOptions, List<JSObject> TransferredPorts) GetPostMessageDispatchOptions(in Arguments a)
    {
        var targetOrigin = "*";
        JSValue transferValue = JSUndefined.Value;

        if (a.Length > 1)
        {
            if (a[1] is JSObject optionsObject &&
                (optionsObject[(KeyString)"targetOrigin"] is { } ||
                 optionsObject[(KeyString)"transfer"] is { }))
            {
                var targetOriginValue = optionsObject[(KeyString)"targetOrigin"];
                if (targetOriginValue != null && !targetOriginValue.IsNullOrUndefined)
                    targetOrigin = targetOriginValue.ToString();

                transferValue = optionsObject[(KeyString)"transfer"] ?? JSUndefined.Value;
            }
            else
            {
                targetOrigin = a[1].ToString();
            }
        }

        if (a.Length > 2)
            transferValue = a[2];

        var (ports, cloneOptions, transferredPorts) = ExtractTransferList(transferValue);
        return (targetOrigin, ports, cloneOptions, transferredPorts);
    }

    private (JSArray Ports, JSValue CloneOptions, List<JSObject> TransferredPorts) ExtractTransferList(JSValue transferValue)
    {
        if (transferValue.IsNullOrUndefined)
            return (new JSArray(), JSUndefined.Value, []);

        if (transferValue is not JSArray)
        {
            ThrowDOMException(_jsContext!, "The transfer list contains a non-transferable value.", "DataCloneError");
            return (new JSArray(), JSUndefined.Value, []);
        }

        var transferArray = (JSArray)transferValue;

        var transferredPorts = new List<JSValue>();
        var transferredPortObjects = new List<JSObject>();
        var seenPorts = new HashSet<JSObject>(ReferenceEqualityComparer.Instance);
        var transferredBuffers = new List<JSValue>();
        var seenBuffers = new HashSet<JSArrayBuffer>(ReferenceEqualityComparer.Instance);
        foreach (var (_, item) in transferArray.GetArrayElements(withHoles: false))
        {
            if (item is JSObject port && _messagePortPeers.ContainsKey(port))
            {
                if (!seenPorts.Add(port))
                    ThrowDOMException(_jsContext!, "The transfer list contains duplicate transferable values.", "DataCloneError");

                transferredPorts.Add(port);
                transferredPortObjects.Add(port);
                continue;
            }

            if (item is JSArrayBuffer arrayBuffer)
            {
                if (arrayBuffer.Detached)
                    ThrowDOMException(_jsContext!, "The transfer list contains a detached ArrayBuffer.", "DataCloneError");

                if (!seenBuffers.Add(arrayBuffer))
                    ThrowDOMException(_jsContext!, "The transfer list contains duplicate transferable values.", "DataCloneError");

                transferredBuffers.Add(arrayBuffer);
                continue;
            }

            ThrowDOMException(_jsContext!, "The transfer list contains a non-transferable value.", "DataCloneError");
        }

        JSValue cloneOptions = JSUndefined.Value;
        if (transferredBuffers.Count > 0)
        {
            var transferOptions = new JSObject();
            transferOptions.FastAddValue((KeyString)"transfer", new JSArray(transferredBuffers), JSPropertyAttributes.EnumerableConfigurableValue);
            cloneOptions = transferOptions;
        }

        return (new JSArray(transferredPorts), cloneOptions, transferredPortObjects);
    }

    private void CommitTransferredPorts(IEnumerable<JSObject> transferredPorts, JSObject targetWindow)
    {
        foreach (var port in transferredPorts)
            _eventTargetOwnerWindows[port] = targetWindow;
    }

    private bool ShouldDeliverWindowMessage(JSObject targetWindow, JSObject? sourceWindow, string targetOrigin)
    {
        if (string.IsNullOrWhiteSpace(targetOrigin) || targetOrigin == "*")
            return true;

        if (targetOrigin == "/")
            targetOrigin = GetWindowOrigin(sourceWindow);

        return string.Equals(targetOrigin, GetWindowOrigin(targetWindow), StringComparison.Ordinal);
    }

    private string GetWindowOrigin(JSObject? window)
    {
        if (window == null)
            return string.Empty;

        if (window[(KeyString)"location"] is JSObject location)
        {
            var href = location[(KeyString)"href"]?.ToString() ?? string.Empty;
            if (string.Equals(href, "about:srcdoc", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(href, "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                return GetWindowOrigin(window[(KeyString)"parent"] as JSObject);
            }

            var origin = location[(KeyString)"origin"]?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(origin))
                return origin;

            if (Uri.TryCreate(href, UriKind.Absolute, out var hrefUri))
                return $"{hrefUri.Scheme}://{(hrefUri.IsDefaultPort ? hrefUri.Host : $"{hrefUri.Host}:{hrefUri.Port}")}";
        }

        return string.Empty;
    }

    private JSValue CloneForMessaging(JSValue value, JSValue cloneOptions = default)
    {
        try
        {
            if (cloneOptions == null || cloneOptions.IsNullOrUndefined)
                return JavaScript.Globals.JSGlobalStatic.StructuredClone(new Arguments(JSUndefined.Value, value));

            return JavaScript.Globals.JSGlobalStatic.StructuredClone(new Arguments(JSUndefined.Value, value, cloneOptions));
        }
        catch (JSException)
        {
            ThrowDOMException(_jsContext!, "The object could not be cloned.", "DataCloneError");
            return JSUndefined.Value;
        }
    }

    private JSObject CreateMessageEvent(JSValue data, JSObject? sourceWindow, string origin, JSArray ports)
    {
        var evt = new JSObject();
        evt.FastAddValue((KeyString)"type", new JSString("message"), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"bubbles", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"cancelable", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"data", data, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"origin", new JSString(origin), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"lastEventId", new JSString(string.Empty), JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"source", sourceWindow ?? JSNull.Value, JSPropertyAttributes.EnumerableConfigurableValue);
        evt.FastAddValue((KeyString)"ports", ports, JSPropertyAttributes.EnumerableConfigurableValue);
        return evt;
    }

    private JSObject CreateMessagePort(JSObject? ownerWindow)
    {
        var port = new JSObject();
        var effectiveOwner = ownerWindow ?? _windowJSObject ?? ResolveCurrentWindow() ?? port;
        _eventTargetOwnerWindows[port] = effectiveOwner;
        InstallEventTargetApi(port, "DomBridge.messagePort.dispatchEvent");
        JSValue onMessageHandler = JSNull.Value;

        port.FastAddValue(
            (KeyString)"postMessage",
            new JSFunction((in Arguments a) => JsMessagingPostMessage013Core(port, in a), "postMessage", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        port.FastAddProperty(
            (KeyString)"onmessage",
            new JSFunction((in Arguments _) => onMessageHandler, "get onmessage"),
            new JSFunction((in Arguments a) => JsMessagingSetOnmessage015Core(ref onMessageHandler, port, in a), "set onmessage"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        port.FastAddValue(
            (KeyString)"start",
            new JSFunction((in Arguments a) => JsMessagingStart016Core(port, in a), "start", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        port.FastAddValue(
            (KeyString)"close",
            new JSFunction((in Arguments a) => JsMessagingClose017Core(port, in a), "close", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return port;
    }

    private JSObject CreateMessageChannel()
    {
        var ownerWindow = ResolveCurrentWindow();
        var port1 = CreateMessagePort(ownerWindow);
        var port2 = CreateMessagePort(ownerWindow);
        _messagePortPeers[port1] = port2;
        _messagePortPeers[port2] = port1;

        var channel = new JSObject();
        channel.FastAddValue((KeyString)"port1", port1, JSPropertyAttributes.EnumerableConfigurableValue);
        channel.FastAddValue((KeyString)"port2", port2, JSPropertyAttributes.EnumerableConfigurableValue);
        return channel;
    }

    private void DispatchOrQueueMessagePortEvent(JSObject targetPort, JSObject evt)
    {
        if (_closedMessagePorts.Contains(targetPort))
            return;

        if (CanDispatchMessagePortEvent(targetPort))
        {
            DispatchMessagePortEvent(targetPort, evt);
            return;
        }

        if (!_queuedMessagePortEvents.TryGetValue(targetPort, out var queuedEvents))
        {
            queuedEvents = [];
            _queuedMessagePortEvents[targetPort] = queuedEvents;
        }

        queuedEvents.Add(evt);
    }

    private bool CanDispatchMessagePortEvent(JSObject targetPort)
        => _startedMessagePorts.Contains(targetPort) || HasOnMessageHandler(targetPort);

    private bool HasOnMessageHandler(JSObject targetPort)
        => targetPort[(KeyString)"onmessage"] is { } handler && !handler.IsNullOrUndefined;

    private void ActivateMessagePort(JSObject port)
    {
        if (_closedMessagePorts.Contains(port))
            return;

        _startedMessagePorts.Add(port);

        if (!_queuedMessagePortEvents.TryGetValue(port, out var queuedEvents) || queuedEvents.Count == 0)
            return;

        _queuedMessagePortEvents.Remove(port);
        foreach (var evt in queuedEvents)
        {
            DispatchMessagePortEvent(port, evt);
        }
    }

    private void DispatchMessagePortEvent(JSObject targetPort, JSObject evt)
    {
        var targetOwner = ResolveOwnerWindow(targetPort);
        if (targetOwner == null)
        {
            DispatchEventTarget(targetPort, evt, "DomBridge.messagePort.postMessage");
        }
        else
        {
            RunWithWindowContext(targetOwner, () =>
                DispatchEventTarget(targetPort, evt, "DomBridge.messagePort.postMessage"));
        }
    }
}
