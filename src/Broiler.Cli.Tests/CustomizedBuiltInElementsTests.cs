using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Customized built-in elements (HTML §4.13) — the <c>extends</c> option, the <c>is</c> value, and
/// the interface constructors a customized class reaches through <c>super()</c>.
/// </summary>
/// <remarks>
/// <para>
/// The core Custom Elements slice left these out and said so: <c>define</c> rejected an
/// <c>extends</c> option with a <c>NotSupportedError</c> rather than accepting it and ignoring it. A
/// page that extends a built-in — the idiom for keeping a native control's behaviour and adding to it
/// — therefore lost its component entirely, and lost it at the <c>define</c> call, which takes the
/// rest of the script with it.
/// </para>
/// <para>
/// <b>An element's <c>is</c> value is not its <c>is</c> attribute.</b> That is the part that cannot
/// be guessed and is measured here: an element parsed from <c>&lt;button is="fancy-b"&gt;</c> has
/// both, while <c>new FancyButton()</c> and <c>createElement('button', {is: 'fancy-b'})</c> produce
/// an element whose <c>getAttribute('is')</c> is <c>null</c> — and which still serializes as
/// <c>&lt;button is="fancy-b"&gt;</c>, because HTML §13.3 writes the is value out so the markup
/// re-parses into the same element.
/// </para>
/// <para>Every expectation is Chromium's measured answer over the same probe run against both.</para>
/// </remarks>
public sealed class CustomizedBuiltInElementsTests
{
    private const string Markup =
        "<!DOCTYPE html><html><body>" +
        "<form id=\"f\"><button is=\"fancy-b\" id=\"parsed\">p</button></form>" +
        "</body></html>";

    private static DomBridge Attach(out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, Markup, "https://example.com/index.html");
        return bridge;
    }

    private static string Eval(JSContext context, string body) =>
        context.Eval($$"""
            (() => {
                {{body}}
            })()
            """).ToString();

    /// <summary>
    /// The element a customized class constructs is the extended tag carrying the class — a
    /// <c>&lt;button&gt;</c> that is an instance of both the class and <c>HTMLButtonElement</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Customized_Class_Constructs_The_Extended_Element()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("BUTTON/true/true/f", Eval(context, """
            class F extends HTMLButtonElement { greet() { return 'f'; } }
            customElements.define('fancy-b', F, { extends: 'button' });
            var f = new F();
            return f.tagName + '/' + (f instanceof F) + '/' + (f instanceof HTMLButtonElement) + '/' + f.greet();
            """));
    }

    /// <summary>
    /// The is value and the <c>is</c> attribute are different things: a constructed customized
    /// built-in has the first and not the second, and serializes with it anyway.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Constructed_Element_Has_An_Is_Value_Without_An_Is_Attribute()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("null/false/<button is=\"fancy-b\"></button>", Eval(context, """
            class F extends HTMLButtonElement {}
            customElements.define('fancy-b', F, { extends: 'button' });
            var f = new F();
            return JSON.stringify(f.getAttribute('is')) + '/' + f.hasAttribute('is') + '/' + f.outerHTML;
            """));
    }

    /// <summary><c>createElement(tag, {is})</c> runs the definition's constructor, exactly as
    /// <c>createElement</c> of an autonomous name does.</summary>
    [Fact(Timeout = 600000)]
    public void CreateElement_With_An_Is_Option_Runs_The_Definitions_Constructor()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("F/true/BUTTON/<button is=\"fancy-b\"></button>", Eval(context, """
            class F extends HTMLButtonElement {}
            customElements.define('fancy-b', F, { extends: 'button' });
            var c = document.createElement('button', { is: 'fancy-b' });
            return c.constructor.name + '/' + (c instanceof F) + '/' + c.tagName + '/' + c.outerHTML;
            """));
    }

    /// <summary>
    /// An <c>is</c> option naming nothing defined still gives the element that is value: the name is
    /// the element's from then on, so a later <c>define</c> upgrades it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Undefined_Is_Option_Is_Kept_And_Upgraded_Later()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("HTMLButtonElement/<button is=\"later-b\"></button>/L/true", Eval(context, """
            var early = document.createElement('button', { is: 'later-b' });
            document.body.appendChild(early);
            var before = early.constructor.name + '/' + early.outerHTML;
            class L extends HTMLButtonElement {}
            customElements.define('later-b', L, { extends: 'button' });
            return before + '/' + early.constructor.name + '/' + (early instanceof L);
            """));
    }

    /// <summary>An element parsed with an <c>is</c> attribute is upgraded in place, keeping its
    /// identity, its children and its other attributes.</summary>
    [Fact(Timeout = 600000)]
    public void An_Element_Parsed_With_An_Is_Attribute_Is_Upgraded_In_Place()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("HTMLButtonElement->F/true/true/p/fancy-b", Eval(context, """
            var before = document.getElementById('parsed');
            var beforeCtor = before.constructor.name;
            class F extends HTMLButtonElement {}
            customElements.define('fancy-b', F, { extends: 'button' });
            var after = document.getElementById('parsed');
            return beforeCtor + '->' + after.constructor.name + '/' + (after === before) + '/' +
                   (after instanceof F) + '/' + after.textContent + '/' + after.getAttribute('is');
            """));
    }

    /// <summary>
    /// A definition only reaches the tag it extends. A plain <c>&lt;button&gt;</c> is untouched by a
    /// <c>button</c>-extending definition, and an <c>is</c> naming one on the wrong tag is inert.
    /// </summary>
    /// <remarks>
    /// The second half is the one worth pinning: <c>&lt;div is="fancy-b"&gt;</c> reads as if it
    /// asked to be upgraded, and a browser leaves it a plain <c>&lt;div&gt;</c> rather than running a
    /// <c>HTMLButtonElement</c> subclass's constructor against something that is not a button.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void A_Definition_Only_Reaches_The_Tag_It_Extends()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("false/HTMLDivElement/false", Eval(context, """
            class F extends HTMLButtonElement {}
            customElements.define('fancy-b', F, { extends: 'button' });
            var plain = document.createElement('button');
            var wrong = document.createElement('div');
            wrong.setAttribute('is', 'fancy-b');
            document.body.appendChild(wrong);
            return (plain instanceof F) + '/' + wrong.constructor.name + '/' + (wrong instanceof F);
            """));
    }

    /// <summary>
    /// Only a built-in can be extended. A name that is itself a valid custom element name and a name
    /// no HTML element has are both <c>NotSupportedError</c>s, and they are distinct failures rather
    /// than one "bad tag" case.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Extending_Something_That_Is_Not_A_Builtin_Is_A_NotSupportedError()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "NotSupportedError:\"some-thing\" is a valid custom element name/" +
            "NotSupportedError:\"nosuchtag\" is an HTMLUnknownElement/ok",
            Eval(context, """
                var r = [];
                ['some-thing', 'nosuchtag', 'span'].forEach(function (tag, i) {
                    try {
                        customElements.define('ext-' + i, class extends HTMLElement {}, { extends: tag });
                        r.push('ok');
                    } catch (e) {
                        r.push(e.name + ':' + e.message.replace(
                            "Failed to execute 'define' on 'CustomElementRegistry': ", ''));
                    }
                });
                return r.join('/');
                """));
    }

    /// <summary>
    /// A class must extend the interface its definition names (HTML §4.13.3's active-function-object
    /// check), and the two ways of getting it wrong report differently.
    /// </summary>
    /// <remarks>
    /// Without this check both would silently construct the wrong element: an autonomous
    /// <c>&lt;bad-3&gt;</c> reached through <c>HTMLButtonElement</c>, and a <c>&lt;button&gt;</c>
    /// reached through <c>HTMLElement</c>. The interface a <c>super()</c> call goes through is
    /// therefore passed to the registry rather than inferred from the definition.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void A_Class_Must_Extend_The_Interface_Its_Definition_Names()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "TypeError:Failed to construct 'HTMLButtonElement': Illegal constructor: " +
            "autonomous custom elements must extend HTMLElement/" +
            "TypeError:Failed to construct 'HTMLElement': Illegal constructor: " +
            "localName does not match the HTML element interface",
            Eval(context, """
                var r = [];
                class I extends HTMLButtonElement {}
                customElements.define('bad-3', I);
                try { new I(); r.push('constructed'); } catch (e) { r.push(e.name + ':' + e.message); }
                class J extends HTMLElement {}
                customElements.define('bad-4', J, { extends: 'button' });
                try { new J(); r.push('constructed'); } catch (e) { r.push(e.name + ':' + e.message); }
                return r.join('/');
                """));
    }

    /// <summary>
    /// The interface globals are constructible now, but only as a custom element's base: calling one
    /// directly, or through a class that was never registered, is still the <c>Illegal
    /// constructor</c> a browser answers.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Interface_Global_Is_Still_Not_Directly_Constructible()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("TypeError/TypeError/HTMLButtonElement", Eval(context, """
            var direct, unregistered;
            try { new HTMLButtonElement(); direct = 'ok'; } catch (e) { direct = e.name; }
            class Unreg extends HTMLButtonElement {}
            try { new Unreg(); unregistered = 'ok'; } catch (e) { unregistered = e.name; }
            return direct + '/' + unregistered + '/' + HTMLButtonElement.name;
            """));
    }

    /// <summary>
    /// The tag-name <c>instanceof</c> the interface globals answer with is theirs alone. A subclass
    /// inherits <c>@@hasInstance</c> statically, so without this it would report every
    /// <c>&lt;button&gt;</c> on the page as one of its own instances.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Subclass_Does_Not_Inherit_The_Interfaces_Tag_Test()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true/false/true/true", Eval(context, """
            class F extends HTMLButtonElement {}
            customElements.define('fancy-b', F, { extends: 'button' });
            var plain = document.createElement('button');
            var fancy = new F();
            return (plain instanceof HTMLButtonElement) + '/' + (plain instanceof F) + '/' +
                   (fancy instanceof F) + '/' + (document.createElement('a') instanceof HTMLAnchorElement);
            """));
    }

    /// <summary>
    /// The reaction callbacks reach a customized built-in exactly as they reach an autonomous
    /// element — the definition it belongs to is selected by its is value rather than its tag.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Reactions_Reach_A_Customized_Builtin()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("connected,watched:null->1,disconnected", Eval(context, """
            var log = [];
            class F extends HTMLButtonElement {
                static get observedAttributes() { return ['watched']; }
                connectedCallback() { log.push('connected'); }
                disconnectedCallback() { log.push('disconnected'); }
                attributeChangedCallback(n, o, v) { log.push(n + ':' + o + '->' + v); }
            }
            customElements.define('other-b', F, { extends: 'button' });
            var f = document.createElement('button', { is: 'other-b' });
            document.body.appendChild(f);
            f.setAttribute('watched', '1');
            f.remove();
            return log.join(',');
            """));
    }
}
