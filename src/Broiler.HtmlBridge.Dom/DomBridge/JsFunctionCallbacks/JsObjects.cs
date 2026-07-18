using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.HtmlBridge.Logging;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    // HTMLElement global content-attribute reflectors (id, className, title, lang, accessKey, dir,
    // draggable) moved to the GlobalAttributeBinding feature module (Phase 3 P3.54).

    // innerHTML / outerHTML / textContent get+set moved to the ElementContentBinding feature module
    // (Phase 3 P3.57).

    private JSValue JsJsObjectsGetShadowRoot019Core(DomElement element, in Arguments _)
    {
        var shadowRoot = GetShadowRoot(element);
        if (shadowRoot == null)
            return JSNull.Value;
        var mode = ShadowStateFor(element).Mode.TryGet(out var rawMode) ? rawMode as string : null;
        return string.Equals(mode, "open", StringComparison.OrdinalIgnoreCase) ? ToJSObject(shadowRoot) : JSNull.Value;
    }


    private JSValue JsJsObjectsSetStyle025Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSString s)
        {
            // Setting element.style = "prop: val; ..." parses as cssText
            InlineStyle(element).Clear();
            InlineStyleStateFor(element).JsSetStyleProps.Clear();
            foreach (var kv in ParseStyle(s.ToString(), reportDrops: true))
            {
                InlineStyle(element)[kv.Key] = kv.Value;
                InlineStyleStateFor(element).JsSetStyleProps.Add(kv.Key);
            }

            // Phase 4 item 2: write-through so getAttribute("style") observes the assignment.
            bridge.SyncStyleAttributeFromInlineStyle(element);
            bridge.InvalidateStyleScope(element);
        }

        return JSUndefined.Value;
    }


    // insertBefore(newChild, refChild) moved to the TreeMutationBinding feature module (Phase 3 P3.58).

    private JSValue JsJsObjectsAttachShadow087Core(DomElement element, in Arguments a)
    {
        if (GetShadowRoot(element) != null)
            ThrowDOMException(_jsContext!, "Shadow root already attached.", "NotSupportedError");
        var mode = "open";
        if (a.Length > 0 && a[0] is JSObject options)
        {
            var modeValue = options[(KeyString)"mode"];
            if (modeValue != null && !modeValue.IsUndefined && !modeValue.IsNull)
            {
                mode = modeValue.ToString();
            }
        }

        mode = string.Equals(mode, "closed", StringComparison.OrdinalIgnoreCase) ? "closed" : "open";
        var shadowRoot = CreateBridgeElement("#shadow-root");
        // SetParent links the shadow root to its host, so GetOwningDocument derives the shadow root's
        // owning document from the host's tree position — no OwnerDocRoot inheritance needed (P4.4c).
        SetParent(shadowRoot, element);
        ShadowStateFor(shadowRoot).Host.Set(element);
        ShadowStateFor(shadowRoot).Mode.Set(mode);
        ShadowStateFor(element).Root.Set(shadowRoot);
        ShadowStateFor(element).Mode.Set(mode);
        return ToJSObject(shadowRoot);
    }


    // appendChild / append / prepend / removeChild / replaceChild moved to the TreeMutationBinding
    // feature module (Phase 3 P3.58).

    private JSValue JsJsObjectsCallback104Core(DomElement element, global::System.String? eventName, in Arguments _)
    {
        if (GetInlineEventHandlers(element).TryGetValue(eventName, out var handler))
            return handler;
        return JSNull.Value;
    }


    private JSValue JsJsObjectsCallback105Core(DomElement element, global::System.String? eventName, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSFunction fn)
            GetInlineEventHandlers(element)[eventName] = fn;
        else
            GetInlineEventHandlers(element).Remove(eventName);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetValue106Core(DomElement element, in Arguments a)
    {
        if (string.Equals(element.TagName, "select", StringComparison.OrdinalIgnoreCase))
            return new JSString(_select.GetValue(element));
        if (FormControlStateFor(element).Value.TryGet(out var domVal) && domVal is string sv)
            return new JSString(sv);
        if (TryGetAttribute(element, "value", out var val))
            return new JSString(val);
        return new JSString(string.Empty);
    }


    private JSValue JsJsObjectsSetValue107Core(DomElement element, in Arguments a)
    {
        var tag = element.TagName.ToLowerInvariant();
        var v = a.Length > 0 ? a[0].ToString() : string.Empty;
        if (tag == "input")
            FormControlStateFor(element).Value.Set(v); // IDL value, not reflected
        else if (tag == "select")
            _select.SetValue(element, v);
        else
            SetAttr(element, "value", v);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetChecked108Core(DomElement element, in Arguments a)
    {
        // IDL property takes precedence over content attribute
        if (FormControlStateFor(element).Checked.TryGet(out var v))
            return v is true ? JSBoolean.True : JSBoolean.False;
        return HasAttr(element, "checked") ? JSBoolean.True : JSBoolean.False;
    }


    private JSValue JsJsObjectsSetChecked109Core(DomElement element, in Arguments a)
    {
        bool newVal = a.Length > 0 && a[0].BooleanValue;
        FormControlStateFor(element).Checked.Set(newVal);
        if (newVal)
        {
            // Radio button mutual exclusion: uncheck others in same group
            if (TryGetAttribute(element, "type", out var tp) && string.Equals(tp, "radio", StringComparison.OrdinalIgnoreCase) && TryGetAttribute(element, "name", out var radioName) && !string.IsNullOrEmpty(radioName))
            {
                // Find the scope for radio group — form parent, or document root if not in a form
                var scope = ParentEl(element);
                while (scope != null && !string.Equals(scope.TagName, "form", StringComparison.OrdinalIgnoreCase))
                    scope = ParentEl(scope);
                if (scope == null)
                {
                    scope = element;
                    while (ParentEl(scope) != null)
                        scope = ParentEl(scope);
                }

                UncheckRadioSiblings(scope, element, radioName);
            }
        }

        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetType110Core(DomElement element, in Arguments a)
    {
        if (TryGetAttribute(element, "type", out var t))
            return new JSString(t.ToLowerInvariant());
        // Default type values per HTML spec
        var tag = element.TagName.ToLowerInvariant();
        if (tag == "button")
            return new JSString("submit");
        return new JSString(string.Empty);
    }


    private JSValue JsJsObjectsSetType111Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "type", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetName112Core(DomElement element, in Arguments a)
    {
        if (TryGetAttribute(element, "name", out var n))
            return new JSString(n);
        return new JSString(string.Empty);
    }


    private JSValue JsJsObjectsSetName113Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "name", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetDisabled115Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0].BooleanValue)
            SetAttr(element, "disabled", "disabled");
        else
            RemoveAttr(element, "disabled");
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetHidden117Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0].BooleanValue)
            SetAttr(element, "hidden", string.Empty);
        else
            RemoveAttr(element, "hidden");
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetTabIndex118Core(DomElement element, in Arguments _)
    {
        if (TryGetAttribute(element, "tabindex", out var rawTabIndex) && int.TryParse(rawTabIndex, out var parsedTabIndex))
        {
            return new JSNumber(parsedTabIndex);
        }

        return new JSNumber(-1);
    }


    private JSValue JsJsObjectsSetTabIndex119Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var tabIndex = (int)Math.Truncate(a[0].DoubleValue);
        SetAttr(element, "tabindex", tabIndex.ToString());
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetRequired121Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0].BooleanValue)
            SetAttr(element, "required", "required");
        else
            RemoveAttr(element, "required");
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSubmit125Core(DomElement element, JSObject? obj, in Arguments a)
    {
        if (string.Equals(element.TagName, "form", StringComparison.OrdinalIgnoreCase))
        {
            // Fire submit event
            var submitEvt = new JSObject();
            submitEvt.FastAddValue((KeyString)"type", new JSString("submit"), JSPropertyAttributes.EnumerableConfigurableValue);
            submitEvt.FastAddValue((KeyString)"target", obj, JSPropertyAttributes.EnumerableConfigurableValue);
            submitEvt.FastAddValue((KeyString)"bubbles", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
            submitEvt.FastAddValue((KeyString)"cancelable", JSBoolean.True, JSPropertyAttributes.EnumerableConfigurableValue);
            var prevented = false;
            submitEvt.FastAddValue((KeyString)"defaultPrevented", JSBoolean.False, JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsJsObjectsPreventDefault124(in Arguments _)
            {
                prevented = true;
                submitEvt[(KeyString)"defaultPrevented"] = JSBoolean.True;
                return JSUndefined.Value;
            }

            submitEvt.FastAddValue((KeyString)"preventDefault", new JSFunction(JsJsObjectsPreventDefault124, "preventDefault", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            submitEvt.FastAddValue((KeyString)"stopPropagation", UndefinedFunction("stopPropagation", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            if (GetEventListeners(element).TryGetValue("submit", out var submitListeners))
            {
                foreach (var registration in submitListeners.ToList())
                {
                    InvokeEventListener(registration.Listener, submitEvt, "DomBridge.submit");
                }
            }

            // If preventDefault was called, do not proceed with default action
            if (prevented)
            {
                RenderLogger.LogDebug(LogCategory.JavaScript, "DomBridge.submit", "Default action prevented");
            }
        }

        return JSUndefined.Value;
    }


    // insertAdjacentElement / insertAdjacentText / insertAdjacentHTML (and their
    // NormalizeInsertAdjacentPosition / GetInsertAdjacentTarget helpers) moved to the
    // InsertAdjacentBinding feature module (Phase 3 P3.56).

    private JSValue JsJsObjectsGetContext134Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSNull.Value;
        var contextType = a[0].ToString();
        if (!string.Equals(contextType, "2d", StringComparison.OrdinalIgnoreCase))
            return JSNull.Value;
        if (!string.Equals(element.TagName, "canvas", StringComparison.OrdinalIgnoreCase))
            return JSNull.Value;
#if BROILER_CLI
                            return JSNull.Value; // Canvas 2D context not available in CLI mode
#else
        return BuildCanvas2DContext(element);
#endif
    }


    // <iframe> browsing-context accessors (contentDocument/contentWindow/getSVGDocument, src/srcdoc
    // setters) moved to the IframeElementBinding feature module (Phase 3 P3.55).

}
