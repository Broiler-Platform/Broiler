namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 6 Acid3 compliance: SVG DOM and Cross-Document APIs —
/// localName property, contentDocument for iframe/object, getSVGDocument(),
/// document.open/write/close, DOCTYPE nodes, document.styleSheets/images/links,
/// SVG element interfaces (SVGAnimatedLength, getNumberOfChars), and
/// nested event dispatch on sub-documents.
/// </summary>
public class SvgDomAndCrossDocTests
{
    // ────────────────────── Test 66: localName property ──────────────────────

    [Fact]
    public void LocalName_TextNode_Returns_Null()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
r.push(document.createTextNode('test').localName === null);
r.push(document.createComment('test').localName === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void LocalName_Element_Returns_Lowercase_Tag()
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
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void LocalName_CreateElementNS_Returns_LocalName()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var rect = document.createElementNS(svgns, 'rect');
var r = [];
r.push(rect.localName === 'rect');
r.push(rect.namespaceURI === svgns);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── Test 68: UTF-16 surrogate pairs ──────────────────────

    [Fact]
    public void Surrogate_Pairs_In_Input_Value_Handled()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
try {
    var unpaired = String.fromCharCode(0xd863);
    var before = unpaired + 'text';
    var elt = document.createElement('input');
    elt.value = before;
    var after = elt.value;
    // Any of these outcomes is acceptable
    if (after == before && before.length == 5) r.push('kept');
    else if (after == 'text') r.push('removed');
    else if (after == String.fromCharCode(0xfffd) + 'text') r.push('replaced');
    else r.push('unknown');
} catch(ex) {
    r.push('exception');  // Also acceptable
}
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // Any of kept, removed, replaced, exception is valid
        Assert.True(
            result.Contains("kept") || result.Contains("removed") ||
            result.Contains("replaced") || result.Contains("exception"),
            $"Unexpected surrogate handling: {result}");
    }

    // ────────────────────── Iframe contentDocument ──────────────────────

    [Fact]
    public void Iframe_ContentDocument_Is_Not_Null()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var r = [];
r.push(iframe.contentDocument !== null);
r.push(iframe.contentDocument !== undefined);
r.push(iframe.contentDocument.nodeType === 9);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Iframe_ContentDocument_Has_DocumentElement()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
var r = [];
r.push(doc.documentElement !== null);
r.push(doc.documentElement.tagName === 'HTML');
r.push(doc.body !== null);
r.push(doc.head !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void Iframe_ContentDocument_Has_DOM_Methods()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
var r = [];
r.push(typeof doc.createElement === 'function');
r.push(typeof doc.createTextNode === 'function');
r.push(typeof doc.getElementById === 'function');
r.push(typeof doc.getElementsByTagName === 'function');
r.push(typeof doc.querySelector === 'function');
r.push(typeof doc.querySelectorAll === 'function');
r.push(typeof doc.createElementNS === 'function');
r.push(typeof doc.createComment === 'function');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void Iframe_ContentDocument_Same_Reference()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var r = [];
r.push(iframe.contentDocument === iframe.contentDocument);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    // ────────────────────── Object element contentDocument ──────────────────────

    [Fact]
    public void Object_ContentDocument_Is_Not_Null()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var obj = document.createElement('object');
var r = [];
r.push(obj.contentDocument !== null);
r.push(obj.contentDocument !== undefined);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── Test 74: getSVGDocument() ──────────────────────

    [Fact]
    public void Iframe_GetSVGDocument_Returns_ContentDocument()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var r = [];
r.push(typeof iframe.getSVGDocument === 'function');
r.push(iframe.getSVGDocument() !== null);
r.push(iframe.getSVGDocument() === iframe.contentDocument);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Object_GetSVGDocument_Returns_ContentDocument()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var obj = document.createElement('object');
var r = [];
r.push(typeof obj.getSVGDocument === 'function');
r.push(obj.getSVGDocument() !== null);
r.push(obj.getSVGDocument() === obj.contentDocument);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── Test 71: document.open/write/close ──────────────────────

    [Fact]
    public void SubDoc_Open_Write_Close_Creates_DOM()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
var r = [];
doc.open();
doc.write('<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN""><title></title><span></span>');
doc.close();
r.push(doc.childNodes.length === 2);  // DOCTYPE + HTML
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void SubDoc_Write_DOCTYPE_Has_Name_And_PublicId()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
doc.open();
doc.write('<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN""><title></title><span></span>');
doc.close();
var r = [];
r.push(doc.firstChild.name.toUpperCase() === 'HTML');
r.push(doc.firstChild.publicId === '-//W3C//DTD HTML 4.0 Transitional//EN');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void SubDoc_Write_DOCTYPE_With_SystemId()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
doc.open();
doc.write('<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.01 Transitional//EN"" ""http://www.w3.org/TR/html4/loose.dtd""><title></title>');
doc.close();
var r = [];
r.push(doc.firstChild.name.toUpperCase() === 'HTML');
r.push(doc.firstChild.publicId === '-//W3C//DTD HTML 4.01 Transitional//EN');
r.push(doc.firstChild.systemId === 'http://www.w3.org/TR/html4/loose.dtd');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void SubDoc_Write_Creates_Proper_Head_Body()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
doc.open();
doc.write('<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN""><title></title><span></span>');
doc.close();
var r = [];
r.push(doc.documentElement.firstChild.nodeName === 'HEAD');
r.push(doc.documentElement.firstChild.firstChild.tagName === 'TITLE');
r.push(doc.documentElement.lastChild.nodeName === 'BODY');
r.push(doc.documentElement.lastChild.firstChild.tagName === 'SPAN');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void SubDoc_InternalSubset_Is_Null()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
doc.open();
doc.write('<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN""><title></title>');
doc.close();
var r = [];
var dt = doc.firstChild;
r.push(dt.internalSubset === null || dt.internalSubset === undefined);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    // ────────────────────── Test 72: document.styleSheets ──────────────────────

    [Fact]
    public void SubDoc_StyleSheets_Is_Available()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
doc.open();
doc.write('<!DOCTYPE HTML><head><style>img { height: 10px; }</style></head><body><img src=""data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7""></body>');
doc.close();
var r = [];
r.push(doc.styleSheets !== null);
r.push(doc.styleSheets !== undefined);
r.push(doc.styleSheets.length > 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void SubDoc_StyleSheet_OwnerNode_Is_Style_Element()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
doc.open();
doc.write('<!DOCTYPE HTML><head><style>img { height: 10px; }</style></head><body><img></body>');
doc.close();
var r = [];
var sheet = doc.styleSheets[0];
r.push(sheet.ownerNode !== null);
r.push(sheet.ownerNode.tagName === 'STYLE');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void SubDoc_StyleSheet_Href_Is_Null_For_Inline()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
doc.open();
doc.write('<!DOCTYPE HTML><head><style>p { color: red; }</style></head><body></body>');
doc.close();
var r = [];
r.push(doc.styleSheets[0].href === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void SubDoc_StyleSheet_CssRules_Has_Correct_Length()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
doc.open();
doc.write('<!DOCTYPE HTML><head><style>img { height: 10px; }</style></head><body><img></body>');
doc.close();
var r = [];
var rules = doc.styleSheets[0].cssRules;
r.push(rules.length === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void SubDoc_StyleSheet_InsertRule_Increases_CssRules_Length()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
doc.open();
doc.write('<!DOCTYPE HTML><head><style>img { height: 10px; }</style></head><body><img></body>');
doc.close();
var r = [];
doc.styleSheets[0].insertRule('img { height: 40px; }', 1);
var rules = doc.styleSheets[0].cssRules;
r.push(rules.length === 2);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void Main_Doc_StyleSheets_Href_Null_For_Inline()
    {
        var html = @"<!DOCTYPE html>
<html><head><style>body { color: black; }</style></head><body>
<div id=""result""></div>
<script>
var r = [];
r.push(document.styleSheets !== null);
r.push(document.styleSheets.length > 0);
r.push(document.styleSheets[0].href === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── Test 73: Nested event dispatch ──────────────────────

    [Fact]
    public void SubDoc_Nested_Event_Dispatch()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
var up = 0;
var down = 0;
var button = doc.createElement('button');
button.addEventListener('test', function () {
    up += 1;
    var e = doc.createEvent('HTMLEvents');
    e.initEvent('test', false, false);
    if (up < 20) button.dispatchEvent(e);
    down += up;
}, false);
var evt = doc.createEvent('HTMLEvents');
evt.initEvent('test', false, false);
button.dispatchEvent(evt);
var r = [];
r.push(up === 20);
r.push(down === 400);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void SubDoc_CreateEvent_CustomEvent_Has_InitCustomEvent()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
var evt = doc.createEvent('CustomEvent');
var before = [typeof evt.initCustomEvent, evt.detail].join(',');
evt.initCustomEvent('build', true, false, 'payload');
var after = [];
after.push(evt.type);
after.push(evt.bubbles);
after.push(evt.cancelable);
after.push(evt.detail);
document.getElementById('result').textContent = before + '|' + after.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("function,0|build,true,false,payload", result);
    }

    [Fact]
    public void SubDoc_CreateEvent_Event_Has_IsTrusted_False()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
var evt = doc.createEvent('Event');
var r = [];
r.push(typeof evt.isTrusted);
r.push(evt.isTrusted);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("boolean,false", result);
    }

    // ────────────────────── Test 75: SVG rect element ──────────────────────

    [Fact]
    public void SVG_Rect_Width_Is_Truthy()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var rect = document.createElementNS(svgns, 'rect');
rect.setAttribute('fill', 'red');
rect.setAttribute('width', '100');
rect.setAttribute('height', '100');
var r = [];
r.push(!!rect.width);  // SVGAnimatedLength should be truthy
r.push(rect.getAttribute('width') === '100');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void SVG_Rect_Width_Has_BaseVal_AnimVal()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var rect = document.createElementNS(svgns, 'rect');
rect.setAttribute('width', '100');
var r = [];
r.push(!!rect.width.baseVal);
r.push(!!rect.width.animVal);
r.push(rect.width.baseVal.value === 100);
r.push(rect.width.animVal.value === 100);
r.push(rect.width.baseVal.unitType === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    // ────────────────────── Test 76: SVG getElementById ──────────────────────

    [Fact]
    public void SubDoc_GetElementById_Finds_SVG_Element()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
var svgns = 'http://www.w3.org/2000/svg';
var rect = doc.createElementNS(svgns, 'rect');
rect.setAttribute('id', 'myrect');
rect.setAttribute('width', '100');
doc.documentElement.appendChild(rect);
var r = [];
var found = doc.getElementById('myrect');
r.push(found !== null);
r.push(found === rect);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── Test 77: SVG text getNumberOfChars ──────────────────────

    [Fact]
    public void SVG_Text_GetNumberOfChars_Returns_Length()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var text = document.createElementNS(svgns, 'text');
text.appendChild(document.createTextNode('abc'));
var r = [];
r.push(typeof text.getNumberOfChars === 'function');
r.push(text.getNumberOfChars() === 3);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── Test 79: Sub-document manipulation ──────────────────────

    [Fact]
    public void SubDoc_HasChildNodes_And_RemoveChild()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var obj = document.createElement('object');
var doc = obj.contentDocument;
var r = [];
r.push(doc.hasChildNodes());
while (doc.hasChildNodes())
    doc.removeChild(doc.firstChild);
r.push(!doc.hasChildNodes());
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void SubDoc_AppendChild_Creates_New_Root()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var obj = document.createElement('object');
var doc = obj.contentDocument;
while (doc.hasChildNodes())
    doc.removeChild(doc.firstChild);
var svgns = 'http://www.w3.org/2000/svg';
var svg = doc.createElementNS(svgns, 'svg:svg');
doc.appendChild(svg);
var r = [];
r.push(doc.hasChildNodes());
r.push(doc.documentElement.tagName.toUpperCase().indexOf('SVG') >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── document.images ──────────────────────

    [Fact]
    public void SubDoc_Images_Returns_Img_Elements()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
doc.open();
doc.write('<!DOCTYPE HTML><head><title></title><style>img { height: 10px; }</style></head><body><p><img src=""data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"" alt=""""></p></body>');
doc.close();
var r = [];
r.push(doc.images !== null);
r.push(doc.images.length === 1);
r.push(doc.images[0].tagName === 'IMG');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── document.links ──────────────────────

    [Fact]
    public void Main_Doc_Links_Returns_Anchor_Elements_With_Href()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<a id=""noHref"">No href</a>
<a id=""withHref"" href=""#test"">With href</a>
<div id=""result""></div>
<script>
var r = [];
r.push(document.links !== null);
r.push(document.links.length === 1);
r.push(document.links[0].textContent === 'With href');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── ContentWindow ──────────────────────

    [Fact]
    public void Iframe_ContentWindow_Has_Document()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var r = [];
r.push(iframe.contentWindow !== null);
r.push(iframe.contentWindow.document !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── Sub-document createElement and DOM ops ──────────────────────

    [Fact]
    public void SubDoc_CreateElement_And_AppendChild()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
var p = doc.createElement('p');
p.textContent = 'hello';
doc.body.appendChild(p);
var r = [];
r.push(doc.body.childNodes.length >= 1);
r.push(doc.body.firstChild.textContent === 'hello');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void SubDoc_CreateElementNS_With_SVG_Namespace()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
var svgns = 'http://www.w3.org/2000/svg';
var rect = doc.createElementNS(svgns, 'rect');
rect.setAttribute('width', '50');
var r = [];
r.push(rect.namespaceURI === svgns);
r.push(rect.localName === 'rect');
r.push(!!rect.width);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── Sub-document getElementsByTagName ──────────────────────

    [Fact]
    public void SubDoc_GetElementsByTagName()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
var p1 = doc.createElement('p');
var p2 = doc.createElement('p');
doc.body.appendChild(p1);
doc.body.appendChild(p2);
var r = [];
var ps = doc.getElementsByTagName('p');
r.push(ps.length === 2);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    // ────────────────────── document.open/close ──────────────────────

    [Fact]
    public void Main_Doc_Open_Close_Are_Available()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
r.push(typeof document.open === 'function');
r.push(typeof document.close === 'function');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── NamespaceURI ──────────────────────

    [Fact]
    public void Element_NamespaceURI_Default_Is_XHTML()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var div = document.createElement('div');
var r = [];
r.push(div.namespaceURI === 'http://www.w3.org/1999/xhtml');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    // ────────────────────── DOCTYPE nodeType ──────────────────────

    [Fact]
    public void DocType_NodeType_Is_10()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var iframe = document.createElement('iframe');
var doc = iframe.contentDocument;
doc.open();
doc.write('<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN""><title></title>');
doc.close();
var r = [];
var dt = doc.firstChild;
r.push(dt.nodeType === 10);
r.push(dt.name.toUpperCase() === 'HTML');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }
}
