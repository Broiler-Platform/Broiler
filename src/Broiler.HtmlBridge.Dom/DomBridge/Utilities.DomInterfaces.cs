using Broiler.JavaScript.Engine;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    /// <summary>
    /// Registers the DOM interface-constructor globals a page reaches for by bare name —
    /// <c>Element</c>, <c>HTMLElement</c>, <c>HTMLUnknownElement</c>, <c>Document</c>,
    /// <c>DocumentFragment</c>, <c>CharacterData</c>, <c>Text</c>, <c>Comment</c> and
    /// <c>Attr</c> — and teaches the pre-existing <c>Node</c> global to answer
    /// <c>instanceof</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bridge DOM object is a plain <c>JSObject</c> whose prototype is <c>Object.prototype</c>:
    /// it carries its members directly rather than inheriting them from an interface prototype.
    /// So the ordinary <c>instanceof</c> walk — follow the operand's prototype chain looking for
    /// the constructor's <c>prototype</c> — can never succeed for one, which is why the
    /// long-standing <c>Node</c> global (see <c>RegisterNodeConstructor</c>) reported
    /// <c>document.createElement('div') instanceof Node === false</c>.
    /// </para>
    /// <para>
    /// Each constructor therefore carries an <c>@@hasInstance</c> that answers from the object's
    /// own <c>nodeType</c>/<c>namespaceURI</c>/<c>tagName</c> instead of from a prototype chain.
    /// That is the spec's own extension point (ES §13.10.1 consults <c>@@hasInstance</c> before
    /// the prototype walk), so this is a real answer rather than a shim — and it is installed with
    /// <c>Object.defineProperty</c> because <c>Function.prototype[@@hasInstance]</c> is
    /// non-writable, so a plain assignment would silently do nothing in sloppy mode.
    /// </para>
    /// <para>
    /// Giving DOM objects genuine per-interface prototype chains would subsume this and is the
    /// better long-term shape; it is a far larger change to the object model than making
    /// <c>instanceof HTMLElement</c> — the single most common way a page asks "is this an
    /// element?" — stop throwing <c>ReferenceError</c>.
    /// </para>
    /// </remarks>
    private static void RegisterDomInterfaceConstructors(JSContext context)
    {
        context.Eval(@"
            // Calling one of these directly throws, as it does in a browser: these interfaces are
            // not constructible, and their objects come from document.createElement and friends.
            // Answering with a plain object instead would hand back something that looks like an
            // element to the caller and is not one — worse than the ReferenceError this replaces.
            function Element() { throw new TypeError('Illegal constructor'); }
            function HTMLElement() { throw new TypeError('Illegal constructor'); }
            function HTMLUnknownElement() { throw new TypeError('Illegal constructor'); }
            function Document() { throw new TypeError('Illegal constructor'); }
            function DocumentFragment() { throw new TypeError('Illegal constructor'); }
            function CharacterData() { throw new TypeError('Illegal constructor'); }
            function Text() { throw new TypeError('Illegal constructor'); }
            function Comment() { throw new TypeError('Illegal constructor'); }
            function Attr() { throw new TypeError('Illegal constructor'); }

            (function () {
                var HTML_NS = 'http://www.w3.org/1999/xhtml';

                // The HTML elements the parser knows. Anything else with no '-' in its name is an
                // HTMLUnknownElement; a name *with* a '-' is an (undefined) custom element, which
                // the spec makes an HTMLElement rather than an unknown one.
                var known = {};
                var names = ('a abbr acronym address applet area article aside audio b base ' +
                    'basefont bdi bdo big blockquote body br button canvas caption center cite ' +
                    'code col colgroup data datalist dd del details dfn dialog dir div dl dt em ' +
                    'embed fieldset figcaption figure font footer form frame frameset h1 h2 h3 ' +
                    'h4 h5 h6 head header hgroup hr html i iframe img input ins kbd keygen label ' +
                    'legend li link listing main map mark marquee menu meta meter nav nobr ' +
                    'noembed noframes noscript object ol optgroup option output p param picture ' +
                    'plaintext pre progress q rb rp rt rtc ruby s samp script search section ' +
                    'select slot small source span strike strong style sub summary sup table ' +
                    'tbody td template textarea tfoot th thead time title tr track tt u ul var ' +
                    'video wbr xmp').split(' ');
                for (var i = 0; i < names.length; i++) known[names[i]] = true;

                function define(ctor, test) {
                    Object.defineProperty(ctor, Symbol.hasInstance, {
                        value: test, writable: false, enumerable: false, configurable: true
                    });
                }

                function isNode(o) {
                    return !!o && typeof o === 'object' && typeof o.nodeType === 'number';
                }

                function isElement(o) {
                    return isNode(o) && o.nodeType === 1;
                }

                function isHtmlElement(o) {
                    if (!isElement(o)) return false;
                    var ns = o.namespaceURI;
                    // A bridge element created outside a namespace-aware path may report no
                    // namespace at all; treat that as HTML rather than as neither.
                    return ns === HTML_NS || ns === null || typeof ns === 'undefined';
                }

                define(Node, isNode);
                define(Element, isElement);
                define(HTMLElement, isHtmlElement);

                // HTMLUnknownElement is a *subtype* of HTMLElement, so an unknown element is an
                // instance of both. html5test's `x instanceof HTMLElement &&
                // !(x instanceof HTMLUnknownElement)` check relies on exactly that split.
                define(HTMLUnknownElement, function (o) {
                    if (!isHtmlElement(o)) return false;
                    var tag = typeof o.tagName === 'string' ? o.tagName.toLowerCase() : '';
                    if (tag === '' || known[tag] === true) return false;
                    return tag.indexOf('-') === -1;
                });

                define(Document, function (o) { return isNode(o) && (o.nodeType === 9 || o.nodeType === 10); });
                define(DocumentFragment, function (o) { return isNode(o) && o.nodeType === 11; });
                define(CharacterData, function (o) {
                    return isNode(o) && (o.nodeType === 3 || o.nodeType === 4 || o.nodeType === 8);
                });
                define(Text, function (o) { return isNode(o) && (o.nodeType === 3 || o.nodeType === 4); });
                define(Comment, function (o) { return isNode(o) && o.nodeType === 8; });
                define(Attr, function (o) { return isNode(o) && o.nodeType === 2; });
            })();
        ");
    }
}
