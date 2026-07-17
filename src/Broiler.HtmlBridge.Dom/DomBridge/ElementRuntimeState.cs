using Broiler.JavaScript.Runtime;
using Broiler.CSS;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Runtime;

internal readonly record struct EventListenerRegistration(JSValue Listener, bool Capture, bool Once = false, bool Passive = false);

/// <summary>
/// JavaScript and browser-runtime state associated with a legacy DOM node.
/// The node model deliberately does not own this state.
/// </summary>
internal sealed class ElementRuntimeState
{
    // P2.5: addEventListener listeners moved off this (process-global) table into the instance-scoped
    // EventTargetRegistry; only inline on* handlers remain node-runtime state here.
    public Dictionary<string, JSValue> InlineEventHandlers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Inline-style property names last written through the JS <c>element.style</c> /
    /// <c>setAttribute("style", …)</c> path, tracked so serialization and computed-style
    /// invalidation preserve author-set intent. Relocated off the <c>Broiler.Dom.DomElement</c> facade
    /// (RF-BRIDGE-1c Phase A — the node model does not own this bridge state).
    /// </summary>
    public HashSet<string> JsSetStyleProps { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Phase 4 item 1 (P4.4c): the OwnerDocRoot parallel-state field is deleted. A node's owning
    // (sub-)document is now derived from the canonical tree (a connected node's absolute root is a
    // Broiler.Dom.DomDocument after the P4.4b sever) or the node's canonical OwnerDocument when
    // detached — see DomBridge.GetOwningDocument. Sub-document createElement nodes are adopted into
    // their content document (DomDocument.AdoptNode) so their detached OwnerDocument is correct.

    /// <summary>
    /// The node's inline style in CSS kebab-case — the authoritative in-memory inline
    /// style (mutated by JS <c>element.style</c>, the anchor resolver, and synthetic
    /// form-control styling; synced back to the <c>style=</c> attribute at serialization).
    /// Relocated off the <c>Broiler.Dom.DomElement</c> facade (RF-BRIDGE-1c Phase B); reached through
    /// <c>DomBridge.InlineStyle(element)</c>, which lazily seeds it from the <c>style=</c>
    /// attribute on first access (see <see cref="StyleSeeded"/>).
    /// </summary>
    public Dictionary<string, string> Style { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <see cref="Style"/> has been seeded from the element's <c>style=</c>
    /// attribute yet. The lazy seed runs once on first <c>InlineStyle</c> access; the
    /// <c>style=</c> attribute setter and <c>cloneNode</c> set this explicitly.
    /// </summary>
    public bool StyleSeeded { get; set; }

    public FormControlRuntimeState FormControl { get; } = new();

    // Phase 2 item 4 (de-globalization, 2026-07-17): the Scroll slot was moved out of this
    // process-static table into a per-bridge instance table (DomBridge._scrollRuntimeStates,
    // reached via ScrollStateFor); the clone copy lives in CloneDomElement now. See
    // ScrollRuntimeState below (still used by that instance table).

    public DialogRuntimeState Dialog { get; } = new();

    public ShadowRuntimeState Shadow { get; } = new();

    public StyleSheetRuntimeState StyleSheet { get; } = new();

    public DocumentRuntimeState Document { get; } = new();

    public AnimationRuntimeState Animation { get; } = new();

    public void CopyRuntimeValuesTo(ElementRuntimeState target)
    {
        FormControl.Value.CopyTo(target.FormControl.Value);
        FormControl.Checked.CopyTo(target.FormControl.Checked);
        FormControl.DefaultSelected.CopyTo(target.FormControl.DefaultSelected);
        FormControl.SelectedIndex.CopyTo(target.FormControl.SelectedIndex);
        FormControl.ReturnValue.CopyTo(target.FormControl.ReturnValue);
        // Scroll offsets are copied separately in CloneDomElement (moved to a per-bridge
        // instance table; this static-table struct no longer owns them).
        Dialog.Modal.CopyTo(target.Dialog.Modal);
        Dialog.TopLayerOrder.CopyTo(target.Dialog.TopLayerOrder);
        Dialog.PopoverOpen.CopyTo(target.Dialog.PopoverOpen);
        Shadow.Root.CopyTo(target.Shadow.Root);
        Shadow.Host.CopyTo(target.Shadow.Host);
        Shadow.Mode.CopyTo(target.Shadow.Mode);
        StyleSheet.FetchedCss.CopyTo(target.StyleSheet.FetchedCss);
        target.StyleSheet.Rules = StyleSheet.Rules is null ? null : [.. StyleSheet.Rules];
        target.StyleSheet.RulesSourceText = StyleSheet.RulesSourceText;
        target.StyleSheet.RulesMutated = StyleSheet.RulesMutated;
        Document.HasViewport.CopyTo(target.Document.HasViewport);
        Animation.CurrentTimeMilliseconds.CopyTo(target.Animation.CurrentTimeMilliseconds);
    }
}

internal sealed class FormControlRuntimeState
{
    public RuntimeValue<string> Value { get; } = new();
    public RuntimeValue<bool> Checked { get; } = new();
    public RuntimeValue<bool> DefaultSelected { get; } = new();
    public RuntimeValue<int> SelectedIndex { get; } = new();
    public RuntimeValue<string> ReturnValue { get; } = new();
}

internal sealed class ScrollRuntimeState
{
    public RuntimeValue<double> Left { get; } = new();
    public RuntimeValue<double> Top { get; } = new();
}

internal sealed class DialogRuntimeState
{
    public RuntimeValue<bool> Modal { get; } = new();
    public RuntimeValue<int> TopLayerOrder { get; } = new();

    // Popover API (HTML §popover): set by showPopover(), cleared by
    // hidePopover() — except when an `overlay` allow-discrete transition keeps
    // the element in the top layer as it animates out, in which case it stays
    // set so its ::backdrop still renders for the snapshot.
    public RuntimeValue<bool> PopoverOpen { get; } = new();
}

internal sealed class ShadowRuntimeState
{
    public RuntimeValue<DomElement> Root { get; } = new();
    public RuntimeValue<DomElement> Host { get; } = new();
    public RuntimeValue<string> Mode { get; } = new();
}

internal sealed class StyleSheetRuntimeState
{
    public RuntimeValue<string> FetchedCss { get; } = new();

    /// <summary>
    /// The live, mutable CSSOM rule list backing this style element's stylesheet —
    /// the single source of truth shared by the CSSOM (<c>cssRules</c>/<c>insertRule</c>/
    /// <c>deleteRule</c>), the renderer/legacy-cascade text, and the
    /// <c>getComputedStyle</c> engine sheet (Phase 6 store unification). <c>null</c>
    /// until first materialized from <see cref="RulesSourceText"/>.
    /// </summary>
    public List<CssRule>? Rules { get; set; }

    /// <summary>
    /// The source text <see cref="Rules"/> was last parsed from. When the element's
    /// current source text differs (e.g. <c>textContent</c> was replaced), the rules
    /// are reparsed — discarding any <c>insertRule</c>/<c>deleteRule</c> mutations,
    /// per CSSOM semantics.
    /// </summary>
    public string? RulesSourceText { get; set; }

    /// <summary>
    /// <c>true</c> once <c>insertRule</c>/<c>deleteRule</c> has mutated <see cref="Rules"/>
    /// away from the parsed source. While <c>false</c>, the renderer text is the raw
    /// author source (byte-identical to pre-Phase-6); once <c>true</c>, the renderer
    /// text is serialized from the model so the mutation is observed downstream.
    /// </summary>
    public bool RulesMutated { get; set; }
}

internal sealed class DocumentRuntimeState
{
    public RuntimeValue<bool> HasViewport { get; } = new();
}

internal sealed class AnimationRuntimeState
{
    public RuntimeValue<double> CurrentTimeMilliseconds { get; } = new();
}

internal sealed class RuntimeValue<T>
{
    public bool IsSet { get; private set; }

    public T? Value { get; private set; }

    public void Set(object? value)
    {
        Value = value is null ? default : (T)value;
        IsSet = true;
    }

    public bool TryGet(out object? value)
    {
        value = Value;
        return IsSet;
    }

    public bool Remove()
    {
        var wasSet = IsSet;
        Value = default;
        IsSet = false;
        return wasSet;
    }

    public void CopyTo(RuntimeValue<T> target)
    {
        if (IsSet)
            target.Set(Value);
        else
            target.Remove();
    }
}
