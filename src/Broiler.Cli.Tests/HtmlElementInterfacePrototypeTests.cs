namespace Broiler.Cli.Tests;

/// <summary>
/// <c>HTMLElement</c>'s interface on <c>HTMLElement.prototype</c>, found through the receiver, rather
/// than copied onto every element wrapper.
/// </summary>
/// <remarks>
/// <para>
/// The sequel to <c>ElementInterfacePrototypeTests</c> and the same mechanism: the members HTML gives
/// every HTML element — the global reflectors, <c>style</c>, <c>dataset</c>, the text pair,
/// <c>click</c>/<c>focus</c>/<c>blur</c>, <c>attachInternals</c>, the seventeen <c>on*</c> handlers
/// and the <c>offset*</c> metrics — move to the prototype, taking an element from 77 own properties
/// to 40. <c>HTMLElement.prototype</c> owned nothing but its <c>constructor</c>.
/// </para>
/// <para>
/// Two of them are per-instance objects rather than plain relocations, and get the treatment
/// <c>classList</c> got: <c>style</c> was a declaration built with the wrapper and captured by the
/// accessor, <c>dataset</c> a self-replacing accessor that wrote its map back onto the wrapper it
/// closed over. Identity has to survive both.
/// </para>
/// </remarks>
public class HtmlElementInterfacePrototypeTests
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
    [InlineData("title")]
    [InlineData("lang")]
    [InlineData("accessKey")]
    [InlineData("dir")]
    [InlineData("draggable")]
    [InlineData("innerText")]
    [InlineData("outerText")]
    [InlineData("hidden")]
    [InlineData("tabIndex")]
    [InlineData("style")]
    [InlineData("dataset")]
    [InlineData("click")]
    [InlineData("focus")]
    [InlineData("blur")]
    [InlineData("attachInternals")]
    [InlineData("onclick")]
    [InlineData("onscrollend")]
    [InlineData("offsetWidth")]
    [InlineData("offsetParent")]
    public void TheMemberIsInheritedFromHtmlElementPrototype(string member)
        => Assert.Contains(">false|true<", Run(
            $"[Object.prototype.hasOwnProperty.call(d, '{member}'),"
            + $" Object.prototype.hasOwnProperty.call(HTMLElement.prototype, '{member}')].join('|')"));

    /// <summary>One function per member, shared by every element, and borrowable.</summary>
    [Fact(Timeout = 600000)]
    public void TheOneFunctionIsShared()
        => Assert.Contains(">true|true|true<", Run(
            "[d.click === HTMLElement.prototype.click, d.focus === s.focus,"
            + " Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'style').get"
            + "   === Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'style').get].join('|')"));

    /// <summary>
    /// <c>style</c> was a declaration built with the wrapper, so its identity came for free. As a
    /// prototype accessor it is memoized per element, and everything it owes still happens: the write
    /// reaches the <c>style</c> content attribute, and assigning a string sets <c>cssText</c> rather
    /// than replacing the object.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheInlineStyleKeepsItsIdentityAndStillWritesThrough()
        => Assert.Contains(">true|false|red|color: red|true|blue|color: blue<", Run(
            "(function () { var before = d.style; d.style.color = 'red';"
            + "var first = [before === d.style, d.style === s.style, d.style.color, d.getAttribute('style')];"
            + "d.style = 'color: blue';"
            + "return first.concat([before === d.style, d.style.getPropertyValue('color'),"
            + " d.getAttribute('style')]).join('|'); })()"));

    /// <summary>
    /// <c>dataset</c> was an accessor that replaced itself with a value on the wrapper it closed
    /// over — which both memoized the map and left an own property behind. The weak cache keeps the
    /// identity without the property.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheDatasetKeepsItsIdentityAndStaysLive()
        => Assert.Contains(">true|false|bar|bar|foo|false<", Run(
            "(function () { var before = d.dataset; d.dataset.foo = 'bar';"
            + "return [before === d.dataset, d.dataset === s.dataset, d.dataset.foo,"
            + " d.getAttribute('data-foo'), Object.keys(d.dataset).join(','),"
            + " Object.prototype.hasOwnProperty.call(d, 'dataset')].join('|'); })()"));

    /// <summary>The reflectors still reflect, reading their element from the receiver.</summary>
    [Fact(Timeout = 600000)]
    public void TheGlobalReflectorsStillBehave()
        => Assert.Contains(">t|fr|k|rtl|true|true|5|hi<", Run(
            "(function () { d.title = 't'; d.lang = 'fr'; d.accessKey = 'k'; d.dir = 'rtl';"
            + "d.draggable = true; d.hidden = true; d.tabIndex = 5;"
            + "return [d.getAttribute('title'), d.getAttribute('lang'), d.getAttribute('accesskey'),"
            + " d.getAttribute('dir'), d.draggable, d.hidden, d.tabIndex, d.innerText].join('|'); })()"));

    /// <summary>
    /// Each <c>on*</c> reflector answers for its own event. Installed in a loop, so the one way to get
    /// this wrong is to let every handler close over the last name in it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void EachInlineHandlerReflectsItsOwnEvent()
        => Assert.Contains(">clicked|true|null|true<", Run(
            "(function () { var seen = 'no'; d.onclick = function () { seen = 'clicked'; };"
            + "d.onscrollend = function () { seen = 'wrong'; };"
            + "d.click();"
            + "return [seen, typeof d.onscrollend === 'function',"
            + " String(s.onclick), d.onclick !== d.onscrollend].join('|'); })()"));

    /// <summary>And the three operations still dispatch.</summary>
    [Fact(Timeout = 600000)]
    public void ClickFocusAndBlurStillDispatch()
        => Assert.Contains(">click,focus,blur<", Run(
            "(function () { var got = [];"
            + "['click', 'focus', 'blur'].forEach(function (name) {"
            + "  d.addEventListener(name, function () { got.push(name); });"
            + "});"
            + "d.click(); d.focus(); d.blur(); return got.join(','); })()"));

    /// <summary>The box metrics answer from the prototype for the receiver's element.</summary>
    [Fact(Timeout = 600000)]
    public void TheOffsetMetricsStillAnswer()
        => Assert.Contains(">number|number|number|number|true<", Run(
            "[typeof d.offsetWidth, typeof d.offsetHeight, typeof d.offsetTop, typeof d.offsetLeft,"
            + " d.offsetParent === null || d.offsetParent.nodeType === 1].join('|')"));

    /// <summary>A receiver that is not an element is the illegal invocation a browser raises.</summary>
    [Fact(Timeout = 600000)]
    public void ANonElementReceiverIsAnIllegalInvocation()
        => Assert.Contains(">TypeError|TypeError<", Run(
            "['{}', 'document'].map(function (kind) {"
            + "  var receiver = kind === '{}' ? {} : document;"
            + "  try { HTMLElement.prototype.click.call(receiver); return 'no throw'; }"
            + "  catch (e) { return e.constructor.name; }"
            + "}).join('|')"));

    /// <summary>
    /// <b>An SVG element keeps its own copies, deliberately.</b> <c>SVGElement</c> derives straight
    /// from <c>Element</c>, so it inherits none of these; installing them on itself is what preserves
    /// the surface it has today. That surface is not a browser's — an <c>SVGElement</c> shares only
    /// <c>style</c>, <c>dataset</c>, <c>tabIndex</c>, <c>focus</c>/<c>blur</c> and the <c>on*</c>
    /// handlers, and has no <c>title</c> or <c>offsetWidth</c> — and narrowing it is the per-tag SVG
    /// interface decision this track holds open. Pinned so it stays a decision.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnSvgElementStillCarriesTheseItself()
        => Assert.Contains(">true|true|red|true|true<", Run(
            "(function () { var r = document.createElementNS('http://www.w3.org/2000/svg', 'rect');"
            + "r.style.fill = 'red';"
            + "return [Object.prototype.hasOwnProperty.call(r, 'style'),"
            + " Object.prototype.hasOwnProperty.call(r, 'onclick'), r.style.fill,"
            + " r.style === r.style,"
            + " Object.prototype.hasOwnProperty.call(r, 'getAttribute') === false].join('|'); })()"));

    /// <summary>
    /// What is still each wrapper's own after this: <c>textContent</c> (<c>Node</c>'s, and
    /// deliberately shadowed here), the <c>Node</c> tree mutations, the per-control reflectors this
    /// bridge installs on every element where a browser gives them to the interfaces that declare
    /// them, and the bridge's own <c>scrollParent</c>.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("textContent")]
    [InlineData("appendChild")]
    [InlineData("insertBefore")]
    [InlineData("value")]
    [InlineData("checked")]
    [InlineData("type")]
    [InlineData("disabled")]
    [InlineData("checkValidity")]
    [InlineData("scrollParent")]
    public void WhatIsNotHtmlElementsStaysTheWrappersOwn(string member)
        => Assert.Contains(">true|false<", Run(
            $"[Object.prototype.hasOwnProperty.call(d, '{member}'),"
            + $" Object.prototype.hasOwnProperty.call(HTMLElement.prototype, '{member}')].join('|')"));
}
