using System.Runtime.CompilerServices;
using Broiler.HtmlBridge.Dom.Runtime;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // Phase 2 item 4 (de-globalization, 2026-07-17): the per-element dialog / popover top-layer state
    // (modal flag, top-layer order, popover-open flag) was the Dialog slot of the process-static
    // ElementRuntimeState table; it is now a per-bridge instance table, owned by the session's bridge.
    // Still element-keyed, so it GCs with the element and the cloneNode copy (see CloneDomElement) is
    // preserved. The former static TopLayerOrderOf / IsAnchorAccessible / FindModalDialogs /
    // FindOpenPopovers helpers became instance methods (all their callers were already on the bridge
    // instance), so no cross-class host threading was needed.
    private readonly ConditionalWeakTable<DomElement, DialogRuntimeState> _dialogRuntimeStates = [];

    private DialogRuntimeState DialogStateFor(DomElement element) =>
        _dialogRuntimeStates.GetValue(element, static _ => new DialogRuntimeState());

    // -----------------------------------------------------------------
    // Dialog UA default positioning
    // -----------------------------------------------------------------

    // CSS Position 4 §top-layer: benign marker the renderer's native top-layer paint pass keys
    // on (Broiler.Layout FragmentTreeBuilder → Fragment.TopLayerOrder → PaintWalker.PaintTopLayer,
    // patch 0010 — applied and pinned). The attribute value is the element's top-layer order; a
    // later-added element (higher order) paints over an earlier one. Stamping it lets the native
    // pass paint modal dialogs, open popovers, and ::backdrops above every ordinary stacking
    // context — the correct top-layer behaviour, superseding the approximate very-large-z-index
    // emulation (now written only on the retired NativeTopLayer-off rollback path).
    private const string TopLayerOrderAttr = "data-broiler-top-layer";

    // Native ::backdrop marker: the resolved backdrop background (UA modal/popover scrim default
    // folded with any author `background`) the renderer materialises into a native ::backdrop box
    // (Broiler.HTML DomParser, patch 0011 — applied and pinned). Stamped in NativeBackdrop mode;
    // the baked path inserts a styled <div> instead. The <div> path is still retained (not yet
    // deletable) because it carries author `::backdrop` position-try-fallbacks, which the native
    // path does not yet reproduce (see InsertDialogBackdrops).
    private const string BackdropBgAttr = "data-broiler-backdrop";

    private void StampTopLayerOrder(DomElement el, int order) =>
        SetAttr(el, TopLayerOrderAttr, order.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private int TopLayerOrderOf(DomElement el) =>
        DialogStateFor(el).TopLayerOrder.TryGet(out var o) && o is int oi ? oi : 0;

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
            if (!HasAttr(el, "open"))
                continue;
            if (!(DialogStateFor(el).Modal.TryGet(out var m) && m is true))
                continue;

            // Mark the open modal dialog as a top-layer box (native paint reads this; inert on
            // the baked path). Independent of the position-fixed default applied below.
            if (NativeTopLayer)
                StampTopLayerOrder(el, TopLayerOrderOf(el));

            // Check if position is already set (inline or CSS).
            // position:absolute dialogs keep their author position so that
            // scroll simulation can shift them, matching Chromium behaviour.
            var props = GetComputedProps(el);
            if (props.TryGetValue("position", out var pos) &&
                (pos == "fixed" || pos == "absolute"))
                continue;

            // Set position:fixed as UA default for modal dialogs that have
            // no explicit position, matching Chromium's top-layer behaviour.
            BakedInlineStyle(el)["position"] = "fixed";

            // HTML UA `dialog:modal { inset:0; margin:auto }` centring. With the box
            // fixed-positioned, both insets 0 and auto margins, the layout engine's
            // §10.3.7 / §10.6.4 auto-margin resolution centres a definite-size box in the
            // viewport (Broiler.Layout CssBox.ResolveOverconstrainedAutoMargins).
            ApplyModalCenteringDefaults(el);
        }
    }

    // The HTML UA `dialog:modal` inset/margin properties (checked both as their shorthands and
    // as longhands): any author declaration on these means the page positions the modal itself,
    // so the UA centring default must not fight it.
    private static readonly string[] ModalPositioningProps =
    [
        "inset", "inset-block", "inset-inline", "left", "right", "top", "bottom",
        "margin", "margin-block", "margin-inline",
        "margin-left", "margin-right", "margin-top", "margin-bottom",
    ];

    /// <summary>
    /// Applies the HTML user-agent <c>dialog:modal { inset:0; width:fit-content; height:fit-content;
    /// margin:auto }</c> centring default to a modal <c>&lt;dialog&gt;</c> the bridge just gave the UA
    /// <c>position:fixed</c>. The layout engine centres a box on an axis when it has a resolvable used
    /// size there and both opposing insets + auto margins (CSS2.1 §10.3.7/§10.6.4): the inline axis and
    /// definite heights are centred in-line during layout, and a content / intrinsic-keyword block size is
    /// centred by the engine's block-axis root post-pass (<c>CssBox.CenterOutOfFlowBlockAxis</c>) once the
    /// final height is known. Per axis: a modal with no author size gets the UA <c>fit-content</c> so it
    /// shrink-wraps to content and centres; an explicit or intrinsic author size centres as-is; an
    /// explicit author <c>auto</c> is left alone (with both insets it fills the viewport — no free space).
    /// The default is suppressed entirely when the author declares any inset/margin — the page owns the
    /// positioning then.
    /// </summary>
    private void ApplyModalCenteringDefaults(DomElement el)
    {
        var specified = BuildSpecifiedStyleMap(el);

        foreach (var prop in ModalPositioningProps)
            if (specified.ContainsKey(prop))
                return;

        if (ResolveModalAxisCentres(el, specified, "width"))
        {
            BakedInlineStyle(el)["left"] = "0";
            BakedInlineStyle(el)["right"] = "0";
            BakedInlineStyle(el)["margin-left"] = "auto";
            BakedInlineStyle(el)["margin-right"] = "auto";
        }

        if (ResolveModalAxisCentres(el, specified, "height"))
        {
            BakedInlineStyle(el)["top"] = "0";
            BakedInlineStyle(el)["bottom"] = "0";
            BakedInlineStyle(el)["margin-top"] = "auto";
            BakedInlineStyle(el)["margin-bottom"] = "auto";
        }
    }

    // Decides whether the modal's axis can be centred, applying the UA <c>fit-content</c> default when the
    // author gave no size so the box shrink-wraps to content. Returns false only when the author explicitly
    // set the axis to <c>auto</c> — with both insets that fills the containing block, leaving no free space
    // for the auto margins to distribute.
    private bool ResolveModalAxisCentres(DomElement el, Dictionary<string, string> specified, string sizeProperty)
    {
        if (!specified.TryGetValue(sizeProperty, out var value) || string.IsNullOrWhiteSpace(value))
        {
            BakedInlineStyle(el)[sizeProperty] = "fit-content"; // UA dialog:modal shrink-to-fit default
            return true;
        }

        return !string.Equals(value.Trim(), "auto", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies UA top-layer positioning to open popovers (HTML §popover): a
    /// popover is laid out in the top layer as a fixed-position box, so give one
    /// with no explicit position <c>position: fixed</c> anchored at the viewport
    /// origin. Later-shown popovers keep their source order, so they paint over
    /// earlier ones — matching the top-layer stacking these overlay tests probe.
    /// </summary>
    // Base z-index for the synthetic top layer. The real top layer sits above
    // every painted stacking context (CSS Position §top-layer); Broiler has no
    // dedicated top-layer paint pass, so approximate it with a very large
    // z-index offset by each element's top-layer order, keeping open popovers
    // above ordinary positioned content and correctly ordered amongst themselves
    // (a later-shown popover paints over an earlier one). Kept below int.MaxValue
    // so the counter has headroom.
    private const int TopLayerZIndexBase = 2_000_000_000;

    private void ApplyPopoverUAPositioning(DomElement root)
    {
        foreach (var el in Elements)
        {
            if (!(DialogStateFor(el).PopoverOpen.TryGet(out var open) && open is true))
                continue;

            var props = GetComputedProps(el);
            bool alreadyPositioned = props.TryGetValue("position", out var pos) &&
                (pos == "fixed" || pos == "absolute");

            if (!alreadyPositioned)
            {
                BakedInlineStyle(el)["position"] = "fixed";
                if (!BakedInlineStyle(el).ContainsKey("top") && !props.ContainsKey("top"))
                    BakedInlineStyle(el)["top"] = "0";
                if (!BakedInlineStyle(el).ContainsKey("left") && !props.ContainsKey("left"))
                    BakedInlineStyle(el)["left"] = "0";
            }

            // Elevate into the top layer, ordered by show order, so the popover paints above
            // non-top-layer content (e.g. a plain position:fixed sibling) and later popovers paint over
            // earlier ones. Native path: the renderer's top-layer paint pass (patch 0010, pinned) keys on
            // the `data-broiler-top-layer` marker and lifts the box out of normal stacking. The very-large
            // z-index is the older approximate emulation, now needed only on the retired baked
            // (NativeTopLayer-off) rollback path — so the two are mutually exclusive rather than both.
            int order = TopLayerOrderOf(el);
            if (NativeTopLayer)
                StampTopLayerOrder(el, order);
            else
                BakedInlineStyle(el)["z-index"] = (TopLayerZIndexBase + order).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
    // -----------------------------------------------------------------
    // Dialog backdrop insertion
    // -----------------------------------------------------------------

    private void InsertDialogBackdrops(
        DomElement root, int vpW, int vpH,
        Dictionary<string, AnchorInfo> anchorRegistry,
        Dictionary<string, Dictionary<string, string>> positionTryRules)
    {
        var modals = new List<(DomElement dialog, DomElement parent, bool isPopover)>();
        FindModalDialogs(root, modals);
        FindOpenPopovers(root, modals);

        foreach (var (dialog, parent, isPopover) in modals)
        {
            // Collect ::backdrop CSS properties for this element. Look for
            // selectors ending with "::backdrop" that would match it (e.g.
            // "dialog::backdrop", "[popover]::backdrop", "#target::backdrop").
            // A modal dialog's ::backdrop defaults to the UA dimming scrim; a
            // popover's ::backdrop defaults to transparent (no scrim) — either
            // is overridden by an author `background`/`background-color`.
            var backdropBg = GetBackdropBackground(
                dialog, isPopover ? "transparent" : "rgb(229, 229, 229)");

            if (NativeBackdrop)
            {
                // Native ::backdrop: don't mutate the box tree with a synthesized <div>. Stamp the
                // resolved backdrop background on the element and let the renderer generate the
                // ::backdrop box natively (Broiler.HTML DomParser, patch 0011) as a top-layer box
                // beneath the dialog. The resolved background already folds the UA modal/popover
                // scrim default with any author `background` (which the renderer cannot decide
                // without the modal/popover runtime state); the renderer overlays author ::backdrop
                // *geometry* from the ::backdrop cascade. Author position-try-fallbacks on a
                // ::backdrop are not yet carried natively (no corpus); the baked path below still
                // handles them.
                SetAttr(dialog, BackdropBgAttr, backdropBg);
            }
            else
            {
                // Insert a backdrop div BEFORE the dialog.
                // Use 'position: fixed' with explicit pixel viewport dimensions
                // because the Broiler renderer cannot resolve opposing insets.
                // These viewport-covering defaults materialise the ::backdrop UA
                // style (position:fixed; inset:0); any author-declared geometry
                // overlaid below overrides them, so an explicitly sized/positioned
                // backdrop is honoured instead of always filling the viewport.
                var backdropStyle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["position"] = "fixed",
                    ["top"] = "0",
                    ["left"] = "0",
                    ["width"] = $"{vpW}px",
                    ["height"] = $"{vpH}px",
                    ["background-color"] = backdropBg,
                };

                var backdropDecls = GetSyncedScopedEngine(dialog)
                    .GetCascadedDeclaredValues(dialog, "::backdrop");
                OverlayBackdropAuthorGeometry(backdropDecls, backdropStyle);

                var backdrop = CreateBridgeElement("div");
                foreach (var kv in backdropStyle)
                    BakedInlineStyle(backdrop)[kv.Key] = kv.Value;
                SetParent(backdrop, parent);

                int idx = ChildIndexOf(parent, dialog);
                if (idx >= 0)
                    InsertChildAt(parent, idx, backdrop);

                // If the ::backdrop declares position-try-fallbacks and its base
                // geometry overflows the containing block, resolve the fallback
                // now: the main position-try pass (ResolvePositionTryFallbacks) ran
                // before this backdrop div existed, so it never saw it.
                if (backdropStyle.ContainsKey("position-try-fallbacks") ||
                    backdropStyle.ContainsKey("position-try"))
                {
                    ResolvePositionTryFallbacksTree(backdrop, anchorRegistry, positionTryRules);
                }
            }

            // The modal <dialog> box chrome (display:block, border:1px solid black, padding:1em,
            // white background) is no longer baked here — the native UA rule
            // `dialog { display: block; border: 1px solid black; padding: 1em; background-color: white }`
            // (Broiler.HTML CssDefaults, patches 0001+0002 + the box-chrome patch 0004, all applied and
            // pinned) supplies it through the real cascade, with the shorthand-vs-longhand origin fix so an
            // author reset still wins. Popovers never had UA box chrome (author CSS only). So nothing
            // dialog-specific remains in this loop past the backdrop handling above.
        }
    }
    /// <summary>
    /// Property names on a <c>::backdrop</c> rule that control the backdrop's
    /// geometry and fallback positioning. When the author declares any of
    /// these they override the viewport-covering defaults so an explicitly
    /// sized or positioned backdrop is honoured (e.g. WPT
    /// <c>position-try-backdrop.html</c>, where the backdrop is a 100×100 box
    /// moved by <c>position-try-fallbacks</c>).
    /// </summary>
    private static readonly string[] BackdropGeometryProps =
    [
        "width", "height", "left", "right", "top", "bottom",
        "position", "position-anchor", "position-try-fallbacks", "position-try",
    ];

    /// <summary>
    /// Overlays author-declared <c>::backdrop</c> geometry / fallback
    /// properties onto the synthesized backdrop div's style, replacing the
    /// viewport-covering defaults where the author was explicit.
    /// </summary>
    private static void OverlayBackdropAuthorGeometry(
        IReadOnlyDictionary<string, string> declarations,
        Dictionary<string, string> backdropStyle)
    {
        foreach (var prop in BackdropGeometryProps)
        {
            if (declarations.TryGetValue(prop, out var value) &&
                !string.IsNullOrWhiteSpace(value))
                backdropStyle[prop] = value.Trim();
        }

        // The default fills the viewport with top:0/left:0 + width/height. If
        // the author positions the backdrop from the opposite edge only, drop
        // the conflicting default inset so the box is not over-constrained
        // (the renderer cannot resolve opposing left+right / top+bottom insets).
        if (declarations.ContainsKey("right") && !declarations.ContainsKey("left"))
            backdropStyle.Remove("left");
        if (declarations.ContainsKey("bottom") && !declarations.ContainsKey("top"))
            backdropStyle.Remove("top");
    }

    /// <summary>
    /// Determines the background color for a dialog's <c>::backdrop</c>
    /// pseudo-element by checking CSS rules for <c>::backdrop</c> selectors
    /// that match the given dialog element.
    /// </summary>
    private string GetBackdropBackground(DomElement dialog, string defaultBg = "rgb(229, 229, 229)")
    {
        // Default modal-dialog backdrop color: pre-composited rgba(0,0,0,0.1) over
        // white (255*(1-0.1) + 0*0.1 = 229.5 ≈ 229). Callers pass "transparent"
        // for popovers, whose ::backdrop has no UA scrim.

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
    private bool IsAnchorAccessible(DomElement? anchorElement, DomElement targetElement)
    {
        if (anchorElement == null) return true;

        bool anchorIsTopLayer =
            DialogStateFor(anchorElement).Modal.TryGet(out var am) && am is true;
        bool targetIsTopLayer =
            DialogStateFor(targetElement).Modal.TryGet(out var tm) && tm is true;

        if (!anchorIsTopLayer)
            return true; // Non-top-layer anchors are accessible from anywhere.

        if (!targetIsTopLayer)
            return false; // Non-top-layer target cannot see top-layer anchor.

        // Both are in top layer — anchor must have been added BEFORE the target.
        int anchorOrder = DialogStateFor(anchorElement).TopLayerOrder.TryGet(out var ao) && ao is int aoi ? aoi : 0;
        int targetOrder = DialogStateFor(targetElement).TopLayerOrder.TryGet(out var to) && to is int toi ? toi : 0;

        return anchorOrder < targetOrder;
    }
    private void FindModalDialogs(DomElement element, List<(DomElement, DomElement, bool)> results)
    {
        if (string.Equals(element.TagName, "dialog", StringComparison.OrdinalIgnoreCase) &&
            HasAttr(element, "open") &&
            DialogStateFor(element).Modal.TryGet(out var isModal) &&
            isModal is bool modal && modal &&
            ParentEl(element) != null)
        {
            results.Add((element, ParentEl(element), false));
        }

        // Snapshot before recursing: the live child list can be mutated mid-walk
        // (concurrent/lazy DOM edit) and throw, aborting the walk. SnapshotChildren
        // tolerates that — same idiom as the other anchor-resolver tree walks.
        foreach (var child in SnapshotChildren(element))
            FindModalDialogs(child, results);
    }

    // Popover API (HTML §popover): an element whose showPopover() ran (and whose
    // hidePopover() did not tear it down — see PopoverKeepsOverlayOnHide) is in
    // the top layer and generates a ::backdrop, just like a modal dialog.
    private void FindOpenPopovers(DomElement element, List<(DomElement, DomElement, bool)> results)
    {
        if (ParentEl(element) != null &&
            DialogStateFor(element).PopoverOpen.TryGet(out var open) &&
            open is true)
        {
            results.Add((element, ParentEl(element), true));
        }

        foreach (var child in SnapshotChildren(element))
            FindOpenPopovers(child, results);
    }

    // CSS Position §overlay: whether hiding this popover leaves it in the top
    // layer because its `overlay` is being transitioned out with
    // `transition-behavior: allow-discrete`. A static render snapshots
    // mid-transition, so such a popover (and its ::backdrop) stays rendered.
    private bool PopoverKeepsOverlayOnHide(DomElement element)
    {
        var props = GetComputedProps(element);

        // allow-discrete is required for a discrete property like `overlay` to
        // participate in a transition at all. It lives on transition-behavior;
        // tolerate it also appearing folded into a `transition` shorthand.
        string behavior =
            props.GetValueOrDefault("transition-behavior", string.Empty) + " " +
            props.GetValueOrDefault("transition", string.Empty);
        if (behavior.IndexOf("allow-discrete", StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        // The transitioned property list must include `overlay` (or `all`).
        string transitioned =
            props.GetValueOrDefault("transition-property", string.Empty) + " " +
            props.GetValueOrDefault("transition", string.Empty);
        foreach (var token in transitioned.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("overlay", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("all", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
