using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsMessagingAddEventListener001Core(global::Broiler.JavaScript.Runtime.JSObject target, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        var type = a[0].ToString();
        var listener = a[1];
        var listeners = GetOrCreateEventTargetListeners(target, type);
        var registration = CreateEventListenerRegistration(listener, a.Length > 2 ? a[2] : JSUndefined.Value);
        if (!HasMatchingEventListener(listeners, registration))
            listeners.Add(registration);
        return JSUndefined.Value;
    }


    private JSValue JsMessagingRemoveEventListener002Core(global::Broiler.JavaScript.Runtime.JSObject target, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        var type = a[0].ToString();
        var listener = a[1];
        var capture = GetCaptureForRemoval(a.Length > 2 ? a[2] : JSUndefined.Value);
        if (_eventTargetListeners.TryGetValue(target, out var listenersByType) && listenersByType.TryGetValue(type, out var listeners))
        {
            for (var i = listeners.Count - 1; i >= 0; i--)
            {
                if (listeners[i].Listener == listener && listeners[i].Capture == capture)
                {
                    listeners.RemoveAt(i);
                    break;
                }
            }
        }

        return JSUndefined.Value;
    }


    private JSValue JsMessagingDispatchEvent003Core(global::System.String logContext, global::Broiler.JavaScript.Runtime.JSObject target, in Arguments a)
    {
        if (a.Length == 0 || a[0] is not JSObject evt)
            return JSBoolean.True;
        return DispatchEventTarget(target, evt, logContext);
    }


    private JSValue JsMessagingStopPropagation004Core(ref global::System.Boolean legacyCancelBubble, in Arguments _)
    {
        legacyCancelBubble = true;
        return JSUndefined.Value;
    }


    private JSValue JsMessagingStopImmediatePropagation005Core(ref global::System.Boolean immediateStopped, ref global::System.Boolean legacyCancelBubble, in Arguments _)
    {
        immediateStopped = true;
        legacyCancelBubble = true;
        return JSUndefined.Value;
    }


    private JSValue JsMessagingPreventDefault006Core(global::System.Boolean currentListenerPassive, global::Broiler.JavaScript.Runtime.JSObject evt, ref global::System.Boolean prevented, in Arguments _)
    {
        var cancelable = evt[(KeyString)"cancelable"];
        if (!currentListenerPassive && cancelable != null && cancelable.BooleanValue)
        {
            prevented = true;
            evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
        }

        return JSUndefined.Value;
    }


    private JSValue JsMessagingSetCancelBubble008Core(ref global::System.Boolean legacyCancelBubble, in Arguments setArgs)
    {
        if (setArgs.Length > 0 && setArgs[0].BooleanValue)
            legacyCancelBubble = true;
        return JSUndefined.Value;
    }


    private JSValue JsMessagingSetReturnValue010Core(global::System.Boolean currentListenerPassive, global::Broiler.JavaScript.Runtime.JSObject evt, ref global::System.Boolean prevented, in Arguments setArgs)
    {
        var cancelable = evt[(KeyString)"cancelable"];
        if (setArgs.Length > 0 && !setArgs[0].BooleanValue && !currentListenerPassive && cancelable != null && cancelable.BooleanValue)
        {
            prevented = true;
            evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
        }

        return JSUndefined.Value;
    }


    private JSValue JsMessagingPostMessage012Core(global::Broiler.JavaScript.Runtime.JSObject window, in Arguments a)
    {
        var targetWindow = a.This as JSObject ?? window;
        var sourceWindow = ResolveCurrentWindow();
        var (targetOrigin, ports, cloneOptions, transferredPorts) = GetPostMessageDispatchOptions(a);
        if (!ShouldDeliverWindowMessage(targetWindow, sourceWindow, targetOrigin))
            return JSUndefined.Value;
        var payload = CloneForMessaging(a.Length > 0 ? a[0] : JSUndefined.Value, cloneOptions);
        CommitTransferredPorts(transferredPorts, targetWindow);
        var origin = GetWindowOrigin(sourceWindow);
        QueueFrameAction(() =>
        {
            var evt = CreateMessageEvent(payload, sourceWindow, origin, ports);
            if (ReferenceEquals(targetWindow, _windowJSObject))
            {
                DispatchWindowEvent(evt);
            }
            else
            {
                RunWithWindowContext(targetWindow, () => DispatchEventTarget(targetWindow, evt, "DomBridge.window.postMessage"));
            }
        });
        return JSUndefined.Value;
    }


    private JSValue JsMessagingPostMessage013Core(global::Broiler.JavaScript.Runtime.JSObject? port, in Arguments a)
    {
        var sourcePort = a.This as JSObject ?? port;
        if (_closedMessagePorts.Contains(sourcePort) || !_messagePortPeers.TryGetValue(sourcePort, out var targetPort) || _closedMessagePorts.Contains(targetPort))
        {
            return JSUndefined.Value;
        }

        JSValue transferValue = JSUndefined.Value;
        if (a.Length > 1)
        {
            if (a[1] is JSObject optionsObject && optionsObject[(KeyString)"transfer"] is { })
            {
                transferValue = optionsObject[(KeyString)"transfer"] ?? JSUndefined.Value;
            }
            else
            {
                transferValue = a[1];
            }
        }

        var targetOwner = ResolveOwnerWindow(targetPort) ?? _windowJSObject ?? sourcePort;
        var (ports, cloneOptions, transferredPorts) = ExtractTransferList(transferValue);
        var payload = CloneForMessaging(a.Length > 0 ? a[0] : JSUndefined.Value, cloneOptions);
        CommitTransferredPorts(transferredPorts, targetOwner);
        QueueFrameAction(() =>
        {
            if (_closedMessagePorts.Contains(sourcePort) || _closedMessagePorts.Contains(targetPort))
            {
                return;
            }

            var evt = CreateMessageEvent(payload, null, string.Empty, ports);
            DispatchOrQueueMessagePortEvent(targetPort, evt);
        });
        return JSUndefined.Value;
    }


    private JSValue JsMessagingSetOnmessage015Core(ref global::Broiler.JavaScript.Runtime.JSValue onMessageHandler, global::Broiler.JavaScript.Runtime.JSObject? port, in Arguments a)
    {
        onMessageHandler = a.Length > 0 ? a[0] : JSUndefined.Value;
        if (!onMessageHandler.IsNullOrUndefined)
        {
            ActivateMessagePort(a.This as JSObject ?? port);
        }

        return JSUndefined.Value;
    }


    private JSValue JsMessagingStart016Core(global::Broiler.JavaScript.Runtime.JSObject? port, in Arguments a)
    {
        ActivateMessagePort(a.This as JSObject ?? port);
        return JSUndefined.Value;
    }


    private JSValue JsMessagingClose017Core(global::Broiler.JavaScript.Runtime.JSObject? port, in Arguments a)
    {
        var currentPort = a.This as JSObject ?? port;
        _closedMessagePorts.Add(currentPort);
        _queuedMessagePortEvents.Remove(currentPort);
        return JSUndefined.Value;
    }

}
