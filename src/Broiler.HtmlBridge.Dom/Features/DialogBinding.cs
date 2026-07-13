using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The dialog / popover / details JS API feature binding (HtmlBridge complexity-reduction roadmap
/// Phase 3, P3.7) — <c>HTMLDialogElement</c> (<c>showModal</c>/<c>show</c>/<c>close</c>/<c>open</c>/
/// <c>returnValue</c>), the popover API (<c>showPopover</c>/<c>hidePopover</c> on any element with
/// the global <c>popover</c> attribute) and <c>HTMLDetailsElement.open</c>. It drives the element's
/// <c>open</c> attribute and the modal/popover/top-layer/return-value runtime state through the
/// narrow <see cref="IDialogHost"/> contract; the backdrop/top-layer <em>rendering</em> stays in the
/// bridge's anchor resolver.
/// </summary>
internal sealed class DialogBinding(IDialogHost host)
{
    private readonly IDialogHost _host = host;

    /// <summary>
    /// Installs the dialog/details interface members and the popover methods on
    /// <paramref name="obj"/> for <paramref name="element"/> (by <paramref name="tag"/> for
    /// dialog/details; by <paramref name="hasPopover"/> for the tag-agnostic popover API).
    /// </summary>
    internal void Install(JSObject obj, DomElement element, string tag, bool hasPopover)
    {
        if (tag == "details")
        {
            obj.FastAddProperty((KeyString)"open",
                new JSFunction((in _) => _host.HasOpenAttribute(element) ? JSBoolean.True : JSBoolean.False, "get open"),
                new JSFunction((in a) => SetOpenState(element, in a), "set open"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        if (tag == "dialog")
        {
            obj.FastAddValue((KeyString)"showModal", new JSFunction((in _) => ShowModal(element), "showModal", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            obj.FastAddValue((KeyString)"show", new JSFunction((in _) => Show(element), "show", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            obj.FastAddValue((KeyString)"close", new JSFunction((in a) => Close(element, in a), "close", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            obj.FastAddProperty((KeyString)"open",
                new JSFunction((in _) => _host.HasOpenAttribute(element) ? JSBoolean.True : JSBoolean.False, "get open"),
                new JSFunction((in a) => SetOpenState(element, in a), "set open"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
            obj.FastAddProperty((KeyString)"returnValue",
                new JSFunction((in _) => new JSString(_host.GetReturnValue(element)), "get returnValue"),
                new JSFunction((in a) => SetReturnValue(element, in a), "set returnValue"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // Popover API (HTML §popover) — showPopover()/hidePopover() are exposed on any element
        // carrying the global `popover` attribute, not tied to a tag.
        if (hasPopover)
        {
            obj.FastAddValue((KeyString)"showPopover", new JSFunction((in _) => ShowPopover(element), "showPopover", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            obj.FastAddValue((KeyString)"hidePopover", new JSFunction((in _) => HidePopover(element), "hidePopover", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        }
    }

    // details.open = value / dialog.open = value — reflect the boolean open attribute.
    private JSValue SetOpenState(DomElement element, in Arguments a)
    {
        _host.SetOpenAttribute(element, a.Length > 0 && a[0].BooleanValue);
        _host.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }

    private JSValue ShowModal(DomElement element)
    {
        _host.SetOpenAttribute(element, true);
        _host.SetDialogModal(element, true);
        _host.AssignNextTopLayerOrder(element);
        _host.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }

    private JSValue Show(DomElement element)
    {
        _host.SetOpenAttribute(element, true);
        _host.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }

    // showPopover() promotes the element to the top layer (so its ::backdrop renders), modeled with
    // the same runtime flag + top-layer order the modal-dialog path uses.
    private JSValue ShowPopover(DomElement element)
    {
        _host.SetPopoverOpen(element, true);
        _host.AssignNextTopLayerOrder(element);
        _host.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }

    private JSValue HidePopover(DomElement element)
    {
        // CSS Position §overlay: hiding a popover whose `overlay` is transitioned with
        // `transition-behavior: allow-discrete` keeps it in the top layer for the duration of the
        // transition. A static render snapshots mid-transition, so the popover (and its ::backdrop)
        // must stay rendered — leave the flag set. Without such a transition it hides immediately.
        if (!_host.PopoverKeepsOverlayOnHide(element))
            _host.SetPopoverOpen(element, false);
        _host.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }

    private JSValue Close(DomElement element, in Arguments a)
    {
        _host.SetOpenAttribute(element, false);
        _host.SetDialogModal(element, false);
        if (a.Length > 0)
            _host.SetReturnValue(element, a[0].ToString());
        _host.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }

    private JSValue SetReturnValue(DomElement element, in Arguments a)
    {
        _host.SetReturnValue(element, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }
}
