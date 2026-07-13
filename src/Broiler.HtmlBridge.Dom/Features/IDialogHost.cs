using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The narrow bridge services the <see cref="DialogBinding"/> feature module needs (HtmlBridge
/// complexity-reduction roadmap Phase 3, P3.7). The dialog/popover JS API sets the element's
/// <c>open</c> attribute and a small amount of per-element browser-runtime state (modal flag,
/// popover-open flag, top-layer order, dialog return value) that today lives on the bridge's
/// <c>ElementRuntimeState</c>, and asks the renderer whether a hiding popover keeps its overlay.
/// These are exposed as named primitives so the feature module never reaches the runtime-state
/// object directly; the eventual TopLayerManager can re-home the state behind the same contract.
/// Backdrop/top-layer rendering stays in the bridge.
/// </summary>
internal interface IDialogHost
{
    /// <summary>Adds (<paramref name="open"/> true) or removes the boolean <c>open</c> attribute.</summary>
    void SetOpenAttribute(DomElement element, bool open);

    /// <summary>Whether <paramref name="element"/> currently has the <c>open</c> attribute.</summary>
    bool HasOpenAttribute(DomElement element);

    /// <summary>Invalidates the element's style scope so open/top-layer changes re-cascade.</summary>
    void InvalidateStyleScope(DomElement element);

    /// <summary>Assigns <paramref name="element"/> the next monotonic top-layer order (promotes it
    /// above previously promoted dialogs/popovers).</summary>
    void AssignNextTopLayerOrder(DomElement element);

    /// <summary>Sets or clears the dialog's modal flag.</summary>
    void SetDialogModal(DomElement element, bool modal);

    /// <summary>Sets or clears the element's popover-open flag.</summary>
    void SetPopoverOpen(DomElement element, bool open);

    /// <summary>The dialog's current <c>returnValue</c> (empty string if unset).</summary>
    string GetReturnValue(DomElement element);

    /// <summary>Sets the dialog's <c>returnValue</c>.</summary>
    void SetReturnValue(DomElement element, string value);

    /// <summary>Whether a hiding popover must stay in the top layer (mid-transition overlay per
    /// CSS Position §overlay) — a renderer decision.</summary>
    bool PopoverKeepsOverlayOnHide(DomElement element);
}
