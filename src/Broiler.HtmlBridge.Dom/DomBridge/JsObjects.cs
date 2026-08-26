using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// Conversion of <see cref="DomElement"/> instances to YantraJS
/// <see cref="JSObject"/> representations, including sub-document
/// construction and tree-search helpers.
/// </summary>
public sealed partial class DomBridge
{
    private const double DefaultBodyMarginPixels = 8;
    private const int MaxScrollContinuationDepth = 16;

    // RF-BRIDGE-1c Phase F (F3b): the JS-object registry is keyed by canonical DomNode so
    // text/comment nodes (which get JS wrappers) can round-trip once they flip to canonical
    // DomText/DomComment. A facade node IS-A DomNode, so this is a behaviour-preserving widen.
    // P2.2: wrapper identity now lives in JsObjectRegistry, the single authority (was the scattered
    // _jsObjectCache/_docRootToDocJSObject fields).
    private readonly Dom.Runtime.JsObjectRegistry _jsObjects = new();
    /// <summary>Counter for tracking top-layer insertion order via showModal().</summary>
    private int _topLayerCounter;

    internal JSObject ToJSObject(DomNode node)
    {
        if (_jsObjects.TryGet(node, out var cached))
            return cached;

        // Phase 4 item 1: a canonical DomDocument is the document root. The main document is in the
        // node-wrapper map above; a sub-document root's wrapper lives in the document-wrapper map
        // (P2.2/P4.4a). Resolve it here so e.g. documentElement.parentNode returns the document
        // object, not a fallthrough character-data wrapper.
        if (node is DomDocument documentNode && _jsObjects.TryGetDocument(documentNode, out var documentWrapper))
            return documentWrapper;

        // A <form> gets a wrapper that additionally resolves an unknown name to the control carrying
        // it (HTMLFormElement's named getter). It is decided here rather than in the form binding
        // because a wrapper's type is fixed when it is created, and every member installed below goes
        // on this same object.
        var obj = node is DomElement formElement &&
                  string.Equals(formElement.TagName, "form", StringComparison.OrdinalIgnoreCase)
            ? new Dom.Features.FormElementJSObject(formElement, this)
            : new JSObject();
        _jsObjects.Set(node, obj);

        // Point the wrapper at its interface prototype before any member is installed, so
        // constructor.name and Object.getPrototypeOf answer the interface rather than Object.
        // Non-element nodes only — see WrapperPrototypes.cs for why an element's is a separate
        // question.
        ApplyInterfacePrototype(obj, node);

        // RF-BRIDGE-1c Phase F (F3c): canonical character-data nodes (DomText/DomComment) are not
        // Broiler.Dom.DomElement, so they receive a minimal Node/CharacterData wrapper instead of the full
        // element surface below. This branch is dead on today's homogeneous facade tree — facade
        // text/comment nodes are Broiler.Dom.DomElement and fall through to the element wrapper, preserving
        // behaviour — and goes live once text/comment construction flips to canonical
        // DomText/DomComment (F3c construction cutover).
        if (node is DomDocumentType docType)
        {
            // Phase 4 item 1: the doctype is a canonical DomDocumentType (was a #doctype sentinel
            // element). It gets the minimal DocumentType surface, not the full element wrapper.
            PopulateDocumentTypeJSObject(obj, docType);
            return obj;
        }

        if (node is DomDocumentFragment fragment)
        {
            // Phase 4 item 1: the fragment is a canonical DomDocumentFragment (was a
            // #document-fragment sentinel element). It gets the DocumentFragment container surface
            // (Node base + ParentNode mixin + child manipulation), not the full element wrapper.
            PopulateDocumentFragmentJSObject(obj, fragment);
            return obj;
        }

        if (node is not DomElement element)
        {
            PopulateCharacterDataJSObject(obj, node);
            return obj;
        }

        var bridge = this;

        // Element's whole interface — tagName, id/className, the attribute surface, classList,
        // innerHTML/outerHTML, the shadow-host pair, the ParentNode/ChildNode/element-sibling members,
        // the selector lookups, the box metrics, requestFullscreen and animate — lives on
        // Element.prototype and this wrapper inherits it (DomBridge.ElementInterface.cs). A wrapper
        // minted before the realm carried the interfaces inherits nothing and installs its own, from
        // the same installer, so the two shapes cannot drift.
        if (!_elementInterfacePrototypeReady)
            PopulateElementInterfaceOnInstance(obj, element);

        // The global content-attribute reflectors HTMLElement owns (title, lang, accessKey, dir,
        // draggable) — Phase 3 P3.54: extracted into the co-located GlobalAttributeBinding feature
        // module. dir invalidates the style scope on write through the one-member IGlobalAttributeHost
        // contract (DomBridge.GlobalAttributeHost.cs); id and className are Element's and are on its
        // prototype.
        Dom.Features.GlobalAttributeBinding.InstallHtmlElementMembers(this, obj, element);

        // textContent (read/write) + innerText / outerText (read) — Phase 3 P3.57: ElementContentBinding.
        Dom.Features.ElementContentBinding.InstallTextContent(this, obj, element);

        // style object — CSS property access and manipulation.
        // In browsers, `element.style` is a read-only property: assigning a
        // string sets `style.cssText` instead of replacing the object.
        // Phase 4 item 2: after every element.style mutation (per-property set, cssText, setProperty,
        // removeProperty, cssFloat — all route through this onMutation), write the dict through to the
        // canonical style= attribute so element.style and getAttribute("style") observe one state,
        // then invalidate computed style.
        // Phase 4 item 2: every element.style mutation writes the inline-style dict through to the
        // canonical style= attribute (so element.style and getAttribute("style") observe one state) and
        // then invalidates computed style. Shared by the declaration object's per-property/cssText
        // mutations and the `element.style = "..."` assignment setter (Phase 3 P3.63:
        // StyleDeclarationBinding.SetInlineStyleCssText).
        void OnStyleMutation()
        {
            bridge.SyncStyleAttributeFromInlineStyle(element);
            bridge.InvalidateStyleScope(element);
        }

        var styleObj = Dom.Features.StyleDeclarationBinding.BuildInlineDeclaration(bridge, element, OnStyleMutation,
            onPositionAreaInvalidate: bridge.ClearPositionAreaResolution);
        obj.FastAddProperty((KeyString)"style",
            new DomFunction((in a) => styleObj, "get style"),
            new DomFunction((in a) => Dom.Features.StyleDeclarationBinding.SetInlineStyleCssText(bridge, element, OnStyleMutation, in a), "set style"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // dataset — the live DOMStringMap over the element's data-* attributes.
        //
        // Built on first read rather than with the rest of the element: a document has thousands of
        // elements and few of them are ever asked for their dataset, so building every map up front
        // would allocate a proxy and four callbacks per element for nothing. The built map replaces
        // this accessor with a value property, which both memoizes it and keeps
        // `el.dataset === el.dataset` true as it is in a browser — the map reads and writes the
        // attributes on every trap, so one instance is already live and a second would be redundant
        // rather than fresher.
        obj.FastAddProperty((KeyString)"dataset",
            new DomFunction((in _) =>
            {
                if (bridge._jsContext is not { } datasetContext
                    || Dom.Features.DatasetBinding.Build(datasetContext, element, bridge.InvalidateStyleScope) is not { } dataset)
                {
                    // No Proxy in this realm to build the map from. Undefined is honest: an absent
                    // dataset is at least not one that silently drops writes.
                    return JSUndefined.Value;
                }

                obj.FastAddValue((KeyString)"dataset", dataset, JSPropertyAttributes.EnumerableConfigurableValue);
                return dataset;
            }, "get dataset"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // -- DOM tree navigation --

        // The Node members are on Node.prototype and this wrapper inherits them
        // (DomBridge.CharacterDataInterface.cs). Each was a byte-identical copy of what lives
        // there, so nothing about them changes; only their location does. A wrapper minted before
        // the realm carried the interfaces inherits nothing and still installs its own.
        if (!_nodeInterfacePrototypesReady)
            PopulateElementNodeMembersOnInstance(obj, element);

        // data (read/write) — for text nodes and comment nodes (alias for nodeValue/textContent)
        obj.FastAddProperty((KeyString)"data",
            new DomFunction((in a) => Dom.Features.CharacterDataBinding.GetData(element, in a), "get data"),
            new DomFunction((in a) => Dom.Features.CharacterDataBinding.SetData(this, element, in a), "set data"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // length (read-only) — character count for text/comment nodes, child count for elements
        obj.FastAddProperty((KeyString)"length",
            new DomFunction((in a) => Dom.Features.CharacterDataBinding.GetLength(element, in a), "get length"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // splitText(offset) — splits a text node at the given character offset
        if (IsText(element))
        {
            obj.FastAddValue((KeyString)"splitText",
                new DomFunction((in a) => Dom.Features.CharacterDataBinding.SplitText(this, element, in a), "splitText", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // substringData(offset, count) — for text/comment CharacterData nodes
        if (IsText(element) || IsComment(element))
        {
            obj.FastAddValue((KeyString)"substringData",
                new DomFunction((in a) => Dom.Features.CharacterDataBinding.SubstringData(this, element, in a), "substringData", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"appendData",
                new DomFunction((in a) => Dom.Features.CharacterDataBinding.AppendData(this, element, in a), "appendData", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"deleteData",
                new DomFunction((in a) => Dom.Features.CharacterDataBinding.DeleteData(this, element, in a), "deleteData", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"insertData",
                new DomFunction((in a) => Dom.Features.CharacterDataBinding.InsertData(this, element, in a), "insertData", 2),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"replaceData",
                new DomFunction((in a) => Dom.Features.CharacterDataBinding.ReplaceData(this, element, in a), "replaceData", 3),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // removeAttributeNodeNS(attr) — the one attribute member that stays the wrapper's own. DOM
        // §4.9 pairs setAttributeNode with setAttributeNodeNS but gives removeAttributeNode no
        // namespace-qualified sibling (an Attr already knows its namespace), so no browser has one and
        // putting it on Element.prototype would give that prototype a member a browser's has not got.
        obj.FastAddValue((KeyString)"removeAttributeNodeNS",
            new DomFunction((in a) => _attributes.RemoveAttributeNodeNS(element, obj, in a), "removeAttributeNodeNS", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // insertBefore(newChild, refChild)
        obj.FastAddValue((KeyString)"insertBefore",
            new DomFunction((in a) => Dom.Features.TreeMutationBinding.InsertBefore(this, element, in a), "insertBefore", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // moveBefore(node, refChild) — the atomic, state-preserving sibling of insertBefore.
        obj.FastAddValue((KeyString)"moveBefore",
            new DomFunction((in a) => Dom.Features.TreeMutationBinding.MoveBefore(this, element, in a), "moveBefore", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- DOM manipulation methods --

        // HTMLTemplateElement.content — the template contents fragment. Every component idiom goes
        // through it (`importNode(t.content, true)`, `t.content.cloneNode(true)`,
        // `t.content.querySelector(...)`), and without it `t.content` was undefined and the whole
        // component script threw. See GetTemplateContent for what this fragment is and is not.
        if (string.Equals(element.TagName, "template", StringComparison.OrdinalIgnoreCase))
        {
            obj.FastAddProperty((KeyString)"content",
                new DomFunction((in a) => bridge.ToJSObject(bridge.GetTemplateContent(element)), "get content"),
                null, JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // attachInternals() — form-associated custom elements (HTML §4.13.5). On every element
        // rather than only the custom ones, because that is where a browser puts it: it is a member
        // of HTMLElement, and it refuses at call time for an element that is not a custom element.
        // Being absent instead would make the standard feature-detect answer the wrong way.
        obj.FastAddValue((KeyString)"attachInternals",
            new DomFunction((in a) => ElementInternals.AttachInternals(element, in a), "attachInternals", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // appendChild(child)
        obj.FastAddValue((KeyString)"appendChild",
            new DomFunction((in a) => Dom.Features.TreeMutationBinding.AppendChild(this, element, in a), "appendChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // removeChild(child)
        obj.FastAddValue((KeyString)"removeChild",
            new DomFunction((in a) => Dom.Features.TreeMutationBinding.RemoveChild(this, element, in a), "removeChild", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // replaceChild(newChild, oldChild)
        obj.FastAddValue((KeyString)"replaceChild",
            new DomFunction((in a) => Dom.Features.TreeMutationBinding.ReplaceChild(this, element, in a), "replaceChild", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // -- DOM events --

        // addEventListener / removeEventListener / dispatchEvent are on EventTarget.prototype,
        // routed by receiver (DomBridge.EventTargetInterface.cs) — one function for every target, as
        // in a browser. A wrapper minted before the realm carried it installs its own.
        if (!_eventTargetRoutingReady)
        {
            obj.FastAddValue((KeyString)"addEventListener",
                new DomFunction((in a) => Dom.Features.EventTargetBinding.AddEventListener(this, element, in a), "addEventListener", 3),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"removeEventListener",
                new DomFunction((in a) => Dom.Features.EventTargetBinding.RemoveEventListener(this, element, in a), "removeEventListener", 3),
                JSPropertyAttributes.EnumerableConfigurableValue);

            obj.FastAddValue((KeyString)"dispatchEvent",
                new DomFunction((in a) => Dom.Features.EventTargetBinding.DispatchEvent(this, element, in a), "dispatchEvent", 1),
                JSPropertyAttributes.EnumerableConfigurableValue);
        }

        // element.click() — creates and dispatches a MouseEvent
        // For checkboxes and radio buttons, toggles checked state.
        obj.FastAddValue((KeyString)"click",
            new DomFunction((in _) => Dom.Features.EventTargetBinding.Click(this, element, in _), "click", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // element.focus() — creates and dispatches a FocusEvent-like object
        obj.FastAddValue((KeyString)"focus",
            new DomFunction((in _) => Dom.Features.EventTargetBinding.Focus(this, element, in _), "focus", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // element.blur() — creates and dispatches a FocusEvent-like object
        obj.FastAddValue((KeyString)"blur",
            new DomFunction((in _) => Dom.Features.EventTargetBinding.Blur(this, element, in _), "blur", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // on* inline event handler properties (onclick, onload, etc.)
        foreach (var eventName in InlineEventNames)
        {
            obj.FastAddProperty((KeyString)$"on{eventName}",
                new DomFunction((in _) => Dom.Features.EventHandlerReflectorBinding.GetOn(this, element, eventName, in _), $"get on{eventName}"),
                new DomFunction((in a) => Dom.Features.EventHandlerReflectorBinding.SetOn(this, element, eventName, in a), $"set on{eventName}"),
                JSPropertyAttributes.EnumerableConfigurableProperty);
        }

        // Compile on* HTML attributes into inline event handler functions
        CompileInlineEventAttributes(element);

        // -- Form element support --

        // Form-control IDL reflectors (value/checked/type/name/disabled/hidden/tabIndex/required) —
        // Phase 3 P3.60: extracted into the co-located FormControlBinding feature module, reached
        // through the IFormControlHost contract (DomBridge.FormControlHost.cs).
        _formControl.Install(obj, element);

        // checkValidity() — form validation (Phase 3 P3.9: FormBinding owns the validity check)
        obj.FastAddValue((KeyString)"checkValidity",
            new DomFunction((in a) => _forms.IsElementValid(element) ? JSBoolean.True : JSBoolean.False, "checkValidity", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // reportValidity() — form validation
        obj.FastAddValue((KeyString)"reportValidity",
            new DomFunction((in a) => _forms.IsElementValid(element) ? JSBoolean.True : JSBoolean.False, "reportValidity", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // submit() — for form elements (Phase 3 P3.61: co-located FormSubmitBinding feature module,
        // reached through IFormSubmitHost; DomBridge.FormSubmitHost.cs).
        obj.FastAddValue((KeyString)"submit",
            new DomFunction((in a) => Dom.Features.FormSubmitBinding.Submit(this, element, obj, in a), "submit", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // getContext(contextType) — for <canvas> elements. Phase 3 P3.64: extracted into the co-located
        // CanvasBinding feature module (unblocked once Phase 6/P8.9 dissolved Broiler.HtmlBridge.Rendering).
        Dom.Features.CanvasBinding.Install(this, obj, element);

        // <iframe> browsing-context accessors (contentDocument/contentWindow/getSVGDocument, src/srcdoc
        // read/write, sandbox reflection) — Phase 3 P3.55: extracted into the co-located IframeElementBinding
        // feature module, sibling of the P3.52 <object> ObjectElementBinding. Reaches the frames machinery
        // through the IIframeElementHost contract (DomBridge.IframeElementHost.cs).
        Dom.Features.IframeElementBinding.Install(this, obj, element);

        AddElementSpecificMembers(obj, element);

        // Node interface constants (DOM §4.4: these exist on all Node objects) — the type values and
        // the DOCUMENT_POSITION_* bits compareDocumentPosition returns.
        Dom.Features.NodeConstantsBinding.Install(obj);

        return obj;
    }


    /// <summary>
    /// The <c>Node</c> members as own properties of one element wrapper — the shape every element
    /// had before they moved to <c>Node.prototype</c>, kept for the one case that cannot use them: a
    /// wrapper minted before the realm carried the interfaces, which inherits from nothing.
    /// </summary>
    private void PopulateElementNodeMembersOnInstance(JSObject obj, DomElement element)
    {
        // parentNode (read-only, dynamic)
        obj.FastAddProperty((KeyString)"parentNode",
            new DomFunction((in a) => element.ParentNode != null ? ToJSObject(element.ParentNode) : JSNull.Value, "get parentNode"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        obj.FastAddProperty((KeyString)"isConnected",
            new DomFunction((in _) => Dom.Features.NodeAccessorsBinding.GetIsConnected(this, element, in _), "get isConnected"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // childNodes (read-only, dynamic)
        obj.FastAddProperty((KeyString)"childNodes",
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.GetChildNodes(this, element, in a), "get childNodes"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // firstChild (read-only, dynamic)
        obj.FastAddProperty((KeyString)"firstChild",
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.GetFirstChild(this, element, in a), "get firstChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // lastChild (read-only, dynamic)
        obj.FastAddProperty((KeyString)"lastChild",
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.GetLastChild(this, element, in a), "get lastChild"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nextSibling (read-only, dynamic)
        obj.FastAddProperty((KeyString)"nextSibling",
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNextSibling(this, element, in a), "get nextSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // previousSibling (read-only, dynamic)
        obj.FastAddProperty((KeyString)"previousSibling",
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.GetPreviousSibling(this, element, in a), "get previousSibling"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeType (read-only)
        obj.FastAddProperty((KeyString)"nodeType",
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNodeType(element, in a), "get nodeType"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeName (read-only)
        obj.FastAddProperty((KeyString)"nodeName",
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNodeName(element, in a), "get nodeName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // localName (read-only) — null for non-element nodes; local part of tag name for elements
        obj.FastAddProperty((KeyString)"localName",
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.GetLocalName(element, in a), "get localName"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // prefix (read-only) — namespace prefix or null
        obj.FastAddProperty((KeyString)"prefix",
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.GetPrefix(element, in a), "get prefix"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // namespaceURI (read-only) — returns namespace URI for elements created via createElementNS
        obj.FastAddProperty((KeyString)"namespaceURI",
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNamespaceURI(element, in a), "get namespaceURI"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // nodeValue (read/write) — null for elements, text content for text/comment nodes
        obj.FastAddProperty((KeyString)"nodeValue",
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.GetNodeValue(element, in a), "get nodeValue"),
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.SetNodeValue(this, element, in a), "set nodeValue"),
            JSPropertyAttributes.EnumerableConfigurableProperty);

        // ownerDocument (read-only) — returns the Document node (nodeType=9)
        obj.FastAddProperty((KeyString)"ownerDocument",
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.GetOwnerDocument(this, element, in a), "get ownerDocument"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // parentElement (read-only, dynamic) — like parentNode but returns null for non-element parents
        obj.FastAddProperty((KeyString)"parentElement",
            new DomFunction((in a) => Dom.Features.NodeAccessorsBinding.GetParentElement(this, element, in a), "get parentElement"),
            null, JSPropertyAttributes.EnumerableConfigurableProperty);

        // hasChildNodes()
        obj.FastAddValue((KeyString)"hasChildNodes",
            new DomFunction((in a) => element.ChildNodes.Count > 0 ? JSBoolean.True : JSBoolean.False, "hasChildNodes", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // contains(otherNode) — returns true if otherNode is a descendant
        obj.FastAddValue((KeyString)"contains",
            new DomFunction((in a) => Dom.Features.NodeRelationshipsBinding.Contains(this, element, in a), "contains", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // compareDocumentPosition(otherNode)
        obj.FastAddValue((KeyString)"compareDocumentPosition",
            new DomFunction((in a) => Dom.Features.NodeRelationshipsBinding.CompareDocumentPosition(this, element, in a), "compareDocumentPosition", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // isSameNode(otherNode)
        obj.FastAddValue((KeyString)"isSameNode",
            new DomFunction((in a) => Dom.Features.NodeRelationshipsBinding.IsSameNode(this, element, in a), "isSameNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // normalize()
        obj.FastAddValue((KeyString)"normalize",
            new DomFunction((in _) => Dom.Features.NodeRelationshipsBinding.Normalize(this, element, in _), "normalize", 0),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // isEqualNode(otherNode)
        obj.FastAddValue((KeyString)"isEqualNode",
            new DomFunction((in a) => Dom.Features.NodeRelationshipsBinding.IsEqualNode(this, element, in a), "isEqualNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        obj.FastAddValue((KeyString)"getRootNode",
            new DomFunction((in a) => Dom.Features.NodeRelationshipsBinding.GetRootNode(this, element, in a), "getRootNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);

        // cloneNode(deep)
        obj.FastAddValue((KeyString)"cloneNode",
            new DomFunction((in a) => Dom.Features.NodeRelationshipsBinding.CloneNode(this, element, in a), "cloneNode", 1),
            JSPropertyAttributes.EnumerableConfigurableValue);
    }
}
