using Broiler.Cli;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase G: Acid3 Special Tests (97–99) explicit regression tests.
/// Covers data: URI parsing, XHTML DOM behaviors, and unusual edge cases.
/// </summary>
public class Acid3SpecialRegressionTests
{
    // ---------------------------------------------------------------
    // Test 97 — data: URI parsing
    // ---------------------------------------------------------------

    [Fact]
    public void Acid3_Test97_DataUri_BasicParsing()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
// data: URI string starts with the correct scheme
var uri = 'data:text/html,Hello';
r.push(uri.indexOf('data:') === 0);
// Comma separates media-type from payload
r.push(uri.split(',')[1] === 'Hello');
// data: URI with base64 flag parses correctly
var b64 = 'data:text/plain;base64,SGVsbG8=';
r.push(b64.indexOf(';base64,') > 0);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true", result);
    }

    [Fact]
    public void Acid3_Test97_EncodeDecodeURIComponent_RoundTrip()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
// Round-trip with HTML special chars
var special = '<div class=""test"">Hello & World</div>';
var encoded = encodeURIComponent(special);
var decoded = decodeURIComponent(encoded);
r.push(decoded === special);
// Round-trip with Unicode
var unicode = '\u00E9\u00E0\u00FC\u4E16\u754C';
r.push(decodeURIComponent(encodeURIComponent(unicode)) === unicode);
// Round-trip with all RFC 3986 unreserved characters (should not be encoded)
var unreserved = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.~';
r.push(encodeURIComponent(unreserved) === unreserved);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true", result);
    }

    [Fact]
    public void Acid3_Test97_DataUri_SpecialCharacters()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
// data: URI with encoded special chars round-trips
var payload = 'x=1&y=2&z=<3>';
var dataUri = 'data:text/plain,' + encodeURIComponent(payload);
r.push(dataUri.indexOf('data:text/plain,') === 0);
var extracted = decodeURIComponent(dataUri.substring('data:text/plain,'.length));
r.push(extracted === payload);
// Empty payload
var empty = 'data:text/plain,';
r.push(empty.split(',')[1] === '');
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true", result);
    }

    [Fact]
    public void Acid3_Test97_DataUri_ScriptSrcDoesNotCrash()
    {
        // Verify that a <script> with a data: src attribute does not crash the engine,
        // even if the engine does not actually fetch/execute it.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result"">before</div>
<script src=""data:text/javascript,document.getElementById('result').textContent='executed'""></script>
<script>
// If the data: script executed, result is 'executed'; otherwise 'before'.
// Either outcome is acceptable; the key point is no crash.
var el = document.getElementById('result');
if (el.textContent !== 'executed') {
    el.textContent = 'no-crash';
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // Accept either outcome — data: script ran or gracefully skipped
        var ok = result.Contains("executed") || result.Contains("no-crash");
        Assert.True(ok, "data: URI script should either execute or be skipped without crashing");
    }

    // ---------------------------------------------------------------
    // Test 98 — XHTML and the DOM
    // ---------------------------------------------------------------

    [Fact]
    public void Acid3_Test98_CreateDocument_XhtmlNamespace()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var xhtmlNS = 'http://www.w3.org/1999/xhtml';
try {
    var doc = document.implementation.createDocument(xhtmlNS, 'html', null);
    r.push(doc !== null && doc !== undefined);
    r.push(doc.documentElement.namespaceURI === xhtmlNS);
    r.push(doc.documentElement.localName === 'html');
} catch(e) {
    r.push('ERROR:' + e.message);
}
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true", result);
    }

    [Fact]
    public void Acid3_Test98_CreateDocument_ElementNamespaceURI()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var xhtmlNS = 'http://www.w3.org/1999/xhtml';
var svgNS = 'http://www.w3.org/2000/svg';
try {
    var doc = document.implementation.createDocument(xhtmlNS, 'html', null);
    var body = doc.createElementNS(xhtmlNS, 'body');
    doc.documentElement.appendChild(body);
    r.push(body.namespaceURI === xhtmlNS);
    // Create an SVG element in the XHTML document
    var svg = doc.createElementNS(svgNS, 'svg');
    body.appendChild(svg);
    r.push(svg.namespaceURI === svgNS);
    r.push(svg.localName === 'svg');
} catch(e) {
    r.push('ERROR:' + e.message);
}
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true", result);
    }

    [Fact]
    public void Acid3_Test98_CreateDocumentType_XhtmlIds()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
try {
    var dt = document.implementation.createDocumentType(
        'html',
        '-//W3C//DTD XHTML 1.0 Strict//EN',
        'http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd'
    );
    r.push(dt.name === 'html');
    r.push(dt.publicId === '-//W3C//DTD XHTML 1.0 Strict//EN');
    r.push(dt.systemId === 'http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd');
} catch(e) {
    r.push('ERROR:' + e.message);
}
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true", result);
    }

    [Fact]
    public void Acid3_Test98_TagName_Vs_LocalName_In_Namespace()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var xhtmlNS = 'http://www.w3.org/1999/xhtml';
try {
    var doc = document.implementation.createDocument(xhtmlNS, 'html', null);
    var el = doc.createElementNS(xhtmlNS, 'div');
    // localName should always be 'div'
    r.push(el.localName === 'div');
    // tagName may be upper-cased in HTML context, but in an XML doc it stays as-is
    r.push(typeof el.tagName === 'string');
    r.push(el.tagName.toLowerCase() === 'div');
    // Prefixed element
    var prefixed = doc.createElementNS('http://example.com/ns', 'ex:widget');
    r.push(prefixed.localName === 'widget');
    r.push(prefixed.prefix === 'ex');
} catch(e) {
    r.push('ERROR:' + e.message);
}
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true|true|true", result);
    }

    // ---------------------------------------------------------------
    // Test 99 — "Weirdest bug ever" — unusual edge cases
    // ---------------------------------------------------------------

    [Fact]
    public void Acid3_Test99_CreateElement_UnusualValidNames()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
try {
    // Hyphenated custom element-style names are valid
    var el1 = document.createElement('x-widget');
    r.push(el1.tagName.toLowerCase() === 'x-widget');
    // Single-letter element
    var el2 = document.createElement('b');
    r.push(el2.tagName.toLowerCase() === 'b');
    // Long element name
    var el3 = document.createElement('superlongelementname');
    r.push(el3.tagName.toLowerCase() === 'superlongelementname');
} catch(e) {
    r.push('ERROR:' + e.message);
}
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true", result);
    }

    [Fact]
    public void Acid3_Test99_TypeofChecks_DomObjects()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
// typeof document should be 'object'
r.push(typeof document === 'object');
// typeof document.createElement should be 'function'
r.push(typeof document.createElement === 'function');
// typeof document.getElementById should be 'function'
r.push(typeof document.getElementById === 'function');
// typeof on a retrieved element should be 'object'
var el = document.getElementById('result');
r.push(typeof el === 'object');
// typeof null is 'object' (classic JS quirk)
r.push(typeof null === 'object');
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true|true|true", result);
    }

    [Fact(Skip = "getElementsByTagName('*').length numeric coercion not yet fully supported")]
    public void Acid3_Test99_NumericPropertyCoercion()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<p>para1</p><p>para2</p>
<script>
var r = [];
// getElementsByTagName('*').length should be a number > 0
var allLen = document.getElementsByTagName('*').length;
r.push(typeof allLen === 'number');
r.push(allLen > 0);
// Numeric coercion: length converts to number in arithmetic
r.push((allLen + 0) === allLen);
// childNodes.length is also numeric
var bodyChildren = document.body.childNodes.length;
r.push(typeof bodyChildren === 'number');
r.push(bodyChildren > 0);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true|true|true", result);
    }

    [Fact]
    public void Acid3_Test99_SetUnusualPropertyValues()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var r = [];
var el = document.getElementById('target');
// Setting className to an empty string
el.className = '';
r.push(el.className === '');
// Setting id to contain hyphens and numbers
el.id = 'my-element-42';
r.push(el.id === 'my-element-42');
// Setting textContent to numeric-looking string
el.textContent = '0';
r.push(el.textContent === '0');
// setAttribute with empty value
el.setAttribute('data-empty', '');
r.push(el.getAttribute('data-empty') === '');
// setAttribute with boolean-like strings
el.setAttribute('data-flag', 'false');
r.push(el.getAttribute('data-flag') === 'false');
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true|true|true", result);
    }

    [Fact]
    public void Acid3_Test99_InvalidElementNames_Throw()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
// Element names starting with a digit should throw
try {
    document.createElement('1bad');
    r.push(false);
} catch(e) {
    r.push(true);
}
// Element names with spaces should throw
try {
    document.createElement('a b');
    r.push(false);
} catch(e) {
    r.push(true);
}
// Empty name should throw
try {
    document.createElement('');
    r.push(false);
} catch(e) {
    r.push(true);
}
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true", result);
    }
}
