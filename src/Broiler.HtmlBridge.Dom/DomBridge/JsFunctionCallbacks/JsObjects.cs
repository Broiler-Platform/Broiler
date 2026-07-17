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

    private JSValue JsJsObjectsSetId002Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        var val = a.Length > 0 ? a[0].ToString() : string.Empty;
        element.Id = val;
        SetAttr(element, "id", val);
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetClassName003Core(DomElement element, in Arguments a)
    {
        // Prefer Attributes['class'] (synced by setAttribute and className setter).
        // Fall back to element.ClassName for elements created with a class in the constructor
        // but not yet synced to Attributes (e.g. parsed HTML elements).
        if (TryGetAttribute(element, "class", out var cls))
            return new JSString(cls);
        return element.ClassName != null ? new JSString(element.ClassName) : new JSString(string.Empty);
    }


    private JSValue JsJsObjectsSetClassName004Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        var val = a.Length > 0 ? a[0].ToString() : string.Empty;
        element.ClassName = val;
        SetAttr(element, "class", val);
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetTitle006Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "title", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetLang008Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "lang", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetAccessKey010Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "accesskey", a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetDir012Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        SetAttr(element, "dir", a.Length > 0 ? a[0].ToString() : string.Empty);
        bridge.InvalidateStyleScope(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetDraggable013Core(DomElement element, in Arguments _)
    {
        if (TryGetAttribute(element, "draggable", out var draggable))
            return string.Equals(draggable, "true", StringComparison.OrdinalIgnoreCase) ? JSBoolean.True : JSBoolean.False;
        return JSBoolean.False;
    }


    private JSValue JsJsObjectsSetDraggable014Core(DomElement element, in Arguments a)
    {
        SetAttr(element, "draggable", a.Length > 0 && a[0].BooleanValue ? "true" : "false");
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetInnerHTML016Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        bridge.SetElementInnerHtml(element, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetOuterHTML018Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        bridge.SetElementOuterHtml(element, a.Length > 0 ? a[0].ToString() : string.Empty);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetShadowRoot019Core(DomElement element, in Arguments _)
    {
        var shadowRoot = GetShadowRoot(element);
        if (shadowRoot == null)
            return JSNull.Value;
        var mode = GetElementRuntimeState(element).Shadow.Mode.TryGet(out var rawMode) ? rawMode as string : null;
        return string.Equals(mode, "open", StringComparison.OrdinalIgnoreCase) ? ToJSObject(shadowRoot) : JSNull.Value;
    }


    private JSValue JsJsObjectsSetTextContent021Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        var text = a.Length > 0 ? a[0].ToString() : string.Empty;
        // Setting textContent replaces all children with a single text node per DOM spec.
        bridge.SetElementTextContent(element, text);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetStyle025Core(DomBridge? bridge, DomElement element, in Arguments a)
    {
        if (a.Length > 0 && a[0] is JSString s)
        {
            // Setting element.style = "prop: val; ..." parses as cssText
            InlineStyle(element).Clear();
            GetElementRuntimeState(element).JsSetStyleProps.Clear();
            foreach (var kv in ParseStyle(s.ToString(), reportDrops: true))
            {
                InlineStyle(element)[kv.Key] = kv.Value;
                GetElementRuntimeState(element).JsSetStyleProps.Add(kv.Key);
            }

            // Phase 4 item 2: write-through so getAttribute("style") observes the assignment.
            bridge.SyncStyleAttributeFromInlineStyle(element);
            bridge.InvalidateStyleScope(element);
        }

        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsInsertBefore080Core(DomBridge? bridgeForInsert, DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        if (a[0] is not JSObject newChildObj)
            return JSUndefined.Value;
        var newEl = FindDomNodeByJSObject(newChildObj);
        if (newEl == null)
            return a[0];
        // Prevent circular references (HierarchyRequestError per DOM spec)
        if (ReferenceEquals(newEl, element) || element.IsDescendantOf(newEl))
            ThrowDOMException(_jsContext!, "The new child element contains the parent.", "HierarchyRequestError");
        if (a.Length < 2 || a[1].IsNull || a[1].IsUndefined)
        {
            bridgeForInsert.InsertNodeAt(element, newEl, element.ChildNodes.Count);
            return a[0];
        }

        if (a[1] is not JSObject refChildObj)
            return a[0];
        var refEl = FindDomNodeByJSObject(refChildObj);
        if (refEl == null)
            return a[0];
        if (ReferenceEquals(newEl, refEl))
            return a[0];
        var idx = ChildIndexOf(element, refEl);
        if (idx < 0)
            throw new JSException("NotFoundError: The node before which the new node is to be inserted is not a child of this node.");
        bridgeForInsert.InsertNodeAt(element, newEl, idx);
        return a[0];
    }


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
        GetElementRuntimeState(shadowRoot).Shadow.Host.Set(element);
        GetElementRuntimeState(shadowRoot).Shadow.Mode.Set(mode);
        GetElementRuntimeState(element).Shadow.Root.Set(shadowRoot);
        GetElementRuntimeState(element).Shadow.Mode.Set(mode);
        return ToJSObject(shadowRoot);
    }


    private JSValue JsJsObjectsAppendChild088Core(DomBridge? bridgeForAppend, DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        if (a[0] is not JSObject childObj)
            return JSUndefined.Value;
        // Find the Broiler.Dom.DomElement for this child JSObject
        var childEl = FindDomNodeByJSObject(childObj);
        if (childEl == null)
            return a[0];
        // Prevent circular references (HierarchyRequestError per DOM spec)
        if (ReferenceEquals(childEl, element) || element.IsDescendantOf(childEl))
            ThrowDOMException(_jsContext!, "The new child element contains the parent.", "HierarchyRequestError");
        bridgeForAppend.InsertNodeAt(element, childEl, element.ChildNodes.Count);
        return a[0];
    }


    private JSValue JsJsObjectsAppend089Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var nodes = BuildChildNodeArgumentNodes(a);
        var insertIndex = element.ChildNodes.Count;
        foreach (var node in nodes)
            InsertNodeAt(element, node, insertIndex++);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsPrepend090Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var nodes = BuildChildNodeArgumentNodes(a);
        var insertIndex = 0;
        foreach (var node in nodes)
            InsertNodeAt(element, node, insertIndex++);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsRemoveChild091Core(DomBridge? bridgeForAppend, DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        if (a[0] is not JSObject childObj)
            return JSUndefined.Value;
        var childEl = FindDomNodeByJSObject(childObj);
        if (childEl == null)
            return a[0];
        var idx = ChildIndexOf(element, childEl);
        if (idx < 0)
            return a[0];
        NotifyNodeIteratorPreRemoval(childEl);
        RemoveNthChild(element, idx);
        SetParent(childEl, null);
        bridgeForAppend.InvalidateStyleScope(element);
        NotifyChildRemoved(element, childEl, idx);
        return a[0];
    }


    private JSValue JsJsObjectsReplaceChild092Core(DomBridge? bridgeForAppend, DomElement element, in Arguments a)
    {
        if (a.Length < 2)
            return JSUndefined.Value;
        if (a[0] is not JSObject newChildObj || a[1] is not JSObject oldChildObj)
            return JSUndefined.Value;
        var newEl = FindDomNodeByJSObject(newChildObj);
        var oldEl = FindDomNodeByJSObject(oldChildObj);
        if (newEl == null || oldEl == null)
            return a[1];
        // Prevent circular references (HierarchyRequestError per DOM spec)
        if (ReferenceEquals(newEl, element) || element.IsDescendantOf(newEl))
            ThrowDOMException(_jsContext!, "The new child element contains the parent.", "HierarchyRequestError");
        var idx = ChildIndexOf(element, oldEl);
        if (idx < 0)
            return a[1];
        var previousSibling = idx > 0 ? ChildAt(element, idx - 1) : null;
        var nextSibling = idx + 1 < element.ChildNodes.Count ? ChildAt(element, idx + 1) : null;
        // If newChild is already in this parent, remove it first and re-find idx
        if (ReferenceEquals(ParentEl(newEl), element))
        {
            RemoveChildFrom(element, newEl);
            idx = ChildIndexOf(element, oldEl);
            if (idx < 0)
                return a[1];
        }
        else
        {
            if (ParentEl(newEl) != null)
            {
                var oldParent = ParentEl(newEl);
                var oldIndex = ChildIndexOf(oldParent, newEl);
                if (oldIndex >= 0)
                {
                    NotifyNodeIteratorPreRemoval(newEl);
                    RemoveNthChild(oldParent, oldIndex);
                    NotifyChildRemoved(oldParent, newEl, oldIndex);
                }
            }
        }

        SetParent(oldEl, null);
        SetParent(newEl, element);
        element.ReplaceChild(newEl, element.ChildNodes[idx]);
        bridgeForAppend.InvalidateStyleScope(element);
        NotifyChildRemoved(element, oldEl, idx, previousSibling, nextSibling);
        NotifyChildAdded(element, newEl, idx);
        return a[1]; // returns the old child
    }


    private JSValue JsJsObjectsRemove093Core(DomNode element, in Arguments a)
    {
        // Capture the parent up front: ParentEl(node) is computed from the canonical
        // ParentNode, and Children.RemoveAt detaches it — so reading ParentEl(element) after
        // the removal would return null (→ NRE in InvalidateStyleScope). Mirrors the
        // working removeChild path, which holds the parent reference independently.
        var parent = ParentEl(element);
        if (parent != null)
        {
            var idx = ChildIndexOf(parent, element);
            if (idx >= 0)
            {
                NotifyNodeIteratorPreRemoval(element);
                RemoveNthChild(parent, idx);
                SetParent(element, null);
                InvalidateStyleScope(parent);
                NotifyChildRemoved(parent, element, idx);
            }
        }

        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsBefore094Core(DomNode element, in Arguments a)
    {
        if (ParentEl(element) == null || a.Length == 0)
            return JSUndefined.Value;
        var nodes = BuildChildNodeArgumentNodes(a);
        var insertIndex = ChildIndexOf(ParentEl(element), element);
        if (insertIndex < 0)
            return JSUndefined.Value;
        foreach (var node in nodes)
            InsertNodeAt(ParentEl(element), node, insertIndex++);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsAfter095Core(DomNode element, in Arguments a)
    {
        if (ParentEl(element) == null || a.Length == 0)
            return JSUndefined.Value;
        var nodes = BuildChildNodeArgumentNodes(a);
        var insertIndex = ChildIndexOf(ParentEl(element), element);
        if (insertIndex < 0)
            return JSUndefined.Value;
        insertIndex++;
        foreach (var node in nodes)
            InsertNodeAt(ParentEl(element), node, insertIndex++);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsReplaceWith096Core(DomNode element, in Arguments a)
    {
        if (ParentEl(element) == null)
            return JSUndefined.Value;
        var parent = ParentEl(element);
        var replacementIndex = ChildIndexOf(parent, element);
        if (replacementIndex < 0)
            return JSUndefined.Value;
        var nodes = BuildChildNodeArgumentNodes(a);
        NotifyNodeIteratorPreRemoval(element);
        RemoveNthChild(parent, replacementIndex);
        SetParent(element, null);
        InvalidateStyleScope(parent);
        NotifyChildRemoved(parent, element, replacementIndex);
        foreach (var node in nodes)
            InsertNodeAt(parent, node, replacementIndex++);
        return JSUndefined.Value;
    }


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
        if (GetElementRuntimeState(element).FormControl.Value.TryGet(out var domVal) && domVal is string sv)
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
            GetElementRuntimeState(element).FormControl.Value.Set(v); // IDL value, not reflected
        else if (tag == "select")
            _select.SetValue(element, v);
        else
            SetAttr(element, "value", v);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsGetChecked108Core(DomElement element, in Arguments a)
    {
        // IDL property takes precedence over content attribute
        if (GetElementRuntimeState(element).FormControl.Checked.TryGet(out var v))
            return v is true ? JSBoolean.True : JSBoolean.False;
        return HasAttr(element, "checked") ? JSBoolean.True : JSBoolean.False;
    }


    private JSValue JsJsObjectsSetChecked109Core(DomElement element, in Arguments a)
    {
        bool newVal = a.Length > 0 && a[0].BooleanValue;
        GetElementRuntimeState(element).FormControl.Checked.Set(newVal);
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


    private JSValue JsJsObjectsInsertAdjacentElement130Core(DomElement element, in Arguments a)
    {
        if (a.Length < 2)
            return JSNull.Value;
        var position = NormalizeInsertAdjacentPosition(a[0]);
        if (a[1] is not JSObject adjacentObject)
            return JSNull.Value;
        var adjacentElement = FindDomElementByJSObject(adjacentObject);
        if (adjacentElement == null)
            return JSNull.Value;
        var (parent, index) = GetInsertAdjacentTarget(element, position);
        InsertNodeAt(parent, adjacentElement, index);
        return a[1];
    }


    private JSValue JsJsObjectsInsertAdjacentText131Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var position = NormalizeInsertAdjacentPosition(a[0]);
        var text = a.Length > 1 ? a[1].ToString() : string.Empty;
        var (parent, index) = GetInsertAdjacentTarget(element, position);
        var textNode = CreateBridgeTextNode(text);
        InsertNodeAt(parent, textNode, index);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsInsertAdjacentHTML132Core(DomElement element, in Arguments a)
    {
        if (a.Length == 0)
            return JSUndefined.Value;
        var position = NormalizeInsertAdjacentPosition(a[0]);
        var html = a.Length > 1 ? a[1].ToString() : string.Empty;
        if (string.IsNullOrEmpty(html))
            return JSUndefined.Value;
        DomElement parsingContext;
        switch (position)
        {
            case "beforebegin":
            case "afterend":
                if (ParentEl(element) == null)
                    ThrowDOMException(_jsContext!, "Cannot insert adjacent HTML without a parent node.", "NoModificationAllowedError");
                parsingContext = ParentEl(element)!;
                break;
            default:
                parsingContext = element;
                break;
        }

        var (parent, index) = GetInsertAdjacentTarget(element, position);
        var nodes = BuildAdjacentHtmlNodes(parsingContext, html);
        foreach (var node in nodes)
            InsertNodeAt(parent, node, index++);
        ResetComputedStyleEngines();
        return JSUndefined.Value;
    }


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


    private JSValue JsJsObjectsGetContentDocument135Core(DomElement element, in Arguments _)
    {
        // Cross-origin iframes return null for contentDocument (same-origin policy)
        if (IsCurrentIframeCrossOrigin(element))
            return JSNull.Value;
        // Non-HTML resources get a minimal empty sub-document (no parsed fallback content)
        return GetOrCreateSubDocument(element);
    }


    private JSValue JsJsObjectsGetContentWindow136Core(DomElement element, in Arguments _)
    {
        if (IsCurrentIframeCrossOrigin(element))
            return JSNull.Value;
        return _subWindows.GetOrCreate(element);
    }


    private JSValue JsJsObjectsGetSVGDocument137Core(DomElement element, in Arguments _)
    {
        if (IsCurrentIframeCrossOrigin(element))
            return JSNull.Value;
        return GetOrCreateSubDocument(element);
    }


    private JSValue JsJsObjectsSetSrc139Core(DomBridge? bridgeForSrc, DomElement element, in Arguments a)
    {
        SetAttr(element, "src", a.Length > 0 ? a[0].ToString() : string.Empty);
        // Invalidate cached sub-document when src changes
        InvalidateCachedSubDocument(element);
        _browsingContexts.ClearOnloadFired(element);
        // Fire onload for the new resource
        bridgeForSrc.FireSubDocumentOnload(element);
        return JSUndefined.Value;
    }


    private JSValue JsJsObjectsSetSrcdoc141Core(DomBridge? bridgeForSrc, DomElement element, in Arguments a)
    {
        SetAttr(element, "srcdoc", a.Length > 0 ? a[0].ToString() : string.Empty);
        InvalidateCachedSubDocument(element);
        _browsingContexts.ClearOnloadFired(element);
        bridgeForSrc.FireSubDocumentOnload(element);
        return JSUndefined.Value;
    }

}
