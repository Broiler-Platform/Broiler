using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Element wrappers, the document and attribute nodes report the interface they implement, and the
/// interfaces inherit along the chain Web IDL defines for them.
/// </summary>
/// <remarks>
/// <para>
/// Every wrapper used to report <c>constructor.name</c> of <c>"Object"</c> with
/// <c>Object.getPrototypeOf(el) === Object.prototype</c>. The non-element node kinds were fixed
/// first; elements were left because an element's interface is a tag question the engine's table
/// could not answer — it carried an overlapping <c>HTMLMediaElement → "audio video"</c> entry beside
/// <c>HTMLAudioElement → "audio"</c>, so <c>audio</c> named two interfaces and a reverse lookup had
/// none, and a tag the table omitted had to fall back to something a browser splits three ways.
/// </para>
/// <para>
/// The answer was measured rather than guessed: every HTML tag was run through Chromium's own
/// <c>document.createElement(tag).constructor.name</c> and the table rebuilt from the result. The
/// expectations below are that measurement, including the three cases that reasoning gets wrong —
/// <c>plaintext</c> is <c>HTMLElement</c> and not <c>HTMLPreElement</c>, a hyphenated unknown name is
/// <c>HTMLElement</c> and not <c>HTMLUnknownElement</c>, and a removed element like <c>applet</c> is
/// <c>HTMLUnknownElement</c> even though the parser still knows the name.
/// </para>
/// </remarks>
public sealed class DomInterfacePrototypeTests
{
    private static DomBridge Attach(out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!DOCTYPE html><html><body><div id=\"a\" class=\"c\">x</div></body></html>",
            "https://example.com/index.html");
        return bridge;
    }

    private static string Eval(JSContext context, string expression) =>
        context.Eval(expression).ToString();

    /// <summary>A tag with an interface of its own names it.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("div", "HTMLDivElement")]
    [InlineData("span", "HTMLSpanElement")]
    [InlineData("a", "HTMLAnchorElement")]
    [InlineData("input", "HTMLInputElement")]
    [InlineData("form", "HTMLFormElement")]
    [InlineData("td", "HTMLTableCellElement")]
    [InlineData("th", "HTMLTableCellElement")]
    [InlineData("h3", "HTMLHeadingElement")]
    [InlineData("blockquote", "HTMLQuoteElement")]
    [InlineData("q", "HTMLQuoteElement")]
    [InlineData("del", "HTMLModElement")]
    [InlineData("pre", "HTMLPreElement")]
    [InlineData("listing", "HTMLPreElement")]
    [InlineData("xmp", "HTMLPreElement")]
    // audio and video name their own interface, not the abstract base they share. This is the pair
    // that made the old table ambiguous.
    [InlineData("audio", "HTMLAudioElement")]
    [InlineData("video", "HTMLVideoElement")]
    public void A_Tag_With_Its_Own_Interface_Names_It(string tag, string expected)
    {
        using var bridge = Attach(out var context);

        Assert.Equal(expected, Eval(context, $"document.createElement('{tag}').constructor.name"));
    }

    /// <summary>
    /// The three-way fallback for a tag with no interface of its own — the part that could not be
    /// guessed, since all three answers are plausible and only one is right per tag.
    /// </summary>
    [Theory(Timeout = 600000)]
    // Known to the parser, no interface of its own.
    [InlineData("section", "HTMLElement")]
    [InlineData("abbr", "HTMLElement")]
    [InlineData("nav", "HTMLElement")]
    [InlineData("summary", "HTMLElement")]
    [InlineData("wbr", "HTMLElement")]
    [InlineData("noframes", "HTMLElement")]
    // The one the old table had wrong: it sat under HTMLPreElement with listing/pre/xmp.
    [InlineData("plaintext", "HTMLElement")]
    // A hyphen makes it a valid custom element name, which is an HTMLElement even undefined.
    [InlineData("x-foo", "HTMLElement")]
    [InlineData("my-element", "HTMLElement")]
    // Neither known nor a valid custom element name.
    [InlineData("foo", "HTMLUnknownElement")]
    [InlineData("blink", "HTMLUnknownElement")]
    [InlineData("bgsound", "HTMLUnknownElement")]
    // Removed from HTML, so unknown despite being a name the parser still recognizes.
    [InlineData("applet", "HTMLUnknownElement")]
    [InlineData("keygen", "HTMLUnknownElement")]
    public void A_Tag_Without_Its_Own_Interface_Falls_Back_The_Way_A_Browser_Does(string tag, string expected)
    {
        using var bridge = Attach(out var context);

        Assert.Equal(expected, Eval(context, $"document.createElement('{tag}').constructor.name"));
    }

    /// <summary>A tag name is matched case-insensitively, as an HTML document's are.</summary>
    [Fact(Timeout = 600000)]
    public void A_Tag_Name_Is_Matched_Case_Insensitively()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("HTMLDivElement", Eval(context, "document.createElement('DIV').constructor.name"));
        Assert.Equal("HTMLTableCellElement", Eval(context, "document.createElement('TD').constructor.name"));
    }

    /// <summary>
    /// Parsed elements answer the same as constructed ones — the link is applied where every wrapper
    /// is minted, not on the <c>createElement</c> path.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Parsed_Element_Reports_Its_Interface_Too()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("HTMLDivElement", Eval(context, "document.getElementById('a').constructor.name"));
        Assert.Equal("HTMLBodyElement", Eval(context, "document.body.constructor.name"));
        Assert.Equal("HTMLHtmlElement", Eval(context, "document.documentElement.constructor.name"));
        Assert.Equal("HTMLHeadElement", Eval(context, "document.head.constructor.name"));
    }

    /// <summary>
    /// The interface chain, which is what makes the prototype link more than cosmetic. Measured from
    /// Chromium's own chains.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("HTMLDivElement", "HTMLDivElement -> HTMLElement -> Element -> Node -> EventTarget")]
    [InlineData("HTMLAudioElement", "HTMLAudioElement -> HTMLMediaElement -> HTMLElement -> Element -> Node -> EventTarget")]
    [InlineData("HTMLVideoElement", "HTMLVideoElement -> HTMLMediaElement -> HTMLElement -> Element -> Node -> EventTarget")]
    [InlineData("HTMLUnknownElement", "HTMLUnknownElement -> HTMLElement -> Element -> Node -> EventTarget")]
    [InlineData("Text", "Text -> CharacterData -> Node -> EventTarget")]
    [InlineData("Comment", "Comment -> CharacterData -> Node -> EventTarget")]
    [InlineData("Attr", "Attr -> Node -> EventTarget")]
    [InlineData("HTMLDocument", "HTMLDocument -> Document -> Node -> EventTarget")]
    public void An_Interface_Inherits_Along_The_Chain_Web_IDL_Gives_It(string name, string expected)
    {
        using var bridge = Attach(out var context);

        Assert.Equal(expected, Eval(context, $$"""
            (() => {
                var names = [], p = {{name}}.prototype;
                while (p && p !== Object.prototype) {
                    names.push(p.constructor && p.constructor.name);
                    p = Object.getPrototypeOf(p);
                }
                return names.join(' -> ');
            })()
            """));
    }

    /// <summary>
    /// The functional payoff, and the reason this is not a cosmetic change: extending an interface
    /// prototype — the ordinary polyfill idiom — now reaches instances. Before, the assignment went
    /// to an object nothing inherited from, so it silently did nothing.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Extending_An_Interface_Prototype_Reaches_Instances()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("7|9|11|13", Eval(context, """
            (() => {
                HTMLDivElement.prototype.viaConcrete = 7;   // the element's own interface
                Element.prototype.viaElement = 9;           // two levels up
                Node.prototype.viaNode = 11;                // three levels up
                HTMLMediaElement.prototype.viaAbstract = 13; // an abstract base
                var d = document.createElement('div');
                var a = document.createElement('audio');
                return d.viaConcrete + '|' + d.viaElement + '|' + d.viaNode + '|' + a.viaAbstract;
            })()
            """));
    }

    /// <summary>
    /// <c>instanceof</c> keeps answering, including for an abstract base that no tag names directly
    /// any more. That is the regression risk in making the table single-valued: <c>audio</c> stopped
    /// naming <c>HTMLMediaElement</c>, so the base's answer now has to come from the inheritance
    /// edges instead.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Instanceof_Still_Answers_Including_For_An_Abstract_Base()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true|true|true|true|true|true", Eval(context, """
            (() => {
                var a = document.createElement('audio');
                var d = document.createElement('div');
                return (a instanceof HTMLAudioElement) + '|' + (a instanceof HTMLMediaElement) + '|' +
                       (a instanceof HTMLElement) + '|' + (d instanceof HTMLDivElement) + '|' +
                       (d instanceof Element) + '|' + (d instanceof Node);
            })()
            """));
    }

    /// <summary>
    /// The <c>instanceof</c> half of the <c>plaintext</c> correction. It used to be an
    /// <c>HTMLPreElement</c>, which is what the wrong table entry made it; a browser says it is not.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Plaintext_Is_Not_A_Pre_Element()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("false|true|true|true", Eval(context, """
            (() => {
                var p = document.createElement('plaintext');
                var pre = document.createElement('pre');
                return (p instanceof HTMLPreElement) + '|' + (p instanceof HTMLElement) + '|' +
                       (pre instanceof HTMLPreElement) + '|' + (pre instanceof HTMLElement);
            })()
            """));
    }

    /// <summary>
    /// A tag with no interface of its own is an <c>HTMLElement</c> and not an
    /// <c>HTMLUnknownElement</c>; a genuinely unknown one is both. html5test's
    /// <c>x instanceof HTMLElement &amp;&amp; !(x instanceof HTMLUnknownElement)</c> is the check
    /// that split depends on.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Known_And_Unknown_Element_Split_Holds()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true,false|true,true|true,false", Eval(context, """
            (() => {
                function split(tag) {
                    var e = document.createElement(tag);
                    return (e instanceof HTMLElement) + ',' + (e instanceof HTMLUnknownElement);
                }
                return split('section') + '|' + split('foo') + '|' + split('x-foo');
            })()
            """));
    }

    /// <summary>
    /// The document and attribute wrappers, neither of which is minted at the node choke point, so
    /// each needed its own link. <c>HTMLDocument</c> is an interface the engine did not register at
    /// all.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Document_And_Attribute_Wrappers_Report_Their_Interfaces()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("HTMLDocument", Eval(context, "document.constructor.name"));
        Assert.Equal("Attr", Eval(context, "document.getElementById('a').attributes[0].constructor.name"));
        Assert.Equal("true", Eval(context, "String(document instanceof HTMLDocument)"));
        Assert.Equal("true", Eval(context, "String(document instanceof Document)"));
        // A frame's document is a document like any other.
        Assert.Equal("HTMLDocument", Eval(context,
            "document.implementation.createHTMLDocument('t').constructor.name"));
    }

    /// <summary>
    /// Linking the prototype must not change what an element *owns*. The bindings still install every
    /// member as an own property, so this is what confirms the link added inheritance without moving
    /// anything — and that <c>for...in</c> gained nothing, since a prototype's <c>constructor</c> is
    /// non-enumerable and setPrototypeOf preserved it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Linking_The_Prototype_Does_Not_Change_What_An_Element_Owns()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true|true|false", Eval(context, """
            (() => {
                var d = document.getElementById('a');
                var ownsTagName = Object.prototype.hasOwnProperty.call(d, 'tagName');
                var protoIsInterface = Object.getPrototypeOf(d) === HTMLDivElement.prototype;
                var enumeratesConstructor = false;
                for (var k in d) { if (k === 'constructor') enumeratesConstructor = true; }
                return ownsTagName + '|' + protoIsInterface + '|' + enumeratesConstructor;
            })()
            """));
    }

    /// <summary>
    /// An SVG element is left at <c>SVGElement</c> rather than given a per-tag name. A browser has
    /// <c>SVGRectElement</c> and the rest; this engine registers no SVG element interfaces to point
    /// at, and minting globals only so a name can be reported is what track 6 action 1 rules out.
    /// Pinned so the boundary is a decision rather than an omission.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_SVG_Element_Reports_The_Base_Interface()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("SVGElement|SVGElement|true", Eval(context, """
            (() => {
                var NS = 'http://www.w3.org/2000/svg';
                var rect = document.createElementNS(NS, 'rect');
                var svg = document.createElementNS(NS, 'svg');
                return rect.constructor.name + '|' + svg.constructor.name + '|' + (rect instanceof SVGElement);
            })()
            """));
    }
}
