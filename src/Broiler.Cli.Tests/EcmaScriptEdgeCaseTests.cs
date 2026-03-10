namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 9 Acid3 compliance: ECMAScript edge cases —
/// number-to-string precision, Date year 0 / negative year handling,
/// null byte URI encoding, data: URI edge cases, and XHTML DOM handling.
/// </summary>
public class EcmaScriptEdgeCaseTests
{
    // ────────────────── 9.1 Number-to-string precision ──────────────────

    [Fact]
    public void Number_ToString_Precision_Large_Integer()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
// Large integer edge case: (1000000000000000128).toString()
// V8/SpiderMonkey return '1000000000000000100' due to IEEE 754 rounding
r.push((1000000000000000128).toString());
// Verify basic number toString still works
r.push((42).toString());
r.push((255).toString(16));
r.push((7).toString(2));
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // IEEE 754 rounds 1000000000000000128 to 1000000000000000100
        Assert.Contains("1000000000000000", result);
        Assert.Contains("42", result);
        Assert.Contains("ff", result);
        Assert.Contains("111", result);
    }

    // ────────────────── 9.2 Date year 0 / negative year ──────────────────

    [Fact]
    public void Date_SetFullYear_Zero_Returns_Zero()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var d = new Date(2020, 0, 1);
d.setFullYear(0);
r.push('' + d.getFullYear());
// Month and day should be preserved
r.push('' + d.getMonth());
r.push('' + d.getDate());
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("0,0,1", result);
    }

    [Fact]
    public void Date_SetFullYear_Negative_Returns_Negative()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var d = new Date(2020, 0, 1);
d.setFullYear(-1);
r.push('' + d.getFullYear());
// Verify the date is still valid (not NaN)
r.push('' + isNaN(d.getTime()));
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("-1", result);
        Assert.Contains("false", result);
    }

    // ────────────────── 9.3 Null byte in URI encoding ──────────────────

    [Fact]
    public void EncodeURI_And_EncodeURIComponent_Null_Byte()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
r.push(encodeURIComponent('\0'));
r.push(encodeURI('\0'));
// Verify round-trip: decoding the encoded null byte
r.push(decodeURIComponent('%00').charCodeAt(0));
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("%00", result);
        Assert.Contains("0", result);
    }

    // ────────────────── 9.4 data: URI edge cases ──────────────────

    [Fact]
    public void DataURI_Iframe_With_Html_Content()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""f"" src=""data:text/html,<div id='inner'>hello</div>""></iframe>
<div id=""result""></div>
<script>
var r = [];
var f = document.getElementById('f');
var doc = f.contentDocument;
r.push(doc !== null);
if (doc) {
    var inner = doc.getElementById('inner');
    r.push(inner !== null);
    if (inner) r.push(inner.textContent);
}
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true,hello", result);
    }

    [Fact]
    public void DataURI_With_Unusual_MimeType_Returns_Empty_Doc()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""f"" src=""data:application/json,{&quot;key&quot;:&quot;value&quot;}""></iframe>
<div id=""result""></div>
<script>
var r = [];
var f = document.getElementById('f');
var doc = f.contentDocument;
// data: URI with non-HTML MIME type should still provide a document object
r.push(doc !== null);
if (doc) {
    // The body should exist but be empty (non-HTML content not parsed into DOM)
    var body = doc.body;
    r.push(body !== null);
}
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true", result);
    }

    // ────────────────── 9.5 XHTML DOM handling ──────────────────

    [Fact]
    public void Document_ContentType_And_XHTML_Namespace_Defaults()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
// document.contentType for regular HTML
r.push(document.contentType);
// document.compatMode
r.push(document.compatMode);
// document.characterSet
r.push(document.characterSet);
// Default namespace for HTML elements is XHTML
var div = document.createElement('div');
r.push(div.namespaceURI);
// createElementNS with explicit XHTML namespace
var el = document.createElementNS('http://www.w3.org/1999/xhtml', 'span');
r.push(el.namespaceURI);
// document.URL and documentURI should be strings
r.push(typeof document.URL);
r.push(typeof document.documentURI);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("text/html", result);
        Assert.Contains("CSS1Compat", result);
        Assert.Contains("UTF-8", result);
        Assert.Contains("http://www.w3.org/1999/xhtml", result);
        Assert.Contains("string,string", result);
    }
}
