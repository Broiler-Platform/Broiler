using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The form-control IDL reflectors (HtmlBridge complexity-reduction roadmap Phase 3) — <c>value</c>,
/// <c>checked</c>, <c>type</c>, <c>name</c>, <c>disabled</c>, <c>hidden</c>, <c>tabIndex</c> and
/// <c>required</c>, registered on every element wrapper. <c>value</c>/<c>checked</c> read and write the
/// input's dirty IDL state (and, for <c>&lt;select&gt;</c>, delegate to <see cref="SelectBinding"/>) via
/// the named primitives of the <see cref="IFormControlHost"/> contract; the remaining members are plain
/// content-attribute reflection through the assembly's static <c>DomBridge</c> attribute helpers, with
/// the boolean setters invalidating the style scope (the <c>:disabled</c>/<c>[hidden]</c>/<c>:required</c>
/// selectors depend on it). Was the bridge's <c>JsJsObjectsGetValue106Core</c>..<c>SetRequired121Core</c>
/// callbacks plus their inline registration.
/// </summary>
internal sealed class FormControlBinding(IFormControlHost host)
{
    private readonly IFormControlHost _host = host;

    /// <summary>Installs the form-control IDL reflector members on <paramref name="obj"/> for <paramref name="element"/>.</summary>
    internal void Install(JSObject obj, DomElement element)
    {
        // value (read/write) — for input, textarea, select elements.
        // The IDL 'value' property is NOT reflected as a content attribute for inputs.
        obj.FastAddProperty((KeyString)"value",
            new JSFunction((in _) => GetValue(element), "get value"),
            new JSFunction((in a) => SetValue(element, in a), "set value"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // checked (read/write) — for checkbox and radio inputs. Uses the typed checked-state slot as the
        // "dirty" IDL state that tracks programmatic changes; setAttribute("checked") only sets the
        // content attribute and does NOT affect this IDL state.
        obj.FastAddProperty((KeyString)"checked",
            new JSFunction((in _) => GetChecked(element), "get checked"),
            new JSFunction((in a) => SetChecked(element, in a), "set checked"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // type (read/write) — for input/button elements; getter returns lowercase.
        obj.FastAddProperty((KeyString)"type",
            new JSFunction((in _) => GetType(element), "get type"),
            new JSFunction((in a) => SetType(element, in a), "set type"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // name (read/write) — for form elements; syncs with content attribute.
        obj.FastAddProperty((KeyString)"name",
            new JSFunction((in _) => GetName(element), "get name"),
            new JSFunction((in a) => SetName(element, in a), "set name"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // disabled (read/write) — for form controls.
        obj.FastAddProperty((KeyString)"disabled",
            new JSFunction((in _) => DomBridge.HasAttr(element, "disabled") ? JSBoolean.True : JSBoolean.False, "get disabled"),
            new JSFunction((in a) => SetDisabled(element, in a), "set disabled"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // hidden (read/write) — global reflected boolean attribute.
        obj.FastAddProperty((KeyString)"hidden",
            new JSFunction((in _) => DomBridge.HasAttr(element, "hidden") ? JSBoolean.True : JSBoolean.False, "get hidden"),
            new JSFunction((in a) => SetHidden(element, in a), "set hidden"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // tabIndex (read/write) — global reflected numeric attribute.
        obj.FastAddProperty((KeyString)"tabIndex",
            new JSFunction((in _) => GetTabIndex(element), "get tabIndex"),
            new JSFunction((in a) => SetTabIndex(element, in a), "set tabIndex"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // required (read/write) — form validation.
        obj.FastAddProperty((KeyString)"required",
            new JSFunction((in _) => DomBridge.HasAttr(element, "required") ? JSBoolean.True : JSBoolean.False, "get required"),
            new JSFunction((in a) => SetRequired(element, in a), "set required"),
            JSPropertyAttributes.EnumerableConfigurableProperty);
    }

    private JSValue GetValue(DomElement element)
    {
        if (string.Equals(element.TagName, "select", StringComparison.OrdinalIgnoreCase))
            return new JSString(_host.GetSelectValue(element));
        if (_host.TryGetFormControlValue(element, out var sv))
            return new JSString(sv);
        if (DomBridge.TryGetAttribute(element, "value", out var val))
            return new JSString(val);
        return new JSString(string.Empty);
    }

    private JSValue SetValue(DomElement element, in Arguments a)
    {
        var tag = element.TagName.ToLowerInvariant();
        var v = a.Length > 0 ? a[0].ToString() : string.Empty;
        if (tag == "input")
            _host.SetFormControlValue(element, v); // IDL value, not reflected
        else if (tag == "select")
            _host.SetSelectValue(element, v);
        else
            DomBridge.SetAttr(element, "value", v);
        return JSUndefined.Value;
    }

    private JSValue GetChecked(DomElement element)
    {
        // IDL property takes precedence over content attribute
        if (_host.TryGetFormControlChecked(element, out var v))
            return v ? JSBoolean.True : JSBoolean.False;
        return DomBridge.HasAttr(element, "checked") ? JSBoolean.True : JSBoolean.False;
    }

    private JSValue SetChecked(DomElement element, in Arguments a)
    {
        bool newVal = a.Length > 0 && a[0].BooleanValue;
        _host.SetFormControlChecked(element, newVal);
        if (newVal)
        {
            // Radio button mutual exclusion: uncheck others in same group
            if (DomBridge.TryGetAttribute(element, "type", out var tp) && string.Equals(tp, "radio", StringComparison.OrdinalIgnoreCase) && DomBridge.TryGetAttribute(element, "name", out var radioName) && !string.IsNullOrEmpty(radioName))
            {
                // Find the scope for radio group — form parent, or document root if not in a form
                var scope = DomBridge.ParentEl(element);
                while (scope != null && !string.Equals(scope.TagName, "form", StringComparison.OrdinalIgnoreCase))
                    scope = DomBridge.ParentEl(scope);
                if (scope == null)
                {
                    scope = element;
                    while (DomBridge.ParentEl(scope) != null)
                        scope = DomBridge.ParentEl(scope);
                }

                _host.UncheckRadioSiblings(scope, element, radioName);
            }
        }

        return JSUndefined.Value;
    }

    private JSValue GetType(DomElement element)
    {
        if (DomBridge.TryGetAttribute(element, "type", out var t))
            return new JSString(t.ToLowerInvariant());
        // Default type values per HTML spec
        var tag = element.TagName.ToLowerInvariant();
        if (tag == "button")
            return new JSString("submit");
        return new JSString(string.Empty);
    }

    private JSValue SetType(DomElement element, in Arguments a)
    {
        DomBridge.SetAttr(element, "type", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }

    private JSValue GetName(DomElement element)
    {
        if (DomBridge.TryGetAttribute(element, "name", out var n))
            return new JSString(n);
        return new JSString(string.Empty);
    }

    private JSValue SetName(DomElement element, in Arguments a)
    {
        DomBridge.SetAttr(element, "name", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }

    private JSValue SetDisabled(DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0].BooleanValue)
            DomBridge.SetAttr(element, "disabled", "disabled");
        else
            DomBridge.RemoveAttr(element, "disabled");
        _host.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }

    private JSValue SetHidden(DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0].BooleanValue)
            DomBridge.SetAttr(element, "hidden", string.Empty);
        else
            DomBridge.RemoveAttr(element, "hidden");
        _host.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }

    private JSValue GetTabIndex(DomElement element)
    {
        if (DomBridge.TryGetAttribute(element, "tabindex", out var rawTabIndex) && int.TryParse(rawTabIndex, out var parsedTabIndex))
        {
            return new JSNumber(parsedTabIndex);
        }

        return new JSNumber(-1);
    }

    private JSValue SetTabIndex(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var tabIndex = (int)Math.Truncate(a[0].DoubleValue);
        DomBridge.SetAttr(element, "tabindex", tabIndex.ToString());
        return JSUndefined.Value;
    }

    private JSValue SetRequired(DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0].BooleanValue)
            DomBridge.SetAttr(element, "required", "required");
        else
            DomBridge.RemoveAttr(element, "required");
        _host.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }
}
