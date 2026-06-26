using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

internal readonly record struct EventListenerRegistration(
    JSValue Listener,
    bool Capture,
    bool Once = false,
    bool Passive = false);

internal sealed class MutationObserverOptions
{
    public bool ChildList { get; set; }

    public bool Attributes { get; set; }

    public bool AttributeOldValue { get; set; }

    public bool CharacterData { get; set; }

    public bool CharacterDataOldValue { get; set; }

    public bool Subtree { get; set; }
}

/// <summary>
/// JavaScript and browser-runtime state associated with a legacy DOM node.
/// The node model deliberately does not own this state.
/// </summary>
internal sealed class ElementRuntimeState
{
    public Dictionary<string, List<EventListenerRegistration>> EventListeners { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, JSValue> InlineEventHandlers { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public FormControlRuntimeState FormControl { get; } = new();

    public ScrollRuntimeState Scroll { get; } = new();

    public LayoutRuntimeState Layout { get; } = new();

    public DialogRuntimeState Dialog { get; } = new();

    public ShadowRuntimeState Shadow { get; } = new();

    public StyleSheetRuntimeState StyleSheet { get; } = new();

    public DocumentRuntimeState Document { get; } = new();

    public AnimationRuntimeState Animation { get; } = new();

    public DocumentTypeRuntimeState DocumentType { get; } = new();

    public void CopyRuntimeValuesTo(ElementRuntimeState target)
    {
        FormControl.Value.CopyTo(target.FormControl.Value);
        FormControl.Checked.CopyTo(target.FormControl.Checked);
        FormControl.DefaultSelected.CopyTo(target.FormControl.DefaultSelected);
        FormControl.SelectedIndex.CopyTo(target.FormControl.SelectedIndex);
        FormControl.ReturnValue.CopyTo(target.FormControl.ReturnValue);
        Scroll.Left.CopyTo(target.Scroll.Left);
        Scroll.Top.CopyTo(target.Scroll.Top);
        Layout.Left.CopyTo(target.Layout.Left);
        Layout.Top.CopyTo(target.Layout.Top);
        Layout.Width.CopyTo(target.Layout.Width);
        Layout.Height.CopyTo(target.Layout.Height);
        Dialog.Modal.CopyTo(target.Dialog.Modal);
        Dialog.TopLayerOrder.CopyTo(target.Dialog.TopLayerOrder);
        Shadow.Root.CopyTo(target.Shadow.Root);
        Shadow.Host.CopyTo(target.Shadow.Host);
        Shadow.Mode.CopyTo(target.Shadow.Mode);
        StyleSheet.FetchedCss.CopyTo(target.StyleSheet.FetchedCss);
        target.StyleSheet.Rules = StyleSheet.Rules is null ? null : [.. StyleSheet.Rules];
        target.StyleSheet.RulesSourceText = StyleSheet.RulesSourceText;
        target.StyleSheet.RulesMutated = StyleSheet.RulesMutated;
        Document.HasViewport.CopyTo(target.Document.HasViewport);
        Animation.CurrentTimeMilliseconds.CopyTo(target.Animation.CurrentTimeMilliseconds);
        DocumentType.Name.CopyTo(target.DocumentType.Name);
        DocumentType.PublicId.CopyTo(target.DocumentType.PublicId);
        DocumentType.SystemId.CopyTo(target.DocumentType.SystemId);
        DocumentType.InternalSubset.CopyTo(target.DocumentType.InternalSubset);
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

internal sealed class LayoutRuntimeState
{
    public RuntimeValue<double> Left { get; } = new();
    public RuntimeValue<double> Top { get; } = new();
    public RuntimeValue<double> Width { get; } = new();
    public RuntimeValue<double> Height { get; } = new();
}

internal sealed class DialogRuntimeState
{
    public RuntimeValue<bool> Modal { get; } = new();
    public RuntimeValue<int> TopLayerOrder { get; } = new();
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
    public List<Broiler.CSS.CssRule>? Rules { get; set; }

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

internal sealed class DocumentTypeRuntimeState
{
    public RuntimeValue<string> Name { get; } = new();
    public RuntimeValue<string> PublicId { get; } = new();
    public RuntimeValue<string> SystemId { get; } = new();
    public RuntimeValue<object> InternalSubset { get; } = new();
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
