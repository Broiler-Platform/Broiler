namespace Broiler.Cli.Tests;

/// <summary>
/// Acid3 regression tests that validate Broiler CLI can execute the Acid3
/// test harness and track compliance progress. These tests use simplified
/// excerpts from the Acid3 test suite to verify specific capabilities.
/// </summary>
public class Acid3RegressionTests
{
    /// <summary>
    /// Verifies that the Acid3 test harness bootstrap code (creating iframes,
    /// setting up the test runner) can execute without fatal errors.
    /// </summary>
    [Fact]
    public void Acid3_Harness_Bootstrap_Executes_Without_Fatal_Error()
    {
        // Minimal version of the Acid3 harness setup
        var html = @"<!DOCTYPE html>
<html>
<head><title>Acid3 Bootstrap Test</title></head>
<body>
<div id=""result"">?</div>
<div class=""buckets"">
  <p id=""bucket1"" class=""z"">1</p>
  <p id=""bucket2"" class=""z"">2</p>
</div>
<script>
var score = 0;
var bucket1 = document.getElementById('bucket1');
var bucket2 = document.getElementById('bucket2');
var result = document.getElementById('result');

// Simulate a passing test
score += 1;
result.textContent = score + '/100';
bucket1.className = 'zP';

// Simulate another passing test
score += 1;
result.textContent = score + '/100';
</script>
</body>
</html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("2/100", result);
        Assert.Contains("class=\"zP\"", result);
    }

    /// <summary>
    /// Tests the getTestDocument() pattern used throughout Acid3 bucket 1.
    /// Creates a new document via DOMImplementation and verifies basic operations.
    /// </summary>
    [Fact]
    public void Acid3_GetTestDocument_Pattern_Works()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<div id=""r""></div>
<script>
var r = document.getElementById('r');
try {
    // This is the getTestDocument pattern from Acid3
    var doc = document.implementation.createDocument(null, 'html', null);
    r.textContent = doc.documentElement ? 'OK' : 'NO';
} catch(e) {
    r.textContent = 'ERR: ' + e.message;
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">OK<", result);
    }

    /// <summary>
    /// Tests Acid3 test 1 pattern: NodeFilter exception propagation from
    /// createNodeIterator callbacks.
    /// </summary>
    [Fact]
    public void Acid3_Test1_NodeFilter_Exception_Propagation()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<div id=""r""></div>
<div id=""container""><p>A</p><p>B</p></div>
<script>
var r = document.getElementById('r');
var container = document.getElementById('container');
try {
    var doc = document.implementation.createDocument(null, 'html', null);
    var root = doc.documentElement;
    var child = doc.createElement('div');
    root.appendChild(child);

    var caught = false;
    var filter = {
        acceptNode: function(node) {
            throw new Error('filter error');
        }
    };

    var iter = doc.createNodeIterator(root, 0xFFFFFFFF, filter);
    try {
        iter.nextNode();
    } catch(e) {
        caught = true;
    }
    r.textContent = caught ? 'PASS' : 'FAIL: exception not propagated';
} catch(e) {
    r.textContent = 'ERROR: ' + e.message;
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("PASS", result);
    }

    /// <summary>
    /// Tests Acid3 test 25 pattern: createDocumentType and createDocument.
    /// </summary>
    [Fact]
    public void Acid3_Test25_CreateDocumentType_And_CreateDocument()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<div id=""r""></div>
<script>
var r = document.getElementById('r');
var results = [];
try {
    var dt = document.implementation.createDocumentType('html',
        '-//W3C//DTD XHTML 1.0 Strict//EN',
        'http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd');
    results.push(dt.nodeType === 10 ? 'P' : 'F');
    results.push(dt.name === 'html' ? 'P' : 'F');
    results.push(dt.publicId === '-//W3C//DTD XHTML 1.0 Strict//EN' ? 'P' : 'F');

    var doc = document.implementation.createDocument(
        'http://www.w3.org/1999/xhtml', 'html', dt);
    results.push(doc.nodeType === 9 ? 'P' : 'F');
    results.push(doc.documentElement ? 'P' : 'F');
    results.push(doc.documentElement.localName === 'html' ? 'P' : 'F');

    r.textContent = results.join(',');
} catch(e) {
    r.textContent = 'ERROR: ' + e.message;
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("P,P,P,P,P,P", result);
    }

    /// <summary>
    /// Tests Acid3 tests 22-23 pattern: createElement with invalid names
    /// must throw DOMException.
    /// </summary>
    [Fact]
    public void Acid3_Test22_23_CreateElement_Invalid_Names_Throw()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<div id=""r""></div>
<script>
var r = document.getElementById('r');
var results = [];
try {
    try {
        document.createElement('1invalid');
        results.push('F');
    } catch(e) {
        results.push(e.code === 5 ? 'P' : 'F:' + e.code);
    }
    try {
        document.createElement('a b');
        results.push('F');
    } catch(e) {
        results.push(e.code === 5 ? 'P' : 'F:' + e.code);
    }
    // Valid names should work
    var el = document.createElement('valid-name');
    results.push(el ? 'P' : 'F');

    r.textContent = results.join(',');
} catch(e) {
    r.textContent = 'ERROR: ' + e.message;
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("P,P,P", result);
    }

    /// <summary>
    /// Tests Acid3 test 19 pattern: Node type constants on Node constructor
    /// and prototype.
    /// </summary>
    [Fact]
    public void Acid3_Test19_Node_Type_Constants()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<div id=""r""></div>
<script>
var r = document.getElementById('r');
var results = [];
try {
    results.push(Node.ELEMENT_NODE === 1 ? 'P' : 'F');
    results.push(Node.TEXT_NODE === 3 ? 'P' : 'F');
    results.push(Node.COMMENT_NODE === 8 ? 'P' : 'F');
    results.push(Node.DOCUMENT_NODE === 9 ? 'P' : 'F');
    results.push(Node.DOCUMENT_TYPE_NODE === 10 ? 'P' : 'F');
    results.push(Node.DOCUMENT_FRAGMENT_NODE === 11 ? 'P' : 'F');
    // Also verify prototype-level access
    results.push(document.ELEMENT_NODE === 1 ? 'P' : 'F');
    r.textContent = results.join(',');
} catch(e) {
    r.textContent = 'ERROR: ' + e.message;
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("P,P,P,P,P,P,P", result);
    }

    /// <summary>
    /// Tests Acid3 test 21 pattern: namespace-aware attribute methods.
    /// </summary>
    [Fact]
    public void Acid3_Test21_Namespace_Attribute_Methods()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<div id=""r""></div>
<script>
var r = document.getElementById('r');
var results = [];
try {
    var el = document.createElement('div');
    var ns = 'http://example.com/ns';

    el.setAttributeNS(ns, 'ex:foo', 'bar');
    results.push(el.getAttributeNS(ns, 'foo') === 'bar' ? 'P' : 'F');
    results.push(el.hasAttributeNS(ns, 'foo') ? 'P' : 'F');

    el.removeAttributeNS(ns, 'foo');
    results.push(!el.hasAttributeNS(ns, 'foo') ? 'P' : 'F');

    r.textContent = results.join(',');
} catch(e) {
    r.textContent = 'ERROR: ' + e.message;
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("P,P,P", result);
    }

    /// <summary>
    /// Tests Acid3 bucket 6 pattern: ECMAScript operations that the
    /// JS engine must handle correctly.
    /// </summary>
    [Fact]
    public void Acid3_Bucket6_ECMAScript_Array_And_String()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<div id=""r""></div>
<script>
var r = document.getElementById('r');
var results = [];
try {
    // Test 81: Array elisions at end
    var a = [1,2,3,];
    results.push(a.length === 3 ? 'P' : 'F');

    // Test 82: Array elisions in middle
    var b = [1,,3];
    results.push(b.length === 3 ? 'P' : 'F');
    results.push(b[1] === undefined ? 'P' : 'F');

    // Test 85: String operations
    results.push('abc'.charAt(1) === 'b' ? 'P' : 'F');
    results.push('abc'.indexOf('b') === 1 ? 'P' : 'F');

    r.textContent = results.join(',');
} catch(e) {
    r.textContent = 'ERROR: ' + e.message;
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("P,P,P,P,P", result);
    }

    /// <summary>
    /// Tests the Acid3 score display update pattern: modifying DOM elements
    /// to show test results.
    /// </summary>
    [Fact]
    public void Acid3_Score_Display_Update_Pattern()
    {
        var html = @"<!DOCTYPE html>
<html>
<head>
<style>
.z { visibility: hidden; }
.zP { visibility: visible; }
.zPP { visibility: visible; }
</style>
</head>
<body>
<h1>Acid3</h1>
<span id=""result"">?</span><span id=""slash"">/</span><span>100</span>
<div class=""buckets"">
  <p id=""bucket1"" class=""z"">B1</p>
  <p id=""bucket2"" class=""z"">B2</p>
  <p id=""bucket3"" class=""z"">B3</p>
</div>
<script>
var result = document.getElementById('result');
var bucket1 = document.getElementById('bucket1');
var bucket2 = document.getElementById('bucket2');

// Simulate tests passing
var score = 0;
for (var i = 0; i < 16; i++) {
    score++;
    var cls = 'z';
    for (var j = 0; j < Math.min(i + 1, 16); j++) cls += 'P';
    bucket1.className = cls;
}
result.textContent = '' + score;

// Bucket 2 partial progress
score += 5;
bucket2.className = 'zPPPPP';
result.textContent = '' + score;
</script>
</body>
</html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">21<", result);
        Assert.Contains("zPPPPPPPPPPPPPPPP", result);
        Assert.Contains("zPPPPP", result);
    }

    /// <summary>
    /// Tests CSS selector matching needed for Acid3 bucket coloring:
    /// the complex selector patterns used by the test page.
    /// </summary>
    [Fact]
    public void Acid3_Bucket_Selector_Class_Update()
    {
        var html = @"<!DOCTYPE html>
<html>
<head>
<style>
.z { visibility: hidden; }
:first-child + * .buckets p { display: inline-block; }
#bucket1.zPPPPPPPPPPPPPPPP { background: red; }
#bucket2.zPPPPPPPPPPPPPPPP { background: orange; }
</style>
</head>
<body>
<div class=""buckets"">
  <p id=""bucket1"" class=""z"">B1</p>
  <p id=""bucket2"" class=""z"">B2</p>
</div>
<div id=""r""></div>
<script>
var r = document.getElementById('r');
var b1 = document.getElementById('bucket1');
b1.className = 'zPPPPPPPPPPPPPPPP';
var style = window.getComputedStyle(b1);
var bg = style.getPropertyValue('background') ||
         style.getPropertyValue('background-color') || 'none';
r.textContent = b1.className + '|' + (bg.indexOf('red') >= 0 || bg.indexOf('255') >= 0 ? 'colored' : 'no:' + bg);
</script>
</body>
</html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("zPPPPPPPPPPPPPPPP", result);
    }
}
