using System.Text.RegularExpressions;
using Broiler.Dom;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// Custom Elements (HTML §4.13) — the <c>customElements</c> registry, a constructible
/// <c>HTMLElement</c> base, and the reaction callbacks a definition receives.
/// </summary>
/// <remarks>
/// <para>
/// There was no production implementation at all: <c>customElements</c> was undefined and
/// <c>HTMLElement</c> threw <c>Illegal constructor</c>, so <c>class X extends HTMLElement</c>
/// followed by <c>customElements.define(…)</c> failed on the bare name and took the whole script
/// with it. The WPT runner carried a shim to get past that, which had to fake the parts it could not
/// reach — its <c>HTMLElement</c> handed back a plain element that did not carry the class's
/// prototype, so the shim copied the reaction callbacks across by hand and a component's own methods
/// were simply unreachable.
/// </para>
/// <para>
/// <b>Why the base constructor is JavaScript and everything else is not.</b> Constructing a custom
/// element is the one step that needs <c>new.target</c>: <c>new X()</c> runs <c>X</c>'s constructor,
/// which calls <c>super()</c>, and only <c>new.target</c> says which subclass is being built and so
/// which prototype and tag name the element must get. A host function cannot see it — the engine's
/// <c>Arguments</c> does not carry it — so the base lives in JavaScript and calls back here for the
/// element itself. Everything else (the registry, name validation, upgrades, reaction dispatch) is
/// C#, where the DOM is.
/// </para>
/// <para>
/// <b>Upgrading reuses the same constructor path rather than a second one</b>, which is what the
/// specification's "custom element construction stack" is for. Upgrading pushes the existing element
/// onto <see cref="_constructionStack"/> and calls the definition's constructor; the base's callback
/// finds a pending element and hands that one back instead of minting a new one, so the author's
/// constructor body runs against the element already in the tree. Without it an upgrade would have
/// to copy attributes and children onto a fresh element and swap it in — which is what the shim did,
/// and it loses node identity: a page holding a reference to the element before the definition
/// landed would keep pointing at the discarded one.
/// </para>
/// <para>
/// Reactions are dispatched off the canonical <c>DomDocument.Mutated</c> stream, which is raised
/// synchronously at mutation time. That matters: a browser runs <c>connectedCallback</c> before the
/// statement after <c>appendChild</c>, so building this on <c>MutationObserver</c> — whose delivery
/// is a microtask — would have put every reaction one checkpoint late.
/// </para>
/// <para>
/// <b>Not in this slice, and deliberately:</b> customized built-ins (the <c>extends</c> option and
/// <c>is=</c> attribute), form-associated custom elements, and <c>adoptedCallback</c>. Each is a
/// separate capability rather than a piece of this one, and none is faked — <c>define</c> rejects an
/// <c>extends</c> option rather than accepting it and ignoring it.
/// </para>
/// </remarks>
internal sealed partial class CustomElementsBinding(ICustomElementsHost host)
{
    private readonly ICustomElementsHost _host = host;

    /// <summary>The definitions, by tag name.</summary>
    private readonly Dictionary<string, CustomElementDefinition> _byName = new(StringComparer.Ordinal);

    /// <summary>The same definitions by constructor, so <c>getName</c> and the base's callback can
    /// go the other way. One constructor may define only one element (HTML §4.13.4).</summary>
    private readonly Dictionary<JSObject, CustomElementDefinition> _byConstructor = [];

    /// <summary>
    /// The elements currently being upgraded, innermost last — the specification's custom element
    /// construction stack. Non-empty exactly while a definition's constructor is running for an
    /// element that already exists.
    /// </summary>
    private readonly List<DomElement> _constructionStack = [];

    /// <summary><c>whenDefined</c> resolvers waiting on a name that is not defined yet.</summary>
    private readonly Dictionary<string, List<JSObject>> _whenDefined = new(StringComparer.Ordinal);

    private sealed record CustomElementDefinition(
        string Name,
        JSObject Constructor,
        HashSet<string> ObservedAttributes,
        bool HasAttributeChangedCallback);

    /// <summary>
    /// A valid custom element name (HTML §4.13.1): starts with an ASCII lower alpha, contains a
    /// hyphen, and holds no upper-case letters.
    /// </summary>
    /// <remarks>
    /// The reserved names below are the SVG and MathML element names that already contain a hyphen,
    /// so they would otherwise pass the shape test while naming something that exists. Measured
    /// against a browser rather than transcribed: each is a <c>SyntaxError</c>, as is a name with no
    /// hyphen, an empty name, one with an upper-case letter, and one starting with a digit.
    /// </remarks>
    [GeneratedRegex(@"^[a-z][-._0-9a-z]*-[-._0-9a-z]*$", RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex ValidNamePattern();

    private static readonly HashSet<string> ReservedNames = new(StringComparer.Ordinal)
    {
        "annotation-xml", "color-profile", "font-face", "font-face-src", "font-face-uri",
        "font-face-format", "font-face-name", "missing-glyph",
    };

    internal static bool IsValidCustomElementName(string name) =>
        name.Length > 0 && !ReservedNames.Contains(name) && ValidNamePattern().IsMatch(name);

    /// <summary>Whether <paramref name="tagName"/> has a definition — used by the element-wrapper
    /// interface lookup so a defined element reports its own class.</summary>
    internal bool IsDefined(string tagName) => _byName.ContainsKey(tagName);

    // ---------------- registry ----------------

    /// <summary><c>customElements.define(name, constructor)</c>.</summary>
    internal JSValue Define(in Arguments a)
    {
        var context = _host.JsContext;
        var name = a.Length > 0 ? a[0].ToString() : string.Empty;

        if (a.Length < 2 || a[1] is not JSObject constructor || constructor is not JSFunction)
            throw new JSException(new JSString(
                "TypeError: Failed to execute 'define' on 'CustomElementRegistry': the constructor is not a constructor."));

        if (!IsValidCustomElementName(name))
        {
            DomBridge.ThrowDOMException(
                context,
                $"Failed to execute 'define' on 'CustomElementRegistry': '{name}' is not a valid custom element name.",
                "SyntaxError");
            return JSUndefined.Value;
        }

        if (a.Length > 2 && a[2] is JSObject options && options[(KeyString)"extends"] is { } extends &&
            !extends.IsUndefined && !extends.IsNull)
        {
            // Customized built-ins are a separate capability. Rejecting is the honest answer:
            // accepting and ignoring would leave a page believing its <button is="..."> was upgraded.
            DomBridge.ThrowDOMException(
                context,
                "Failed to execute 'define' on 'CustomElementRegistry': customized built-in elements " +
                "(the 'extends' option) are not supported.",
                "NotSupportedError");
            return JSUndefined.Value;
        }

        if (_byName.ContainsKey(name))
        {
            DomBridge.ThrowDOMException(
                context,
                $"Failed to execute 'define' on 'CustomElementRegistry': the name '{name}' has already been used.",
                "NotSupportedError");
            return JSUndefined.Value;
        }

        if (_byConstructor.ContainsKey(constructor))
        {
            DomBridge.ThrowDOMException(
                context,
                "Failed to execute 'define' on 'CustomElementRegistry': this constructor has already been used.",
                "NotSupportedError");
            return JSUndefined.Value;
        }

        var definition = new CustomElementDefinition(
            name,
            constructor,
            ReadObservedAttributes(constructor),
            constructor[(KeyString)"prototype"] is JSObject prototype &&
                prototype[(KeyString)"attributeChangedCallback"] is JSFunction);

        _byName[name] = definition;
        _byConstructor[constructor] = definition;

        UpgradeDefined(definition);
        ResolveWhenDefined(name, constructor);
        return JSUndefined.Value;
    }

    /// <summary>
    /// The definition's <c>observedAttributes</c>, read once at definition time as the specification
    /// requires — a later change to the static getter does not retroactively widen what is observed.
    /// </summary>
    private static HashSet<string> ReadObservedAttributes(JSObject constructor)
    {
        var observed = new HashSet<string>(StringComparer.Ordinal);
        if (constructor[(KeyString)"observedAttributes"] is not JSObject list)
            return observed;

        var length = (int)list[(KeyString)"length"].DoubleValue;
        for (var index = 0; index < length; index++)
        {
            var entry = list[(uint)index];
            if (entry is not null && !entry.IsUndefined && !entry.IsNull)
                observed.Add(entry.ToString());
        }

        return observed;
    }

    internal JSValue Get(in Arguments a)
    {
        var name = a.Length > 0 ? a[0].ToString() : string.Empty;
        return _byName.TryGetValue(name, out var definition) ? definition.Constructor : JSUndefined.Value;
    }

    internal JSValue GetName(in Arguments a) =>
        a.Length > 0 && a[0] is JSObject constructor && _byConstructor.TryGetValue(constructor, out var definition)
            ? new JSString(definition.Name)
            : JSNull.Value;

    /// <summary>
    /// <c>customElements.whenDefined(name)</c> — a real promise that resolves with the constructor,
    /// rejecting with a <c>SyntaxError</c> for an invalid name.
    /// </summary>
    internal JSValue WhenDefined(in Arguments a)
    {
        var name = a.Length > 0 ? a[0].ToString() : string.Empty;
        if (!IsValidCustomElementName(name))
            return _host.RejectedPromise(
                $"SyntaxError: '{name}' is not a valid custom element name.");

        if (_byName.TryGetValue(name, out var defined))
            return _host.ResolvedPromise(defined.Constructor);

        var (promise, resolver) = _host.PendingPromise();
        if (!_whenDefined.TryGetValue(name, out var waiting))
            _whenDefined[name] = waiting = [];
        waiting.Add(resolver);
        return promise;
    }

    private void ResolveWhenDefined(string name, JSObject constructor)
    {
        if (!_whenDefined.Remove(name, out var waiting))
            return;

        foreach (var resolver in waiting)
            _host.Resolve(resolver, constructor);
    }

    /// <summary><c>customElements.upgrade(root)</c> — upgrades the shadow-including inclusive
    /// descendants of <paramref name="a"/>[0] that have a definition and are not upgraded yet.</summary>
    internal JSValue Upgrade(in Arguments a)
    {
        if (a.Length == 0 || a[0] is not JSObject wrapper || _host.NodeFor(wrapper) is not { } root)
            return JSUndefined.Value;

        foreach (var element in InclusiveElements(root))
        {
            if (_byName.TryGetValue(element.TagName, out var definition))
                TryUpgrade(element, definition);
        }

        return JSUndefined.Value;
    }

    // ---------------- construction and upgrade ----------------

    /// <summary>
    /// The host half of the JavaScript <c>HTMLElement</c> base: given the <c>new.target</c> the base
    /// read, hands back the element the constructor should become.
    /// </summary>
    /// <remarks>
    /// An element already being upgraded is returned as it is — that is the construction stack, and
    /// it is what lets an upgrade run the author's constructor against the node already in the tree
    /// rather than a replacement. Otherwise a fresh element is minted for the definition's tag name.
    /// A <c>new.target</c> with no definition, and a bare <c>new HTMLElement()</c> (no
    /// <c>new.target</c> at all), are both <c>TypeError</c>s, as in a browser.
    /// </remarks>
    internal JSValue ConstructForNewTarget(in Arguments a)
    {
        if (_constructionStack.Count > 0)
        {
            var pending = _constructionStack[^1];
            _constructionStack.RemoveAt(_constructionStack.Count - 1);
            return _host.ToJSObject(pending);
        }

        if (a.Length == 0 || a[0] is not JSObject newTarget ||
            !_byConstructor.TryGetValue(newTarget, out var definition))
        {
            // Null rather than a throw: the JavaScript base turns this into a real TypeError, which
            // a host throw could not carry a name and message for.
            return JSNull.Value;
        }

        var created = _host.CreateBridgeElement(definition.Name);
        // A constructed element is upgraded by definition — it was built from its own class. Marking
        // it here is what makes its reactions fire: without it a `document.createElement('x-thing')`
        // instance took no attributeChangedCallback, because the reaction dispatch only knows about
        // elements that went through an upgrade.
        _upgraded.Add(created);
        return _host.ToJSObject(created);
    }

    /// <summary>
    /// Creates an element for a defined custom tag by running its constructor, which is what makes
    /// <c>document.createElement('x-thing')</c> hand back an instance of the class rather than a
    /// plain element. Returns <see langword="null"/> when the tag has no definition, so the ordinary
    /// path takes over.
    /// </summary>
    internal JSObject? CreateDefined(string tagName)
    {
        if (!_byName.TryGetValue(tagName, out var definition))
            return null;

        return _host.Construct(definition.Constructor) is { } created ? created : null;
    }

    /// <summary>Upgrades every element in the document that this definition now names, in tree
    /// order — the elements a page parsed before the definition landed.</summary>
    private void UpgradeDefined(CustomElementDefinition definition)
    {
        foreach (var element in _host.Elements)
        {
            if (string.Equals(element.TagName, definition.Name, StringComparison.Ordinal))
                TryUpgrade(element, definition);
        }
    }

    /// <summary>The elements already marked as upgraded, so an element is never upgraded twice.</summary>
    private readonly HashSet<DomElement> _upgraded = [];

    /// <summary>
    /// Runs <paramref name="definition"/>'s constructor against <paramref name="element"/>, then the
    /// reactions the specification enqueues for an upgrade: <c>attributeChangedCallback</c> for each
    /// observed attribute it already carries, and <c>connectedCallback</c> when it is in the tree.
    /// </summary>
    /// <remarks>
    /// The attribute callbacks come before the connected one and report an <c>oldValue</c> of
    /// <see langword="null"/> — the element is only now becoming a custom element, so from the
    /// definition's point of view every attribute it already has is being set for the first time.
    /// Measured.
    /// </remarks>
    private void TryUpgrade(DomElement element, CustomElementDefinition definition)
    {
        if (!_upgraded.Add(element))
            return;

        _constructionStack.Add(element);
        try
        {
            _host.Construct(definition.Constructor);
        }
        catch (Exception)
        {
            // A constructor that throws leaves the element un-upgraded rather than taking the
            // define() call down with it — one bad definition must not stop the others.
            _upgraded.Remove(element);
            return;
        }
        finally
        {
            if (_constructionStack.Count > 0 && ReferenceEquals(_constructionStack[^1], element))
                _constructionStack.RemoveAt(_constructionStack.Count - 1);
        }

        foreach (var attributeName in DomBridge.AttributeNames(element).ToList())
        {
            if (definition.ObservedAttributes.Contains(attributeName) &&
                DomBridge.TryGetAttribute(element, attributeName, out var value))
            {
                InvokeReaction(element, "attributeChangedCallback",
                    new JSString(attributeName), JSNull.Value, new JSString(value));
            }
        }

        if (_host.IsConnected(element))
            InvokeReaction(element, "connectedCallback");
    }

    // ---------------- reactions ----------------

    /// <summary>Runs a reaction callback on an upgraded element, if its class declares one.</summary>
    /// <remarks>
    /// Looked up on the element itself rather than on the definition's prototype: the element's
    /// prototype <em>is</em> the class's after an upgrade, so this finds an override on the instance
    /// and an inherited callback alike, and calls it with <c>this</c> bound to the element the way
    /// the author wrote it expecting.
    /// </remarks>
    private void InvokeReaction(DomElement element, string callback, params JSValue[] arguments)
    {
        if (!_upgraded.Contains(element) || !_host.TryGetWrapper(element, out var wrapper))
            return;

        if (wrapper[(KeyString)callback] is not JSFunction reaction)
            return;

        try
        {
            _host.Call(reaction, wrapper, arguments);
        }
        catch (Exception)
        {
            // A throwing reaction is reported to the page's error handling, not propagated into the
            // DOM operation that triggered it — an appendChild must not fail because a component's
            // connectedCallback did.
        }
    }

    /// <summary>Dispatches the connected/disconnected reactions for a tree mutation.</summary>
    internal void OnChildListMutation(IReadOnlyList<DomNode> added, IReadOnlyList<DomNode> removed)
    {
        foreach (var node in removed)
        {
            foreach (var element in InclusiveElements(node))
                InvokeReaction(element, "disconnectedCallback");
        }

        foreach (var node in added)
        {
            foreach (var element in InclusiveElements(node))
            {
                // An element inserted with a definition already in place becomes a custom element
                // now — the shape a page produces with `innerHTML` or by appending parsed markup
                // after its component script ran. Upgrading dispatches the connected reaction itself,
                // so it must not be dispatched twice.
                if (!_upgraded.Contains(element) && _byName.TryGetValue(element.TagName, out var definition))
                {
                    TryUpgrade(element, definition);
                    continue;
                }

                if (_host.IsConnected(element))
                    InvokeReaction(element, "connectedCallback");
            }
        }
    }

    /// <summary>Dispatches <c>attributeChangedCallback</c> for an observed attribute.</summary>
    internal void OnAttributeMutation(DomElement element, string attributeName, string? oldValue)
    {
        if (!_upgraded.Contains(element) ||
            !_byName.TryGetValue(element.TagName, out var definition) ||
            !definition.ObservedAttributes.Contains(attributeName))
            return;

        var newValue = DomBridge.TryGetAttribute(element, attributeName, out var current)
            ? new JSString(current)
            : (JSValue)JSNull.Value;

        InvokeReaction(element, "attributeChangedCallback",
            new JSString(attributeName),
            oldValue is null ? JSNull.Value : new JSString(oldValue),
            newValue);
    }

    private static IEnumerable<DomElement> InclusiveElements(DomNode node) =>
        node.InclusiveDescendants().OfType<DomElement>();
}
