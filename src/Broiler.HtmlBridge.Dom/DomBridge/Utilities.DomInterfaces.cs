using System.Text;

using Broiler.JavaScript.Engine;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    /// <summary>
    /// The per-tag HTML element interfaces (HTML §4, "Element interfaces"), as the pairs
    /// <c>interface name → the tag names whose elements implement it</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>HTMLElement</c> answers for every HTML element and is registered separately; this table is
    /// only the subtypes below it. A tag absent from the table implements <c>HTMLElement</c> itself
    /// (<c>span</c> is the exception — it has a named interface that adds nothing — and the grouped
    /// entries are the spec's own: one interface serving several tags, as <c>HTMLQuoteElement</c>
    /// does for <c>blockquote</c> and <c>q</c>).
    /// </para>
    /// <para>
    /// <c>HTMLMediaElement</c> is the one abstract entry: it is never an element's own interface, but
    /// <c>audio</c> and <c>video</c> both derive from it, so it has to answer for both — which is why
    /// the mapping is interface-to-tags rather than the tag-to-interface direction a lookup would
    /// suggest. The same shape leaves room for any other interface that acquires a second tag.
    /// </para>
    /// </remarks>
    private static readonly (string Interface, string Tags)[] HtmlElementInterfaces =
    [
        ("HTMLAnchorElement", "a"),
        ("HTMLAreaElement", "area"),
        ("HTMLMediaElement", "audio video"),
        ("HTMLAudioElement", "audio"),
        ("HTMLBaseElement", "base"),
        ("HTMLQuoteElement", "blockquote q"),
        ("HTMLBodyElement", "body"),
        ("HTMLBRElement", "br"),
        ("HTMLButtonElement", "button"),
        ("HTMLCanvasElement", "canvas"),
        ("HTMLTableCaptionElement", "caption"),
        ("HTMLTableColElement", "col colgroup"),
        ("HTMLDataElement", "data"),
        ("HTMLDataListElement", "datalist"),
        ("HTMLModElement", "del ins"),
        ("HTMLDetailsElement", "details"),
        ("HTMLDialogElement", "dialog"),
        ("HTMLDirectoryElement", "dir"),
        ("HTMLDivElement", "div"),
        ("HTMLDListElement", "dl"),
        ("HTMLEmbedElement", "embed"),
        ("HTMLFieldSetElement", "fieldset"),
        ("HTMLFontElement", "font"),
        ("HTMLFormElement", "form"),
        ("HTMLFrameElement", "frame"),
        ("HTMLFrameSetElement", "frameset"),
        ("HTMLHeadingElement", "h1 h2 h3 h4 h5 h6"),
        ("HTMLHeadElement", "head"),
        ("HTMLHRElement", "hr"),
        ("HTMLHtmlElement", "html"),
        ("HTMLIFrameElement", "iframe"),
        ("HTMLImageElement", "img"),
        ("HTMLInputElement", "input"),
        ("HTMLLabelElement", "label"),
        ("HTMLLegendElement", "legend"),
        ("HTMLLIElement", "li"),
        ("HTMLLinkElement", "link"),
        ("HTMLPreElement", "listing plaintext pre xmp"),
        ("HTMLMapElement", "map"),
        ("HTMLMarqueeElement", "marquee"),
        ("HTMLMenuElement", "menu"),
        ("HTMLMetaElement", "meta"),
        ("HTMLMeterElement", "meter"),
        ("HTMLObjectElement", "object"),
        ("HTMLOListElement", "ol"),
        ("HTMLOptGroupElement", "optgroup"),
        ("HTMLOptionElement", "option"),
        ("HTMLOutputElement", "output"),
        ("HTMLParagraphElement", "p"),
        ("HTMLParamElement", "param"),
        ("HTMLPictureElement", "picture"),
        ("HTMLProgressElement", "progress"),
        ("HTMLScriptElement", "script"),
        ("HTMLSelectElement", "select"),
        ("HTMLSlotElement", "slot"),
        ("HTMLSourceElement", "source"),
        ("HTMLSpanElement", "span"),
        ("HTMLStyleElement", "style"),
        ("HTMLTableElement", "table"),
        ("HTMLTableSectionElement", "tbody tfoot thead"),
        ("HTMLTableCellElement", "td th"),
        ("HTMLTemplateElement", "template"),
        ("HTMLTextAreaElement", "textarea"),
        ("HTMLTimeElement", "time"),
        ("HTMLTitleElement", "title"),
        ("HTMLTableRowElement", "tr"),
        ("HTMLTrackElement", "track"),
        ("HTMLUListElement", "ul"),
        ("HTMLVideoElement", "video"),
    ];

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
            function SVGElement() { throw new TypeError('Illegal constructor'); }
            function CanvasRenderingContext2D() { throw new TypeError('Illegal constructor'); }

            // ImageData, unlike the interfaces above, *is* constructible — HTML defines
            // new ImageData(w, h) and new ImageData(data, w, h) — so it gets a real body rather
            // than an illegal-constructor throw.
            function ImageData(a, b, c) {
                var data, width, height;
                if (typeof a === 'object' && a !== null) {
                    data = a;
                    width = b >>> 0;
                    height = arguments.length > 2 ? (c >>> 0) : (data.length / 4) / width;
                    if (data.length !== width * height * 4)
                        throw new TypeError(""ImageData: the source data length is not a multiple of the row length."");
                } else {
                    width = a >>> 0;
                    height = b >>> 0;
                    data = new Uint8ClampedArray(width * height * 4);
                }
                if (width === 0 || height === 0)
                    throw new TypeError(""ImageData: the source dimensions are zero."");
                this.width = width;
                this.height = height;
                this.data = data;
            }

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

                // The canvas types are not nodes, so nodeType cannot discriminate them; they answer
                // from the members that define the interface instead. A 2D context is the only
                // object in the bridge carrying the drawing surface and the pixel readback together.
                define(CanvasRenderingContext2D, function (o) {
                    return !!o && typeof o === 'object'
                        && typeof o.getImageData === 'function'
                        && typeof o.fillRect === 'function'
                        && typeof o.fillStyle === 'string';
                });

                // Accepts an ImageData this constructor produced and one getImageData returned,
                // which are different objects: the readback is built in C# and does not run through
                // the constructor, so a prototype walk would answer false for the commoner of the two.
                define(ImageData, function (o) {
                    return !!o && typeof o === 'object'
                        && typeof o.width === 'number' && typeof o.height === 'number'
                        && !!o.data && typeof o.data === 'object'
                        && typeof o.data.length === 'number'
                        && o.data.length === o.width * o.height * 4;
                });

                // SVGElement is the namespace's counterpart to HTMLElement, and the one interface
                // here that HTML_NS excludes rather than selects.
                define(SVGElement, function (o) {
                    return isElement(o) && o.namespaceURI === 'http://www.w3.org/2000/svg';
                });
            })();
        ");

        RegisterHtmlElementInterfaces(context);
    }

    /// <summary>
    /// The per-tag HTML element interfaces from <see cref="HtmlElementInterfaces"/> —
    /// <c>HTMLFormElement</c>, <c>HTMLInputElement</c>, <c>HTMLAnchorElement</c> and the rest —
    /// as globals that answer <c>instanceof</c> from the element's tag name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These sit under the <c>HTMLElement</c> the method above registers, and they are what a page
    /// uses when it has an element in hand and wants to know <em>which</em> element it is. Only the
    /// bare name existing is not enough for that: it has to answer, so each one carries the same
    /// <c>@@hasInstance</c> the interfaces above do, reading <c>tagName</c> rather than walking a
    /// prototype chain a bridge DOM object does not have.
    /// </para>
    /// <para>
    /// Their absence is a whole-page failure rather than a missing feature, because a bare name that
    /// does not exist is a <c>ReferenceError</c> — which aborts the script at that statement, not
    /// merely the test that named it. <c>duckduckgo.com</c> is the case that prompted this: its SSG
    /// bootstrap sets each search form's method behind
    /// <c>form instanceof HTMLFormElement</c>, and the two statements it never reached afterwards
    /// were the <c>--vh</c> custom property and
    /// <c>documentElement.classList.add('partially-hydrated')</c>. The page ships
    /// <c>body { display: none }</c> with <c>html.partially-hydrated body { display: block }</c>, so
    /// losing that one class left the start page rendering as an empty white viewport.
    /// </para>
    /// <para>
    /// The declarations are generated rather than written out because the table is the interface
    /// list: a <c>function</c> declaration per name is what makes the bare identifier resolve (the
    /// same mechanism the hand-written interfaces above use), and pairing each with its tags in one
    /// place keeps the name and the test it answers with from drifting apart.
    /// </para>
    /// </remarks>
    private static void RegisterHtmlElementInterfaces(JSContext context)
    {
        var script = new StringBuilder();

        foreach (var (name, _) in HtmlElementInterfaces)
            script.Append("function ").Append(name).Append("() { throw new TypeError('Illegal constructor'); }\n");

        script.Append("(function () {\n");
        script.Append("    var byInterface = {\n");
        foreach (var (name, tags) in HtmlElementInterfaces)
            script.Append("        ").Append(name).Append(": [").Append(name).Append(", '").Append(tags).Append("'],\n");
        script.Append("    };\n");
        script.Append("""
                for (var key in byInterface) {
                    if (!Object.prototype.hasOwnProperty.call(byInterface, key)) continue;
                    var ctor = byInterface[key][0];
                    var tags = byInterface[key][1].split(' ');
                    var set = {};
                    for (var i = 0; i < tags.length; i++) set[tags[i]] = true;
                    // `set` is captured per iteration through the factory rather than through the
                    // loop body, whose `var` bindings are one shared pair by the time a test runs.
                    Object.defineProperty(ctor, Symbol.hasInstance, {
                        value: (function (tagSet) {
                            return function (o) {
                                if (!o || typeof o !== 'object' || o.nodeType !== 1) return false;
                                var ns = o.namespaceURI;
                                if (ns !== 'http://www.w3.org/1999/xhtml' && ns !== null && typeof ns !== 'undefined')
                                    return false;
                                var tag = typeof o.tagName === 'string' ? o.tagName.toLowerCase() : '';
                                return tagSet[tag] === true;
                            };
                        })(set),
                        writable: false, enumerable: false, configurable: true
                    });
                }
            })();
            """);

        context.Eval(script.ToString());
    }
}
