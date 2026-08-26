namespace Broiler.Cli.Tests;

/// <summary>
/// The HTML subtree inside an <c>&lt;svg&gt;</c>'s <c>&lt;foreignObject&gt;</c> — the one place an
/// SVG subtree re-enters CSS layout.
/// </summary>
/// <remarks>
/// <para>
/// An SVG subtree is not laid out by CSS box rules here, so the style pass hid every box under the
/// viewport — <c>&lt;foreignObject&gt;</c> among them. Its content therefore had no box at all: a
/// <c>&lt;div&gt;</c> inside one reported <c>0,0,0,0</c> and an <c>offsetWidth</c>/<c>offsetHeight</c>
/// of <c>0</c>, and <c>elementFromPoint</c> over the child answered the <c>&lt;foreignObject&gt;</c>.
/// The element itself always had a rect — it resolves from its own geometry attributes, like every
/// other shape — which is why the gap was in the subtree rather than in the element.
/// </para>
/// <para>
/// Every expectation below is Chromium's measured answer to the same markup, taken from one probe
/// run against both. The svg roots are absolutely positioned at the origin so these assert the
/// content's placement inside the viewport and not the flow position of an inline replaced box.
/// </para>
/// </remarks>
public class SvgForeignObjectContentTests
{
    private static string Report(string setup, string report)
    {
        var html = $@"<!doctype html>
<html><head><title>Test</title></head>
<body style=""margin:0"">
<div id=""result""></div>
<script>
var ns = 'http://www.w3.org/2000/svg';
function mk(tag, id, attrs) {{
    var e = document.createElementNS(ns, tag);
    if (id) e.id = id;
    for (var k in attrs) e.setAttribute(k, attrs[k]);
    return e;
}}
function host(attrs) {{
    var s = mk('svg', 'svgRoot', attrs);
    s.style.position = 'absolute';
    s.style.left = '0';
    s.style.top = '0';
    document.body.appendChild(s);
    return s;
}}
function box(id, w, h) {{
    var d = document.createElement('div');
    if (id) d.id = id;
    if (w) d.style.width = w;
    if (h) d.style.height = h;
    return d;
}}
function rect(el) {{
    var b = el.getBoundingClientRect();
    return [b.left, b.top, b.width, b.height].join(',');
}}
{setup}
document.getElementById('result').textContent = {report};
</script>
</body>
</html>";
        return CaptureService.ExecuteScriptsWithDom(html, "https://example.com");
    }

    /// <summary>
    /// The content is laid out against the <c>&lt;foreignObject&gt;</c>'s viewport rect: a
    /// <c>&lt;div&gt;</c> sized 100×40 inside one at <c>(20,30) 150×90</c> stands at the element's
    /// own corner, with its own size, and reports it through both the rect and the offset metrics.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ContentIsPlacedAtTheForeignObjectsCorner()
        => Assert.Contains(">20,30,100,40|100x40<", Report(
            "var s = host({ width: '300', height: '300' });"
            + "var fo = mk('foreignObject', 'fo', { x: '20', y: '30', width: '150', height: '90' });"
            + "var d = box('d', '100px', '40px');"
            + "fo.appendChild(d); s.appendChild(fo);",
            "rect(d) + '|' + d.offsetWidth + 'x' + d.offsetHeight"));

    /// <summary>
    /// Inside the box the ordinary rules apply with no special case: two block children fill the
    /// element's width and stack, the second clearing the first.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ContentLaysOutByTheOrdinaryRules()
        => Assert.Contains(">10,10,200,30|10,40,200,25<", Report(
            "var s = host({ width: '300', height: '300' });"
            + "var fo = mk('foreignObject', 'fo', { x: '10', y: '10', width: '200', height: '200' });"
            + "var a = box('a', null, '30px'); var b = box('b', null, '25px');"
            + "fo.appendChild(a); fo.appendChild(b); s.appendChild(fo);",
            "rect(a) + '|' + rect(b)"));

    /// <summary>
    /// A <c>&lt;foreignObject&gt;</c> reached through a translated <c>&lt;g&gt;</c> chain is placed
    /// at the accumulated offset, and its content with it — the element's own rect and its subtree's
    /// agree about which offsets counted, because both read the same translate rule.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnAncestorTranslateChainCarriesTheContent()
        => Assert.Contains(">115,75,80,60|115,75,40,20<", Report(
            "var s = host({ width: '300', height: '300' });"
            + "var outer = mk('g', null, { transform: 'translate(100, 50)' });"
            + "var inner = mk('g', null, { transform: 'translate(5, 5)' });"
            + "var fo = mk('foreignObject', 'fo', { x: '10', y: '20', width: '80', height: '60' });"
            + "var d = box('d', '40px', '20px');"
            + "fo.appendChild(d); inner.appendChild(fo); outer.appendChild(inner); s.appendChild(outer);",
            "rect(fo) + '|' + rect(d)"));

    /// <summary>
    /// Hit testing descends into the content, which is what having a box at all buys: the point over
    /// the <c>&lt;div&gt;</c> answers the <c>&lt;div&gt;</c> first and the
    /// <c>&lt;foreignObject&gt;</c> behind it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void HitTestingDescendsIntoTheContent()
        => Assert.Contains(">foDiv,fo,svgRoot<", Report(
            "var s = host({ width: '300', height: '300' });"
            + "var fo = mk('foreignObject', 'fo', { x: '210', y: '110', width: '80', height: '80' });"
            + "var d = box('foDiv', '80px', '80px');"
            + "fo.appendChild(d); s.appendChild(fo);",
            "document.elementsFromPoint(250, 150).slice(0, 3)"
            + ".map(function (n) { return n.id || n.tagName; }).join(',')"));

    /// <summary>
    /// A geometry attribute may be a percentage, and resolves against the viewport — the element's
    /// place and size, and so its content's, follow the viewport rather than a fixed number.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void APercentageGeometryResolvesAgainstTheViewport()
        => Assert.Contains(">20,50,100,20|20,50,100,20<", Report(
            "var s = host({ width: '200', height: '200' });"
            + "var fo = mk('foreignObject', 'fo', { x: '10%', y: '25%', width: '50%', height: '10%' });"
            + "var d = box('d', '100%', '100%');"
            + "fo.appendChild(d); s.appendChild(fo);",
            "rect(fo) + '|' + rect(d)"));

    /// <summary>
    /// Placing a <c>&lt;foreignObject&gt;</c> leaves the rest of the SVG subtree exactly as it was:
    /// a shape sibling still reports its own geometry, and the viewport's own box is unmoved.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheRestOfTheSubtreeIsUnchanged()
        => Assert.Contains(">0,0,300,300|50,50,60,60<", Report(
            "var s = host({ width: '300', height: '300' });"
            + "var r = mk('rect', 'r', { x: '50', y: '50', width: '60', height: '60' });"
            + "var fo = mk('foreignObject', 'fo', { x: '10', y: '10', width: '80', height: '80' });"
            + "fo.appendChild(box('d', '80px', '80px'));"
            + "s.appendChild(r); s.appendChild(fo);",
            "rect(s) + '|' + rect(r)"));

    /// <summary>
    /// The bounded gap, pinned so that closing it is a deliberate change rather than a drift. A
    /// <c>viewBox</c> maps user space by a scale the style pass cannot know — it is a function of
    /// the viewport's used size — so under one the content keeps no box, while the element itself
    /// still reports the rect its attributes resolve to. Chromium answers
    /// <c>60,10,40,40</c> for the element and <c>60,10,20,20</c> for the <c>&lt;div&gt;</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void UnderAViewBoxTheContentKeepsNoBox()
        => Assert.Contains(">60,10,40,40|0,0,0,0<", Report(
            "var s = host({ width: '200', height: '100', viewBox: '0 0 100 100' });"
            + "var fo = mk('foreignObject', 'fo', { x: '10', y: '10', width: '40', height: '40' });"
            + "fo.appendChild(box('d', '20px', '20px'));"
            + "s.appendChild(fo);",
            "rect(fo) + '|' + rect(document.getElementById('d'))"));
}
