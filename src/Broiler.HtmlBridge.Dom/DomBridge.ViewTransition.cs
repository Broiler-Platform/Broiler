
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
/// <c>opacity:1</c> over an author-coloured <c>::view-transition</c> backdrop). This partial
/// reproduces that still: it runs the callback synchronously, applies the
/// <c>:active-view-transition-type()</c> conditional rules that a transition activates, and — at
/// serialize time, alongside the other pseudo-element bakes — materialises the
/// <c>::view-transition</c> overlay tree as real positioned boxes the renderer already knows how to
/// paint. The animation timeline itself is out of scope; the tests that need it are the ones that
/// screenshot mid-animation with unpinned timing.
/// </para>
/// </summary>
public sealed partial class DomBridge
{
    /// <summary>The active view transition, or <c>null</c> when none is running. A transition stays
    /// active from <c>startViewTransition()</c> through the terminal render (these documents render
    /// once), which is when the pseudo tree is baked.</summary>
    private ViewTransitionState? _activeViewTransition;

    private sealed class ViewTransitionState
    {
        /// <summary>The transition's active types (the <c>types</c> option), matched by
        /// <c>:active-view-transition-type()</c>.</summary>
        public HashSet<string> Types { get; } = new(System.StringComparer.Ordinal);
    }

    // :active-view-transition-type( a, b, … ) anywhere in a selector.
    private static readonly System.Text.RegularExpressions.Regex ActiveViewTransitionType =
        new(@":active-view-transition-type\(\s*([^)]*)\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    // html::view-transition[-group|-image-pair|-old|-new]( name ) — the pseudo and its optional
    // name/class/`*` argument. The leading originating selector (html / :root / *) is ignored: the
    // pseudo tree always originates from the document element.
    private static readonly System.Text.RegularExpressions.Regex ViewTransitionPseudo =
        new(@"::view-transition(?:-(group|image-pair|old|new))?\s*(?:\(\s*([^)]*)\s*\))?\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// <c>document.startViewTransition(updateCallback)</c> /
    /// <c>document.startViewTransition({ update, types })</c>. Records the active transition and its
    /// types, runs the update callback synchronously (its DOM mutation is the "new" state the
    /// screenshot captures), and returns a <c>ViewTransition</c> whose <c>ready</c>/<c>finished</c>/
    /// <c>updateCallbackDone</c> promises are already resolved — the reftests gate their screenshot
    /// on <c>ready</c>, so resolving synchronously lets that fire once the new DOM is in place.
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
    /// <paramref name="types"/>. Absent or non-array values contribute nothing.</summary>
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
        transition.FastAddValue((KeyString)"finished", ResolvedThenable(), JSPropertyAttributes.EnumerableConfigurableValue);
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

    // ── Serialize-time rendering ────────────────────────────────────────────

    /// <summary>
    /// Applies the author rules a running transition activates
    /// (<c>:active-view-transition-type(type)</c>) to the live DOM, so the "new" snapshot the pseudo
    /// tree captures reflects them. For every active type, a rule selecting through that pseudo is
    /// re-matched with the pseudo stripped out and its declarations baked onto the matched elements
    /// (e.g. <c>html:active-view-transition-type(t) #x { … }</c> bakes onto <c>#x</c> while the
    /// transition of type <c>t</c> is active). A no-op when no transition is active.
    /// </summary>
    private void ApplyActiveViewTransitionTypeRules(DomElement root)
    {
        if (_activeViewTransition is null || _activeViewTransition.Types.Count == 0)
            return;

        foreach (var (selectorText, declarations) in EnumerateAuthorStyleRules(root))
        {
            var match = ActiveViewTransitionType.Match(selectorText);
            if (!match.Success)
                continue;
            if (!AnyTypeActive(match.Groups[1].Value, _activeViewTransition.Types))
                continue;

            // Strip the pseudo-class; an empty resulting compound (the pseudo stood alone on the
            // originating element) selects the document element itself.
            var stripped = ActiveViewTransitionType.Replace(selectorText, string.Empty).Trim();
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
    /// Materialises the <c>::view-transition</c> pseudo tree as real positioned boxes. For each
    /// element carrying a used <c>view-transition-name</c> (plus the document root's implicit
    /// <c>root</c> name), a group box is placed at the element's border-box geometry holding an
    /// old and a new snapshot box; author <c>::view-transition*</c> declarations are applied to the
    /// corresponding boxes. The whole tree hangs off an overlay box painted above the page (author
    /// <c>::view-transition</c> declarations, e.g. a backdrop colour), reproducing the paused still
    /// the reftests screenshot. A no-op when no transition is active.
    /// </summary>
    private void ApplyViewTransitionPseudoTree(DomElement root)
    {
        if (_activeViewTransition is null)
            return;

        var pseudoRules = CollectViewTransitionPseudoDeclarations(root);
        var captures = CollectViewTransitionCaptures(root);
        if (captures.Count == 0)
            return;

        var overlay = CreateBridgeElement("div");
        SetAttr(overlay, "data-broiler-view-transition", "");
        SetAttr(overlay, "style", ComposeStyle(
            "position: fixed; left: 0; top: 0; width: 100vw; height: 100vh; z-index: 2147483646; pointer-events: none",
            LookupPseudo(pseudoRules, "", null)));

        foreach (var capture in captures)
        {
            var group = CreateBridgeElement("div");
            SetAttr(group, "style", ComposeStyle(
                $"position: absolute; left: {Px(capture.Left)}; top: {Px(capture.Top)}; " +
                $"width: {Px(capture.Width)}; height: {Px(capture.Height)}; overflow: hidden",
                LookupPseudo(pseudoRules, "group", capture)));

            var oldBox = CreateBridgeElement("div");
            SetAttr(oldBox, "style", ComposeStyle(
                $"position: absolute; left: 0; top: 0; width: {Px(capture.Width)}; height: {Px(capture.Height)}; " +
                $"background-color: {capture.BackgroundColor}",
                LookupPseudo(pseudoRules, "old", capture)));

            var newBox = CreateBridgeElement("div");
            SetAttr(newBox, "style", ComposeStyle(
                $"position: absolute; left: 0; top: 0; width: {Px(capture.Width)}; height: {Px(capture.Height)}; " +
                $"background-color: {capture.BackgroundColor}",
                LookupPseudo(pseudoRules, "new", capture)));

            AppendBridgeChild(group, oldBox);
            AppendBridgeChild(group, newBox);
            AppendBridgeChild(overlay, group);
        }

        AppendBridgeChild(root, overlay);
    }

    private void AppendBridgeChild(DomElement parent, DomElement child)
    {
        SetParent(child, parent);
        parent.AppendChild(child);
    }

    private readonly record struct ViewTransitionCapture(
        string Name, double Left, double Top, double Width, double Height, string BackgroundColor);

    /// <summary>The captured elements: the document root (name <c>root</c>) unless it opts out with
    /// <c>view-transition-name: none</c>, then every element with a used <c>view-transition-name</c>,
    /// in document order.</summary>
    private List<ViewTransitionCapture> CollectViewTransitionCaptures(DomElement root)
    {
        var captures = new List<ViewTransitionCapture>();

        // The document element is captured as `root` by default; only an explicit
        // `view-transition-name: none` opts it out.
        var rootStyle = UsedStyleForCapture(root);
        if (!IsExplicitNoneName(rootStyle.GetValueOrDefault("view-transition-name")))
        {
            var (l, t, w, h) = GetBoundingClientRectForDomElement(root, isRoot: true);
            captures.Add(new ViewTransitionCapture("root", l, t, w, h,
                rootStyle.GetValueOrDefault("background-color") ?? "transparent"));
        }

        foreach (var element in root.Descendants().OfType<DomElement>())
        {
            var style = UsedStyleForCapture(element);
            var name = style.GetValueOrDefault("view-transition-name");
            if (IsNoneName(name) || string.Equals(name!.Trim(), "root", System.StringComparison.Ordinal))
                continue;

            var (l, t, w, h) = GetBoundingClientRectForDomElement(element, isRoot: false);
            captures.Add(new ViewTransitionCapture(name.Trim(), l, t, w, h,
                style.GetValueOrDefault("background-color") ?? "transparent"));
        }

        return captures;
    }

    /// <summary>The element's used style for capture purposes: its computed style with the
    /// serialize-time baked overlay layered on top. The overlay carries the
    /// <c>:active-view-transition-type()</c> declarations applied just before this runs — which the
    /// computed-style engine has not re-cascaded yet — so they must be read from the overlay
    /// directly (e.g. a freshly baked <c>view-transition-name</c> or <c>background</c>).</summary>
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
        string.Equals(name.Trim(), "normal", System.StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name.Trim(), "auto", System.StringComparison.OrdinalIgnoreCase);

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
    /// capture, merging the universal (<c>*</c>) and name-specific (and, for groups, class) buckets
    /// in cascade order (specific wins). <paramref name="capture"/> is <c>null</c> for the bare
    /// overlay pseudo.</summary>
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

    /// <summary>Author style rules (selector text + declarations) across every <c>&lt;style&gt;</c>
    /// in the tree, in document order.</summary>
    private IEnumerable<(string SelectorText, CssDeclarationBlock Declarations)> EnumerateAuthorStyleRules(DomElement root)
    {
        foreach (var styleEl in root.Descendants().OfType<DomElement>())
        {
            if (!styleEl.TagName.Equals("style", System.StringComparison.OrdinalIgnoreCase))
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

    /// <summary>Appends the pseudo-element's author declarations after the base layout style, so
    /// author values (opacity, background, display:none, …) win.</summary>
    private static string ComposeStyle(string baseStyle, Dictionary<string, string> declarations)
    {
        if (declarations.Count == 0)
            return baseStyle;
        var extra = string.Join("; ", declarations.Select(kv => $"{kv.Key}: {kv.Value}"));
        return $"{baseStyle}; {extra}";
    }
}
