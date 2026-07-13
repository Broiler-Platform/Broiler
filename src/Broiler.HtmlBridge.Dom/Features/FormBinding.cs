using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The HTMLFormElement feature binding (HtmlBridge complexity-reduction roadmap Phase 3, P3.9) —
/// <c>form.elements</c> (an <c>HTMLFormControlsCollection</c> with numeric and named access),
/// <c>form.length</c>, <c>form.action</c>, and the constraint-validation checks
/// (<c>checkValidity</c>/<c>reportValidity</c>) that the bridge exposes on form-associated elements.
/// Control collection and validity are pure tree/attribute work over the assembly's static
/// <c>DomBridge</c> helpers (<c>CollectFormControls</c>, <c>HasAttr</c>/<c>TryGetAttribute</c>,
/// <c>ChildElements</c>/<c>IsText</c>); the only bridge coupling — wrapping a control as a JS object —
/// goes through the narrow <see cref="IFormHost"/> contract.
/// </summary>
internal sealed class FormBinding(IFormHost host)
{
    private readonly IFormHost _host = host;

    /// <summary>Installs the <c>HTMLFormElement</c> members on <paramref name="obj"/> when
    /// <paramref name="element"/> is a <c>&lt;form&gt;</c>.</summary>
    internal void Install(JSObject obj, DomElement element, string tag)
    {
        if (tag != "form")
            return;

        // elements — the form controls collection (numeric + named access)
        obj.FastAddProperty((KeyString)"elements",
            new JSFunction((in _) => BuildElementsCollection(element), "get elements"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        // length — alias for elements.length
        obj.FastAddProperty((KeyString)"length",
            new JSFunction((in _) => new JSNumber(DomBridge.CollectFormControls(element).Count), "get length"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);
        // action (read/write)
        obj.FastAddProperty((KeyString)"action",
            new JSFunction((in _) => DomBridge.TryGetAttribute(element, "action", out var act) ? new JSString(act) : new JSString(string.Empty), "get action"),
            new JSFunction((in a) => SetAction(element, in a), "set action"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
    }

    private JSObject BuildElementsCollection(DomElement form)
    {
        var controls = DomBridge.CollectFormControls(form);

        // FormElementsCollection returns null for missing named properties (per
        // HTMLFormControlsCollection spec behaviour).
        var collection = new FormElementsCollection(form, _host);
        for (int i = 0; i < controls.Count; i++)
            collection.FastAddValue((uint)i, _host.ToJSObject(controls[i]),
                JSPropertyAttributes.EnumerableConfigurableValue);

        collection.FastAddProperty((KeyString)"length",
            new JSFunction((in _) => new JSNumber(DomBridge.CollectFormControls(form).Count), "get length"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        return collection;
    }

    private static JSValue SetAction(DomElement element, in Arguments a)
    {
        DomBridge.SetAttr(element, "action", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }

    // -------- Constraint validation --------

    /// <summary>Whether <paramref name="element"/> satisfies constraint validation — a form is valid
    /// when all its controls are; a required input/textarea/select needs a non-empty value.</summary>
    internal bool IsElementValid(DomElement element)
    {
        if (string.Equals(element.TagName, "form", StringComparison.OrdinalIgnoreCase))
            return AreFormChildrenValid(element);

        // Individual element validation
        if (!DomBridge.HasAttr(element, "required"))
            return true;

        var tag = element.TagName;
        if (string.Equals(tag, "input", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "textarea", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "select", StringComparison.OrdinalIgnoreCase))
        {
            DomBridge.TryGetAttribute(element, "value", out var val);
            return !string.IsNullOrEmpty(val);
        }

        return true;
    }

    private bool AreFormChildrenValid(DomElement form)
    {
        foreach (var child in DomBridge.ChildElements(form))
        {
            if (!DomBridge.IsText(child) && !IsElementValid(child))
                return false;
            if (!AreFormChildrenValid(child))
                return false;
        }

        return true;
    }

    /// <summary>The <c>form.elements</c> collection object: a JS array-like with numeric indices and
    /// a <c>length</c>, overriding property lookup so a missing name resolves against the form's
    /// named controls (returning null when unmatched, per HTMLFormControlsCollection).</summary>
    private sealed class FormElementsCollection(DomElement form, IFormHost host) : JSObject()
    {
        protected override JSValue GetValue(KeyString key, JSValue receiver, bool throwError = true)
        {
            // First check own properties (length, numeric indices, known names)
            var result = base.GetValue(key, receiver, false);
            if (result != null && !result.IsUndefined)
                return result;

            // Dynamic named lookup in form controls
            var prop = key.Value.ToString();
            if (!string.IsNullOrEmpty(prop))
            {
                var controls = DomBridge.CollectFormControls(form);
                foreach (var ctrl in controls)
                {
                    if (ctrl.GetAttribute("name") is { } name &&
                        string.Equals(name, prop, StringComparison.Ordinal))
                        return host.ToJSObject(ctrl);
                }
            }

            return JSNull.Value; // HTMLFormControlsCollection returns null for missing names
        }
    }
}
