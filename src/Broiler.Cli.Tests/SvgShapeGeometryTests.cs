namespace Broiler.Cli.Tests;

/// <summary>
/// Client-space geometry for the elements inside an SVG viewport — the shapes' own rects, the
/// <c>viewBox</c> mapping, and the <c>translate()</c> chain above them.
/// </summary>
/// <remarks>
/// <para>
/// An SVG child is not in the CSS box tree, so nothing below the <c>&lt;svg&gt;</c> root had a rect:
/// <c>getBoundingClientRect</c> answered <c>0,0,0,0</c> for every shape, and hit testing — which
/// asks each element for a rect and skips anything empty — could not descend past the root, so
/// <c>document.elementFromPoint</c> over a <c>&lt;rect&gt;</c> returned the <c>&lt;svg&gt;</c>.
/// </para>
/// <para>
/// Every expectation below is Chromium's measured answer to the same markup, taken from one probe
/// run against both. The <c>viewBox</c> cases are the ones worth having measured: the second uses a
/// non-zero <c>min-x</c>/<c>min-y</c> and a viewport whose aspect differs from the box's, which is
/// where a plausible-looking formula and the real one part company.
/// </para>
/// <para>
/// The svg roots are absolutely positioned at the origin so these assert the shape mapping and not
/// the flow position of an inline replaced box.
/// </para>
/// </remarks>
public class SvgShapeGeometryTests
{
    private static string Rects(string setup, string report)
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
    var s = mk('svg', null, attrs);
    s.style.position = 'absolute';
    s.style.left = '0';
    s.style.top = '0';
    document.body.appendChild(s);
    return s;
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

    /// <summary>Shapes that carry their own geometry attributes resolve to the rect a browser
    /// reports.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("mk('rect', 'e', { x: '50', y: '50', width: '60', height: '60' })", "50,50,60,60")]
    [InlineData("mk('circle', 'e', { cx: '60', cy: '70', r: '25' })", "35,45,50,50")]
    [InlineData("mk('ellipse', 'e', { cx: '100', cy: '50', rx: '30', ry: '10' })", "70,40,60,20")]
    [InlineData("mk('line', 'e', { x1: '10', y1: '20', x2: '70', y2: '90' })", "10,20,60,70")]
    [InlineData("mk('polygon', 'e', { points: '10,10 50,30 30,70' })", "10,10,40,60")]
    [InlineData("mk('polyline', 'e', { points: '100 100, 140 120, 120 160' })", "100,100,40,60")]
    [InlineData("mk('image', 'e', { x: '5', y: '205', width: '90', height: '90' })", "5,205,90,90")]
    [InlineData("mk('foreignObject', 'e', { x: '210', y: '110', width: '80', height: '80' })", "210,110,80,80")]
    public void AShapeReportsItsOwnGeometry(string shape, string expected)
        => Assert.Contains($">{expected}<", Rects(
            $"var s = host({{ width: '300', height: '300' }}); var e = {shape}; s.appendChild(e);",
            "rect(e)"));

    /// <summary>A geometry attribute may be a percentage, and resolves against the viewport.</summary>
    [Fact(Timeout = 600000)]
    public void APercentageResolvesAgainstTheViewport()
        => Assert.Contains(">20,50,100,20<", Rects(
            "var s = host({ width: '200', height: '200' });"
            + "var e = mk('rect', 'e', { x: '10%', y: '25%', width: '50%', height: '10%' });"
            + "s.appendChild(e);",
            "rect(e)"));

    /// <summary>
    /// A <c>viewBox</c> scales and centres user space. The viewport here is twice as wide as the
    /// box, so the uniform <c>meet</c> scale is 1 and the slack goes half to each side — the rect
    /// lands 50px right of where a naive one-to-one mapping would put it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AViewBoxScalesAndCentresUserSpace()
        => Assert.Contains(">60,10,20,20<", Rects(
            "var s = host({ width: '200', height: '100', viewBox: '0 0 100 100' });"
            + "var e = mk('rect', 'e', { x: '10', y: '10', width: '20', height: '20' });"
            + "s.appendChild(e);",
            "rect(e)"));

    /// <summary>
    /// The same with a non-zero <c>min-x</c>/<c>min-y</c> and a taller-than-wide viewport: scale 2
    /// from the width, the height's slack centred, and the origin shifted by the box's own corner.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AViewBoxOriginAndNonUniformViewportMapTogether()
        => Assert.Contains(">0,50,20,20<", Rects(
            "var s = host({ width: '100', height: '200', viewBox: '20 30 50 50' });"
            + "var e = mk('rect', 'e', { x: '20', y: '30', width: '10', height: '10' });"
            + "s.appendChild(e);",
            "rect(e)"));

    /// <summary>An ancestor <c>translate()</c> chain accumulates in user space.</summary>
    [Fact(Timeout = 600000)]
    public void AnAncestorTranslateChainAccumulates()
        => Assert.Contains(">210,210,80,80<", Rects(
            "var s = host({ width: '300', height: '300' });"
            + "var outer = mk('g', 'outer', { transform: 'translate(200, 200)' });"
            + "var inner = mk('g', 'inner', { transform: 'translate(5, 5)' });"
            + "var e = mk('rect', 'e', { x: '5', y: '5', width: '80', height: '80' });"
            + "inner.appendChild(e); outer.appendChild(inner); s.appendChild(outer);",
            "rect(e)"));

    /// <summary>A group's rect is the union of its children's, which is what the shapes having
    /// rects at all makes possible.</summary>
    [Fact(Timeout = 600000)]
    public void AGroupUnionsItsChildren()
        => Assert.Contains(">10,10,90,90<", Rects(
            "var s = host({ width: '300', height: '300' });"
            + "var g = mk('g', 'g', {});"
            + "g.appendChild(mk('rect', 'a', { x: '10', y: '10', width: '40', height: '40' }));"
            + "g.appendChild(mk('rect', 'b', { x: '60', y: '60', width: '40', height: '40' }));"
            + "s.appendChild(g);",
            "rect(g)"));

    /// <summary>
    /// Hit testing descends into the SVG and reports the topmost shape first, then its ancestors —
    /// the answer the whole of this was for.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void HitTestingDescendsIntoTheSvg()
        => Assert.Contains(">b,a,g,svg<", Rects(
            "var s = host({ width: '300', height: '300' }); s.id = 'svg';"
            + "var g = mk('g', 'g', {});"
            + "g.appendChild(mk('rect', 'a', { x: '10', y: '10', width: '90', height: '90' }));"
            + "g.appendChild(mk('rect', 'b', { x: '20', y: '20', width: '70', height: '70' }));"
            + "s.appendChild(g);",
            "document.elementsFromPoint(50, 50).slice(0, 4)"
            + ".map(function (n) { return n.id || n.tagName; }).join(',')"));

    /// <summary>
    /// A shape whose bounds this does not model reports no rect rather than a wrong one, and so
    /// behaves exactly as every shape did before. <c>path</c> needs the curve and <c>use</c> needs
    /// its referent; both are named in the module's documentation as gaps rather than left to be
    /// discovered as a confidently misplaced box.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("mk('path', 'e', { d: 'M10 10 H 90 V 90 H 10 Z' })")]
    [InlineData("mk('use', 'e', { x: '10', y: '10' })")]
    public void AnUnmodelledShapeReportsNoRectRatherThanAWrongOne(string shape)
        => Assert.Contains(">0,0,0,0<", Rects(
            $"var s = host({{ width: '300', height: '300' }}); var e = {shape}; s.appendChild(e);",
            "rect(e)"));

    /// <summary>
    /// A transform list this does not model leaves the subtree untranslated rather than applying
    /// some functions and dropping others, which would place a shape confidently and wrongly.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnUnmodelledTransformLeavesTheSubtreeUntranslated()
        => Assert.Contains(">5,5,80,80<", Rects(
            "var s = host({ width: '300', height: '300' });"
            + "var g = mk('g', 'g', { transform: 'rotate(45) translate(200, 200)' });"
            + "var e = mk('rect', 'e', { x: '5', y: '5', width: '80', height: '80' });"
            + "g.appendChild(e); s.appendChild(g);",
            "rect(e)"));
}
