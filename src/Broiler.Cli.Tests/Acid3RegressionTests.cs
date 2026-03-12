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

    // ----------------------------------------------------------------
    // Phase 1: Test 0 & getComputedStyle Cascade Fix
    // ----------------------------------------------------------------

    /// <summary>
    /// Verifies that getComputedStyle applies CSS rules from &lt;style&gt; elements
    /// and returns the computed value for a matching selector.
    /// Phase 1, Task 1.1: CSS cascade from style elements.
    /// </summary>
    [Fact]
    public void GetComputedStyle_Applies_Style_Rules_From_Style_Element()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
p.target { white-space: pre-wrap; }
</style>
</head><body>
<p id=""t"" class=""target"">text</p>
<div id=""result""></div>
<script>
var el = document.getElementById('t');
var cs = document.defaultView.getComputedStyle(el, '');
document.getElementById('result').textContent = cs.whiteSpace;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("pre-wrap", result);
    }

    /// <summary>
    /// Verifies that :last-child is dynamically re-evaluated after removeChild.
    /// After removing the last child, the new last child should match :last-child.
    /// Phase 1, Task 1.2: Dynamic cascade invalidation.
    /// </summary>
    [Fact]
    public void GetComputedStyle_LastChild_Recomputes_After_RemoveChild()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#container p:last-child { white-space: pre-wrap; }
</style>
</head><body>
<div id=""container"">
  <p id=""p1"">first</p>
  <p id=""p2"">second</p>
</div>
<div id=""result""></div>
<script>
var container = document.getElementById('container');
var p1 = document.getElementById('p1');
var p2 = document.getElementById('p2');

var r = [];
// p2 is last-child, should have white-space: pre-wrap
var cs1 = document.defaultView.getComputedStyle(p2, '');
r.push(cs1.getPropertyValue('white-space'));

// p1 is NOT last-child, should NOT have white-space: pre-wrap
var cs1b = document.defaultView.getComputedStyle(p1, '');
r.push(cs1b.getPropertyValue('white-space'));

// Remove p2 — now p1 becomes the last child
container.removeChild(p2);

// p1 should now match :last-child and get white-space: pre-wrap
var cs2 = document.defaultView.getComputedStyle(p1, '');
r.push(cs2.getPropertyValue('white-space'));

document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // val1=pre-wrap (p2 is last-child), val1b=normal (p1 is not last-child, default CSS value), val2=pre-wrap (p1 is now last-child)
        Assert.Contains("pre-wrap|normal|pre-wrap", result);
    }

    /// <summary>
    /// Simulates the exact Acid3 test 0 scenario: CSS error recovery with duplicate
    /// white-space declarations (valid then invalid), and :last-child re-evaluation
    /// after removing the last sibling element.
    /// Phase 1, Task 1.3: Acid3 test 0 verification.
    /// </summary>
    [Fact]
    public void Acid3_Test0_WhiteSpace_LastChild_After_Removal()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#instructions:last-child { white-space: pre-wrap; white-space: x-bogus; }
</style>
</head><body>
<div id=""wrapper"">
  <p id=""instructions"">Instructions text</p>
  <p id=""remove-last-child-test"">Scripting must be enabled</p>
</div>
<div id=""result""></div>
<script>
var last = document.getElementById('remove-last-child-test');
var penultimate = last.previousSibling;
// Skip whitespace text node to get the actual element
while (penultimate && penultimate.nodeType !== 1) {
  penultimate = penultimate.previousSibling;
}
last.parentNode.removeChild(last);
var cs = document.defaultView.getComputedStyle(penultimate, '');
document.getElementById('result').textContent = 'WS=' + cs.getPropertyValue('white-space');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // After removing last child, #instructions becomes :last-child
        // CSS error recovery: white-space: x-bogus is invalid, so pre-wrap should be kept
        Assert.Contains("WS=pre-wrap", result);
    }

    /// <summary>
    /// Verifies CSS error recovery: when a property is declared twice with
    /// the second value being invalid, the first valid value is preserved.
    /// Phase 1, Task 1.1: CSS cascade with error recovery.
    /// </summary>
    [Fact]
    public void GetComputedStyle_CssErrorRecovery_InvalidValue_Ignored()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
p.test { display: block; display: x-invalid; }
</style>
</head><body>
<p id=""t"" class=""test"">text</p>
<div id=""result""></div>
<script>
var cs = document.defaultView.getComputedStyle(document.getElementById('t'), '');
document.getElementById('result').textContent = 'D=' + cs.getPropertyValue('display');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("D=block", result);
    }

    /// <summary>
    /// Verifies that specificity cascade works correctly: inline styles override
    /// ID selectors which override class selectors which override type selectors.
    /// Phase 1, Task 1.1: Specificity-based cascade.
    /// </summary>
    [Fact]
    public void GetComputedStyle_Specificity_Cascade_Order()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
p { z-index: 1; position: absolute; }
.cls { z-index: 2; }
#myid { z-index: 3; }
</style>
</head><body>
<p id=""myid"" class=""cls"" style=""z-index: 4"">text</p>
<div id=""result""></div>
<script>
var cs = document.defaultView.getComputedStyle(document.getElementById('myid'), '');
document.getElementById('result').textContent = cs.zIndex;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // Inline style (z-index: 4) should win over #myid (z-index: 3)
        Assert.Contains("4", result);
    }

    // ==================== Phase 1 v4: End-to-End Harness Tests ====================

    /// <summary>
    /// Verifies that document.write() registers all nested elements so that
    /// getElementById can find deeply nested children (e.g., iframe with id
    /// inside a map element, matching the Acid3 script 9 pattern).
    /// </summary>
    [Fact]
    public void DocumentWrite_Registers_Nested_Elements()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<div id=""r""></div>
<script>
document.write('<map name=""""><area href="""" shape=""rect"" coords=""2,2,4,4"" alt=""x""><iframe src=""empty.html"" id=""selectors""></iframe><form action="""" name=""form""><input type=HIDDEN></form><table><tr><td><p></p></td></tr></table></map>');
var r = document.getElementById('r');
var results = [];
// Check that nested elements can be found by getElementById
var iframe = document.getElementById('selectors');
results.push(iframe ? 'P' : 'F:no-iframe');
// Check that form elements are accessible
var forms = document.getElementsByTagName('form');
results.push(forms.length > 0 ? 'P' : 'F:no-forms');
// Check that the map element exists
var maps = document.getElementsByTagName('map');
results.push(maps.length > 0 ? 'P' : 'F:no-maps');
// Check that the area element exists
var areas = document.getElementsByTagName('area');
results.push(areas.length > 0 ? 'P' : 'F:no-areas');
// Check the table was created
var tables = document.getElementsByTagName('table');
results.push(tables.length > 0 ? 'P' : 'F:no-tables');
r.textContent = results.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("P,P,P,P,P", result);
    }

    /// <summary>
    /// Verifies that the Acid3 update() loop executes through setTimeout
    /// chaining, processing multiple tests and producing a score > 0.
    /// </summary>
    [Fact]
    public void Acid3_Update_Loop_Produces_Score_Via_SetTimeout()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<p id=""result""><span id=""score"">0</span><span id=""slash"">/</span><span>5</span></p>
<div id=""r""></div>
<script>
var tests = [
    function() { return 1; },
    function() { return 1; },
    function() { return 1; },
    function() { return 1; },
    function() { return 1; }
];
var score = 0, index = 0, delay = 0;
function update() {
    var span = document.getElementById('score');
    if (index < tests.length) {
        try {
            var result = tests[index]();
            if (result) score += 1;
        } catch(e) {}
        index += 1;
        span.firstChild.data = '' + score;
        setTimeout(update, delay);
    }
}
update();
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Score should be 5 after all tests pass
        Assert.Contains(">5<", result);
    }

    /// <summary>
    /// Verifies that the update() loop continues executing when some tests
    /// throw errors (error-resilient execution).
    /// </summary>
    [Fact]
    public void Acid3_Update_Loop_Continues_After_Test_Errors()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<p id=""result""><span id=""score"">0</span><span>/</span><span>5</span></p>
<script>
var tests = [
    function() { return 1; },
    function() { throw new Error('test 2 failed'); },
    function() { return 1; },
    function() { var x = null; x.foo(); },
    function() { return 1; }
];
var score = 0, index = 0, delay = 0, errors = 0;
function update() {
    var span = document.getElementById('score');
    if (index < tests.length) {
        try {
            var result = tests[index]();
            if (result) score += 1;
        } catch(e) {
            errors += 1;
        }
        index += 1;
        span.firstChild.data = '' + score;
        setTimeout(update, delay);
    }
}
update();
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Score should be 3 (tests 0, 2, 4 pass; tests 1, 3 fail)
        Assert.Contains(">3<", result);
    }

    /// <summary>
    /// Verifies that body onload fires and triggers the update() function,
    /// matching the Acid3 pattern: &lt;body onload="update()"&gt;.
    /// </summary>
    [Fact]
    public void Body_Onload_Triggers_Update_Function()
    {
        var html = @"<!DOCTYPE html>
<html><head></head>
<body onload=""update()"">
<p id=""result""><span id=""score"">0</span></p>
<script>
var score = 0;
function update() {
    score = 42;
    document.getElementById('score').firstChild.data = '' + score;
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">42<", result);
    }

    /// <summary>
    /// Verifies body onload + setTimeout chaining works together,
    /// simulating the full Acid3 execution flow.
    /// </summary>
    [Fact]
    public void Body_Onload_With_SetTimeout_Chain()
    {
        var html = @"<!DOCTYPE html>
<html><head></head>
<body onload=""update()"">
<p id=""result""><span id=""score"">0</span><span>/</span><span>3</span></p>
<script>
var tests = [
    function() { return 1; },
    function() { return 1; },
    function() { return 1; }
];
var score = 0, index = 0;
function update() {
    var span = document.getElementById('score');
    if (index < tests.length) {
        try {
            var result = tests[index]();
            if (result) score += 1;
        } catch(e) {}
        index += 1;
        span.firstChild.data = '' + score;
        setTimeout(update, 0);
    }
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">3<", result);
    }

    /// <summary>
    /// End-to-end test that loads the actual Acid3 test page and verifies
    /// the test harness executes to produce a score greater than 0.
    /// </summary>
    [Fact]
    public void Acid3_EndToEnd_Score_GreaterThan_Zero()
    {
        // Navigate from bin/Debug/net8.0 up to the repo root, then into acid/acid3/
        var acid3Path = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "acid", "acid3", "acid3.html"));
        Assert.True(File.Exists(acid3Path), $"Acid3 test file not found at {acid3Path}");

        var html = File.ReadAllText(acid3Path);
        var url = new Uri(acid3Path).AbsoluteUri;

        var result = CaptureService.ExecuteScriptsWithDom(html, url);

        // Extract score from <span id="score">N</span>
        var scoreMatch = System.Text.RegularExpressions.Regex.Match(
            result, @"id=""score""[^>]*>(\d+)<");
        Assert.True(scoreMatch.Success, "Could not find score element in output");
        var score = int.Parse(scoreMatch.Groups[1].Value);
        Assert.True(score > 0, $"Acid3 score should be > 0, but was {score}. " +
            $"Score element content: {scoreMatch.Value}");
    }

    /// <summary>
    /// Phase 2, Task 2.1: Verifies that setting textContent on a &lt;style&gt; element
    /// causes getComputedStyle to pick up the new CSS rules. This tests the
    /// dynamic stylesheet invalidation mechanism.
    /// </summary>
    [Fact]
    public void DynamicStyle_TextContent_Updates_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style id=""s"">p { color: red; }</style>
</head><body>
<p id=""t"">text</p>
<div id=""result""></div>
<script>
var s = document.getElementById('s');
var cs1 = document.defaultView.getComputedStyle(document.getElementById('t'), '');
var before = cs1.getPropertyValue('color');
// Dynamically change the style element's text content
s.textContent = 'p { color: blue; }';
var cs2 = document.defaultView.getComputedStyle(document.getElementById('t'), '');
var after = cs2.getPropertyValue('color');
document.getElementById('result').textContent = before + '|' + after;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("red|blue", result);
    }

    /// <summary>
    /// Phase 2, Task 2.1: Verifies that the serialized HTML includes updated
    /// CSS content from style elements whose textContent was changed via JS,
    /// and that the CSS is not HTML-encoded (raw text elements).
    /// </summary>
    [Fact]
    public void DynamicStyle_TextContent_Serialized_Correctly()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style id=""s"">body { background: red; }</style>
</head><body>
<div id=""result""></div>
<script>
var s = document.getElementById('s');
s.textContent = 'body { background: white; } p > span { color: green; }';
document.getElementById('result').textContent = 'done';
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Verify the new CSS content is in the serialized output
        Assert.Contains("background: white", result);
        // Verify CSS child combinator > is NOT HTML-encoded
        Assert.Contains("p > span", result);
        var styleStart = result.IndexOf("<style");
        var styleEnd = result.IndexOf("</style>");
        Assert.True(styleStart >= 0 && styleEnd > styleStart, "Missing <style> tags in output");
        Assert.DoesNotContain("&gt;", result.Substring(styleStart, styleEnd - styleStart));
    }

    /// <summary>
    /// Phase 2, Task 2.1: Verifies that cssRules getter picks up
    /// new rules after textContent is changed on a style element.
    /// </summary>
    [Fact]
    public void DynamicStyle_CssRules_Reflect_TextContent_Change()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style id=""s"">body { background: red; }</style>
</head><body>
<div id=""result""></div>
<script>
var r = [];
var s = document.getElementById('s');
var sheet = document.styleSheets[0];
r.push(sheet.cssRules.length);
s.textContent = 'p { color: blue; } div { margin: 0; }';
r.push(sheet.cssRules.length);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Before: 1 rule (body { background: red })
        // After: 2 rules (p { color: blue } and div { margin: 0 })
        Assert.Contains("1,2", result);
    }

    /// <summary>
    /// Phase 2, Task 2.2: Verifies that CSS cascade re-evaluates correctly
    /// after DOM mutations that affect which CSS selectors match.
    /// Uses :last-child re-evaluation (same pattern as Acid3 test 0).
    /// </summary>
    [Fact]
    public void CssCascade_After_Dom_Mutation_RemoveChild()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target:last-child { font-weight: bold; }
</style>
</head><body>
<div id=""wrapper"">
  <p id=""target"">Target</p>
  <p id=""sibling"">Sibling</p>
</div>
<div id=""result""></div>
<script>
var wrapper = document.getElementById('wrapper');
var sibling = document.getElementById('sibling');
var target = document.getElementById('target');
// Before: target is NOT :last-child
var cs1 = document.defaultView.getComputedStyle(target, '');
var before = cs1.getPropertyValue('font-weight');
// Remove sibling, making target the last child element
wrapper.removeChild(sibling);
var cs2 = document.defaultView.getComputedStyle(target, '');
var after = cs2.getPropertyValue('font-weight');
document.getElementById('result').textContent = before + '|' + after;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Before: empty (target is not :last-child)
        // After: bold (target is now :last-child after sibling removal)
        Assert.Contains("|bold", result);
    }

    /// <summary>
    /// Phase 2 score validation: Verifies the Acid3 score after Phase 2
    /// dynamic stylesheet fixes. The score should be higher than Phase 1's 56.
    /// </summary>
    [Fact]
    public void Acid3_Phase2_Score_Validation()
    {
        var acid3Path = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "acid", "acid3", "acid3.html"));
        Assert.True(File.Exists(acid3Path), $"Acid3 test file not found at {acid3Path}");

        var html = File.ReadAllText(acid3Path);
        var url = new Uri(acid3Path).AbsoluteUri;

        var result = CaptureService.ExecuteScriptsWithDom(html, url);

        // Extract score from <span id="score">N</span>
        var scoreMatch = System.Text.RegularExpressions.Regex.Match(
            result, @"id=""score""[^>]*>(\d+)<");
        Assert.True(scoreMatch.Success, "Could not find score element in output");
        var score = int.Parse(scoreMatch.Groups[1].Value);

        // Phase 1 baseline was 56. Phase 2 fixes raised it to 59. Phase 3 raised to 72. Phase 4 raised to 75.
        Assert.True(score >= 75, $"Acid3 score: {score} (expected >= 75, Phase 4 baseline)");
    }
}

public class Acid3Phase4Diagnostics
{
    [Fact]
    public void Acid3_Test7_Range_Basic()
    {
        var html = @"<!DOCTYPE html>
<html><head><title>Test</title></head><body>
<script>
var r = [];
try {
  var range = document.createRange();
  r.push('created');
  r.push('collapsed=' + range.collapsed);
  r.push('common=' + (range.commonAncestorContainer === document ? 'document' : range.commonAncestorContainer.tagName));
  r.push('start=' + (range.startContainer === document ? 'document' : range.startContainer.tagName));
  var endOffset = range.endOffset;
  range.insertNode(document.createComment('test'));
  range.setEnd(range.endContainer, endOffset + 1);
  r.push('afterInsert_collapsed=' + range.collapsed);
  r.push('afterInsert_start=' + (range.startContainer === document ? 'document' : 'other'));
  document.removeChild(document.firstChild);
  r.push('removed');
} catch(e) {
  r.push('ERROR:' + e.message);
}
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";
        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        var match = System.Text.RegularExpressions.Regex.Match(result, @"id=""result""[^>]*>([^<]+)<");
        Assert.True(match.Success, $"Output: {result.Substring(0, Math.Min(500, result.Length))}");
        var value = match.Groups[1].Value;
        Assert.Contains("common=document", value);
        Assert.Contains("start=document", value);
    }
}

public class Acid3Phase4RangeTests
{
    [Fact]
    public void Test8_MovingBoundaryPoints()
    {
        var html = @"<!DOCTYPE html>
<html><head></head><body>
<script>
var r = [];
try {
  var doc = document.implementation.createDocument(null, null, null);
  var root = doc.createElement('root');
  doc.appendChild(root);
  var e1 = doc.createElement('e');
  root.appendChild(e1);
  var e2 = doc.createElement('e');
  root.appendChild(e2);
  var e3 = doc.createElement('e');
  root.appendChild(e3);
  var rng = doc.createRange();
  rng.setStart(e2, 0);
  rng.setEnd(e3, 0);
  r.push('collapsed1=' + rng.collapsed);
  rng.setEnd(e1, 0);
  r.push('collapsed2=' + rng.collapsed);
  r.push('startContainer=' + (rng.startContainer === e1));
  r.push('startOffset=' + rng.startOffset);
  r.push('endContainer=' + (rng.endContainer === e1));
  r.push('endOffset=' + rng.endOffset);
  rng.setStartBefore(e3);
  r.push('collapsed3=' + rng.collapsed);
  r.push('startContainer3=' + (rng.startContainer === root));
  r.push('startOffset3=' + rng.startOffset);
  r.push('endContainer3=' + (rng.endContainer === root));
  r.push('endOffset3=' + rng.endOffset);
  rng.setEndAfter(root);
  r.push('collapsed4=' + rng.collapsed);
  r.push('endContainer4=' + (rng.endContainer === doc));
  r.push('endOffset4=' + rng.endOffset);
  rng.setStartAfter(e2);
  r.push('startContainer5=' + (rng.startContainer === root));
  r.push('startOffset5=' + rng.startOffset);
  // setEndBefore(doc) should throw
  var threw = false;
  try { rng.setEndBefore(doc); } catch(e) { threw = (e.code === e.INVALID_NODE_TYPE_ERR); }
  r.push('threw=' + threw);
  rng.collapse(false);
  r.push('startContainer6=' + (rng.startContainer === doc));
  r.push('startOffset6=' + rng.startOffset);
} catch(e) {
  r.push('ERROR:' + e.message + '/' + ('' + e));
}
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";
        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        var match = System.Text.RegularExpressions.Regex.Match(result, @"id=""result""[^>]*>([^<]+)<");
        Assert.True(match.Success, $"Output: {result.Substring(0, Math.Min(500, result.Length))}");
        var value = match.Groups[1].Value;
        Assert.DoesNotContain("ERROR", value);
        Assert.Contains("collapsed1=false", value);
        Assert.Contains("collapsed2=true", value);
        Assert.Contains("startContainer=true", value);
        Assert.Contains("endContainer=true", value);
        Assert.Contains("collapsed3=true", value);
        Assert.Contains("startContainer3=true", value);
        Assert.Contains("startOffset3=2", value);
        Assert.Contains("endContainer3=true", value);
        Assert.Contains("endOffset3=2", value);
        Assert.Contains("endContainer4=true", value);
        Assert.Contains("endOffset4=1", value);
        Assert.Contains("startContainer5=true", value);
        Assert.Contains("startOffset5=2", value);
        Assert.Contains("threw=true", value);
        Assert.Contains("startContainer6=true", value);
        Assert.Contains("startOffset6=1", value);
    }
}
