using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The two argument-syntax checks a scripted DOM method owes its caller: <c>setAttribute</c>'s
/// attribute name (DOM §4.9.1, <c>InvalidCharacterError</c>) and <c>querySelector</c>'s selector
/// (DOM §4.2.6, <c>SyntaxError</c>). Both used to be absent, so an invalid argument was accepted.
/// </summary>
/// <remarks>
/// <para>
/// Every expectation here is Chromium's measured answer, taken from the same corpus run against both
/// engines rather than reasoned from the grammar — which is what caught two assumptions that were
/// wrong in opposite directions: <c>[tabindex=0]</c> is a syntax error (an unquoted attribute value
/// must be an identifier, and a digit cannot start one) while <c>setAttribute('a:b:c', …)</c> is
/// perfectly valid (the XML <c>Name</c> production admits colons).
/// </para>
/// <para>
/// The permissive cases matter as much as the throwing ones and are pinned beside them, because the
/// risk this change carries is over-rejection: a selector or attribute name a page legitimately uses
/// that starts throwing would break a page that worked before.
/// </para>
/// </remarks>
public sealed class DomApiSyntaxTests
{
    private static DomBridge Attach(out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(
            context,
            "<!DOCTYPE html><html><body><div id=a class=c><p>one</p><p>two</p></div></body></html>",
            "https://example.com/index.html");
        return bridge;
    }

    /// <summary>Runs <paramref name="body"/> and reports either <c>OK</c> or the thrown error's name
    /// and legacy code, so a failure prints what the engine actually did.</summary>
    private static string Outcome(JSContext context, string body) =>
        context.Eval($$"""
            (() => {
                var el = document.getElementById('a');
                try { {{body}}; return 'OK'; }
                catch (e) { return 'THREW ' + (e.name || '?') + '/' + (e.code === undefined ? '?' : e.code); }
            })()
            """).ToString();

    // ---------------- setAttribute ----------------

    [Theory(Timeout = 600000)]
    // Characters the XML Name production does not admit.
    [InlineData("@click")]
    [InlineData("foo bar")]
    [InlineData("x/y")]
    [InlineData("ns:*")]
    [InlineData("a=b")]
    [InlineData("a<b")]
    // A name must start with a letter, an underscore or a colon — not a digit, a dot, a hyphen or a
    // sigil.
    [InlineData("1abc")]
    [InlineData("-x")]
    [InlineData(".a")]
    [InlineData("#a")]
    // The empty string. It did fail before, but as a bare Error with no name and no code, so a
    // caller had nothing to branch on.
    [InlineData("")]
    public void SetAttribute_Rejects_An_Invalid_Name_With_InvalidCharacterError(string name)
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "THREW InvalidCharacterError/5",
            Outcome(context, $"el.setAttribute({Json(name)}, 'v')"));
    }

    /// <summary>
    /// The names that must keep working. Colons are the load-bearing case: <c>Name</c> allows them, so
    /// all of <c>xlink:href</c>, <c>v-on:click</c> and even <c>a:b:c</c> are valid — reusing the
    /// element-name rule, which deliberately forbids colons, would have broken inline SVG and every
    /// framework that spells a binding that way.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("xlink:href")]
    [InlineData("v-on:click")]
    [InlineData("a:b:c")]
    [InlineData("data-x")]
    [InlineData("_x")]
    [InlineData("a.b")]
    // Non-ASCII names are accepted, as they are by a browser — the rule is Unicode categories, not
    // ASCII letters.
    [InlineData("aé")]
    [InlineData("éa")]
    public void SetAttribute_Accepts_A_Valid_Name(string name)
    {
        using var bridge = Attach(out var context);

        Assert.Equal("OK", Outcome(context, $"el.setAttribute({Json(name)}, 'v')"));
        Assert.Equal(
            "v",
            context.Eval($"document.getElementById('a').getAttribute({Json(name)})").ToString());
    }

    /// <summary>
    /// <c>toggleAttribute</c> runs the same check (DOM §4.9.4), while the three methods that only
    /// *ask* about a name — <c>getAttribute</c>, <c>hasAttribute</c>, <c>removeAttribute</c> — do not.
    /// A browser draws the line in exactly that place, which is why the permissive half is pinned.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Only_The_Attribute_Methods_That_Create_A_Name_Validate_It()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("THREW InvalidCharacterError/5", Outcome(context, "el.toggleAttribute('@click')"));
        Assert.Equal("OK", Outcome(context, "el.toggleAttribute('ok-name')"));
        Assert.Equal("OK", Outcome(context, "el.getAttribute('@click')"));
        Assert.Equal("OK", Outcome(context, "el.hasAttribute('@click')"));
        Assert.Equal("OK", Outcome(context, "el.removeAttribute('@click')"));
    }

    /// <summary>
    /// <c>setAttributeNS</c> validates through the qualified-name rule <c>createElementNS</c> already
    /// owned (DOM §4.9.2 "validate and extract"), so an invalid character is an
    /// <c>InvalidCharacterError</c> and a prefix with no namespace a <c>NamespaceError</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void SetAttributeNS_Validates_Its_Qualified_Name()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "THREW InvalidCharacterError/5",
            Outcome(context, "el.setAttributeNS(null, '@x', 'v')"));
        Assert.Equal("OK", Outcome(context, "el.setAttributeNS(null, 'okn', 'v')"));
        Assert.Equal(
            "OK",
            Outcome(context, "el.setAttributeNS('http://www.w3.org/1999/xlink', 'xlink:href', 'v')"));
    }

    // ---------------- querySelector and friends ----------------

    /// <summary>
    /// The reason this half matters is not the missing exception but the wrong answer it was hiding.
    /// The lenient matcher read <c>div:::bogus</c> as <c>div</c> and returned a real element, and
    /// <c>[</c> matched several — so an invalid selector did not fail, it silently succeeded at
    /// something else.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Invalid_Selector_Throws_Instead_Of_Matching_The_Wrong_Elements()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("THREW SyntaxError/12", Outcome(context, "document.querySelector('div:::bogus')"));
        Assert.Equal("THREW SyntaxError/12", Outcome(context, "document.querySelectorAll('[')"));
    }

    [Theory(Timeout = 600000)]
    // Unbalanced or empty delimiters.
    [InlineData("[")]
    [InlineData("[]")]
    [InlineData(":nope(")]
    [InlineData(":not()")]
    [InlineData(":nth-child()")]
    // An empty complex selector anywhere in the list.
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("div,")]
    [InlineData(",div")]
    [InlineData("div,,p")]
    // A combinator with nothing on one side, or two in a row.
    [InlineData(">>>")]
    [InlineData("div >")]
    [InlineData("> div")]
    [InlineData("a++b")]
    [InlineData("div >> p")]
    [InlineData("div /deep/ p")]
    // A sigil with no identifier after it.
    [InlineData("#")]
    [InlineData(".")]
    [InlineData(":")]
    [InlineData("::")]
    [InlineData("a:")]
    [InlineData("a::")]
    [InlineData("div:::bogus")]
    // An identifier cannot start with a digit.
    [InlineData(".1a")]
    [InlineData("#1a")]
    // A character that is not part of any simple selector.
    [InlineData("div@x")]
    [InlineData("div!")]
    // Malformed attribute selectors. The unquoted-value cases are the ones reasoning gets wrong: the
    // value must be an identifier, so a bare number is a syntax error.
    [InlineData("[data-x=1]")]
    [InlineData("[tabindex=0]")]
    [InlineData("[a=b c]")]
    [InlineData("[a=]")]
    [InlineData("[=b]")]
    // A named namespace prefix cannot be resolved by querySelector, which has no declarations.
    [InlineData("svg|rect")]
    [InlineData("a|b")]
    [InlineData("a|*")]
    [InlineData("*|")]
    [InlineData("|")]
    // The argument of a selector-list pseudo is itself validated.
    [InlineData(":not(@bad)")]
    public void QuerySelector_Rejects_An_Unparsable_Selector_With_SyntaxError(string selector)
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "THREW SyntaxError/12",
            Outcome(context, $"document.querySelector({Json(selector)})"));
    }

    /// <summary>
    /// The selectors that must keep working — the half that would turn a silent non-match into a
    /// thrown exception if the validator were too strict.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("#a")]
    [InlineData("*")]
    [InlineData(".a.b#c")]
    [InlineData("a b")]
    [InlineData("a  b")]
    [InlineData("a ~ b + c > d")]
    [InlineData("a,b")]
    [InlineData("a , b")]
    [InlineData(" p ")]
    // Type selectors that look like they start wrongly but do not: '-' is an identifier start.
    [InlineData("-foo")]
    [InlineData("--foo")]
    // Attribute selectors, every operator, quoted and unquoted values, the case flag, and a '['
    // inside a string.
    [InlineData("[a]")]
    [InlineData("[a=b]")]
    [InlineData("[a=\"b\"]")]
    [InlineData("[a='b']")]
    [InlineData("[a~=b]")]
    [InlineData("[a|=b]")]
    [InlineData("[a^=b]")]
    [InlineData("[a$=b]")]
    [InlineData("[a*=b]")]
    [InlineData("[a=b i]")]
    [InlineData("[a=-b]")]
    [InlineData("[a=--b]")]
    [InlineData("[ a = b ]")]
    [InlineData("[a=b][c=d]")]
    [InlineData("[title=\"a[b\"]")]
    // The namespace forms that need no declaration.
    [InlineData("*|a")]
    [InlineData("|a")]
    [InlineData("*|*")]
    [InlineData("|*")]
    // Pseudos, including nested and functional ones.
    [InlineData("::before")]
    [InlineData(":root")]
    [InlineData("input:checked")]
    [InlineData("p:first-child")]
    [InlineData("a[href]:hover")]
    [InlineData("a:not(.x):not(.y)")]
    [InlineData("p:not(:not(a))")]
    [InlineData(":is(a, b)")]
    [InlineData(":where(a)")]
    [InlineData(":not(a, b)")]
    [InlineData(":nth-child(2n+1)")]
    [InlineData(":nth-child(odd)")]
    [InlineData(":nth-child(2n of .c)")]
    [InlineData(":lang(en)")]
    // :has() takes a *relative* selector, so a leading combinator is legal there and only there.
    [InlineData(":has(p)")]
    [InlineData(":has(> p)")]
    [InlineData(":has(+ p)")]
    [InlineData(":has(~ p)")]
    // Escapes, including the hex form whose trailing space belongs to the escape rather than
    // separating two compounds.
    [InlineData("a\\:b")]
    [InlineData(".a\\.b")]
    [InlineData("[data-a\\.b]")]
    [InlineData("a\\ b")]
    [InlineData("\\31 23")]
    public void QuerySelector_Accepts_A_Valid_Selector(string selector)
    {
        using var bridge = Attach(out var context);

        Assert.Equal("OK", Outcome(context, $"document.querySelector({Json(selector)})"));
    }

    /// <summary>
    /// All five scripted entry points that take a selector throw, and throw the same thing. A browser
    /// does; that it follows from them sharing an algorithm is not evidence, so it is measured and
    /// pinned.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("document.querySelector(%)")]
    [InlineData("document.querySelectorAll(%)")]
    [InlineData("el.querySelector(%)")]
    [InlineData("el.querySelectorAll(%)")]
    [InlineData("el.matches(%)")]
    [InlineData("el.closest(%)")]
    [InlineData("document.createDocumentFragment().querySelector(%)")]
    [InlineData("document.getElementById('fr').contentDocument.querySelector(%)")]
    public void Every_Selector_Entry_Point_Throws_The_Same_SyntaxError(string call)
    {
        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(
            context,
            """
            <!DOCTYPE html><html><body><div id="a"><p></p></div>
            <iframe id="fr" srcdoc='<!DOCTYPE html><html><body><p></p></body></html>'></iframe>
            </body></html>
            """,
            "https://example.com/index.html");

        Assert.Equal(
            "THREW SyntaxError/12",
            Outcome(context, call.Replace("%", "'div@x'")));
        Assert.Equal("OK", Outcome(context, call.Replace("%", "'#a'")));
    }

    /// <summary>
    /// The two divergences from Chromium that are deliberate, pinned so a later change to either is a
    /// decision rather than a drift. Both are permissive — Broiler accepts where Chromium throws —
    /// which is the only safe direction: the opposite would turn a page that works into one that
    /// does not.
    /// </summary>
    /// <remarks>
    /// A well-formed but unknown pseudo is accepted because rejecting one needs a list of every
    /// pseudo this engine supports, and such a list drifts against the page's expectations rather
    /// than the specification's — Chromium itself accepts <c>:focus-visible</c> and
    /// <c>::-webkit-scrollbar</c> while rejecting <c>::-moz-focus-inner</c>. The Selectors 4 <c>s</c>
    /// case flag is accepted because it is valid per specification and Chromium simply has not
    /// implemented it.
    /// </remarks>
    [Theory(Timeout = 600000)]
    [InlineData(":nope")]
    [InlineData("::bogus")]
    [InlineData("::-moz-focus-inner")]
    [InlineData("::before:hover")]
    [InlineData("[a=b s]")]
    public void A_WellFormed_But_Unsupported_Selector_Is_Accepted_Rather_Than_Rejected(string selector)
    {
        using var bridge = Attach(out var context);

        Assert.Equal("OK", Outcome(context, $"document.querySelector({Json(selector)})"));
        // Accepting must not mean matching something arbitrary: the answer is still no element,
        // which is what a browser answers for every one of these.
        Assert.Equal(
            "null",
            context.Eval($"String(document.querySelector({Json(selector)}))").ToString());
    }

    /// <summary>
    /// <b>Characterization of a gap this work narrowed but did not close.</b> An unknown pseudo-class
    /// <em>with an argument</em> still matches the first element instead of nothing, so
    /// <c>querySelector(':matches(a)')</c> answers <c>&lt;html&gt;</c> where a browser throws and
    /// where the argument-less <c>:nope</c> already answers <c>null</c>.
    /// </summary>
    /// <remarks>
    /// The cause is in the matcher, not here: its pseudo-class dispatch falls through to a lenient
    /// default for a name it does not know, and the argument-less path reaches a stricter arm than
    /// the functional one. That is <c>Broiler.CSS.Dom</c>'s <c>CssSelectorMatcher</c>, a submodule,
    /// and it is a matching question rather than a syntax one — this file's validator has already
    /// done its job by the time it is reached. Pinned as the current answer, wrong though it is, so
    /// that fixing the matcher trips this test rather than passing unnoticed.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void An_Unknown_Functional_Pseudo_Class_Still_Over_Matches()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("OK", Outcome(context, "document.querySelector(':matches(a)')"));
        Assert.Equal(
            "HTML",
            context.Eval("document.querySelector(':matches(a)').tagName").ToString());
        // The argument-less form is already right, which is what makes this a matcher arm rather
        // than a whole missing rule.
        Assert.Equal("null", context.Eval("String(document.querySelector(':nope'))").ToString());
    }

    /// <summary>
    /// A pseudo-element selects a box rather than an element, so a selector carrying one matches
    /// nothing through the DOM API — <c>null</c> from <c>querySelector</c> and <c>closest</c>,
    /// <c>false</c> from <c>matches</c>, an empty list from <c>querySelectorAll</c> — however well it
    /// parses.
    /// </summary>
    /// <remarks>
    /// This was the same defect as the invalid-selector one, reached by a different route: the
    /// matcher strips the pseudo-element and matches whatever is left, so
    /// <c>querySelector('::before')</c> returned the <c>&lt;html&gt;</c> element. Both the modern
    /// two-colon and legacy one-colon spellings are covered, and a pseudo-element that is *not* the
    /// subject is a syntax error instead, which is the one case in this family that throws.
    /// </remarks>
    [Theory(Timeout = 600000)]
    [InlineData("::before")]
    [InlineData("::after")]
    [InlineData("::marker")]
    [InlineData("::selection")]
    [InlineData("div::before")]
    [InlineData("#a::before")]
    [InlineData("p::first-line")]
    [InlineData(":before")]
    [InlineData(":first-letter")]
    public void A_Selector_Carrying_A_Pseudo_Element_Matches_Nothing(string selector)
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "null|0|false|null",
            context.Eval($$"""
                (() => {
                    var s = {{Json(selector)}};
                    var el = document.getElementById('a');
                    return String(document.querySelector(s)) + '|' + document.querySelectorAll(s).length +
                           '|' + el.matches(s) + '|' + String(el.closest(s));
                })()
                """).ToString());
    }

    /// <summary>A pseudo-element is only ever the subject of a selector, so anything after it is a
    /// syntax error — the one member of the pseudo-element family that throws rather than
    /// not matching.</summary>
    [Fact(Timeout = 600000)]
    public void A_Pseudo_Element_Before_A_Combinator_Is_A_SyntaxError()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("THREW SyntaxError/12", Outcome(context, "document.querySelector('div::before p')"));
        Assert.Equal("THREW SyntaxError/12", Outcome(context, "document.querySelector('div::before > p')"));
        Assert.Equal("OK", Outcome(context, "document.querySelector('div::before')"));
    }

    /// <summary>
    /// The cascade still applies a <c>::before</c> rule — the never-matches rule above is a DOM-API
    /// rule and must not reach the renderer, which is what paints generated content.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Pseudo_Element_Rule_Still_Applies_In_The_Cascade()
    {
        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(
            context,
            """
            <!DOCTYPE html><html><head><style>
            #a::before { content: "hi" }
            #a { color: rgb(0, 128, 0) }
            </style></head><body><div id="a">x</div></body></html>
            """,
            "https://example.com/index.html");

        Assert.Equal(
            "rgb(0, 128, 0)",
            context.Eval("getComputedStyle(document.getElementById('a')).color").ToString());
        Assert.Equal(
            "\"hi\"",
            context.Eval("getComputedStyle(document.getElementById('a'), '::before').content").ToString());
    }

    /// <summary>
    /// The CSS cascade does not go through the DOM API, and must not: a stylesheet rule whose
    /// selector does not parse is dropped (CSS error handling), never fatal. So a document carrying
    /// an invalid rule still renders, and its valid rules still apply.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Invalid_Selector_In_A_Stylesheet_Does_Not_Throw_And_Does_Not_Stop_The_Cascade()
    {
        using var context = new JSContext();
        using var bridge = new DomBridge();
        bridge.Attach(
            context,
            """
            <!DOCTYPE html><html><head><style>
            div@bogus { color: red }
            #a { color: rgb(0, 128, 0) }
            </style></head><body><div id="a">x</div></body></html>
            """,
            "https://example.com/index.html");

        Assert.Equal(
            "rgb(0, 128, 0)",
            context.Eval("getComputedStyle(document.getElementById('a')).color").ToString());
    }

    private static string Json(string value) =>
        System.Text.Json.JsonSerializer.Serialize(value);
}
