using Broiler.JavaScript.Runtime;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// <see cref="DomBridge"/>'s implementation of <see cref="ISelectHost"/>, the narrow contract the
/// extracted <see cref="Broiler.HtmlBridge.Dom.Features.SelectBinding"/> feature module consumes
/// (HtmlBridge complexity-reduction roadmap Phase 3, P3.8). Explicit interface members, so these
/// seams do not widen the public <c>DomBridge</c> surface. The select/option form-control state
/// stays on the per-element <see cref="ElementRuntimeState"/>; these named accessors are the seam a
/// future runtime-state consolidation re-homes.
/// </summary>
public sealed partial class DomBridge : ISelectHost
{
    JSObject ISelectHost.ToJSObject(DomNode node) => ToJSObject(node);

    DomElement? ISelectHost.FindDomElementByJSObject(JSObject? jsObj) =>
        jsObj is null ? null : FindDomElementByJSObject(jsObj);

    bool ISelectHost.TryGetSelectedIndex(DomElement select, out int index)
    {
        if (GetElementRuntimeState(select).FormControl.SelectedIndex.TryGet(out var value) && value is int i)
        {
            index = i;
            return true;
        }

        index = 0;
        return false;
    }

    void ISelectHost.SetSelectedIndex(DomElement select, int index) =>
        GetElementRuntimeState(select).FormControl.SelectedIndex.Set(index);

    bool ISelectHost.TryGetOptionValue(DomElement option, out string value)
    {
        if (GetElementRuntimeState(option).FormControl.Value.TryGet(out var stored) && stored is string s)
        {
            value = s;
            return true;
        }

        value = string.Empty;
        return false;
    }

    bool ISelectHost.GetOptionDefaultSelected(DomElement option) =>
        GetElementRuntimeState(option).FormControl.DefaultSelected.TryGet(out var ds) && ds is true;

    void ISelectHost.SetOptionDefaultSelected(DomElement option, bool value) =>
        GetElementRuntimeState(option).FormControl.DefaultSelected.Set(value);
}
