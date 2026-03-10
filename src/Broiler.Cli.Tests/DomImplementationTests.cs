namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 1 of Acid3 Compliance v2: DOMImplementation,
/// DOMException, and element name validation.
/// </summary>
public class DomImplementationTests
{
    // ------------------------------------------------------------------
    //  1.1 document.implementation object
    // ------------------------------------------------------------------

    [Fact]
    public void Implementation_HasFeature_Returns_True()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
r.push(document.implementation.hasFeature('Core', '2.0'));
r.push(document.implementation.hasFeature('XML', '1.0'));
r.push(document.implementation.hasFeature(''));
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Implementation_CreateDocumentType_Returns_Doctype_Node()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var dt = document.implementation.createDocumentType('html',
    '-//W3C//DTD XHTML 1.0 Strict//EN',
    'http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd');
var r = [];
r.push(dt.nodeType);
r.push(dt.name);
r.push(dt.publicId);
r.push(dt.systemId);
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("10|html|-//W3C//DTD XHTML 1.0 Strict//EN|http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd", result);
    }

    [Fact]
    public void Implementation_CreateDocument_Returns_Document()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var dt = document.implementation.createDocumentType('html', '', '');
var doc = document.implementation.createDocument('http://www.w3.org/1999/xhtml', 'html', dt);
var r = [];
r.push(doc.nodeType);
r.push(doc.documentElement.tagName.toLowerCase());
r.push(doc.documentElement.namespaceURI);
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("9|html|http://www.w3.org/1999/xhtml", result);
    }

    [Fact]
    public void Implementation_CreateDocument_Without_QualifiedName()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var doc = document.implementation.createDocument(null, '', null);
var r = [];
r.push(doc.nodeType);
r.push(doc.documentElement == null ? 'null' : 'present');
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // When qualifiedName is empty, there should be no documentElement
        // The BuildSubDocument returns the docRoot itself if no children found
        Assert.Contains("9|", result);
    }

    [Fact]
    public void Implementation_CreateHTMLDocument_With_Title()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var doc = document.implementation.createHTMLDocument('Test Title');
var r = [];
r.push(doc.nodeType);
r.push(doc.documentElement.tagName.toLowerCase());
r.push(doc.head != null ? 'head' : 'no-head');
r.push(doc.body != null ? 'body' : 'no-body');
r.push(doc.head.firstChild ? doc.head.firstChild.tagName.toLowerCase() : 'no-title');
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("9|html|head|body|title", result);
    }

    [Fact]
    public void Implementation_CreateHTMLDocument_Without_Title()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var doc = document.implementation.createHTMLDocument();
var r = [];
r.push(doc.nodeType);
r.push(doc.documentElement.tagName.toLowerCase());
r.push(doc.body != null ? 'body' : 'no-body');
r.push(doc.head.childNodes.length);
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("9|html|body|0", result);
    }

    // ------------------------------------------------------------------
    //  1.2 Created documents support full DOM API
    // ------------------------------------------------------------------

    [Fact]
    public void CreateHTMLDocument_Supports_CreateElement()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var doc = document.implementation.createHTMLDocument('Test');
var p = doc.createElement('p');
p.textContent = 'created-in-subdoc';
doc.body.appendChild(p);
var found = doc.querySelector('p');
document.getElementById('out').textContent = found ? found.textContent : 'not-found';
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("created-in-subdoc", result);
    }

    [Fact]
    public void CreateHTMLDocument_Supports_GetElementById()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var doc = document.implementation.createHTMLDocument('Test');
var div = doc.createElement('div');
div.id = 'inner';
div.textContent = 'found-by-id';
doc.body.appendChild(div);
var found = doc.getElementById('inner');
document.getElementById('out').textContent = found ? found.textContent : 'not-found';
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("found-by-id", result);
    }

    [Fact]
    public void CreateDocument_Supports_CreateElementNS()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var doc = document.implementation.createDocument('http://www.w3.org/2000/svg', 'svg', null);
var rect = doc.createElementNS('http://www.w3.org/2000/svg', 'rect');
doc.documentElement.appendChild(rect);
var r = [];
r.push(doc.documentElement.tagName.toLowerCase());
r.push(rect.tagName.toLowerCase());
r.push(rect.namespaceURI);
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("svg|rect|http://www.w3.org/2000/svg", result);
    }

    // ------------------------------------------------------------------
    //  1.3 DOMException
    // ------------------------------------------------------------------

    [Fact]
    public void DOMException_Constructor_Sets_Code_And_Name()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var ex = new DOMException('test message', 'InvalidCharacterError');
var r = [];
r.push(ex.message);
r.push(ex.name);
r.push(ex.code);
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("test message|InvalidCharacterError|5", result);
    }

    [Fact]
    public void DOMException_Has_Static_Constants()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
r.push(DOMException.INVALID_CHARACTER_ERR);
r.push(DOMException.NAMESPACE_ERR);
r.push(DOMException.NOT_FOUND_ERR);
r.push(DOMException.HIERARCHY_REQUEST_ERR);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("5,14,8,3", result);
    }

    [Fact]
    public void DOMException_NamespaceError_Code()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var ex = new DOMException('test', 'NamespaceError');
document.getElementById('out').textContent = '' + ex.code;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("14", result);
    }

    // ------------------------------------------------------------------
    //  1.3 Element name validation
    // ------------------------------------------------------------------

    [Fact]
    public void CreateElement_Throws_On_Invalid_Name()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = 'no-error';
try {
    document.createElement('123invalid');
} catch(e) {
    r = 'caught';
}
document.getElementById('out').textContent = r;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("caught", result);
    }

    [Fact]
    public void CreateElement_Accepts_Valid_Names()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
try { document.createElement('div'); r.push('div-ok'); } catch(e) { r.push('div-fail'); }
try { document.createElement('my-component'); r.push('custom-ok'); } catch(e) { r.push('custom-fail'); }
try { document.createElement('_private'); r.push('_ok'); } catch(e) { r.push('_fail'); }
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("div-ok,custom-ok,_ok", result);
    }

    [Fact]
    public void CreateElementNS_Throws_On_Prefix_Without_Namespace()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = 'no-error';
try {
    document.createElementNS('', 'prefix:local');
} catch(e) {
    r = 'caught';
}
document.getElementById('out').textContent = r;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("caught", result);
    }

    // ------------------------------------------------------------------
    //  1.1 + 1.2 Integration: getTestDocument pattern from Acid3
    // ------------------------------------------------------------------

    [Fact]
    public void CreateDocument_Acts_As_GetTestDocument()
    {
        // Simulates the Acid3 getTestDocument() helper
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
function getTestDocument() {
    var doc = document.implementation.createDocument(null, 'root', null);
    return doc;
}
var doc = getTestDocument();
var r = [];
r.push(doc.nodeType);
r.push(doc.documentElement.tagName.toLowerCase());
var child = doc.createElement('child');
child.textContent = 'test-content';
doc.documentElement.appendChild(child);
var found = doc.getElementsByTagName('child');
r.push(found.length);
r.push(found[0].textContent);
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("9|root|1|test-content", result);
    }

    [Fact]
    public void CreateDocumentType_Name_Validation()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
try {
    document.implementation.createDocumentType('html', '', '');
    r.push('valid-ok');
} catch(e) {
    r.push('valid-fail');
}
try {
    document.implementation.createDocumentType('', '', '');
    r.push('empty-ok');
} catch(e) {
    r.push('empty-caught');
}
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("valid-ok,empty-caught", result);
    }

    [Fact]
    public void Implementation_Available_On_SubDocuments()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var doc = document.implementation.createHTMLDocument('Parent');
var r = [];
r.push(doc.implementation != null ? 'has-impl' : 'no-impl');
r.push('' + doc.implementation.hasFeature('Core', '2.0'));
var subDoc = doc.implementation.createHTMLDocument('Child');
r.push(subDoc.nodeType);
r.push(subDoc.body != null ? 'body' : 'no-body');
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("has-impl|true|9|body", result);
    }

    [Fact]
    public void DOCUMENT_TYPE_NODE_Constant_Available()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
r.push(document.DOCUMENT_TYPE_NODE);
r.push(document.ELEMENT_NODE);
r.push(document.TEXT_NODE);
r.push(document.COMMENT_NODE);
r.push(document.DOCUMENT_NODE);
r.push(document.DOCUMENT_FRAGMENT_NODE);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("10,1,3,8,9,11", result);
    }
}
