using Broiler.HtmlBridge.Dom.Features;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// <see cref="DomBridge"/>'s implementation of <see cref="IDialogHost"/>, the narrow contract the
/// extracted <see cref="Broiler.HtmlBridge.Dom.Features.DialogBinding"/> feature module consumes
/// (HtmlBridge complexity-reduction roadmap Phase 3, P3.7). Each member is an explicit interface
/// implementation, so these seams do not widen the public <c>DomBridge</c> surface. The dialog/
/// popover runtime state still lives on the per-element <see cref="ElementRuntimeState"/> tables and
/// the top-layer counter; these accessors are the single point a future TopLayerManager re-homes.
/// </summary>
public sealed partial class DomBridge : IDialogHost
{
    void IDialogHost.SetOpenAttribute(DomElement element, bool open)
    {
        if (open)
            SetAttr(element, "open", "");
        else
            RemoveAttr(element, "open");
    }

    bool IDialogHost.HasOpenAttribute(DomElement element) => HasAttr(element, "open");

    void IDialogHost.InvalidateStyleScope(DomElement element) => InvalidateStyleScope(element);

    void IDialogHost.AssignNextTopLayerOrder(DomElement element) =>
        GetElementRuntimeState(element).Dialog.TopLayerOrder.Set(++_topLayerCounter);

    void IDialogHost.SetDialogModal(DomElement element, bool modal)
    {
        if (modal)
            GetElementRuntimeState(element).Dialog.Modal.Set(true);
        else
            GetElementRuntimeState(element).Dialog.Modal.Remove();
    }

    void IDialogHost.SetPopoverOpen(DomElement element, bool open)
    {
        if (open)
            GetElementRuntimeState(element).Dialog.PopoverOpen.Set(true);
        else
            GetElementRuntimeState(element).Dialog.PopoverOpen.Remove();
    }

    string IDialogHost.GetReturnValue(DomElement element) =>
        GetElementRuntimeState(element).FormControl.ReturnValue.TryGet(out var rv) && rv is string s
            ? s
            : string.Empty;

    void IDialogHost.SetReturnValue(DomElement element, string value) =>
        GetElementRuntimeState(element).FormControl.ReturnValue.Set(value);

    bool IDialogHost.PopoverKeepsOverlayOnHide(DomElement element) => PopoverKeepsOverlayOnHide(element);
}
