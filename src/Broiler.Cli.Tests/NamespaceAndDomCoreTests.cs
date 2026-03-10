namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 4 of Acid3 Compliance v2: Namespace-aware attributes,
/// null byte handling, element name validation with DOMException,
/// cloneNode whitespace preservation, and Node type constants.
/// </summary>
public class NamespaceAndDomCoreTests
{
    // ------------------------------------------------------------------
    //  4.1 Namespace-aware attribute methods
    // ------------------------------------------------------------------

    [Fact]
    public void SetAttributeNS_And_GetAttributeNS_Work()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var el = document.createElement('div');
el.setAttributeNS('http://www.w3.org/1999/xlink', 'xlink:href', 'http://example.com');
r.push(el.getAttributeNS('http://www.w3.org/1999/xlink', 'href'));
r.push(el.getAttribute('xlink:href'));
r.push(el.hasAttributeNS('http://www.w3.org/1999/xlink', 'href'));
el.setAttributeNS(null, 'data-foo', 'bar');
r.push(el.getAttributeNS(null, 'data-foo'));
r.push(el.hasAttributeNS(null, 'data-foo'));
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("http://example.com,http://example.com,true,bar,true", result);
    }

    [Fact]
    public void RemoveAttributeNS_Removes_Namespaced_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var el = document.createElement('div');
el.setAttributeNS('http://www.w3.org/1999/xlink', 'xlink:href', 'http://example.com');
r.push(el.hasAttributeNS('http://www.w3.org/1999/xlink', 'href'));
el.removeAttributeNS('http://www.w3.org/1999/xlink', 'href');
r.push(el.hasAttributeNS('http://www.w3.org/1999/xlink', 'href'));
r.push(el.getAttributeNS('http://www.w3.org/1999/xlink', 'href') === null);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,false,true", result);
    }

    // ------------------------------------------------------------------
    //  4.2 Null byte handling
    // ------------------------------------------------------------------

    [Fact]
    public void NullByte_In_TextNode_Preserved_And_Invalid_TagName_Throws()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
// Null bytes in text nodes should be preserved
var t = document.createTextNode('hello\u0000world');
r.push(t.textContent.length);
// Null bytes in attribute values should be preserved
var el = document.createElement('div');
el.setAttribute('data-x', 'a\u0000b');
r.push(el.getAttribute('data-x').length);
// Invalid element name (containing special characters) should throw
var threw = false;
try { document.createElement('1invalid'); } catch(e) { threw = true; }
r.push(threw);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("11,3,true", result);
    }

    // ------------------------------------------------------------------
    //  4.3 Element name validation with proper DOMException
    // ------------------------------------------------------------------

    [Fact]
    public void CreateElement_Throws_DOMException_InvalidCharacterError()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
try {
    document.createElement('bad name with spaces');
} catch(e) {
    r.push(e instanceof DOMException);
    r.push(e.name);
    r.push(e.code);
}
try {
    document.createElement('123');
} catch(e) {
    r.push(e instanceof DOMException);
    r.push(e.code);
}
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,InvalidCharacterError,5,true,5", result);
    }

    [Fact]
    public void CreateElementNS_Throws_DOMException_NamespaceError()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
// Prefixed name with null namespace should throw NamespaceError
try {
    document.createElementNS(null, 'prefix:local');
} catch(e) {
    r.push(e instanceof DOMException);
    r.push(e.name);
    r.push(e.code);
}
// Invalid qualified name should throw InvalidCharacterError
try {
    document.createElementNS('http://example.com', '123invalid');
} catch(e) {
    r.push(e instanceof DOMException);
    r.push(e.name);
    r.push(e.code);
}
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,NamespaceError,14,true,InvalidCharacterError,5", result);
    }

    // ------------------------------------------------------------------
    //  4.4 cloneNode(true) whitespace preservation
    // ------------------------------------------------------------------

    [Fact]
    public void CloneNode_Deep_Preserves_Whitespace_TextNodes()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
var parent = document.createElement('div');
var child1 = document.createElement('span');
child1.textContent = 'hello';
parent.appendChild(child1);
var ws = document.createTextNode('   ');
parent.appendChild(ws);
var child2 = document.createElement('span');
child2.textContent = 'world';
parent.appendChild(child2);

r.push(parent.childNodes.length);
var clone = parent.cloneNode(true);
r.push(clone.childNodes.length);
// Verify whitespace text node survived cloning
r.push(clone.childNodes[1].nodeType);
r.push(clone.childNodes[1].textContent === '   ');
r.push(clone.childNodes[0].textContent);
r.push(clone.childNodes[2].textContent);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("3,3,3,true,hello,world", result);
    }

    // ------------------------------------------------------------------
    //  4.5 Node type constants on constructors
    // ------------------------------------------------------------------

    [Fact]
    public void Node_Constructor_Has_Type_Constants()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
r.push(Node.ELEMENT_NODE);
r.push(Node.ATTRIBUTE_NODE);
r.push(Node.TEXT_NODE);
r.push(Node.CDATA_SECTION_NODE);
r.push(Node.COMMENT_NODE);
r.push(Node.DOCUMENT_NODE);
r.push(Node.DOCUMENT_TYPE_NODE);
r.push(Node.DOCUMENT_FRAGMENT_NODE);
r.push(Node.NOTATION_NODE);
r.push(typeof Node);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("1,2,3,4,8,9,10,11,12,function", result);
    }

    [Fact]
    public void Node_Prototype_Has_Type_Constants()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var r = [];
r.push(Node.prototype.ELEMENT_NODE);
r.push(Node.prototype.TEXT_NODE);
r.push(Node.prototype.COMMENT_NODE);
r.push(Node.prototype.DOCUMENT_NODE);
r.push(Node.prototype.DOCUMENT_FRAGMENT_NODE);
// Also verify element-level constants
r.push(document.ELEMENT_NODE);
r.push(document.TEXT_NODE);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("1,3,8,9,11,1,3", result);
    }
}
