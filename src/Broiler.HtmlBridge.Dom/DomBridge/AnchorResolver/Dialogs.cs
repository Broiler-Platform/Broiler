using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // -----------------------------------------------------------------
    // Dialog UA default positioning
    // -----------------------------------------------------------------

    /// <summary>
    /// Applies the UA default <c>position: fixed</c> to modal dialog elements
    /// that don't already have an explicit position, matching browser behaviour
    /// where top-layer elements are always treated as fixed-positioned.
    /// Must be called <em>before</em> anchor resolution so that anchor()
    /// function values are resolved with the correct positioning context.
    /// </summary>
    private void ApplyDialogUAPositioning(DomElement root)
    {
        foreach (var el in Elements)
        {
            if (!string.Equals(el.TagName, "dialog", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!el.Attributes.ContainsKey("open"))
                continue;
            if (!(GetElementRuntimeState(el).Dialog.Modal.TryGet(out var m) && m is true))
                continue;

            // Check if position is already set (inline or CSS).
            // position:absolute dialogs keep their author position so that
            // scroll simulation can shift them, matching Chromium behaviour.
            var props = GetComputedProps(el);
            if (props.TryGetValue("position", out var pos) &&
                (pos == "fixed" || pos == "absolute"))
                continue;

            // Set position:fixed as UA default for modal dialogs that have
            // no explicit position, matching Chromium's top-layer behaviour.
            el.Style["position"] = "fixed";
        }
    }
    // -----------------------------------------------------------------
    // Dialog backdrop insertion
    // -----------------------------------------------------------------

    private void InsertDialogBackdrops(DomElement root, int vpW, int vpH)
    {
        var modals = new List<(DomElement dialog, DomElement parent)>();
        FindModalDialogs(root, modals);

        foreach (var (dialog, parent) in modals)
        {
            // Collect ::backdrop CSS properties for this dialog element.
            // Look for selectors ending with "::backdrop" that would match
            // the dialog (e.g. "dialog::backdrop", "#target::backdrop").
            var backdropBg = GetBackdropBackground(dialog);

            // Insert a backdrop div BEFORE the dialog.
            // Use 'position: fixed' with explicit pixel viewport dimensions
            // because the Broiler renderer cannot resolve opposing insets.
            var backdropStyle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["position"] = "fixed",
                ["top"] = "0",
                ["left"] = "0",
                ["width"] = $"{vpW}px",
                ["height"] = $"{vpH}px",
                ["background-color"] = backdropBg,
            };

            var backdrop = new DomElement(
                "div", null, null, string.Empty,
                style: backdropStyle);
            backdrop.Parent = parent;

            int idx = parent.Children.IndexOf(dialog);
            if (idx >= 0)
                parent.Children.Insert(idx, backdrop);

            // Ensure the dialog has UA default styles.
            // Check both inline styles and CSS rules before applying defaults.
            var dialogProps = GetComputedProps(dialog);
            if (!dialog.Style.ContainsKey("display"))
                dialog.Style["display"] = "block";
            if (!dialog.Style.ContainsKey("border") &&
                !dialogProps.ContainsKey("border") &&
                !dialogProps.ContainsKey("border-width"))
            {
                dialog.Style["border-width"] = "1px";
                dialog.Style["border-style"] = "solid";
                dialog.Style["border-color"] = "black";
            }
            if (!dialog.Style.ContainsKey("padding") &&
                !dialogProps.ContainsKey("padding"))
                dialog.Style["padding"] = "1em";
            if (!dialog.Style.ContainsKey("background") &&
                !dialog.Style.ContainsKey("background-color") &&
                !dialogProps.ContainsKey("background") &&
                !dialogProps.ContainsKey("background-color"))
                dialog.Style["background-color"] = "white";
        }
    }
    /// <summary>
    /// Determines the background color for a dialog's <c>::backdrop</c>
    /// pseudo-element by checking CSS rules for <c>::backdrop</c> selectors
    /// that match the given dialog element.
    /// </summary>
    private string GetBackdropBackground(DomElement dialog)
    {
        // Default backdrop color: pre-composited rgba(0,0,0,0.1) over white.
        // Alpha-blending: 255*(1-0.1) + 0*0.1 = 229.5 ≈ 229.
        const string defaultBg = "rgb(229, 229, 229)";

        var declarations = GetSyncedScopedEngine(dialog)
            .GetCascadedDeclaredValues(dialog, "::backdrop");

        if (declarations.TryGetValue("background", out var bg))
        {
            if (string.Equals(bg.Trim(), "transparent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(bg.Trim(), "none", StringComparison.OrdinalIgnoreCase))
                return "transparent";
            return bg;
        }

        if (declarations.TryGetValue("background-color", out var bgColor))
        {
            if (string.Equals(bgColor.Trim(), "transparent", StringComparison.OrdinalIgnoreCase))
                return "transparent";
            return bgColor;
        }

        return defaultBg;
    }
    /// <summary>
    /// Checks whether an anchor element is accessible from a target element,
    /// according to CSS Anchor Positioning top-layer visibility rules.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Non-top-layer elements cannot anchor to top-layer elements.</item>
    /// <item>A top-layer element can only anchor to top-layer elements that
    /// were added to the top layer <em>before</em> it (lower order).</item>
    /// <item>Non-top-layer anchors are always accessible.</item>
    /// </list>
    /// </remarks>
    private static bool IsAnchorAccessible(DomElement? anchorElement, DomElement targetElement)
    {
        if (anchorElement == null) return true;

        bool anchorIsTopLayer =
            GetElementRuntimeState(anchorElement).Dialog.Modal.TryGet(out var am) && am is true;
        bool targetIsTopLayer =
            GetElementRuntimeState(targetElement).Dialog.Modal.TryGet(out var tm) && tm is true;

        if (!anchorIsTopLayer)
            return true; // Non-top-layer anchors are accessible from anywhere.

        if (!targetIsTopLayer)
            return false; // Non-top-layer target cannot see top-layer anchor.

        // Both are in top layer — anchor must have been added BEFORE the target.
        int anchorOrder = GetElementRuntimeState(anchorElement).Dialog.TopLayerOrder.TryGet(out var ao) && ao is int aoi ? aoi : 0;
        int targetOrder = GetElementRuntimeState(targetElement).Dialog.TopLayerOrder.TryGet(out var to) && to is int toi ? toi : 0;

        return anchorOrder < targetOrder;
    }
    private static void FindModalDialogs(DomElement element, List<(DomElement, DomElement)> results)
    {
        if (string.Equals(element.TagName, "dialog", StringComparison.OrdinalIgnoreCase) &&
            element.Attributes.ContainsKey("open") &&
            GetElementRuntimeState(element).Dialog.Modal.TryGet(out var isModal) &&
            isModal is bool modal && modal &&
            element.Parent != null)
        {
            results.Add((element, element.Parent));
        }

        foreach (var child in element.Children)
            FindModalDialogs(child, results);
    }
}
