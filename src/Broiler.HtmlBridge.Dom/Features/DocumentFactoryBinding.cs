using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The <c>document</c> node-factory methods — <c>createElement</c>, <c>createTextNode</c>,
/// <c>createDocumentFragment</c>, <c>createElementNS</c>, <c>createAttribute</c>,
/// <c>createAttributeNS</c> — co-located as an HtmlBridge feature module (Phase 3). Each validates
/// its name argument (via the bridge's neutral <c>internal static</c> validators), constructs the
/// canonical node through the <see cref="IDocumentFactoryHost"/> funnel, and returns its JS wrapper.
/// Previously the bridge's <c>JsRegistrationCreateElement014Core</c> etc. in the shared
/// JsFunctionCallbacks/Registration.cs grab-bag. The document-level factories (<c>createDocument</c>,
/// <c>createHTMLDocument</c>, <c>createDocumentType</c>) and <c>createEvent</c> are browsing-context /
/// event-object concerns and are not part of this slice.
/// </summary>
internal static class DocumentFactoryBinding
{
    public static JSValue CreateElement(IDocumentFactoryHost host, JSContext context, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'createElement': 1 argument required, but only 0 present.");
        var tag = a[0].ToString();
        DomBridge.ValidateElementName(tag, context);
        tag = DomBridge.AsciiToLower(tag);
        var el = host.CreateBridgeElement(tag);
        return host.ToJSObject(el);
    }

    public static JSValue CreateTextNode(IDocumentFactoryHost host, in Arguments a)
    {
        var text = a.Length > 0 ? a[0].ToString() : string.Empty;
        return host.ToJSObject(host.CreateBridgeTextNode(text));
    }

    public static JSValue CreateDocumentFragment(IDocumentFactoryHost host, in Arguments a)
        => host.ToJSObject(host.CreateBridgeDocumentFragment());

    /// <summary>
    /// <c>document.importNode(node, deep)</c> — a copy of <paramref name="a"/>[0] owned by this
    /// document. <c>deep</c> defaults to <see langword="false"/>, so a bare
    /// <c>importNode(node)</c> copies the node alone; the idiom that matters,
    /// <c>importNode(template.content, true)</c>, copies the whole stamped subtree. Every node this
    /// bridge mints already belongs to the one document, so importing is exactly a clone — there is
    /// no adoption step to perform.
    /// </summary>
    public static JSValue ImportNode(IDocumentFactoryHost host, JSContext context, in Arguments a)
    {
        if (a.Length == 0 || a[0] is not JSObject source)
        {
            DomBridge.ThrowDOMException(context, "importNode requires a node to import.", "TypeError");
            return JSUndefined.Value;
        }

        Broiler.Dom.DomNode? node = host.FindDomNodeByJSObject(source);
        if (node is null)
        {
            DomBridge.ThrowDOMException(context, "importNode was passed something that is not a node.", "TypeError");
            return JSUndefined.Value;
        }

        bool deep = a.Length > 1 && a[1].BooleanValue;
        return host.ToJSObject(host.CloneDomNode(node, deep));
    }

    public static JSValue CreateElementNS(IDocumentFactoryHost host, JSContext context, in Arguments a)
    {
        var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        var localName = a.Length > 1 ? a[1].ToString() : (a.Length > 0 ? a[0].ToString() : "div");
        DomBridge.ValidateQualifiedName(localName, ns, context);
        var el = string.IsNullOrEmpty(ns)
            ? host.CreateBridgeElement(localName)
            : host.CreateBridgeElementNS(ns, localName);
        return host.ToJSObject(el);
    }

    public static JSValue CreateAttribute(IDocumentFactoryHost host, JSContext context, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'createAttribute': 1 argument required, but only 0 present.");
        var name = a[0].ToString();
        DomBridge.ValidateElementName(name, context);
        return host.BuildStandaloneAttrNode(DomBridge.AsciiToLower(name), null);
    }

    public static JSValue CreateAttributeNS(IDocumentFactoryHost host, JSContext context, in Arguments a)
    {
        var ns = a.Length > 0 && !a[0].IsNull && !a[0].IsUndefined ? a[0].ToString() : null;
        if (a.Length < 2)
            throw new JSException("Failed to execute 'createAttributeNS': 2 arguments required, but fewer present.");
        var qualifiedName = a[1].ToString();
        DomBridge.ValidateQualifiedName(qualifiedName, ns, context);
        return host.BuildStandaloneAttrNode(qualifiedName, ns);
    }
}
