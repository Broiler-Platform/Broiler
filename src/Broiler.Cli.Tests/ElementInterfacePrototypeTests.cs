namespace Broiler.Cli.Tests;

/// <summary>
/// <c>Element</c>'s interface on <c>Element.prototype</c>, found through the receiver, rather than
/// copied onto every element wrapper.
/// </summary>
/// <remarks>
/// <para>
/// An element carried 140 own properties where a browser gives it none, and
/// <c>Element.prototype.getAttribute</c> was <see langword="undefined"/>. This moves the 63 that Web
/// IDL's <c>Element</c> — and the mixins it includes — actually declare, which takes the wrapper to
/// 77. What is left there is <c>HTMLElement</c>'s, <c>Node</c>'s, and the handful of members no
/// browser puts on <c>Element.prototype</c> at all.
/// </para>
/// <para>
/// The assertions are the three halves of moving a member: it answers from the prototype rather than
/// from the instance, the operation still behaves now that it reads its element from the receiver,
/// and a receiver that is not an element is the <c>TypeError</c> a browser raises rather than a
/// crash or a silent wrong answer.
/// </para>
/// </remarks>
public class ElementInterfacePrototypeTests
{
    private static string Run(string script)
    {
        var html = $@"<!DOCTYPE html><html><body>
<div id=""d"" class=""a b""><span id=""s"">hi</span></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var s = document.getElementById('s');
document.getElementById('result').textContent = String({script});
</script></body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");
    }

    /// <summary>The member is inherited, not owned — which is the whole change.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("tagName")]
    [InlineData("id")]
    [InlineData("className")]
    [InlineData("classList")]
    [InlineData("attributes")]
    [InlineData("getAttribute")]
    [InlineData("setAttribute")]
    [InlineData("hasAttribute")]
    [InlineData("removeAttributeNode")]
    [InlineData("innerHTML")]
    [InlineData("outerHTML")]
    [InlineData("insertAdjacentHTML")]
    [InlineData("children")]
    [InlineData("firstElementChild")]
    [InlineData("nextElementSibling")]
    [InlineData("append")]
    [InlineData("remove")]
    [InlineData("replaceWith")]
    [InlineData("querySelector")]
    [InlineData("matches")]
    [InlineData("closest")]
    [InlineData("getElementsByClassName")]
    [InlineData("clientWidth")]
    [InlineData("scrollTop")]
    [InlineData("getBoundingClientRect")]
    [InlineData("scrollIntoView")]
    [InlineData("attachShadow")]
    [InlineData("shadowRoot")]
    [InlineData("requestFullscreen")]
    [InlineData("animate")]
    public void TheMemberIsInheritedFromElementPrototype(string member)
        => Assert.Contains(">false|true<", Run(
            $"[Object.prototype.hasOwnProperty.call(d, '{member}'),"
            + $" Object.prototype.hasOwnProperty.call(Element.prototype, '{member}')].join('|')"));

    /// <summary>
    /// One function per member, shared by every element — which is what makes the defensive idiom
    /// work. Borrowing <c>Element.prototype.matches</c> is what a library does when it cannot trust
    /// the instance's own, and it had nothing to borrow.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheOneFunctionIsShared()
        => Assert.Contains(">true|true|true|d|true<", Run(
            "[d.matches === Element.prototype.matches, d.getAttribute === s.getAttribute,"
            + " Element.prototype.matches.call(d, 'div'),"
            + " Element.prototype.getAttribute.call(d, 'id'),"
            + " Element.prototype.querySelector.call(d, 'span') === s].join('|')"));

    /// <summary>
    /// A receiver that is not an element is an illegal invocation, as it is in a browser — including
    /// the document and a text node, which are nodes but do not implement <c>Element</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ANonElementReceiverIsAnIllegalInvocation()
        => Assert.Contains(">TypeError|TypeError|TypeError<", Run(
            "['{}', 'document', 'text'].map(function (kind) {"
            + "  var receiver = kind === '{}' ? {} : kind === 'document' ? document : document.createTextNode('x');"
            + "  try { Element.prototype.getAttribute.call(receiver, 'id'); return 'no throw'; }"
            + "  catch (e) { return e.constructor.name; }"
            + "}).join('|')"));

    /// <summary>
    /// <c>tagName</c> was a <c>JSString</c> fixed when the wrapper was built — the per-instance value
    /// the roadmap named as the thing that had to become an accessor before it could move. It is one
    /// now, and answers the same upper-cased name.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TagNameIsAnAccessorAndStillAnswers()
        => Assert.Contains(">DIV|SPAN|true|true<", Run(
            "[d.tagName, s.tagName,"
            + " typeof Object.getOwnPropertyDescriptor(Element.prototype, 'tagName').get === 'function',"
            + " Object.getOwnPropertyDescriptor(Element.prototype, 'tagName').set === undefined].join('|')"));

    /// <summary>
    /// <c>classList</c> was a value built with the wrapper, so its identity came for free. As a
    /// prototype accessor it is memoized per element instead, and <c>el.classList === el.classList</c>
    /// still holds — as does its write reaching the <c>class</c> attribute.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ClassListKeepsItsIdentityAndStillWrites()
        => Assert.Contains(">true|false|true|a b c|a b c<", Run(
            "(function () { var before = d.classList; d.classList.add('c');"
            + "return [before === d.classList, d.classList === s.classList,"
            + " d.classList.contains('a'), d.className, d.getAttribute('class')].join('|'); })()"));

    /// <summary>The live <c>NamedNodeMap</c> keeps its identity across the move too.</summary>
    [Fact(Timeout = 600000)]
    public void TheAttributeMapKeepsItsIdentity()
        => Assert.Contains(">true|2|3|value<", Run(
            "(function () { var map = d.attributes; var count = map.length;"
            + "d.setAttribute('data-x', 'value');"
            + "return [map === d.attributes, count, d.attributes.length, d.getAttribute('data-x')].join('|'); })()"));

    /// <summary>The attribute operations still behave, reading their element from the receiver.</summary>
    [Fact(Timeout = 600000)]
    public void TheAttributeOperationsStillBehave()
        => Assert.Contains(">true|v|true|false|true|id,class|true|urn:x<", Run(
            "(function () { d.setAttribute('t', 'v');"
            + "var had = d.hasAttribute('t'); var read = d.getAttribute('t');"
            + "var toggledOn = d.toggleAttribute('flag'); d.removeAttribute('t'); d.removeAttribute('flag');"
            + "d.setAttributeNS('urn:x', 'p:q', 'z');"
            + "return [had, read, toggledOn, d.hasAttribute('t'), d.hasAttributes(),"
            + " d.getAttributeNames().filter(function (n) { return n === 'id' || n === 'class'; }).join(','),"
            + " d.getAttributeNode('id').value === 'd',"
            + " d.getAttributeNodeNS('urn:x', 'q').namespaceURI].join('|'); })()"));

    /// <summary>And so do the tree and selector members.</summary>
    [Fact(Timeout = 600000)]
    public void TheTreeAndSelectorMembersStillBehave()
        => Assert.Contains(">1|s|s|true|DIV|true|1|0<", Run(
            "[d.children.length, d.firstElementChild.id, d.lastElementChild.id,"
            + " s.nextElementSibling === null, s.closest('div').tagName, s.matches('#s'),"
            + " d.getElementsByTagName('span').length, d.getElementsByClassName('a').length].join('|')"));

    /// <summary>
    /// <c>replaceChildren</c> is the <c>ParentNode</c> member the wrapper never had. The document's
    /// counterpart has been here since that mixin was bound there; an element's — the commoner one,
    /// since it is how a page empties a node — threw on an undefined function.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ReplaceChildrenIsThereNow()
        => Assert.Contains(">function|0|2|P|tail<", Run(
            "(function () { var kind = typeof d.replaceChildren; d.replaceChildren();"
            + "var emptied = d.childNodes.length;"
            + "d.replaceChildren(document.createElement('p'), 'tail');"
            + "return [kind, emptied, d.childNodes.length,"
            + " d.firstElementChild.tagName, d.lastChild.nodeValue].join('|'); })()"));

    /// <summary>
    /// A <c>&lt;form&gt;</c>'s wrapper resolves an unknown name to the control carrying it, so an
    /// inherited member has to be found before that fallback runs — otherwise a control named
    /// <c>getAttribute</c> would answer for the real one.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AFormsNamedGetterDoesNotShadowAnInheritedElementMember()
        => Assert.Contains(">f|INPUT|1<", Run(
            "(function () { var f = document.createElement('form'); f.id = 'f';"
            + "var a = document.createElement('input'); a.name = 'getAttribute';"
            + "var b = document.createElement('input'); b.name = 'zz';"
            + "f.appendChild(a); f.appendChild(b); document.body.appendChild(f);"
            + "return [f.getAttribute('id'), f.zz.tagName, f.querySelectorAll('[name=zz]').length].join('|'); })()"));

    /// <summary>
    /// An SVG element inherits the same prototype. <c>SVGElement</c> derives from <c>Element</c>, so
    /// the members reach it without being installed anywhere else — the point of moving them.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnSvgElementInheritsTheSameMembers()
        => Assert.Contains(">true|5|true|true<", Run(
            "(function () { var svg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');"
            + "svg.setAttribute('x', '5');"
            + "return [Object.prototype.hasOwnProperty.call(svg, 'getAttribute') === false, svg.getAttribute('x'),"
            + " svg.getAttribute === Element.prototype.getAttribute,"
            + " Element.prototype.matches.call(svg, 'rect')].join('|'); })()"));

    /// <summary>
    /// The polyfill idiom reaches instances now: assigning to <c>Element.prototype</c> used to write
    /// to an object every element shadowed with its own copy of the same name.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ExtendingElementPrototypeReachesAnInstance()
        => Assert.Contains(">patched:DIV|patched:SPAN<", Run(
            "(function () { var original = Element.prototype.matches;"
            + "Element.prototype.matches = function () { return 'patched:' + this.tagName; };"
            + "var answers = [d.matches('div'), s.matches('span')];"
            + "Element.prototype.matches = original; return answers.join('|'); })()"));

    /// <summary>
    /// <c>removeAttributeNodeNS</c> deliberately stays the wrapper's own. DOM §4.9 pairs
    /// <c>setAttributeNode</c> with <c>setAttributeNodeNS</c> but gives <c>removeAttributeNode</c> no
    /// namespace-qualified sibling, so no browser has one and putting it on the prototype would give
    /// that prototype a member a browser's has not got.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheOneAttributeMemberNoBrowserHasStaysTheInstances()
        => Assert.Contains(">true|false<", Run(
            "[Object.prototype.hasOwnProperty.call(d, 'removeAttributeNodeNS'),"
            + " 'removeAttributeNodeNS' in Element.prototype"
            + "   && !Object.prototype.hasOwnProperty.call(d, 'removeAttributeNodeNS')].join('|')"));

    /// <summary>
    /// The members that are <em>not</em> <c>Element</c>'s stay where they were: <c>textContent</c> is
    /// <c>Node</c>'s and deliberately the element's own, the <c>HTMLElement</c> half is still on the
    /// instance, and so are the <c>Node</c> tree mutations.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("textContent")]
    [InlineData("style")]
    [InlineData("dataset")]
    [InlineData("title")]
    [InlineData("offsetWidth")]
    [InlineData("onclick")]
    [InlineData("appendChild")]
    public void WhatIsNotElementsIsNotOnItsPrototype(string member)
        => Assert.Contains(">true|false<", Run(
            $"[Object.prototype.hasOwnProperty.call(d, '{member}'),"
            + $" Object.prototype.hasOwnProperty.call(Element.prototype, '{member}')].join('|')"));
}
