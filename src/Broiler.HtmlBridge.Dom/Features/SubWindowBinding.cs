using System;
using System.Linq;
using Broiler.Dom;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The co-located nested-browsing-context <c>window</c> (sub-window) feature (HtmlBridge
/// complexity-reduction roadmap Phase 3, P3.17 — the residual Frames surface P3.13 deferred). Owns the
/// sub-window JS object built for an <c>&lt;iframe&gt;</c>/<c>&lt;object&gt;</c>/<c>&lt;frame&gt;</c> —
/// its <c>document</c>/<c>location</c>/<c>self</c>/<c>window</c>/<c>parent</c>/<c>top</c> wiring, the
/// scroll surface (<c>scrollX</c>/<c>scrollY</c>/<c>pageXOffset</c>/<c>pageYOffset</c> +
/// <c>scroll</c>/<c>scrollTo</c>/<c>scrollBy</c>), the mirrored event constructors, and its own
/// <c>getComputedStyle</c> — plus the sub-window-scoped helpers (location href, scroll offset read/write,
/// scrolling-element and parent-window resolution).
/// </summary>
/// <remarks>
/// The state authority (JS-object identity, location/base-URL caches) is the P3.16
/// <see cref="BrowsingContextManager"/>, which the module holds a reference to (as it does the shared
/// <see cref="EventTargetRegistry"/> and <see cref="MessagingBinding"/> it installs on the sub-window).
/// Everything else — the sub-document builder it wraps, sub-resource URL resolution, scroll geometry,
/// computed style, the global constructors — is reached through the narrow <see cref="ISubWindowHost"/>
/// contract, so no callback touches an arbitrary bridge private field.
/// </remarks>
internal sealed class SubWindowBinding(
    ISubWindowHost host,
    BrowsingContextManager browsingContexts,
    EventTargetRegistry eventTargets,
    MessagingBinding messaging)
{
    private readonly ISubWindowHost _host = host;
    private readonly BrowsingContextManager _browsingContexts = browsingContexts;
    private readonly EventTargetRegistry _eventTargets = eventTargets;
    private readonly MessagingBinding _messaging = messaging;

    /// <summary>Gets or builds the sub-window JS object for a nested-browsing-context container.</summary>
    public JSObject GetOrCreate(DomElement containerElement)
    {
        if (_browsingContexts.TryGetSubWindow(containerElement, out var cached))
            return cached;

        var subDocument = _host.GetOrCreateSubDocument(containerElement);
        var subWindow = new JSObject();
        _browsingContexts.SetSubWindow(containerElement, subWindow);
        _eventTargets.SetOwnerWindow(subWindow, subWindow);
        _messaging.InstallEventTargetApi(subWindow, "DomBridge.subWindow.dispatchEvent");
        _messaging.RegisterWindowMessaging(subWindow);

        subWindow.FastAddProperty((KeyString)"document",
            new JSFunction((in _) => _host.GetOrCreateSubDocument(containerElement), "get document"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        var locationHref = GetSubWindowLocationHref(containerElement);
        var iframeLocation = new JSObject();
        iframeLocation.FastAddValue((KeyString)"href",
            new JSString(locationHref), JSPropertyAttributes.EnumerableConfigurableValue);
        if (Uri.TryCreate(locationHref, UriKind.Absolute, out var locationUri))
        {
            iframeLocation.FastAddValue((KeyString)"protocol", new JSString(locationUri.Scheme + ":"), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"host", new JSString(locationUri.IsDefaultPort ? locationUri.Host : $"{locationUri.Host}:{locationUri.Port}"), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"hostname", new JSString(locationUri.Host), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"pathname", new JSString(locationUri.AbsolutePath), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"search", new JSString(locationUri.Query), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"hash", new JSString(locationUri.Fragment), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"origin", new JSString($"{locationUri.Scheme}://{(locationUri.IsDefaultPort ? locationUri.Host : $"{locationUri.Host}:{locationUri.Port}")}"), JSPropertyAttributes.EnumerableConfigurableValue);
        }
        else
        {
            iframeLocation.FastAddValue((KeyString)"search", new JSString(string.Empty), JSPropertyAttributes.EnumerableConfigurableValue);
            iframeLocation.FastAddValue((KeyString)"hash", new JSString(string.Empty), JSPropertyAttributes.EnumerableConfigurableValue);
        }
        subWindow.FastAddValue((KeyString)"location", iframeLocation, JSPropertyAttributes.EnumerableConfigurableValue);

        subWindow.FastAddProperty((KeyString)"scrollX", new JSFunction((in _) => new JSNumber(GetSubWindowScrollOffset(containerElement, vertical: false)), "get scrollX"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        subWindow.FastAddProperty((KeyString)"scrollY", new JSFunction((in _) => new JSNumber(GetSubWindowScrollOffset(containerElement, vertical: true)), "get scrollY"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        subWindow.FastAddProperty((KeyString)"pageXOffset", new JSFunction((in _) => new JSNumber(GetSubWindowScrollOffset(containerElement, vertical: false)), "get pageXOffset"), null, JSPropertyAttributes.EnumerableConfigurableProperty);
        subWindow.FastAddProperty((KeyString)"pageYOffset", new JSFunction((in _) => new JSNumber(GetSubWindowScrollOffset(containerElement, vertical: true)), "get pageYOffset"), null, JSPropertyAttributes.EnumerableConfigurableProperty);

        subWindow.FastAddValue((KeyString)"scroll", new JSFunction((in a) => Scroll(containerElement, in a), "scroll", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        subWindow.FastAddValue((KeyString)"scrollTo", new JSFunction((in a) => ScrollTo(containerElement, in a), "scrollTo", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        subWindow.FastAddValue((KeyString)"scrollBy", new JSFunction((in a) => ScrollBy(containerElement, in a), "scrollBy", 2), JSPropertyAttributes.EnumerableConfigurableValue);

        subWindow.FastAddValue((KeyString)"self", subWindow, JSPropertyAttributes.EnumerableConfigurableValue);
        subWindow.FastAddValue((KeyString)"window", subWindow, JSPropertyAttributes.EnumerableConfigurableValue);

        foreach (var ctorName in new[]
                 {
                     "Event", "CustomEvent", "MouseEvent", "FocusEvent", "KeyboardEvent",
                     "WheelEvent", "UIEvent", "MessageChannel",
                 })
        {
            if (_host.GetGlobal(ctorName) is { } ctor)
                subWindow.FastAddValue((KeyString)ctorName, ctor, JSPropertyAttributes.EnumerableConfigurableValue);
        }

        var parentWindow = GetParentWindowForSubDocument(containerElement);
        if (parentWindow != null)
        {
            subWindow.FastAddValue((KeyString)"parent", parentWindow, JSPropertyAttributes.EnumerableConfigurableValue);
        }

        subWindow.FastAddValue((KeyString)"top", _host.WindowJSObject ?? subWindow, JSPropertyAttributes.EnumerableConfigurableValue);

        subDocument.FastAddValue((KeyString)"defaultView", subWindow, JSPropertyAttributes.EnumerableConfigurableValue);

        // window.getComputedStyle — sub-window needs its own copy so that
        // doc.defaultView.getComputedStyle(node, "") resolves CSS rules from
        // the sub-document's <style> elements rather than the main document.
        subWindow.FastAddValue((KeyString)"getComputedStyle", new JSFunction((in a) => GetComputedStyle(in a), "getComputedStyle", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return subWindow;
    }

    private string GetSubWindowLocationHref(DomElement containerElement)
    {
        if (_browsingContexts.TryGetLocation(containerElement, out var cachedLocation) &&
            !string.IsNullOrWhiteSpace(cachedLocation))
        {
            return cachedLocation;
        }

        if (string.Equals(containerElement.TagName, "iframe", StringComparison.OrdinalIgnoreCase) &&
            DomBridge.HasAttr(containerElement, "srcdoc"))
            return "about:srcdoc";

        var resolvedUrl = _host.ResolveSubResourceUrl(DomBridge.GetSubResourceUrl(containerElement), _host.GetInheritedSubDocumentBaseUrl(containerElement));
        return !string.IsNullOrWhiteSpace(resolvedUrl) ? resolvedUrl : "about:blank";
    }

    private double GetSubWindowScrollOffset(DomElement containerElement, bool vertical)
    {
        var scrollingElement = GetSubDocumentScrollingElement(containerElement);
        return scrollingElement == null ? 0 : _host.GetElementScrollOffset(scrollingElement, vertical);
    }

    private void SetSubWindowScrollOffsets(DomElement containerElement, double? left = null, double? top = null, bool relative = false, string? behavior = null)
    {
        var scrollingElement = GetSubDocumentScrollingElement(containerElement);
        if (scrollingElement == null)
            return;

        _host.SetElementScroll(scrollingElement, left, top, relative, behavior);
    }

    private DomElement? GetSubDocumentScrollingElement(DomElement containerElement)
    {
        var document = _host.GetContentDocument(containerElement);
        return document == null ? null : DomBridge.GetDocumentElement(document);
    }

    private JSObject? GetParentWindowForSubDocument(DomElement containerElement)
    {
        // The container's owning document is a severed sub-document DomDocument when the container is
        // itself nested in another frame; recover that frame via the reverse map (P4.4c: the owning
        // document comes from the canonical tree, was OwnerDocRoot / ParentEl(#subdoc-root)).
        var parentFrame = _host.GetFrameForContentDocument(DomBridge.GetOwningDocument(containerElement));
        if (parentFrame != null)
            return GetOrCreate(parentFrame);

        return _host.WindowJSObject;
    }

    // ── Scroll / getComputedStyle callbacks (were JsSubDocumentsScroll006Core … 009Core) ──
    private JSValue Scroll(DomElement containerElement, in Arguments a)
    {
        var (left, top, behavior) = _host.GetScrollArguments(a);
        SetSubWindowScrollOffsets(containerElement, left, top, behavior: behavior);
        return JSUndefined.Value;
    }

    private JSValue ScrollTo(DomElement containerElement, in Arguments a)
    {
        var (left, top, behavior) = _host.GetScrollArguments(a);
        SetSubWindowScrollOffsets(containerElement, left, top, behavior: behavior);
        return JSUndefined.Value;
    }

    private JSValue ScrollBy(DomElement containerElement, in Arguments a)
    {
        var (left, top, behavior) = _host.GetScrollArguments(a);
        SetSubWindowScrollOffsets(containerElement, left, top, relative: true, behavior: behavior);
        return JSUndefined.Value;
    }

    private JSValue GetComputedStyle(in Arguments a)
    {
        if (a.Length == 0)
            return new JSObject();
        var targetObj = a[0] as JSObject;
        var el = targetObj != null ? _host.FindDomElementByJSObject(targetObj) : null;
        var pseudoElement = a.Length > 1 ? a[1]?.ToString() : null;
        return _host.BuildComputedStyleObject(el, pseudoElement);
    }
}
