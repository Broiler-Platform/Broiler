
using Broiler.CSS;
using Broiler.Dom;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.HtmlBridge.Logging;

namespace Broiler.HtmlBridge;

/// <summary>
/// CSS View Transitions (Level 1/2) — the <c>document.startViewTransition()</c> entry point plus
/// the static-screenshot subset of its rendering. A view transition runs an author callback that
/// mutates the DOM, then paints a top-layer tree of <c>::view-transition-*</c> pseudo-elements that
/// snapshot the old and new states of every element carrying a <c>view-transition-name</c>.
/// <para>
/// A live browser animates that pseudo tree. WPT reftests instead pause the animations and pin the
/// old/new opacities so the screenshot is a deterministic still (e.g. the new snapshot at
/// <c>opacity:1</c> over an author-coloured <c>::view-transition</c> backdrop, at the outgoing
/// element's position). This partial reproduces that still: it snapshots each named element's
/// geometry before the callback (the "old" capture) and after (the "new"), applies the
/// <c>:active-view-transition-type()</c> conditional rules a transition activates, and materialises
/// the <c>::view-transition</c> overlay tree as real positioned boxes the renderer already knows how
/// to paint. The animation timeline itself is out of scope; the tests that need it are the ones that
/// screenshot mid-animation with unpinned timing.
/// </para>
/// <para>
/// The pseudo-tree bake remains separate from <c>ApplySerializationTransforms</c> because capturing
/// old geometry probes layout during the script. <see cref="ApplyViewTransitionRendering"/> runs on
/// each fresh serialize/render projection after the compatibility transforms and skips
/// geometry-snapshot passes (<see cref="_layoutGeometryPassActive"/>).
/// </para>
/// </summary>
public sealed partial class DomBridge
{
    /// <summary>The active view transition, or <c>null</c> when none is running.</summary>
    private ViewTransitionState? _activeViewTransition;

    private sealed class ViewTransitionState
    {
        /// <summary>The transition's active types (the <c>types</c> option), matched by
        /// <c>:active-view-transition-type()</c>.</summary>
        public HashSet<string> Types { get; } = new(System.StringComparer.Ordinal);

        /// <summary>The "old" capture: geometry and background of each named element as it stood
        /// before the update callback ran, keyed by <c>view-transition-name</c>. The group is
        /// positioned at this start geometry, which the reftests freeze it at.</summary>
        public Dictionary<string, NamedSnapshot> OldCaptures { get; } = new(System.StringComparer.Ordinal);

        /// <summary>Generated used values for elements whose <c>view-transition-name</c> is
        /// <c>auto</c>/<c>match-element</c> (css-view-transitions-2). Keyed by element identity so the
        /// same element resolves to the same name across the old and new captures — the two snapshots
        /// must pair into one group — and stays stable for the transition's lifetime.</summary>
        public Dictionary<DomElement, string> AutoNames { get; } = new();
    }

    private readonly record struct NamedSnapshot(
        double Left, double Top, double Width, double Height, string BackgroundColor,
        // A detached box reproducing the element's painted border box (computed paint style baked
        // inline + a clone of its content), or null for the implicit root capture (never shown as a
        // content snapshot in the reftests) and for a name present on only one side.
        DomElement? Content = null);

    // :active-view-transition-type( a, b, … ) anywhere in a selector.
    private static readonly System.Text.RegularExpressions.Regex ActiveViewTransitionType =
        new(@":active-view-transition-type\(\s*([^)]*)\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    // The bare :active-view-transition pseudo-class (css-view-transitions-2) — matches the document
    // element whenever a view transition is active, regardless of type. The negative lookahead keeps
    // it from also matching the :active-view-transition-type(…) functional form handled above.
    private static readonly System.Text.RegularExpressions.Regex ActiveViewTransitionBare =
        new(@":active-view-transition(?!-type)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    // html::view-transition[-group|-image-pair|-old|-new]( name ) — the pseudo and its optional
    // name/class/`*` argument. The leading originating selector (html / :root / *) is ignored: the
    // pseudo tree always originates from the document element.
    private static readonly System.Text.RegularExpressions.Regex ViewTransitionPseudo =
        new(@"::view-transition(?:-(group-children|group|image-pair|old|new))?\s*(?:\(\s*([^)]*)\s*\))?\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    // ── JS API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>document.startViewTransition(updateCallback)</c> /
    /// <c>document.startViewTransition({ update, types })</c>. Snapshots the old state, records the
    /// active transition and its types, runs the update callback synchronously (its DOM mutation is
    /// the "new" state the screenshot captures), and returns a <c>ViewTransition</c> whose
    /// <c>ready</c>/<c>finished</c>/<c>updateCallbackDone</c> promises are already resolved — the
    /// reftests gate their screenshot on <c>ready</c>, so resolving synchronously lets that fire once
    /// the new DOM is in place.
    /// </summary>
    internal JSValue StartViewTransition(in Arguments a)
    {
        var state = new ViewTransitionState();
        JSFunction? updateCallback = null;

        if (a.Length > 0)
        {
            if (a[0] is JSFunction fn)
            {
                updateCallback = fn;
            }
            else if (a[0] is JSObject options)
            {
                if (options[(KeyString)"update"] is JSFunction updateFn)
                    updateCallback = updateFn;
                CollectViewTransitionTypes(options[(KeyString)"types"], state.Types);
            }
        }

        _activeViewTransition = state;

        // The type activates immediately, for the whole transition including the old capture (WPT
        // view-transition-types-match-early: it activates "before tag discovery"). Bake its rules
        // now so the old snapshot reflects them, then snapshot the old state before the callback
        // mutates the DOM. Both are best-effort: a probe this early must never abort the call, and
        // the pseudo-tree bake is still deferred to serialize time (the geometry probe here runs a
        // render snapshot but ApplyViewTransitionRendering skips geometry passes).
        try
        {
            ApplyActiveViewTransitionTypeRules(DocumentElement);
            CaptureOldViewTransitionState(state);
        }
        catch (System.Exception ex)
        {
            RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.startViewTransition",
                $"Old view-transition capture failed: {ex.Message}", ex);
        }

        if (updateCallback is not null)
        {
            try
            {
                updateCallback.InvokeFunction(new Arguments(updateCallback));
            }
            catch (System.Exception ex)
            {
                RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.startViewTransition",
                    $"View transition update callback threw: {ex.Message}", ex);
            }
        }

        return BuildViewTransitionObject(state);
    }

    /// <summary>Reads the <c>types</c> option — a JS array/iterable of strings — into
    /// <paramref name="into"/>. Absent or non-array values contribute nothing.</summary>
    private static void CollectViewTransitionTypes(JSValue? types, HashSet<string> into)
    {
        if (types is not JSObject arrayLike)
            return;

        var lengthValue = arrayLike[(KeyString)"length"];
        if (lengthValue is null || lengthValue.IsUndefined)
            return;

        var length = (int)lengthValue.DoubleValue;
        for (var i = 0; i < length; i++)
        {
            var item = arrayLike[(uint)i];
            if (item is not null && !item.IsUndefined && !item.IsNull)
                into.Add(item.ToString());
        }
    }

    private JSObject BuildViewTransitionObject(ViewTransitionState state)
    {
        var transition = new JSObject();
        transition.FastAddValue((KeyString)"ready", ResolvedThenable(), JSPropertyAttributes.EnumerableConfigurableValue);
        // `finished` resolving means the transition is over: the ::view-transition tree has been
        // removed and the DOM is back to its plain final state. A reftest that screenshots from
        // finished (rather than ready) therefore expects the final DOM, not the pseudo tree — so
        // realizing this thenable clears the active transition, and the serialize-time bake becomes
        // a no-op (WPT element-stops-grouping-after-animation).
        transition.FastAddValue((KeyString)"finished", FinishedThenable(), JSPropertyAttributes.EnumerableConfigurableValue);
        transition.FastAddValue((KeyString)"updateCallbackDone", ResolvedThenable(), JSPropertyAttributes.EnumerableConfigurableValue);

        var typesArray = new JavaScript.BuiltIns.Array.JSArray();
        foreach (var type in state.Types)
            typesArray.Add(new JavaScript.BuiltIns.String.JSString(type));
        transition.FastAddValue((KeyString)"types", typesArray, JSPropertyAttributes.EnumerableConfigurableValue);

        // skipTransition() ends the transition without animating; the still is already the final
        // state here, so it is a no-op beyond clearing the active state.
        transition.FastAddValue((KeyString)"skipTransition",
            new JSFunction((in _) => { _activeViewTransition = null; return JSUndefined.Value; }, "skipTransition", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return transition;
    }

    /// <summary>A minimal already-resolved thenable, mirroring the bridge's synchronous-promise
    /// pattern (see FetchBinding): <c>then</c> invokes its callback immediately with
    /// <c>undefined</c> and returns a thenable so <c>.then().then()</c> chains, and the rAF the
    /// reftests schedule from it is pumped by the event loop as usual.</summary>
    private static JSObject ResolvedThenable()
    {
        var thenable = new JSObject();
        JSValue Then(in Arguments args)
        {
            if (args.Length > 0 && args[0] is JSFunction cb)
            {
                try { cb.InvokeFunction(new Arguments(cb, JSUndefined.Value)); }
                catch (System.Exception ex)
                {
                    RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.viewTransition.then",
                        $"View transition promise callback threw: {ex.Message}", ex);
                }
            }
            return thenable;
        }
        thenable.FastAddValue((KeyString)"then", new JSFunction(Then, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        thenable.FastAddValue((KeyString)"catch", new JSFunction((in _) => thenable, "catch", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        thenable.FastAddValue((KeyString)"finally",
            new JSFunction((in a) => { if (a.Length > 0 && a[0] is JSFunction cb) { try { cb.InvokeFunction(new Arguments(cb)); } catch { } } return thenable; }, "finally", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        return thenable;
    }

    /// <summary>The <c>finished</c> promise as an already-resolved thenable that first marks the
    /// transition complete — clearing <see cref="_activeViewTransition"/> so the serialize-time pseudo
    /// tree bake is skipped and the plain final DOM renders — then, like <see cref="ResolvedThenable"/>,
    /// invokes the callback (e.g. the reftest's <c>takeScreenshot</c>) and chains.</summary>
    private JSObject FinishedThenable()
    {
        // reftest-wait is removed by the reftest's takeScreenshot(). If the finished callback is the
        // one that removes it, the screenshot is being taken from `finished` — the transition is over
        // and the still must be the plain final DOM, so clear the active transition (the serialize-time
        // bake becomes a no-op; WPT element-stops-grouping-after-animation). If the callback only did
        // cleanup and left reftest-wait pending (the screenshot comes later from a `ready` chain — WPT
        // css-view-transitions-2 nested tests via compute-test.js), keep the transition so the pseudo
        // tree still bakes.
        bool ScreenshotPending() =>
            (GetAttr(DocumentElement, "class") ?? string.Empty)
                .Contains("reftest-wait", System.StringComparison.Ordinal);

        void RunAndMaybeFinish(JSFunction? cb)
        {
            bool waitBefore = ScreenshotPending();
            if (cb is not null)
            {
                try { cb.InvokeFunction(new Arguments(cb, JSUndefined.Value)); }
                catch (System.Exception ex)
                {
                    RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.viewTransition.finished.then",
                        $"View transition finished callback threw: {ex.Message}", ex);
                }
            }
            if (waitBefore && !ScreenshotPending())
                _activeViewTransition = null;
        }

        var thenable = new JSObject();
        JSValue Then(in Arguments args)
        {
            RunAndMaybeFinish(args.Length > 0 ? args[0] as JSFunction : null);
            return thenable;
        }
        thenable.FastAddValue((KeyString)"then", new JSFunction(Then, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        thenable.FastAddValue((KeyString)"catch", new JSFunction((in _) => thenable, "catch", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        thenable.FastAddValue((KeyString)"finally",
            new JSFunction((in a) => { RunAndMaybeFinish(a.Length > 0 ? a[0] as JSFunction : null); return thenable; }, "finally", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
        return thenable;
    }

    // ── Serialize-time rendering ────────────────────────────────────────────

    /// <summary>
    /// Renders a running view transition: applies the <c>:active-view-transition-type()</c> rules it
    /// activates and materialises the <c>::view-transition</c> pseudo tree. Invoked from the
    /// serialize/render entry points after the compatibility serialization transforms, so its own layout
    /// probes (already spent capturing the old state) cannot swallow it. Skips geometry-snapshot
    /// passes so a mid-script <c>getBoundingClientRect()</c> never bakes the overlay. Each call
    /// operates on a fresh projection, so repeated renders stay idempotent without a live-tree guard.
    /// </summary>
    private void ApplyViewTransitionRendering(DomElement root)
    {
        if (_activeViewTransition is null || _layoutGeometryPassActive)
            return;

        ApplyActiveViewTransitionTypeRules(root);
        ApplyViewTransitionPseudoTree(root);
    }

    /// <summary>Records the geometry and background of every element with a used
    /// <c>view-transition-name</c> (plus the implicit root) as it stands now — the "old" snapshot
    /// captured before the update callback runs.</summary>
    private void CaptureOldViewTransitionState(ViewTransitionState state)
    {
        var rootStyle = UsedStyleForCapture(DocumentElement);
        if (!IsExplicitNoneName(rootStyle.GetValueOrDefault("view-transition-name")))
        {
            var (l, t, w, h) = GetBoundingClientRectForDomElement(DocumentElement, isRoot: true);
            state.OldCaptures["root"] = new NamedSnapshot(l, t, w, h,
                rootStyle.GetValueOrDefault("background-color") ?? "transparent");
        }

        foreach (var element in DocumentElement.Descendants().OfType<DomElement>())
        {
            var style = UsedStyleForCapture(element);
            var name = ResolveUsedViewTransitionName(element, style.GetValueOrDefault("view-transition-name"));
            if (name is null || string.Equals(name, "root", System.StringComparison.Ordinal))
                continue;

            var (l, t, w, h) = GetBoundingClientRectForDomElement(element, isRoot: false);
            // Snapshot the old content now, before the update callback mutates (or removes) the
            // element — the "old" image must show its pre-callback state.
            state.OldCaptures[name] = new NamedSnapshot(l, t, w, h,
                style.GetValueOrDefault("background-color") ?? "transparent",
                BuildViewTransitionSnapshotContent(element));
        }
    }

    /// <summary>
    /// Applies the author rules a running transition activates to the live DOM, so the "new" snapshot
    /// the pseudo tree captures reflects them. Two selector forms are handled (css-view-transitions-2):
    /// <c>:active-view-transition-type(type)</c>, gated on the transition's active types, and the bare
    /// <c>:active-view-transition</c> pseudo-class, which matches whenever any transition is active.
    /// Each matching rule is re-matched with the pseudo stripped out and its declarations baked onto
    /// the matched elements.
    /// </summary>
    private void ApplyActiveViewTransitionTypeRules(DomElement root)
    {
        foreach (var (selectorText, declarations) in EnumerateAuthorStyleRules(root))
        {
            string stripped;

            var typeMatch = ActiveViewTransitionType.Match(selectorText);
            if (typeMatch.Success)
            {
                if (!AnyTypeActive(typeMatch.Groups[1].Value, _activeViewTransition!.Types))
                    continue;
                stripped = ActiveViewTransitionType.Replace(selectorText, string.Empty).Trim();
            }
            else if (ActiveViewTransitionBare.IsMatch(selectorText))
            {
                // The bare pseudo-class is active for the whole transition, whatever its types.
                stripped = ActiveViewTransitionBare.Replace(selectorText, string.Empty).Trim();
            }
            else
            {
                continue;
            }

            // Strip the pseudo-class; an empty resulting compound (the pseudo stood alone on the
            // originating element) selects the document element itself.
            if (stripped.Length == 0)
                stripped = "html";

            foreach (var element in root.Descendants().OfType<DomElement>())
            {
                if (MatchesSelector(element, stripped, null))
                {
                    foreach (var declaration in declarations.Declarations)
                        BakedInlineStyle(element)[declaration.Name] = declaration.Value.Text;
                }
            }
        }
    }

    private static bool AnyTypeActive(string argumentList, HashSet<string> activeTypes)
    {
        foreach (var raw in argumentList.Split(','))
        {
            if (activeTypes.Contains(raw.Trim()))
                return true;
        }
        return false;
    }

    /// <summary>
    /// The used <c>view-transition-group</c> (css-view-transitions-2) of an element — the last
    /// author declaration whose selector matches. It is not part of the computed-style projection,
    /// so it is read directly from the matched author rules. Returns <c>null</c> for the initial
    /// value (absent / <c>normal</c>), i.e. a top-level group.
    /// </summary>
    private string? ResolveViewTransitionGroupValue(DomElement element, DomElement root)
    {
        string? value = null;
        foreach (var (selectorText, declarations) in EnumerateAuthorStyleRules(root))
        {
            var declared = declarations.Declarations
                .LastOrDefault(d => d.Name.Equals("view-transition-group", System.StringComparison.OrdinalIgnoreCase));
            if (declared is null)
                continue;
            if (MatchesSelector(element, selectorText, null))
                value = declared.Value.Text.Trim();
        }

        if (string.IsNullOrEmpty(value)
            || value.Equals("normal", System.StringComparison.OrdinalIgnoreCase)
            || value.Equals("none", System.StringComparison.OrdinalIgnoreCase))
            return null;
        return value;
    }

    /// <summary>
    /// The parent group name a captured element's group nests under (css-view-transitions-2
    /// <c>view-transition-group</c>):
    /// <list type="bullet">
    /// <item>a <c>&lt;custom-ident&gt;</c> nests under the group of that name (self-reference and
    /// unresolved names fall back to the flat layout);</item>
    /// <item><c>nearest</c> under the nearest ancestor captured element's group;</item>
    /// <item><c>normal</c> (the initial value) and <c>contain</c> nest under the nearest ancestor
    /// that is a <em>containing</em> group — a captured element whose own <c>view-transition-group</c>
    /// is <c>contain</c>.</item>
    /// </list>
    /// Returns <c>null</c> (a top-level group directly under <c>::view-transition</c>) when nothing
    /// resolves, so the flat VT1 layout is the default.
    /// </summary>
    private string? ResolveGroupParentName(
        DomElement element, string name, IReadOnlyDictionary<string, DomElement> elementByName, DomElement root)
    {
        var value = ResolveViewTransitionGroupValue(element, root);

        if (value is not null && value.Equals("nearest", System.StringComparison.OrdinalIgnoreCase))
            return NearestCapturedAncestorName(element, name, elementByName, root, requireContain: false);

        // normal (null) and contain both nest under the nearest *containing* ancestor group — an
        // ancestor whose used view-transition-group is `contain`. `contain` additionally makes this
        // element a container for its own descendants (handled when they resolve their parent). With
        // no containing ancestor the group stays flat (the VT1 default).
        if (value is null || value.Equals("contain", System.StringComparison.OrdinalIgnoreCase))
            return NearestCapturedAncestorName(element, name, elementByName, root, requireContain: true);

        // An explicit <custom-ident>: nest under that group when it is itself captured. A group
        // cannot reference its own name (css-view-transitions-2: a self-reference is invalid and the
        // group falls back to the flat `normal` layout) — WPT compute-explicit-name-self.
        if (string.Equals(value, name, System.StringComparison.Ordinal))
            return null;
        return elementByName.ContainsKey(value) || value == "root" ? value : null;
    }

    /// <summary>
    /// Walks the ancestor chain for the nearest captured element with a view-transition-name.
    /// When <paramref name="requireContain"/> is set, only an ancestor whose own
    /// <c>view-transition-group</c> is <c>contain</c> qualifies (the containing-group parent for
    /// <c>normal</c>/<c>contain</c> children); otherwise any captured ancestor qualifies (the
    /// <c>nearest</c> keyword). Returns <c>null</c> when no ancestor qualifies.
    /// </summary>
    private string? NearestCapturedAncestorName(
        DomElement element, string name, IReadOnlyDictionary<string, DomElement> elementByName,
        DomElement root, bool requireContain)
    {
        for (var p = element.ParentNode; p != null; p = p.ParentNode)
        {
            if (p is not DomElement ancestor)
                continue;
            var ancestorName = ResolveUsedViewTransitionName(
                ancestor, UsedStyleForCapture(ancestor).GetValueOrDefault("view-transition-name"));
            if (ancestorName is null || string.Equals(ancestorName, name, System.StringComparison.Ordinal)
                || !(ancestorName == "root" || elementByName.ContainsKey(ancestorName)))
                continue;

            if (requireContain)
            {
                var ancestorGroup = ResolveViewTransitionGroupValue(ancestor, root);
                if (ancestorGroup is null || !ancestorGroup.Equals("contain", System.StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            return ancestorName;
        }
        return null;
    }

    /// <summary>
    /// Materialises the <c>::view-transition</c> pseudo tree as real positioned boxes. Each captured
    /// name gets a group box at its old geometry (the frozen animation start) holding old and new
    /// snapshot boxes; author <c>::view-transition*</c> declarations are applied to the corresponding
    /// boxes. The whole tree hangs off an overlay painted above the page (author
    /// <c>::view-transition</c> declarations, e.g. a backdrop colour).
    /// </summary>
    private void ApplyViewTransitionPseudoTree(DomElement root)
    {
        var pseudoRules = CollectViewTransitionPseudoDeclarations(root);
        var captures = CollectViewTransitionCaptures(root);

        // A view transition that captured nothing (no element carries a used
        // view-transition-name — e.g. `:root { view-transition-name: none }` with no other
        // named element) finishes immediately, so its ::view-transition tree is already gone by
        // the reftests' screenshot time and the root overlay must not paint — UNLESS an author
        // animation on the bare ::view-transition pins it open. WPT `no-named-elements` freezes it
        // with `::view-transition { animation: no-op 300s }` and its reference is the blue overlay
        // filling the viewport; `nothing-captured` has no such animation, so its
        // `::view-transition { background: red }` must stay hidden. Approximate that timing
        // distinction here: with no captures, bake the (group-less) overlay only when the root
        // pseudo was kept alive, otherwise skip so the page renders unmodified.
        if (captures.Count == 0 && !HasRootOverlayKeepAliveAnimation(pseudoRules))
            return;

        var overlay = CreateStyledBox(BaseStyle(
            ("position", "fixed"), ("left", "0"), ("top", "0"),
            ("width", "100vw"), ("height", "100vh"),
            ("z-index", "2147483646"), ("pointer-events", "none")), LookupPseudo(pseudoRules, "", null));
        SetAttr(overlay, "data-broiler-view-transition", "");

        // Map each captured (non-root) name to its new-side element, so a group's
        // `view-transition-group` (and the ancestry `nearest` walks) can be resolved.
        var elementByName = new Dictionary<string, DomElement>(System.StringComparer.Ordinal);
        foreach (var element in root.Descendants().OfType<DomElement>())
        {
            var elementName = ResolveUsedViewTransitionName(
                element, UsedStyleForCapture(element).GetValueOrDefault("view-transition-name"));
            if (elementName is not null && elementName != "root" && !elementByName.ContainsKey(elementName))
                elementByName[elementName] = element;
        }

        // Pass 1: build every group box (with its old/new snapshots), keyed by name.
        var groupByName = new Dictionary<string, DomElement>(System.StringComparer.Ordinal);
        var captureByName = new Dictionary<string, ViewTransitionCapture>(System.StringComparer.Ordinal);
        foreach (var capture in captures)
        {
            // The group animates old→new geometry; the reftests freeze it at the start, so it sits at
            // the old geometry when the element existed before the transition, else the new.
            var groupW = capture.HasOld ? capture.OldWidth : capture.NewWidth;
            var groupH = capture.HasOld ? capture.OldHeight : capture.NewHeight;
            // The captured position is carried by the group's transform — its UA style, per spec, is
            // `position: absolute; inset: 0` with a transform translating to the snapshot's location.
            // Keeping left/top at 0 lets an author `::view-transition-group(name)` rule that sets
            // `top`/`left`/`inset` compose additively with the captured translation rather than
            // replacing it (WPT content-with-clip offsets a group by `top: -50vh` to cancel a
            // `top: 50vh` on the captured element; overriding a captured `top` outright would push the
            // snapshot off-screen). An author `transform` still overrides the placement, as it should.
            // The snapshot clips to the border box only when the captured element itself establishes a
            // clip (non-visible overflow, contain:paint, a clip-path). A default overflow:visible
            // element must instead show its ink overflow — content its descendants paint outside the
            // box, e.g. an absolutely-positioned child above the box (WPT
            // capture-with-offscreen-child-translated). Root and old-only/unknown captures keep the
            // clip (viewport / prior behaviour).
            bool clipsContent = true;
            if (capture.Name != "root" && elementByName.TryGetValue(capture.Name, out var capturedElement))
                clipsContent = CapturedElementClipsContent(UsedStyleForCapture(capturedElement));

            var group = CreateStyledBox(BaseStyle(
                ("position", "absolute"), ("left", "0"), ("top", "0"),
                ("transform", $"translate({Px(capture.GroupLeft)}, {Px(capture.GroupTop)})"),
                ("width", Px(groupW)), ("height", Px(groupH)),
                ("overflow", clipsContent ? "hidden" : "visible")),
                LookupPseudo(pseudoRules, "group", capture));

            // Snapshot boxes are stacked top-left within the group: old under new. Each box is a
            // transparent positioned container carrying the author ::view-transition-old/-new
            // declarations (e.g. the pinned opacity); the captured content box inside it carries the
            // element's own paint (background, opacity, text) so an element's opacity composites over
            // the backdrop rather than over an opaque snapshot fill.
            if (capture.HasOld)
            {
                var oldBox = CreateStyledBox(BaseStyle(
                    ("position", "absolute"), ("left", "0"), ("top", "0"),
                    ("width", Px(capture.OldWidth)), ("height", Px(capture.OldHeight))),
                    LookupPseudo(pseudoRules, "old", capture));
                AttachSnapshotPaint(oldBox, capture.OldContent, capture.OldBackground);
                AppendBridgeChild(group, oldBox);
            }

            if (capture.HasNew)
            {
                var newBox = CreateStyledBox(BaseStyle(
                    ("position", "absolute"), ("left", "0"), ("top", "0"),
                    ("width", Px(capture.NewWidth)), ("height", Px(capture.NewHeight))),
                    LookupPseudo(pseudoRules, "new", capture));
                AttachSnapshotPaint(newBox, capture.NewContent, capture.NewBackground);
                AppendBridgeChild(group, newBox);
            }

            groupByName[capture.Name] = group;
            captureByName[capture.Name] = capture;
        }

        // Pass 2: parent each group. css-view-transitions-2 `view-transition-group` nests a group
        // under another group's `::view-transition-group-children` wrapper (so its background can
        // inherit down the nesting); the default (`normal`) keeps the group directly under the
        // overlay — today's flat layout, so untouched for the common case.
        var childrenWrapperByName = new Dictionary<string, DomElement>(System.StringComparer.Ordinal);
        foreach (var capture in captures)
        {
            var group = groupByName[capture.Name];
            var parentName = elementByName.TryGetValue(capture.Name, out var el)
                ? ResolveGroupParentName(el, capture.Name, elementByName, root)
                : null;

            if (parentName is not null && groupByName.TryGetValue(parentName, out var parentGroup))
            {
                if (!childrenWrapperByName.TryGetValue(parentName, out var wrapper))
                {
                    var parentContext = captureByName.TryGetValue(parentName, out var pc) ? (ViewTransitionCapture?)pc : null;
                    wrapper = CreateStyledBox(BaseStyle(
                        ("position", "absolute"), ("left", "0"), ("top", "0"),
                        ("width", "100%"), ("height", "100%")),
                        LookupPseudo(pseudoRules, "group-children", parentContext));
                    SetAttr(wrapper, "data-broiler-view-transition-group-children", "");
                    AppendBridgeChild(parentGroup, wrapper);
                    childrenWrapperByName[parentName] = wrapper;
                }
                AppendBridgeChild(wrapper, group);
            }
            else
            {
                AppendBridgeChild(overlay, group);
            }
        }

        AppendBridgeChild(root, overlay);
    }

    private static Dictionary<string, string> BaseStyle(params (string Key, string Value)[] entries)
    {
        var style = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entries)
            style[key] = value;
        return style;
    }

    /// <summary>Creates a bridge <c>&lt;div&gt;</c> whose inline style is <paramref name="baseStyle"/>
    /// with the pseudo-element's author declarations layered on top (author values win). Stored in the
    /// inline-style dict, not the attribute, so it survives <c>ReflectRenderState</c> on the render
    /// path and serializes normally.</summary>
    private DomElement CreateStyledBox(Dictionary<string, string> baseStyle, Dictionary<string, string> pseudoDeclarations)
    {
        foreach (var (key, value) in pseudoDeclarations)
            baseStyle[key] = value;

        var box = CreateBridgeElement("div");
        var inline = InlineStyle(box);
        foreach (var (key, value) in baseStyle)
            inline[key] = value;
        return box;
    }

    private void AppendBridgeChild(DomElement parent, DomElement child)
    {
        SetParent(child, parent);
        parent.AppendChild(child);
    }

    /// <summary>Fills a snapshot box: appends its captured content box (which carries the element's
    /// baked paint and cloned content) when present, else falls back to a flat background fill (the
    /// implicit root capture and one-sided names, which have no content box).</summary>
    private void AttachSnapshotPaint(DomElement box, DomElement? content, string fallbackBackground)
    {
        if (content is not null)
            AppendBridgeChild(box, CloneSnapshotContentForRender(content));
        else
            InlineStyle(box)["background-color"] = fallbackBackground;
    }

    // The paint- and text-affecting computed properties baked onto a snapshot's content box so it
    // renders like the captured element once re-parented under the overlay, where the element's
    // original ancestors and matched author selectors no longer apply. Deliberately excludes
    // geometry (position/inset/margin/width/height) — the group and box size the snapshot from the
    // captured rect and place the content at 0,0 — and view-transition-name (to avoid re-capturing
    // the clone).
    private static readonly string[] SnapshotPaintProperties =
    {
        "background-color", "background-image", "background-repeat", "background-position",
        "background-size", "background-clip", "background-origin", "background-attachment",
        "color", "opacity",
        "font-family", "font-size", "font-style", "font-weight", "font-variant", "font-stretch",
        "line-height", "letter-spacing", "word-spacing",
        "text-align", "text-transform", "text-indent", "text-shadow",
        "text-decoration-line", "text-decoration-color", "text-decoration-style",
        "white-space", "word-break", "overflow-wrap", "direction", "writing-mode",
        "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
        "border-top-style", "border-right-style", "border-bottom-style", "border-left-style",
        "border-top-color", "border-right-color", "border-bottom-color", "border-left-color",
        "border-top-left-radius", "border-top-right-radius",
        "border-bottom-left-radius", "border-bottom-right-radius",
        "box-shadow",
        "padding-top", "padding-right", "padding-bottom", "padding-left",
        "visibility",
    };

    /// <summary>
    /// Builds a detached box reproducing <paramref name="element"/>'s painted border box for a
    /// view-transition snapshot: the element's paint/text computed values baked inline (so it paints
    /// identically once re-parented under the overlay) plus a deep clone of its content, so the
    /// snapshot shows the element's text/children rather than a blank fill. Sized to the enclosing
    /// snapshot box (100%); positioning, insets, margins, and the outer width/height come from the
    /// captured geometry on that box, not from the element's own computed values.
    /// </summary>
    private DomElement BuildViewTransitionSnapshotContent(DomElement element)
    {
        var used = UsedStyleForCapture(element);

        var content = CreateBridgeElement("div");
        SetAttr(content, "data-broiler-view-transition-content", "");
        var inline = InlineStyle(content);
        inline["position"] = "absolute";
        inline["left"] = "0";
        inline["top"] = "0";
        inline["width"] = "100%";
        inline["height"] = "100%";
        inline["box-sizing"] = "border-box";

        foreach (var property in SnapshotPaintProperties)
            if (used.TryGetValue(property, out var value) && !string.IsNullOrWhiteSpace(value))
                inline[property] = value;

        // Clone the element's content verbatim (text and any descendants) so the snapshot paints it.
        foreach (var child in element.ChildNodes.ToArray())
        {
            var clone = child.CloneNode(deep: true);
            StripCapturedIdentifiers(clone);
            content.AppendChild(clone);
        }

        return content;
    }

    /// <summary>Strips identity/capture markers from a cloned snapshot subtree: <c>id</c> (so the
    /// clone does not duplicate a live element's id) and any inline <c>view-transition-name</c> (so a
    /// re-serialize cannot capture the clone as a named element).</summary>
    private void StripCapturedIdentifiers(DomNode node)
    {
        if (node is DomElement element)
        {
            if (element.HasAttribute("id"))
                element.RemoveAttribute("id");
            InlineStyle(element).Remove("view-transition-name");
        }

        foreach (var child in node.ChildNodes.ToArray())
            StripCapturedIdentifiers(child);
    }

    /// <summary>
    /// Whether the captured element establishes a paint clip on its descendants — a non-visible
    /// <c>overflow</c>, <c>contain: paint/content/strict</c>, or a <c>clip-path</c>. Such an element's
    /// view-transition snapshot clips to the border box; a default <c>overflow: visible</c> element's
    /// snapshot instead shows its ink overflow (WPT capture-with-offscreen-child-translated).
    /// </summary>
    private static bool CapturedElementClipsContent(Dictionary<string, string> style)
    {
        var overflow = style.GetValueOrDefault("overflow", "visible");
        if (!IsVisibleOverflow(style.GetValueOrDefault("overflow-x", overflow))
            || !IsVisibleOverflow(style.GetValueOrDefault("overflow-y", overflow)))
            return true;

        var contain = style.GetValueOrDefault("contain", string.Empty);
        if (contain.Contains("paint", System.StringComparison.OrdinalIgnoreCase)
            || contain.Contains("content", System.StringComparison.OrdinalIgnoreCase)
            || contain.Contains("strict", System.StringComparison.OrdinalIgnoreCase))
            return true;

        var clipPath = style.GetValueOrDefault("clip-path", "none");
        return !string.IsNullOrWhiteSpace(clipPath)
            && !clipPath.Equals("none", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVisibleOverflow(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Equals("visible", System.StringComparison.OrdinalIgnoreCase);

    private readonly record struct ViewTransitionCapture(
        string Name,
        double GroupLeft, double GroupTop,
        bool HasOld, double OldWidth, double OldHeight, string OldBackground, DomElement? OldContent,
        bool HasNew, double NewWidth, double NewHeight, string NewBackground, DomElement? NewContent);

    /// <summary>
    /// The captured names, each pairing the "old" snapshot (from before the update callback) with the
    /// "new" one (the element as it stands now), in old-then-new document order. Names appearing only
    /// on one side keep just that snapshot; the group is placed at the old geometry when present.
    /// </summary>
    private List<ViewTransitionCapture> CollectViewTransitionCaptures(DomElement root)
    {
        var newCaptures = new Dictionary<string, NamedSnapshot>(System.StringComparer.Ordinal);
        var order = new List<string>();

        void AddNew(string name, NamedSnapshot snapshot)
        {
            if (newCaptures.TryAdd(name, snapshot))
                order.Add(name);
        }

        var rootStyle = UsedStyleForCapture(root);
        if (!IsExplicitNoneName(rootStyle.GetValueOrDefault("view-transition-name")))
        {
            var (l, t, w, h) = GetBoundingClientRectForDomElement(root, isRoot: true);
            AddNew("root", new NamedSnapshot(l, t, w, h, rootStyle.GetValueOrDefault("background-color") ?? "transparent"));
        }

        foreach (var element in root.Descendants().OfType<DomElement>())
        {
            var style = UsedStyleForCapture(element);
            var name = ResolveUsedViewTransitionName(element, style.GetValueOrDefault("view-transition-name"));
            if (name is null || string.Equals(name, "root", System.StringComparison.Ordinal))
                continue;

            var (l, t, w, h) = GetBoundingClientRectForDomElement(element, isRoot: false);
            AddNew(name, new NamedSnapshot(l, t, w, h,
                style.GetValueOrDefault("background-color") ?? "transparent",
                BuildViewTransitionSnapshotContent(element)));
        }

        var oldCaptures = _activeViewTransition!.OldCaptures;
        // Names present in the old state but gone from the new keep their old-only snapshot.
        foreach (var name in oldCaptures.Keys)
            if (!newCaptures.ContainsKey(name))
                order.Add(name);

        var captures = new List<ViewTransitionCapture>(order.Count);
        foreach (var name in order)
        {
            var hasOld = oldCaptures.TryGetValue(name, out var old);
            var hasNew = newCaptures.TryGetValue(name, out var @new);
            var anchor = hasOld ? old : @new; // group start geometry
            captures.Add(new ViewTransitionCapture(
                name, anchor.Left, anchor.Top,
                hasOld, old.Width, old.Height, old.BackgroundColor, old.Content,
                hasNew, @new.Width, @new.Height, @new.BackgroundColor, @new.Content));
        }

        return captures;
    }

    /// <summary>The element's used style for capture purposes: its computed style with the
    /// serialize-time baked overlay layered on top. The overlay carries the
    /// <c>:active-view-transition-type()</c> declarations applied just before this runs — which the
    /// computed-style engine has not re-cascaded yet — so they must be read from the overlay directly
    /// (e.g. a freshly baked <c>view-transition-name</c> or <c>background</c>).</summary>
    private Dictionary<string, string> UsedStyleForCapture(DomElement element)
    {
        var used = BuildComputedStyleMap(element);
        foreach (var (key, value) in EffectiveInlineStyle(element))
        {
            used[key] = value;
            // `background` shorthand carries the colour the capture box needs; project it.
            if (key.Equals("background", System.StringComparison.OrdinalIgnoreCase))
                used["background-color"] = value;
        }
        return used;
    }

    private static bool IsNoneName(string? name) =>
        string.IsNullOrWhiteSpace(name) ||
        string.Equals(name.Trim(), "none", System.StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name.Trim(), "normal", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the used <c>view-transition-name</c> for <paramref name="element"/> per
    /// css-view-transitions-2: <c>none</c>/absent → null (not captured); a <c>&lt;custom-ident&gt;</c>
    /// → itself; <c>auto</c>/<c>match-element</c> → a generated name that is unique per element and
    /// stable for the transition (so the old and new captures pair into one group). <c>auto</c> on an
    /// element with an id derives from that id (elements sharing an id share the name).
    /// </summary>
    private string? ResolveUsedViewTransitionName(DomElement element, string? rawName)
    {
        if (IsNoneName(rawName))
            return null;

        var trimmed = rawName!.Trim();
        if (trimmed.Equals("auto", System.StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("match-element", System.StringComparison.OrdinalIgnoreCase))
            return GenerateAutoViewTransitionName(element, trimmed);

        return trimmed;
    }

    private string GenerateAutoViewTransitionName(DomElement element, string keyword)
    {
        var map = _activeViewTransition!.AutoNames;
        var identityElement = ResolveRenderSource(element);
        if (map.TryGetValue(identityElement, out var existing))
            return existing;

        // auto with an id → a stable id-derived name (two elements with the same id resolve equal, as
        // the spec requires); auto without an id, and match-element → a unique per-element name. The
        // "-ua-" prefix mirrors the spec's generated-name convention and cannot collide with a
        // <custom-ident> (which may not start with two dashes but may not be "-ua-…" either here).
        var id = identityElement.Id;
        var generated = keyword.Equals("auto", System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(id)
            ? "-ua-id-" + id
            : "-ua-el-" + (map.Count + 1);
        map[identityElement] = generated;
        return generated;
    }

    private static bool IsExplicitNoneName(string? name) =>
        name is not null && string.Equals(name.Trim(), "none", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>Author <c>::view-transition*</c> declarations, keyed by
    /// <c>"&lt;kind&gt;|&lt;argument&gt;"</c> (kind is <c>""</c> for the bare <c>::view-transition</c>,
    /// else <c>group</c>/<c>image-pair</c>/<c>old</c>/<c>new</c>; argument is the name/class/<c>*</c>).
    /// Later rules win, matching document order.</summary>
    private Dictionary<string, Dictionary<string, string>> CollectViewTransitionPseudoDeclarations(DomElement root)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(System.StringComparer.Ordinal);

        foreach (var (selectorText, declarations) in EnumerateAuthorStyleRules(root))
        {
            if (selectorText.IndexOf("::view-transition", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var match = ViewTransitionPseudo.Match(selectorText);
            if (!match.Success)
                continue;

            var kind = match.Groups[1].Value.ToLowerInvariant();
            var argument = match.Groups[2].Success ? match.Groups[2].Value.Trim() : string.Empty;
            var key = $"{kind}|{argument}";

            if (!result.TryGetValue(key, out var bucket))
                result[key] = bucket = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var declaration in declarations.Declarations)
                bucket[declaration.Name] = declaration.Value.Text;
        }

        return result;
    }

    /// <summary>The declarations that apply to a pseudo of <paramref name="kind"/> for a given
    /// capture, merging the universal (<c>*</c>) and name-specific buckets in cascade order (specific
    /// wins). <paramref name="capture"/> is <c>null</c> for the bare overlay pseudo.</summary>
    private static Dictionary<string, string> LookupPseudo(
        Dictionary<string, Dictionary<string, string>> pseudoRules, string kind, ViewTransitionCapture? capture)
    {
        var merged = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        void Merge(string argument)
        {
            if (pseudoRules.TryGetValue($"{kind}|{argument}", out var bucket))
                foreach (var (k, v) in bucket)
                    merged[k] = v;
        }

        if (capture is null)
        {
            Merge(string.Empty);
            return merged;
        }

        Merge("*");
        Merge(capture.Value.Name);
        return merged;
    }

    /// <summary>
    /// Whether the author kept the bare <c>::view-transition</c> root overlay alive with an
    /// animation. Only consulted when the transition captured nothing: an empty transition
    /// finishes at once (its pseudo tree gone by screenshot time), so the overlay paints only when
    /// an author animation on the root pseudo pins it open — the difference between WPT
    /// <c>no-named-elements</c> (blue overlay, <c>animation: no-op 300s</c>) and
    /// <c>nothing-captured</c> (red overlay, no animation, must stay hidden).
    /// </summary>
    private static bool HasRootOverlayKeepAliveAnimation(
        Dictionary<string, Dictionary<string, string>> pseudoRules)
    {
        // The bare ::view-transition bucket is keyed "<kind>|<argument>" with both empty (see
        // CollectViewTransitionPseudoDeclarations), i.e. "|".
        if (!pseudoRules.TryGetValue("|", out var rootDeclarations))
            return false;

        if (rootDeclarations.TryGetValue("animation", out var shorthand) && !IsInertAnimationValue(shorthand))
            return true;
        if (rootDeclarations.TryGetValue("animation-name", out var name) && !IsInertAnimationValue(name))
            return true;
        if (rootDeclarations.TryGetValue("animation-duration", out var duration) && !IsInertAnimationDuration(duration))
            return true;
        return false;
    }

    /// <summary>An <c>animation</c>/<c>animation-name</c> value that starts no animation
    /// (absent, <c>none</c>, or a CSS-wide keyword).</summary>
    private static bool IsInertAnimationValue(string value)
    {
        var v = value.Trim();
        return v.Length == 0 ||
            v.Equals("none", System.StringComparison.OrdinalIgnoreCase) ||
            v.Equals("unset", System.StringComparison.OrdinalIgnoreCase) ||
            v.Equals("initial", System.StringComparison.OrdinalIgnoreCase) ||
            v.Equals("inherit", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An <c>animation-duration</c> value that leaves the animation zero-length (so it
    /// does not keep the overlay alive).</summary>
    private static bool IsInertAnimationDuration(string value)
    {
        var v = value.Trim();
        return v.Length == 0 || v == "0" ||
            v.Equals("0s", System.StringComparison.OrdinalIgnoreCase) ||
            v.Equals("0ms", System.StringComparison.OrdinalIgnoreCase) ||
            IsInertAnimationValue(v);
    }

    /// <summary>Author style rules (selector text + declarations) across every <c>&lt;style&gt;</c>
    /// and external-stylesheet <c>&lt;link rel="stylesheet"&gt;</c> in the tree, in document order.
    /// External links are included because the <c>::view-transition-*</c> pseudo rules and the
    /// <c>view-transition-group</c> values — which are read from the raw author rules here rather than
    /// through computed style — routinely live in a linked stylesheet (the WPT
    /// <c>css-view-transitions-2 nested</c> tests keep the entire pseudo tree styling in
    /// <c>resources/*.css</c>). A disabled sheet contributes nothing (CSSOM §2.3).</summary>
    private IEnumerable<(string SelectorText, CssDeclarationBlock Declarations)> EnumerateAuthorStyleRules(DomElement root)
    {
        foreach (var styleEl in root.Descendants().OfType<DomElement>())
        {
            if (!(styleEl.TagName.Equals("style", System.StringComparison.OrdinalIgnoreCase)
                    || IsExternalStylesheet(styleEl))
                || IsStyleSheetDisabled(styleEl))
                continue;

            var source = GetStyleElementSourceText(styleEl);
            if (string.IsNullOrEmpty(source))
                continue;

            CssStyleSheet sheet;
            try { sheet = new CssParser().ParseStyleSheet(source); }
            catch { continue; }

            foreach (var rule in sheet.Rules)
            {
                if (rule is not CssStyleRule styleRule)
                    continue;
                foreach (var selector in styleRule.Selectors.Selectors)
                    yield return (selector.Text, styleRule.Declarations);
            }
        }
    }

    private static string Px(double value) =>
        value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "px";
}
