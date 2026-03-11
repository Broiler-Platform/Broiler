namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 4 of Acid3 Compliance v3: GC-safe DOM references (tests 26–27),
/// extended attribute edge cases (test 64), XHTML namespace defaults (test 98),
/// and complex DOM/JS interaction (test 99).
/// </summary>
public class DomEdgeCasePhase4Tests
{
    // ------------------------------------------------------------------
    //  4.1 GC-safe DOM references (Acid3 tests 26–27)
    // ------------------------------------------------------------------

    [Fact]
    public void CreateDocument_Element_Has_OwnerDocument_With_NodeType_9()
    {
        // Elements created via createDocument must have ownerDocument = the document (nodeType=9)
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var d = document.implementation.createDocument(null, null, null);
var e1 = d.createElement('test');
d.appendChild(d.createElement('root'));
d.documentElement.appendChild(e1);
r.push('' + (e1.parentNode !== null));
r.push('' + (e1.parentNode.ownerDocument !== null));
r.push('' + e1.parentNode.ownerDocument.nodeType);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true,9", result);
    }

    [Fact]
    public void CreateDocument_Detached_Element_OwnerDocument_Survives()
    {
        // When document reference is dropped, elements must still have valid ownerDocument
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var d = document.implementation.createDocument(null, null, null);
var e1 = d.createElement('test');
d.appendChild(d.createElement('root'));
d.documentElement.appendChild(e1);

// e2 - element that's not in a document
var d2 = document.implementation.createDocument(null, null, null);
var e2 = d2.createElement('test');
d2.createElement('root').appendChild(e2);
r.push('' + (e2.parentNode !== null));
r.push('' + (e2.parentNode.ownerDocument !== null));

// Drop document references
d = null;
d2 = null;

// e1 still connected
r.push('' + (e1.parentNode !== null));
r.push('' + (e1.parentNode.ownerDocument !== null));
r.push('' + e1.parentNode.ownerDocument.nodeType);

// e2 still has parent (the detached root element)
r.push('' + (e2.parentNode !== null));
r.push('' + (e2.parentNode.ownerDocument !== null));
r.push('' + e2.parentNode.ownerDocument.nodeType);

document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // e2 parent exists, e2 ownerDocument exists
        Assert.Contains("true,true", result);
        // e1 survives with nodeType=9
        Assert.Contains("true,true,9", result);
    }

    [Fact]
    public void CreateDocument_Elements_Survive_Across_GC_Stress()
    {
        // Simulates test 26/27: elements survive GC stress loop
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""bucket1""></div>
<div id=""bucket2""></div>
<div id=""out""></div>
<script>
var r = [];
var d = document.implementation.createDocument(null, null, null);
var e1 = d.createElement('test');
d.appendChild(d.createElement('root'));
d.documentElement.appendChild(e1);

var kungFuDeathGrip = [e1];
d = null;

// GC stress loop (Acid3 uses ~1000+ iterations based on Date.valueOf(); reduced to 10 for unit test speed)
for (var i = 0; i < 10; i++) {
    var tmp = document.createTextNode('iteration ' + i);
    document.createElement('a').appendChild(tmp);
    var tmpParent = tmp.parentNode;
    document.body.insertBefore(tmpParent, document.getElementById('bucket1'));
    document.body.removeChild(tmpParent);
}

r.push('' + (e1.parentNode !== null));
r.push('' + (e1.parentNode.ownerDocument !== null));
r.push('' + e1.parentNode.ownerDocument.nodeType);

document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true,9", result);
    }

    [Fact]
    public void Test27_KungFuDeathGrip_Survives_Across_Tests()
    {
        // Continuation: elements stored in global variable survive across test functions
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var d = document.implementation.createDocument(null, null, null);
var e1 = d.createElement('test');
d.appendChild(d.createElement('root'));
d.documentElement.appendChild(e1);

var kungFuDeathGrip = [e1];
d = null;

// Simulate test 27: read from kungFuDeathGrip
var retrieved = kungFuDeathGrip[0];
kungFuDeathGrip = null;

r.push('' + (retrieved !== null));
r.push('' + (retrieved.parentNode !== null));
r.push('' + (retrieved.parentNode.ownerDocument !== null));
r.push('' + retrieved.parentNode.ownerDocument.nodeType);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true,true,9", result);
    }

    // ------------------------------------------------------------------
    //  4.2 Extended attribute tests (Acid3 test 64)
    // ------------------------------------------------------------------

    [Fact]
    public void Object_Data_Resolves_Relative_URI_To_Absolute()
    {
        // object.data must resolve relative URLs to absolute
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var obj1 = document.createElement('object');
obj1.setAttribute('data', 'test.html');
var obj2 = document.createElement('object');
obj2.setAttribute('data', './test.html');
r.push('' + (obj1.data === obj2.data));
r.push('' + (obj1.data.indexOf('test.html') >= 0));
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "http://example.com/page.html");

        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Object_Data_Is_Absolute_With_Http_Base()
    {
        // object.data must be an absolute URL when base is HTTP
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var obj = document.createElement('object');
obj.setAttribute('data', 'test.html');
document.getElementById('out').textContent = '' + /^http:/.test(obj.data);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "http://example.com/page.html");

        Assert.Contains("true", result);
    }

    [Fact]
    public void SetAttribute_Does_Not_Create_JS_Property()
    {
        // setAttribute should NOT create a JS property on the element
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var test = document.createElement('p');
r.push('' + ('TWVvdywgbWV3Li4u' in test));
r.push('' + (test.TWVvdywgbWV3Li4u === undefined));
r.push('' + (test['TWVvdywgbWV3Li4u'] === undefined));
test.setAttribute('TWVvdywgbWV3Li4u', 'woof');
r.push('' + ('TWVvdywgbWV3Li4u' in test));
r.push('' + (test.TWVvdywgbWV3Li4u === undefined));
r.push('' + (test['TWVvdywgbWV3Li4u'] === undefined));
r.push(test.getAttribute('TWVvdywgbWV3Li4u'));
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // in operator returns false; dot/bracket access returns undefined; getAttribute returns value
        Assert.Contains("false,true,true,false,true,true,woof", result);
    }

    [Fact]
    public void Object_GetElementsByTagName_Returns_Children()
    {
        // object.getElementsByTagName('param').length should return 1
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var obj = document.createElement('object');
obj.appendChild(document.createElement('param'));
document.getElementById('out').textContent = '' + obj.getElementsByTagName('param').length;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("1", result);
    }

    // ------------------------------------------------------------------
    //  4.3 XHTML namespace defaults (Acid3 test 98)
    // ------------------------------------------------------------------

    [Fact]
    public void CreateDocument_XHTML_With_DocType_Sets_OwnerDocument()
    {
        // doctype.ownerDocument should be the document it's assigned to
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var doctype = document.implementation.createDocumentType('html',
    '-//W3C//DTD XHTML 1.0 Strict//EN',
    'http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd');
var doc = document.implementation.createDocument('http://www.w3.org/1999/xhtml', 'html', doctype);
r.push('' + (doctype.ownerDocument === doc));
r.push('' + doc.nodeType);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,9", result);
    }

    [Fact]
    public void XHTML_Document_Title_Updates_Dynamically()
    {
        // doc.title must reflect <title> element textContent changes
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var doctype = document.implementation.createDocumentType('html',
    '-//W3C//DTD XHTML 1.0 Strict//EN',
    'http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd');
var doc = document.implementation.createDocument('http://www.w3.org/1999/xhtml', 'html', doctype);
doc.documentElement.appendChild(doc.createElementNS('http://www.w3.org/1999/xhtml', 'head'));
doc.documentElement.appendChild(doc.createElementNS('http://www.w3.org/1999/xhtml', 'body'));
var t = doc.createElementNS('http://www.w3.org/1999/xhtml', 'title');
doc.documentElement.firstChild.appendChild(t);
r.push(doc.title);
t.textContent = 'Sparrow';
r.push(doc.title);
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("|Sparrow", result);
    }

    [Fact]
    public void XHTML_Document_Forms_Collection_Updates()
    {
        // doc.forms.length must update when forms are added
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var doctype = document.implementation.createDocumentType('html',
    '-//W3C//DTD XHTML 1.0 Strict//EN',
    'http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd');
var doc = document.implementation.createDocument('http://www.w3.org/1999/xhtml', 'html', doctype);
doc.documentElement.appendChild(doc.createElementNS('http://www.w3.org/1999/xhtml', 'head'));
doc.documentElement.appendChild(doc.createElementNS('http://www.w3.org/1999/xhtml', 'body'));
r.push('' + doc.forms.length);
doc.body.appendChild(doc.createElementNS('http://www.w3.org/1999/xhtml', 'form'));
r.push('' + doc.forms.length);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("0,1", result);
    }

    [Fact]
    public void XHTML_Document_Body_Accessible()
    {
        // doc.body must return the body element
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var doc = document.implementation.createDocument('http://www.w3.org/1999/xhtml', 'html', null);
doc.documentElement.appendChild(doc.createElementNS('http://www.w3.org/1999/xhtml', 'head'));
doc.documentElement.appendChild(doc.createElementNS('http://www.w3.org/1999/xhtml', 'body'));
r.push('' + (doc.body !== null));
r.push(doc.body.tagName);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,BODY", result);
    }

    // ------------------------------------------------------------------
    //  4.4 Complex DOM/JS interaction (Acid3 test 99)
    // ------------------------------------------------------------------

    [Fact]
    public void Setting_Href_Does_Not_Affect_Child_Text()
    {
        // Setting a.href must NOT change a.firstChild.data (the "weirdest bug ever")
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var a = document.createElement('a');
a.setAttribute('href', 'http://www.example.com/');
a.appendChild(document.createTextNode('www.example.com'));
a.href = 'http://hixie.ch/';
r.push(a.firstChild.data);
a.href = 'http://damowmow.com/';
r.push(a.firstChild.data);
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("www.example.com|www.example.com", result);
    }

    [Fact]
    public void Setting_Href_Updates_Attribute_Only()
    {
        // a.href setter only updates the href attribute, child nodes unchanged
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var a = document.createElement('a');
a.setAttribute('href', 'http://www.example.com/');
a.appendChild(document.createTextNode('click me'));
a.href = 'http://hixie.ch/';
r.push(a.getAttribute('href'));
r.push(a.firstChild.data);
r.push('' + a.childNodes.length);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("http://hixie.ch/,click me,1", result);
    }

    // ------------------------------------------------------------------
    //  4.5 Main document ownerDocument and nodeType
    // ------------------------------------------------------------------

    [Fact]
    public void Main_Document_Has_NodeType_9()
    {
        // document.nodeType must be 9 (DOCUMENT_NODE)
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
document.getElementById('out').textContent = '' + document.nodeType;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("9", result);
    }

    [Fact]
    public void Main_Document_OwnerDocument_Returns_Document()
    {
        // element.ownerDocument must return the document object (nodeType=9)
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var el = document.createElement('div');
r.push('' + (el.ownerDocument !== null));
r.push('' + el.ownerDocument.nodeType);
r.push('' + (el.ownerDocument === document));
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,9,true", result);
    }

    [Fact]
    public void Main_Document_Forms_Collection()
    {
        // document.forms must return form elements
        var html = @"<!DOCTYPE html>
<html><body>
<form id=""f1""></form>
<form id=""f2""></form>
<div id=""out""></div>
<script>
document.getElementById('out').textContent = '' + document.forms.length;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("2", result);
    }
}
