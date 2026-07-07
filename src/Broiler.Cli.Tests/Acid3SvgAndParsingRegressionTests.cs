namespace Broiler.Cli.Tests;

/// <summary>
/// Phase G: Acid3 Bucket 5 — SVG and Parsing (Tests 65–80) explicit regression tests.
/// Tests 72, 75, 77 already have coverage elsewhere.
/// </summary>
public class Acid3SvgAndParsingRegressionTests
{
    // ────────────────────── Test 65: createElementNS with SVG namespace ──────────────────────

    [Fact]
    public void Acid3_Test65_CreateElementNS_SVG_Namespace()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var svgns = 'http://www.w3.org/2000/svg';
var rect = document.createElementNS(svgns, 'rect');
r.push(rect.namespaceURI === svgns);
r.push(rect.localName === 'rect');
var circle = document.createElementNS(svgns, 'circle');
r.push(circle.namespaceURI === svgns);
r.push(circle.localName === 'circle');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ────────────────────── Test 66: localName property ──────────────────────

    [Fact]
    public void Acid3_Test66_LocalName_Elements_TextNodes_Comments()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var div = document.createElement('DIV');
r.push(div.localName === 'div');
var span = document.createElement('span');
r.push(span.localName === 'span');
var text = document.createTextNode('hello');
r.push(text.localName === null || text.localName === undefined);
var comment = document.createComment('a comment');
r.push(comment.localName === null || comment.localName === undefined);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ────────────────────── Test 67: SVG element getAttribute/setAttribute ──────────────────────

    [Fact]
    public void Acid3_Test67_SVG_Element_GetAttribute_SetAttribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var svgns = 'http://www.w3.org/2000/svg';
var rect = document.createElementNS(svgns, 'rect');
rect.setAttribute('width', '50');
rect.setAttribute('height', '30');
rect.setAttribute('fill', 'red');
r.push(rect.getAttribute('width') === '50');
r.push(rect.getAttribute('height') === '30');
r.push(rect.getAttribute('fill') === 'red');
rect.setAttribute('fill', 'blue');
r.push(rect.getAttribute('fill') === 'blue');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ────────────────────── Test 68: SVG viewBox baseVal has width/height ──────────────────────

    [Fact]
    public void Acid3_Test68_SVG_ViewBox_BaseVal_Width_Height()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var svg = document.createElementNS(svgns, 'svg');
svg.setAttribute('viewBox', '0 0 100 200');
var r = [];
var vb = svg.viewBox;
r.push(vb !== null && vb !== undefined);
r.push(vb.baseVal.width === 100);
r.push(vb.baseVal.height === 200);
r.push(vb.baseVal.x === 0);
r.push(vb.baseVal.y === 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    // ────────────────────── Test 69: getElementById in SVG context ──────────────────────

    [Fact]
    public void Acid3_Test69_GetElementById_In_SVG_Context()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var svg = document.createElementNS(svgns, 'svg');
var rect = document.createElementNS(svgns, 'rect');
rect.setAttribute('id', 'svgrect');
rect.setAttribute('width', '50');
svg.appendChild(rect);
document.body.appendChild(svg);
var r = [];
var found = document.getElementById('svgrect');
r.push(found !== null);
r.push(found === rect);
r.push(found.getAttribute('width') === '50');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── Test 70: SVG rect width is truthy, has baseVal/animVal ──────────────────────

    [Fact]
    public void Acid3_Test70_SVG_Rect_Width_Truthy_BaseVal_AnimVal()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var rect = document.createElementNS(svgns, 'rect');
rect.setAttribute('width', '100');
var r = [];
r.push(!!rect.width);
r.push(!!rect.width.baseVal);
r.push(!!rect.width.animVal);
r.push(rect.width.baseVal.value === 100);
r.push(rect.width.animVal.value === 100);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    // ────────────────────── Test 71: SVG text getNumberOfChars ──────────────────────

    [Fact]
    public void Acid3_Test71_SVG_Text_GetNumberOfChars()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var text = document.createElementNS(svgns, 'text');
text.appendChild(document.createTextNode('Hello'));
var r = [];
r.push(typeof text.getNumberOfChars === 'function');
r.push(text.getNumberOfChars() === 5);
var text2 = document.createElementNS(svgns, 'text');
text2.appendChild(document.createTextNode('ab'));
r.push(text2.getNumberOfChars() === 2);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── Test 73: XML declaration in document.write ──────────────────────

    [Fact]
    public void Acid3_Test73_XML_Declaration_No_Element_Nodes()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
document.body.appendChild(iframe);
var doc = iframe.contentDocument;
doc.open();
doc.write('<?xml version=""1.0""?><!DOCTYPE html><html><body><div id=""inner"">ok</div></body></html>');
doc.close();
var r = [];
// The XML declaration should be ignored, not create a processing instruction node
r.push(doc.body !== null);
var inner = doc.getElementById('inner');
r.push(inner !== null);
r.push(inner.textContent === 'ok');
// First child of document should be DOCTYPE or documentElement, not a PI node
var first = doc.childNodes[0];
r.push(first.nodeType === 10 || first.nodeType === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ────────────────────── Test 74: HTML parser auto-closes elements ──────────────────────

    [Fact]
    public void Acid3_Test74_Parser_AutoCloses_P_Inside_P()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""container""><p>First<p>Second</div>
<div id=""result""></div>
<script>
var r = [];
var container = document.getElementById('container');
// Two <p> elements should be siblings, not nested
var paragraphs = container.getElementsByTagName('p');
r.push(paragraphs.length === 2);
r.push(paragraphs[0].textContent === 'First');
r.push(paragraphs[1].textContent === 'Second');
// They should be direct children of the container
r.push(paragraphs[0].parentNode === container);
r.push(paragraphs[1].parentNode === container);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void Acid3_Test74_Parser_AutoCloses_TD_After_TD()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<table><tr><td>A<td>B<td>C</tr></table>
<div id=""result""></div>
<script>
var r = [];
var row = document.getElementsByTagName('tr')[0];
r.push(row.childNodes.length === 3);
r.push(row.childNodes[0].tagName === 'TD');
r.push(row.childNodes[1].tagName === 'TD');
r.push(row.childNodes[2].tagName === 'TD');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ────────────────────── Test 76: SVG getComputedTextLength ──────────────────────

    [Fact]
    public void Acid3_Test76_SVG_GetComputedTextLength_Numeric()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var text = document.createElementNS(svgns, 'text');
text.appendChild(document.createTextNode('Hello World'));
var r = [];
r.push(typeof text.getComputedTextLength === 'function');
var len = text.getComputedTextLength();
r.push(typeof len === 'number');
r.push(len >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── Test 78: SVGLength type constants ──────────────────────

    [Fact]
    public void Acid3_Test78_SVGLength_Type_Constants()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var rect = document.createElementNS(svgns, 'rect');
rect.setAttribute('width', '100');
var svgLength = rect.width.baseVal;
var r = [];
// SVGLength type constants per SVG spec
r.push(svgLength.SVG_LENGTHTYPE_UNKNOWN === 0);
r.push(svgLength.SVG_LENGTHTYPE_NUMBER === 1);
r.push(svgLength.SVG_LENGTHTYPE_PERCENTAGE === 2);
r.push(svgLength.SVG_LENGTHTYPE_EMS === 3);
r.push(svgLength.SVG_LENGTHTYPE_EXS === 4);
r.push(svgLength.SVG_LENGTHTYPE_PX === 5);
r.push(svgLength.SVG_LENGTHTYPE_CM === 6);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true", result);
    }

    // ────────────────────── Test 79: SVGAnimatedLength baseVal.unitType ──────────────────────

    [Fact]
    public void Acid3_Test79_SVGAnimatedLength_BaseVal_UnitType()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var rect = document.createElementNS(svgns, 'rect');
rect.setAttribute('width', '100');
var r = [];
// unitType 1 = SVG_LENGTHTYPE_NUMBER for a plain numeric value
r.push(rect.width.baseVal.unitType === 1);
r.push(rect.width.baseVal.value === 100);
rect.setAttribute('height', '50');
r.push(rect.height.baseVal.unitType === 1);
r.push(rect.height.baseVal.value === 50);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ────────────────────── Test 80: HTML form elements collection ──────────────────────

    [Fact]
    public void Acid3_Test80_Form_Elements_Collection_Dynamic()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form id=""myform"">
  <input type=""text"" name=""field1"">
  <input type=""text"" name=""field2"">
</form>
<div id=""result""></div>
<script>
var r = [];
var form = document.getElementById('myform');
r.push(form.elements.length === 2);
// Dynamically add an input
var newInput = document.createElement('input');
newInput.setAttribute('type', 'text');
newInput.setAttribute('name', 'field3');
form.appendChild(newInput);
r.push(form.elements.length === 3);
// Remove an input
form.removeChild(form.elements[0]);
r.push(form.elements.length === 2);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }
}
