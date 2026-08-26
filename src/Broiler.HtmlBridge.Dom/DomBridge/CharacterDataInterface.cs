using Broiler.Dom;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.HtmlBridge;

/// <summary>
/// <c>Node</c>, <c>CharacterData</c> and <c>Text</c> as real interfaces for a character-data node:
/// their members on the interface prototypes rather than copied onto every text and comment wrapper.
/// </summary>
/// <remarks>
/// <para>
/// Every DOM wrapper in this bridge installs its interface as own properties of each object, so
/// <c>Object.getOwnPropertyNames(node)</c> lists the whole interface and
/// <c>Text.prototype.splitText</c> is <see langword="undefined"/> — track 6's wrapper item. The
/// prototype <em>chain</em> has been real since <see cref="ApplyInterfacePrototype"/>
/// (<c>Text → CharacterData → Node → EventTarget → Object</c>), and the interface objects exist; what
/// had not happened is the engine putting its members on them. A text node carried 57 own properties
/// where a browser gives it none.
/// </para>
/// <para>
/// This is the first node interface to move, and the mechanism it needs is the general one:
/// a member on a prototype has no node captured in a closure, so it finds one from its receiver
/// (<see cref="NodeFromReceiver"/>, over the registry's constant-time reverse map). That is also what
/// makes an illegal invocation — <c>Text.prototype.splitText.call({}, 1)</c> — a <c>TypeError</c>
/// rather than a crash or a silent wrong answer. <c>Range</c>, <c>Selection</c> and <c>Blob</c> are
/// the same shape with their state in a weak table; a node's state is the node, so the registry that
/// already owns wrapper identity is the table.
/// </para>
/// <para>
/// <b>The split across the three prototypes is Web IDL's, not a convenience.</b> The tree accessors,
/// the node methods and the <c>ChildNode</c> mixin members go on <c>Node.prototype</c> and
/// <c>CharacterData.prototype</c> where the specification puts them, so a page walking a prototype's
/// own property names reads the shape a browser has. <c>splitText</c> is <c>Text</c>'s alone, which
/// is why the old wrapper installed it behind an <c>IsText</c> test and why a <c>Comment</c> must not
/// inherit it.
/// </para>
/// <para>
/// <b>Elements and documents are untouched.</b> They keep their own-property surface, so the members
/// installed on <c>Node.prototype</c> here are shadowed for them and nothing about them changes.
/// Moving those is the rest of the item — an element carries 166 own properties across some forty
/// binding modules — and it is what this makes mechanical rather than novel.
/// </para>
/// <para>
/// <b>The three <c>EventTarget</c> members stay on the instance,</b> deliberately. The realm already
/// carries an <c>EventTarget.prototype</c> with its own <c>addEventListener</c> /
/// <c>removeEventListener</c> / <c>dispatchEvent</c>, and a node inherits from it; those store
/// listeners engine-side, where the bridge's dispatch would never find them. Shadowing them on
/// <c>Node.prototype</c> would fix the behaviour and put three members on a prototype no browser has
/// them on. Routing the realm's own <c>EventTarget</c> to the bridge for node receivers is the change
/// that resolves this, and it belongs with the rest of the migration rather than here.
/// </para>
/// </remarks>
public sealed partial class DomBridge
{
    /// <summary>
    /// Whether the character-data interface prototypes carry their members yet, which is what lets a
    /// wrapper stop installing them.
    /// </summary>
    /// <remarks>
    /// A wrapper minted before the realm is up has no prototype to inherit from —
    /// <see cref="ApplyInterfacePrototype"/> is a no-op then — so it still installs its own members,
    /// exactly as before. Without that fallback such a node would have neither, and the shape it gets
    /// is the old one rather than a broken one.
    /// </remarks>
    private bool _characterDataInterfaceReady;

    /// <summary>
    /// Installs the <c>Node</c>, <c>CharacterData</c> and <c>Text</c> members a character-data node
    /// exposes onto their interface prototypes. A no-op when the realm does not carry the interfaces.
    /// </summary>
    internal void RegisterCharacterDataInterface()
    {
        if (PrototypeOfInterface("Node") is not { } nodeProto ||
            PrototypeOfInterface("CharacterData") is not { } characterDataProto ||
            PrototypeOfInterface("Text") is not { } textProto)
        {
            return;
        }

        InstallNodePrototypeMembers(nodeProto);
        InstallCharacterDataPrototypeMembers(characterDataProto);

        // Text's alone: a Comment inherits CharacterData and must not answer splitText.
        AddPrototypeMethod(textProto, "splitText", 1,
            (in Arguments a) => Dom.Features.CharacterDataBinding.SplitText(
                this, RequireNode(in a, "Text", "splitText"), in a));

        _characterDataInterfaceReady = true;
    }

    /// <summary>The prototype object of a registered interface global, if the realm has one.</summary>
    private JSObject? PrototypeOfInterface(string interfaceName) =>
        _jsContext?[interfaceName] is JSObject constructor
            ? constructor[(KeyString)"prototype"] as JSObject
            : null;

    /// <summary>
    /// <c>Node.prototype</c>: the tree accessors and node operations. Installed for every node kind,
    /// though only character-data wrappers read them today — an element or document shadows each one
    /// with its own copy until it is migrated too.
    /// </summary>
    private void InstallNodePrototypeMembers(JSObject proto)
    {
        AddPrototypeAccessor(proto, "nodeType",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetNodeType(RequireNode(in a, "Node", "nodeType"), in a));
        AddPrototypeAccessor(proto, "nodeName",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetNodeName(RequireNode(in a, "Node", "nodeName"), in a));
        AddPrototypeAccessor(proto, "localName",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetLocalName(RequireNode(in a, "Node", "localName"), in a));
        AddPrototypeAccessor(proto, "prefix",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetPrefix(RequireNode(in a, "Node", "prefix"), in a));
        AddPrototypeAccessor(proto, "namespaceURI",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetNamespaceURI(RequireNode(in a, "Node", "namespaceURI"), in a));

        AddPrototypeAccessor(proto, "nodeValue",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetNodeValue(RequireNode(in a, "Node", "nodeValue"), in a),
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.SetNodeValue(this, RequireNode(in a, "Node", "nodeValue"), in a));
        AddPrototypeAccessor(proto, "textContent",
            (in Arguments a) => GetNodeTextValue(RequireNode(in a, "Node", "textContent")),
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.SetNodeValue(this, RequireNode(in a, "Node", "textContent"), in a));

        AddPrototypeAccessor(proto, "parentNode", (in Arguments a) =>
        {
            var node = RequireNode(in a, "Node", "parentNode");
            return node.ParentNode != null ? ToJSObject(node.ParentNode) : JSNull.Value;
        });
        AddPrototypeAccessor(proto, "parentElement",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetParentElement(this, RequireNode(in a, "Node", "parentElement"), in a));
        AddPrototypeAccessor(proto, "isConnected",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetIsConnected(this, RequireNode(in a, "Node", "isConnected"), in a));
        AddPrototypeAccessor(proto, "childNodes",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetChildNodes(this, RequireNode(in a, "Node", "childNodes"), in a));
        AddPrototypeAccessor(proto, "firstChild",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetFirstChild(this, RequireNode(in a, "Node", "firstChild"), in a));
        AddPrototypeAccessor(proto, "lastChild",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetLastChild(this, RequireNode(in a, "Node", "lastChild"), in a));
        AddPrototypeAccessor(proto, "nextSibling",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetNextSibling(this, RequireNode(in a, "Node", "nextSibling"), in a));
        AddPrototypeAccessor(proto, "previousSibling",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetPreviousSibling(this, RequireNode(in a, "Node", "previousSibling"), in a));
        AddPrototypeAccessor(proto, "ownerDocument",
            (in Arguments a) => Dom.Features.NodeAccessorsBinding.GetOwnerDocument(this, RequireNode(in a, "Node", "ownerDocument"), in a));

        AddPrototypeMethod(proto, "hasChildNodes", 0, (in Arguments a) =>
            RequireNode(in a, "Node", "hasChildNodes").ChildNodes.Count > 0 ? JSBoolean.True : JSBoolean.False);
        AddPrototypeMethod(proto, "cloneNode", 1,
            (in Arguments a) => Dom.Features.NodeRelationshipsBinding.CloneNode(this, RequireNode(in a, "Node", "cloneNode"), in a));
        AddPrototypeMethod(proto, "contains", 1,
            (in Arguments a) => Dom.Features.NodeRelationshipsBinding.Contains(this, RequireNode(in a, "Node", "contains"), in a));
        AddPrototypeMethod(proto, "compareDocumentPosition", 1,
            (in Arguments a) => Dom.Features.NodeRelationshipsBinding.CompareDocumentPosition(this, RequireNode(in a, "Node", "compareDocumentPosition"), in a));
        AddPrototypeMethod(proto, "isSameNode", 1,
            (in Arguments a) => Dom.Features.NodeRelationshipsBinding.IsSameNode(this, RequireNode(in a, "Node", "isSameNode"), in a));
        AddPrototypeMethod(proto, "isEqualNode", 1,
            (in Arguments a) => Dom.Features.NodeRelationshipsBinding.IsEqualNode(this, RequireNode(in a, "Node", "isEqualNode"), in a));
        AddPrototypeMethod(proto, "getRootNode", 1,
            (in Arguments a) => Dom.Features.NodeRelationshipsBinding.GetRootNode(this, RequireNode(in a, "Node", "getRootNode"), in a));
        AddPrototypeMethod(proto, "normalize", 0,
            (in Arguments a) => Dom.Features.NodeRelationshipsBinding.Normalize(this, RequireNode(in a, "Node", "normalize"), in a));
    }

    /// <summary>
    /// <c>CharacterData.prototype</c>: the data operations, plus the <c>ChildNode</c> mixin members —
    /// which the mixin gives to <c>CharacterData</c>, <c>Element</c> and <c>DocumentType</c>
    /// separately, so they belong here rather than on <c>Node.prototype</c>.
    /// </summary>
    private void InstallCharacterDataPrototypeMembers(JSObject proto)
    {
        AddPrototypeAccessor(proto, "data",
            (in Arguments a) => Dom.Features.CharacterDataBinding.GetData(RequireNode(in a, "CharacterData", "data"), in a),
            (in Arguments a) => Dom.Features.CharacterDataBinding.SetData(this, RequireNode(in a, "CharacterData", "data"), in a));
        AddPrototypeAccessor(proto, "length",
            (in Arguments a) => Dom.Features.CharacterDataBinding.GetLength(RequireNode(in a, "CharacterData", "length"), in a));

        AddPrototypeMethod(proto, "substringData", 2,
            (in Arguments a) => Dom.Features.CharacterDataBinding.SubstringData(this, RequireNode(in a, "CharacterData", "substringData"), in a));
        AddPrototypeMethod(proto, "appendData", 1,
            (in Arguments a) => Dom.Features.CharacterDataBinding.AppendData(this, RequireNode(in a, "CharacterData", "appendData"), in a));
        AddPrototypeMethod(proto, "deleteData", 2,
            (in Arguments a) => Dom.Features.CharacterDataBinding.DeleteData(this, RequireNode(in a, "CharacterData", "deleteData"), in a));
        AddPrototypeMethod(proto, "insertData", 2,
            (in Arguments a) => Dom.Features.CharacterDataBinding.InsertData(this, RequireNode(in a, "CharacterData", "insertData"), in a));
        AddPrototypeMethod(proto, "replaceData", 3,
            (in Arguments a) => Dom.Features.CharacterDataBinding.ReplaceData(this, RequireNode(in a, "CharacterData", "replaceData"), in a));

        AddPrototypeMethod(proto, "remove", 0,
            (in Arguments a) => Dom.Features.ChildNodeBinding.Remove(this, RequireNode(in a, "CharacterData", "remove"), in a));
        AddPrototypeMethod(proto, "before", 0,
            (in Arguments a) => Dom.Features.ChildNodeBinding.Before(this, RequireNode(in a, "CharacterData", "before"), in a));
        AddPrototypeMethod(proto, "after", 0,
            (in Arguments a) => Dom.Features.ChildNodeBinding.After(this, RequireNode(in a, "CharacterData", "after"), in a));
        AddPrototypeMethod(proto, "replaceWith", 0,
            (in Arguments a) => Dom.Features.ChildNodeBinding.ReplaceWith(this, RequireNode(in a, "CharacterData", "replaceWith"), in a));
    }

    /// <summary>
    /// The node a prototype member was called on, or a <c>TypeError</c> naming the interface and the
    /// member when the receiver is not a node wrapper — which is what a browser answers for
    /// <c>Text.prototype.splitText.call({}, 1)</c>.
    /// </summary>
    private DomNode RequireNode(in Arguments a, string interfaceName, string member)
    {
        if (a.This is JSObject receiver && _jsObjects.TryGetNode(receiver, out var node))
            return node;

        return JSException.ThrowTypeError<DomNode>(
            $"Failed to execute '{member}' on '{interfaceName}': Illegal invocation");
    }

    /// <summary>Adds a WebIDL operation to an interface prototype.</summary>
    /// <remarks>
    /// Enumerable and configurable but not writable-as-data is what the instance properties were, and
    /// what Web IDL asks for on a prototype; keeping the same attributes means only the *location* of
    /// the member changes.
    /// </remarks>
    private static void AddPrototypeMethod(JSObject proto, string name, int length, JSFunctionDelegate body) =>
        proto.FastAddValue((KeyString)name, new DomFunction(body, name, length),
            JSPropertyAttributes.EnumerableConfigurableValue);

    /// <summary>Adds a WebIDL attribute to an interface prototype, read-only unless a setter is given.</summary>
    private static void AddPrototypeAccessor(JSObject proto, string name,
        JSFunctionDelegate getter, JSFunctionDelegate? setter = null) =>
        proto.FastAddProperty((KeyString)name,
            new DomFunction(getter, "get " + name),
            setter is null ? null : new DomFunction(setter, "set " + name),
            JSPropertyAttributes.EnumerableConfigurableProperty);
}
