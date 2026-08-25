using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Custom Elements (HTML §4.13): the <c>customElements</c> registry, a constructible
/// <c>HTMLElement</c> base, and the reaction callbacks a definition receives.
/// </summary>
/// <remarks>
/// <para>
/// There was no production implementation. <c>customElements</c> was undefined and
/// <c>HTMLElement</c> threw <c>Illegal constructor</c>, so <c>class X extends HTMLElement</c>
/// followed by <c>customElements.define(…)</c> failed on the bare name — which aborts the whole
/// script, not the statement. The WPT runner carried a shim to get past it, and the shim could not
/// reach what mattered: its <c>HTMLElement</c> produced a plain element that did not carry the
/// class's prototype, so component methods were unreachable and the reaction callbacks had to be
/// copied across by hand.
/// </para>
/// <para>Every expectation is Chromium's measured answer over the same probe run against both.</para>
/// </remarks>
public sealed class CustomElementsTests
{
    private const string Markup =
        "<!DOCTYPE html><html><body>" +
        "<ce-b>btext</ce-b><ce-d></ce-d><ce-f watched=\"init\"></ce-f>" +
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

    [Fact(Timeout = 600000)]
    public void The_Registry_Exists_As_An_Interface()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("object/function/true", Eval(context, """
            return (typeof customElements) + '/' + (typeof CustomElementRegistry) + '/' +
                   (customElements instanceof CustomElementRegistry);
            """));
    }

    /// <summary>
    /// The core of the feature: <c>new X()</c> produces a real element that carries the class. The
    /// shim could not do this — its base handed back a plain element, so <c>greet</c> did not exist
    /// on it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Defined_Class_Constructs_A_Real_Element_Carrying_The_Class()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("CE-A/true/true/a:CE-A/A/1", Eval(context, """
            class A extends HTMLElement { greet() { return 'a:' + this.tagName; } }
            customElements.define('ce-a', A);
            var a = new A();
            return a.tagName + '/' + (a instanceof A) + '/' + (a instanceof HTMLElement) + '/' +
                   a.greet() + '/' + a.constructor.name + '/' + a.nodeType;
            """));
    }

    /// <summary>The element is a DOM element in every other respect — it goes into the tree, is
    /// found by a query, keeps its identity, and serializes.</summary>
    [Fact(Timeout = 600000)]
    public void A_Constructed_Custom_Element_Behaves_As_An_Element()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true/true/1/true", Eval(context, """
            class A extends HTMLElement { constructor() { super(); this.setAttribute('data-x', '1'); } }
            customElements.define('ce-a', A);
            var a = new A();
            document.body.appendChild(a);
            var found = document.querySelector('ce-a');
            return (found === a) + '/' + (found instanceof A) + '/' + found.getAttribute('data-x') +
                   '/' + (document.body.innerHTML.indexOf('ce-a') >= 0);
            """));
    }

    /// <summary><c>document.createElement</c> runs the definition's constructor (HTML §4.13.6), so
    /// the result is an instance of the class rather than a plain element with the right tag.</summary>
    [Fact(Timeout = 600000)]
    public void CreateElement_Runs_The_Definitions_Constructor()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("A/function/CE-A", Eval(context, """
            class A extends HTMLElement { greet() { return 'a'; } }
            customElements.define('ce-a', A);
            var el = document.createElement('ce-a');
            return el.constructor.name + '/' + (typeof el.greet) + '/' + el.tagName;
            """));
    }

    /// <summary>Custom element name validation (HTML §4.13.1). The reserved names are the SVG and
    /// MathML elements that already contain a hyphen, which is why the shape test alone is not
    /// enough.</summary>
    [Fact(Timeout = 600000)]
    public void An_Invalid_Name_Is_A_SyntaxError()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "nohyphen=SyntaxError,=SyntaxError,CE-Upper=SyntaxError,1-bad=SyntaxError," +
            "annotation-xml=SyntaxError,ce-ok2=ok",
            Eval(context, """
                var r = [];
                ['nohyphen', '', 'CE-Upper', '1-bad', 'annotation-xml', 'ce-ok2'].forEach(function (n) {
                    try { customElements.define(n, class extends HTMLElement {}); r.push(n + '=ok'); }
                    catch (e) { r.push(n + '=' + e.name); }
                });
                return r.join(',');
                """));
    }

    /// <summary>A name and a constructor may each be used once (HTML §4.13.4).</summary>
    [Fact(Timeout = 600000)]
    public void A_Duplicate_Name_Or_Constructor_Is_A_NotSupportedError()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("NotSupportedError/NotSupportedError", Eval(context, """
            class A extends HTMLElement {}
            customElements.define('ce-a', A);
            var byName, byCtor;
            try { customElements.define('ce-a', class extends HTMLElement {}); byName = 'ok'; }
            catch (e) { byName = e.name; }
            try { customElements.define('ce-a2', A); byCtor = 'ok'; }
            catch (e) { byCtor = e.name; }
            return byName + '/' + byCtor;
            """));
    }

    /// <summary>
    /// Customized built-ins are rejected rather than silently ignored — accepting the option and
    /// doing nothing would leave a page believing its <c>&lt;button is="…"&gt;</c> was upgraded.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Customized_Builtin_Is_Rejected_Rather_Than_Ignored()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("NotSupportedError", Eval(context, """
            try {
                customElements.define('ce-btn', class extends HTMLElement {}, { extends: 'button' });
                return 'ok';
            } catch (e) { return e.name; }
            """));
    }

    [Fact(Timeout = 600000)]
    public void Get_GetName_And_WhenDefined_Answer()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("function/A/ce-a/undefined/true/function", Eval(context, """
            class A extends HTMLElement {}
            customElements.define('ce-a', A);
            var got = customElements.get('ce-a');
            var p = customElements.whenDefined('ce-a');
            return (typeof got) + '/' + got.name + '/' + customElements.getName(A) + '/' +
                   String(customElements.get('ce-nope')) + '/' + (p instanceof Promise) + '/' +
                   (typeof p.then);
            """));
    }

    /// <summary>
    /// <c>new HTMLElement()</c> and a subclass that was never registered are both <c>TypeError</c>s:
    /// with no definition there is no tag name to build.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Constructing_Without_A_Definition_Is_A_TypeError()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("TypeError/TypeError", Eval(context, """
            var bare, unreg;
            try { new HTMLElement(); bare = 'ok'; } catch (e) { bare = e.name; }
            class Unreg extends HTMLElement {}
            try { new Unreg(); unreg = 'ok'; } catch (e) { unreg = e.name; }
            return bare + '/' + unreg;
            """));
    }

    /// <summary>
    /// An element parsed before its definition is upgraded in place, keeping its node identity and
    /// its children.
    /// </summary>
    /// <remarks>
    /// Identity is the part a replace-and-swap upgrade cannot give, and it is what the shim lost: a
    /// page holding the element from before the definition landed would have kept pointing at the
    /// discarded one. Running the author's constructor against the existing node — the
    /// specification's construction stack — is what avoids that.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void An_Element_Parsed_Before_Its_Definition_Is_Upgraded_In_Place()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("HTMLElement->B/true/true/b/btext", Eval(context, """
            var before = document.querySelector('ce-b');
            var beforeCtor = before.constructor.name;
            class B extends HTMLElement { greet() { return 'b'; } }
            customElements.define('ce-b', B);
            var after = document.querySelector('ce-b');
            return beforeCtor + '->' + after.constructor.name + '/' + (after === before) + '/' +
                   (after instanceof B) + '/' + after.greet() + '/' + after.textContent;
            """));
    }

    /// <summary>
    /// <c>connectedCallback</c> runs on insertion and <c>disconnectedCallback</c> on removal, both
    /// synchronously — a browser runs them before the statement after the mutation, which is why
    /// these are dispatched off the canonical mutation stream rather than through
    /// <c>MutationObserver</c>, whose delivery is a microtask.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Connected_And_Disconnected_Callbacks_Run_Synchronously()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("created,connected:CE-C,disconnected", Eval(context, """
            var log = [];
            class C extends HTMLElement {
                connectedCallback() { log.push('connected:' + this.tagName); }
                disconnectedCallback() { log.push('disconnected'); }
            }
            customElements.define('ce-c', C);
            var c = document.createElement('ce-c');
            log.push('created');
            document.body.appendChild(c);
            c.remove();
            return log.join(',');
            """));
    }

    /// <summary>Upgrading an element that is already in the tree runs its connected reaction — this
    /// is where a component builds itself, so skipping it leaves every such element empty.</summary>
    [Fact(Timeout = 600000)]
    public void Upgrading_A_Connected_Element_Runs_Its_Connected_Callback()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("connected", Eval(context, """
            var log = [];
            class D extends HTMLElement { connectedCallback() { log.push('connected'); } }
            customElements.define('ce-d', D);
            return log.join(',') || '(none)';
            """));
    }

    /// <summary>
    /// <c>attributeChangedCallback</c> fires for the attributes named by <c>observedAttributes</c>
    /// and no others, with the old and new values, and reports a removal as a new value of
    /// <c>null</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AttributeChangedCallback_Reports_Only_Observed_Attributes()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("watched:null->1,watched:1->2,watched:2->null", Eval(context, """
            var log = [];
            class E extends HTMLElement {
                static get observedAttributes() { return ['watched']; }
                attributeChangedCallback(name, oldV, newV) { log.push(name + ':' + oldV + '->' + newV); }
            }
            customElements.define('ce-e', E);
            var e = document.createElement('ce-e');
            e.setAttribute('watched', '1');
            e.setAttribute('watched', '2');
            e.setAttribute('ignored', 'x');
            e.removeAttribute('watched');
            return log.join(',');
            """));
    }

    /// <summary>
    /// An upgrade reports the attributes the element already carries, with an <c>oldValue</c> of
    /// <c>null</c>: from the definition's point of view each is being set for the first time.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Upgrade_Reports_The_Attributes_The_Element_Already_Had()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("watched:null->init", Eval(context, """
            var log = [];
            class F extends HTMLElement {
                static get observedAttributes() { return ['watched']; }
                attributeChangedCallback(n, o, v) { log.push(n + ':' + o + '->' + v); }
            }
            customElements.define('ce-f', F);
            return log.join(',') || '(none)';
            """));
    }

    /// <summary>
    /// An element built after its definition exists is upgraded as soon as it is created — the shape
    /// a page produces with <c>innerHTML</c> or by appending parsed markup after its component
    /// script ran.
    /// </summary>
    /// <remarks>
    /// It is already upgraded <em>before</em> the subtree is inserted, which is what a browser does
    /// and is measured here rather than assumed: the mutation that builds the subtree is itself what
    /// the upgrade hangs off, so it does not wait for the subtree to reach the document.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void An_Element_Built_After_Its_Definition_Is_Upgraded()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("function->g/true", Eval(context, """
            class G extends HTMLElement { greet() { return 'g'; } }
            customElements.define('ce-g', G);
            var holder = document.createElement('div');
            holder.innerHTML = '<ce-g></ce-g>';
            var inner = holder.firstChild;
            var before = (typeof inner.greet);
            document.body.appendChild(holder);
            return before + '->' + inner.greet() + '/' + (inner instanceof G);
            """));
    }

    /// <summary>
    /// <c>customElements.upgrade(root)</c> walks a detached subtree and is idempotent on elements
    /// that are already upgraded.
    /// </summary>
    /// <remarks>
    /// The element here is upgraded before the call, because building it was itself a mutation — so
    /// what this pins is that <c>upgrade</c> leaves it alone rather than running the constructor a
    /// second time. A browser answers the same way, which is why the "before" half reads
    /// <c>function</c> and not <c>undefined</c>.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void Upgrade_Is_Idempotent_On_An_Already_Upgraded_Element()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("function->g/1", Eval(context, """
            var constructed = 0;
            class G extends HTMLElement {
                constructor() { super(); constructed++; }
                greet() { return 'g'; }
            }
            customElements.define('ce-g', G);
            var detached = document.createElement('div');
            detached.innerHTML = '<ce-g></ce-g>';
            var inner = detached.firstChild;
            var before = (typeof inner.greet);
            customElements.upgrade(detached);
            return before + '->' + inner.greet() + '/' + constructed;
            """));
    }

    /// <summary>A hyphenated tag with no definition stays a plain <c>HTMLElement</c> — a valid
    /// custom element name is an <c>HTMLElement</c> whether or not anything defined it.</summary>
    [Fact(Timeout = 600000)]
    public void An_Undefined_Custom_Tag_Is_A_Plain_HTMLElement()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("HTMLElement/true", Eval(context, """
            var un = document.createElement('ce-undefined');
            return un.constructor.name + '/' + (un instanceof HTMLElement);
            """));
    }

    /// <summary>
    /// A throwing reaction does not take the DOM operation that triggered it down with it: an
    /// <c>appendChild</c> must not fail because a component's <c>connectedCallback</c> did.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Throwing_Reaction_Does_Not_Break_The_Mutation()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("appended/true", Eval(context, """
            class H extends HTMLElement { connectedCallback() { throw new Error('boom'); } }
            customElements.define('ce-h', H);
            var h = document.createElement('ce-h');
            document.body.appendChild(h);
            return 'appended/' + (document.querySelector('ce-h') === h);
            """));
    }
}
