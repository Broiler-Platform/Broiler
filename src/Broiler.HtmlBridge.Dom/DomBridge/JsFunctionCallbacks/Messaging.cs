using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsMessagingAddEventListener001Core(JSObject target, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        var type = a[0].ToString();
        Dom.Features.EventListenerBinding.AddListener(
            GetOrCreateEventTargetListeners(target, type), a[1], a.Length > 2 ? a[2] : JSUndefined.Value);
        return JSUndefined.Value;
    }


    private JSValue JsMessagingRemoveEventListener002Core(JSObject target, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        var type = a[0].ToString();
        var listeners = _eventTargets.TryGetTargetListeners(target, out var listenersByType) &&
                        listenersByType.TryGetValue(type, out var byType)
            ? byType
            : null;
        Dom.Features.EventListenerBinding.RemoveListener(listeners, a[1], a.Length > 2 ? a[2] : JSUndefined.Value);
        return JSUndefined.Value;
    }


    private JSValue JsMessagingDispatchEvent003Core(string logContext, JSObject target, in Arguments a)
    {
        if (a.Length == 0 || a[0] is not JSObject evt)
            return JSBoolean.True;
        return DispatchEventTarget(target, evt, logContext);
    }


    private JSValue JsMessagingStopPropagation004Core(ref bool legacyCancelBubble, in Arguments _)
    {
        legacyCancelBubble = true;
        return JSUndefined.Value;
    }


    private JSValue JsMessagingStopImmediatePropagation005Core(ref bool immediateStopped, ref bool legacyCancelBubble, in Arguments _)
    {
        immediateStopped = true;
        legacyCancelBubble = true;
        return JSUndefined.Value;
    }


    private JSValue JsMessagingPreventDefault006Core(bool currentListenerPassive, JSObject evt, ref bool prevented, in Arguments _)
    {
        var cancelable = evt[(KeyString)"cancelable"];
        if (!currentListenerPassive && cancelable != null && cancelable.BooleanValue)
        {
            prevented = true;
            evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
        }

        return JSUndefined.Value;
    }


    private JSValue JsMessagingSetCancelBubble008Core(ref bool legacyCancelBubble, in Arguments setArgs)
    {
        if (setArgs.Length > 0 && setArgs[0].BooleanValue)
            legacyCancelBubble = true;
        return JSUndefined.Value;
    }


    private JSValue JsMessagingSetReturnValue010Core(bool currentListenerPassive, JSObject evt, ref bool prevented, in Arguments setArgs)
    {
        var cancelable = evt[(KeyString)"cancelable"];
        if (setArgs.Length > 0 && !setArgs[0].BooleanValue && !currentListenerPassive && cancelable != null && cancelable.BooleanValue)
        {
            prevented = true;
            evt[(KeyString)"defaultPrevented"] = JSBoolean.True;
        }

        return JSUndefined.Value;
    }


    private JSValue JsMessagingPostMessage012Core(JSObject window, in Arguments a)
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


    private JSValue JsMessagingPostMessage013Core(JSObject? port, in Arguments a)
    {
        var sourcePort = a.This as JSObject ?? port;
        if (_messagePorts.IsClosed(sourcePort) || !_messagePorts.TryGetPeer(sourcePort, out var targetPort) || _messagePorts.IsClosed(targetPort))
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
            if (_messagePorts.IsClosed(sourcePort) || _messagePorts.IsClosed(targetPort))
            {
                return;
            }

            var evt = CreateMessageEvent(payload, null, string.Empty, ports);
            DispatchOrQueueMessagePortEvent(targetPort, evt);
        });
        return JSUndefined.Value;
    }


    private JSValue JsMessagingSetOnmessage015Core(ref JSValue onMessageHandler, JSObject? port, in Arguments a)
    {
        onMessageHandler = a.Length > 0 ? a[0] : JSUndefined.Value;
        if (!onMessageHandler.IsNullOrUndefined)
        {
            ActivateMessagePort(a.This as JSObject ?? port);
        }

        return JSUndefined.Value;
    }


    private JSValue JsMessagingStart016Core(JSObject? port, in Arguments a)
    {
        ActivateMessagePort(a.This as JSObject ?? port);
        return JSUndefined.Value;
    }


    private JSValue JsMessagingClose017Core(JSObject? port, in Arguments a)
    {
        var currentPort = a.This as JSObject ?? port;
        _messagePorts.Close(currentPort);
        return JSUndefined.Value;
    }

}
