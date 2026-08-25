using Broiler.Dom;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.HtmlBridge;

/// <summary>
/// Links a DOM wrapper to its interface's prototype, so <c>constructor.name</c> and
/// <c>Object.getPrototypeOf</c> answer the interface rather than <c>Object</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every wrapper reported <c>constructor.name</c> of <c>"Object"</c> — a text node, a comment, a
/// fragment, an attribute, an element, all of them. <c>instanceof</c> already answered correctly,
/// because the interface globals carry an <c>@@hasInstance</c> hook that reads <c>nodeType</c>, so
/// the gap was narrower than it looks and also more confusing: <c>node instanceof Text</c> was
/// <see langword="true"/> while <c>node.constructor.name</c> was <c>"Object"</c> and
/// <c>Object.getPrototypeOf(node) === Text.prototype</c> was <see langword="false"/>. Debugging
/// output, logging and any dispatch keyed on the constructor all read the wrong thing.
/// </para>
/// <para>
/// <b>This covers the non-element wrappers only</b>, and that boundary is deliberate rather than
/// convenient. Each of these node kinds has exactly one interface, fixed by its node type, so the
/// mapping is a fact. An <em>element</em>'s interface depends on its tag through a curated table
/// where the entries overlap (<c>HTMLMediaElement</c> covers <c>audio</c> and <c>video</c> while
/// <c>HTMLAudioElement</c> covers <c>audio</c> again) and where a tag the table omits has to fall
/// back to something — a browser distinguishes <c>HTMLSpanElement</c>, <c>HTMLElement</c> and
/// <c>HTMLUnknownElement</c> there, and guessing between them would put a wrong name where an
/// honest <c>"Object"</c> is at least not misleading. That half stays open.
/// </para>
/// <para>
/// <b>What this does not do is move members onto the prototypes.</b> The bindings install every
/// member as an own property of each wrapper, so <c>Text.prototype</c> stays empty and
/// <c>Object.getOwnPropertyNames(node)</c> still lists the whole interface. Linking the prototype is
/// nonetheless a real gain and not only cosmetic: a page that extends <c>Text.prototype</c> — the
/// ordinary polyfill idiom — now reaches instances, where before the assignment went to an object
/// nothing inherited from. Relocating the members is the larger object-model change and is recorded
/// separately.
/// </para>
/// </remarks>
public sealed partial class DomBridge
{
    /// <summary>
    /// Points <paramref name="wrapper"/> at its interface prototype when the realm is up and the
    /// node kind has exactly one interface. A no-op otherwise, including for elements.
    /// </summary>
    private void ApplyInterfacePrototype(JSObject wrapper, DomNode node)
    {
        if (InterfaceNameFor(node) is not { } interfaceName)
            return;

        if (_jsContext?[interfaceName] is JSObject constructor &&
            constructor[(KeyString)"prototype"] is JSObject prototype)
            wrapper.BasePrototypeObject = prototype;
    }

    /// <summary>
    /// The single interface a non-element node implements, or <see langword="null"/> when the node
    /// is an element (whose interface is a tag question, see the class remarks) or a kind this does
    /// not reach. <c>Attr</c> is the latter: an attribute is not a <see cref="DomNode"/> in the
    /// canonical DOM, so its wrapper is not minted here and linking it needs its own hook.
    /// </summary>
    private static string? InterfaceNameFor(DomNode node) => node switch
    {
        DomDocumentType => "DocumentType",
        DomDocumentFragment => "DocumentFragment",
        DomComment => "Comment",
        DomText => "Text",
        _ => null,
    };
}
