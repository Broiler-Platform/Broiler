using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.HtmlBridge;

/// <summary>
/// The document's browsing-context / window-resolution helpers: canonicalising a window candidate
/// against the sub-window caches, resolving the current/owner window, and temporarily switching the
/// global window/document/location/parent bindings into another browsing context. These were
/// physically co-located with web messaging but are browsing-context infrastructure (they read the
/// sub-window/sub-document caches and the active-window override, now owned by
/// <see cref="Broiler.HtmlBridge.Dom.Runtime.BrowsingContextManager"/> — P3.16). These algorithms stay
/// bridge-owned and reach that state through it; the extracted
/// <see cref="Broiler.HtmlBridge.Dom.Features.MessagingBinding"/> reaches the ones it needs through the
/// <see cref="Broiler.HtmlBridge.Dom.Features.IMessagingHost"/> contract, and <c>SubDocuments.cs</c>
/// calls <see cref="RunWithWindowContext"/> directly when running a sub-window's scripts.
/// </summary>
public sealed partial class DomBridge
{
    private JSObject? ResolveCurrentWindow()
        => GetCanonicalWindow(_browsingContexts.CurrentWindowOverride ?? _jsContext?["window"] as JSObject ?? _windowJSObject);

    private JSObject? ResolveOwnerWindow(JSObject target)
        => _eventTargets.TryGetOwnerWindow(target, out var ownerWindow) ? GetCanonicalWindow(ownerWindow) : ResolveCurrentWindow();

    private JSObject? GetCanonicalWindow(JSObject? candidate)
    {
        if (candidate == null || ReferenceEquals(candidate, _windowJSObject))
            return candidate;

        if (_browsingContexts.IsSubWindow(candidate))
            return candidate;

        foreach (var subWindow in _browsingContexts.SubWindows)
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
        var previousCurrentWindow = _browsingContexts.CurrentWindowOverride;

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
            _browsingContexts.CurrentWindowOverride = targetWindow;

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
            _browsingContexts.CurrentWindowOverride = previousCurrentWindow;
        }
    }

    private JSValue GetWindowDocument(JSObject targetWindow)
    {
        if (ReferenceEquals(targetWindow, _windowJSObject))
            return _documentJSObject ?? JSUndefined.Value;

        return _browsingContexts.TryGetSubWindowContainer(targetWindow, out var containerElement)
            ? GetOrCreateSubDocument(containerElement)
            : JSUndefined.Value;
    }

    private JSValue GetWindowParent(JSObject targetWindow)
    {
        if (ReferenceEquals(targetWindow, _windowJSObject))
            return _jsContext?.Eval("this") ?? targetWindow;

        return targetWindow[(KeyString)"parent"] ?? (_windowJSObject ?? targetWindow);
    }
}
