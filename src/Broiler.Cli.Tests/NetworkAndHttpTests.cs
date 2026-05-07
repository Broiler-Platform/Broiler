using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 8 Acid3 compliance: Network and HTTP compliance —
/// fetch() response headers, XMLHttpRequest enhancements (getResponseHeader,
/// getAllResponseHeaders, abort, overrideMimeType), Content-Type handling,
/// same-origin policy, iframe/object contentDocument behavior, and
/// HTTP method support.
/// </summary>
public class NetworkAndHttpTests
{
    // ────────────────── fetch() response headers ──────────────────

    [Fact]
    public void Fetch_Response_Has_Headers_Object()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
fetch('http://example.com').then(function(response) {
    r.push(typeof response.headers);
    r.push(typeof response.headers.get);
    r.push(typeof response.headers.has);
    r.push(typeof response.headers.forEach);
    document.getElementById('result').textContent = r.join(',');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        // If fetch fails (no network), headers should still exist as an object
        Assert.Contains("object", result);
    }

    [Fact]
    public void Fetch_Response_Has_Status_And_StatusText()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
fetch('http://example.com').then(function(response) {
    r.push(typeof response.status);
    r.push(typeof response.statusText);
    r.push(typeof response.ok);
    document.getElementById('result').textContent = r.join(',');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("number", result);
    }

    [Fact]
    public void Fetch_Response_Has_Url_Property()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
fetch('http://example.com').then(function(response) {
    r.push(typeof response.url === 'string');
    document.getElementById('result').textContent = r.join(',');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void Fetch_Response_Has_Type_And_BodyUsed()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
fetch('http://example.com').then(function(response) {
    r.push(typeof response.type === 'string');
    r.push(typeof response.redirected === 'boolean');
    document.getElementById('result').textContent = r.join(',');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void Fetch_Response_Headers_Get_Returns_Null_For_Missing()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
fetch('http://example.com').then(function(response) {
    var v = response.headers.get('X-Nonexistent-Header');
    document.getElementById('result').textContent = String(v);
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("null", result);
    }

    [Fact]
    public void Fetch_Response_Has_Clone_Method()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
fetch('http://example.com').then(function(response) {
    var r = [];
    r.push(typeof response.clone === 'function');
    document.getElementById('result').textContent = r.join(',');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void Fetch_Response_Has_ArrayBuffer_Method()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
fetch('http://example.com').then(function(response) {
    var r = [];
    r.push(typeof response.arrayBuffer === 'function');
    document.getElementById('result').textContent = r.join(',');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void Fetch_Response_ArrayBuffer_Returns_ArrayBuffer_Bytes_And_Sets_BodyUsed()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var response = new Response('ABC');
response.arrayBuffer().then(function(buffer) {
    var view = new Uint8Array(buffer);
    document.getElementById('result').textContent = [
        response.bodyUsed === true,
        buffer instanceof ArrayBuffer,
        buffer.byteLength,
        view[0],
        view[1],
        view[2]
    ].join('|');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|3|65|66|67", result);
    }

    [Fact]
    public void Fetch_Headers_Constructor_Supports_Common_Mutations()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var headers = new Headers({ Accept: 'text/plain' });
headers.append('X-Test', 'one');
headers.append('X-Test', 'two');
headers.set('Content-Type', 'application/json');
var beforeDelete = headers.get('x-test');
headers.delete('X-Test');
var r = [];
r.push(headers.get('accept'));
r.push(headers.get('content-type'));
r.push(beforeDelete);
r.push(headers.has('x-test'));
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("text/plain|application/json|one, two|false", result);
    }

    [Fact]
    public void Fetch_FormData_Constructor_Supports_Common_Mutations()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var formData = new FormData({ name: 'broiler' });
formData.append('name', 'oven bird');
formData.set('count', 2);
var names = formData.getAll('name').join(',');
formData.delete('count');
document.getElementById('result').textContent = [
    typeof FormData === 'function',
    formData.get('name'),
    names,
    formData.has('count'),
    formData.toString().split('&').join(';')
].join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|broiler|broiler,oven bird|false|name=broiler;name=oven+bird", result);
    }

    [Fact]
    public void Fetch_Request_Constructor_Exposes_Url_Method_Headers_And_Body()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var request = new Request('http://example.com/data', {
    method: 'post',
    headers: { Accept: 'application/json' },
    body: 'payload'
});
var r = [];
r.push(request.url);
r.push(request.method);
r.push(request.headers.get('accept'));
request.text().then(function(text) {
    r.push(text);
    document.getElementById('result').textContent = r.join('|');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("http://example.com/data|POST|application/json|payload", result);
    }

    [Fact]
    public void Fetch_Request_Constructor_Exposes_Default_Request_Properties()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var request = new Request('http://example.com/data');
document.getElementById('result').textContent = [
    request.mode,
    request.credentials,
    request.cache,
    request.redirect,
    request.referrer,
    request.integrity
].join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("cors|same-origin|default|follow|about:client|", result);
    }

    [Fact]
    public void Fetch_Request_Constructor_Exposes_Explicit_Request_Properties()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var request = new Request('http://example.com/data', {
    mode: 'same-origin',
    credentials: 'include',
    cache: 'no-store',
    redirect: 'manual',
    referrer: 'http://example.com/source',
    integrity: 'sha256-test'
});
document.getElementById('result').textContent = [
    request.mode,
    request.credentials,
    request.cache,
    request.redirect,
    request.referrer,
    request.integrity
].join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("same-origin|include|no-store|manual|http://example.com/source|sha256-test", result);
    }

    [Fact]
    public void Fetch_Request_Clone_Preserves_Request_Properties()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var request = new Request('http://example.com/data', {
    method: 'POST',
    body: 'payload',
    mode: 'same-origin',
    credentials: 'include',
    cache: 'reload',
    redirect: 'error',
    referrer: 'http://example.com/source',
    integrity: 'sha256-test'
});
var clone = request.clone();
document.getElementById('result').textContent = [
    clone.mode,
    clone.credentials,
    clone.cache,
    clone.redirect,
    clone.referrer,
    clone.integrity,
    clone.method,
    clone.headers.get('content-type') === null,
    clone.bodyUsed === false
].join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("same-origin|include|reload|error|http://example.com/source|sha256-test|POST|true|true", result);
    }

    [Fact]
    public void Fetch_Request_ArrayBuffer_Returns_ArrayBuffer_Bytes_And_Sets_BodyUsed()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var request = new Request('http://example.com/data', { method: 'POST', body: 'ABC' });
request.arrayBuffer().then(function(buffer) {
    var view = new Uint8Array(buffer);
    document.getElementById('result').textContent = [
        request.bodyUsed === true,
        buffer instanceof ArrayBuffer,
        buffer.byteLength,
        view[0],
        view[1],
        view[2]
    ].join('|');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|true|3|65|66|67", result);
    }

    [Fact]
    public void Fetch_Request_Blob_Returns_Blob_Text_Type_Size_And_Sets_BodyUsed()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var request = new Request('http://example.com/data', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: 'broiler'
});
request.blob().then(function(blob) {
    blob.text().then(function(text) {
        document.getElementById('result').textContent = [
            request.bodyUsed === true,
            typeof blob.text === 'function',
            blob.size,
            blob.type,
            text
        ].join('|');
    });
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|true|7|application/json|broiler", result);
    }

    [Fact]
    public void Fetch_Request_Clone_Preserves_Body_Without_Consuming_Original()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var request = new Request('http://example.com/data', {
    method: 'POST',
    body: 'payload'
});
var clone = request.clone();
clone.text().then(function(cloneText) {
    request.text().then(function(originalText) {
        document.getElementById('result').textContent = [
            cloneText,
            originalText,
            request.bodyUsed === true,
            clone.bodyUsed === true
        ].join('|');
    });
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("payload|payload|true|true", result);
    }

    [Fact]
    public void Fetch_Request_Clone_Throws_After_Body_Is_Used()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var request = new Request('http://example.com/data', {
    method: 'POST',
    body: 'payload'
});
request.text().then(function() {
    try {
        request.clone();
        document.getElementById('result').textContent = 'NO_THROW';
    } catch (e) {
        document.getElementById('result').textContent = e.message;
    }
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("body is already used", result);
    }

    [Fact]
    public void Fetch_Request_FormData_Parses_Body_And_Sets_BodyUsed()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var request = new Request('http://example.com/data', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: 'name=broiler&name=oven+bird&count=2'
});
request.formData().then(function(value) {
    document.getElementById('result').textContent = [
        request.bodyUsed === true,
        typeof value.get === 'function',
        value.get('name'),
        value.getAll('name').join(','),
        value.get('count')
    ].join('|');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|true|broiler|broiler,oven bird|2", result);
    }

    [Fact]
    public void Fetch_Request_Json_Parses_Body_And_Sets_BodyUsed()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var request = new Request('http://example.com/data', {
    method: 'POST',
    body: '{""name"":""broiler"",""count"":2}'
});
request.json().then(function(value) {
    document.getElementById('result').textContent = [
        request.bodyUsed === true,
        value.name,
        value.count
    ].join('|');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|broiler|2", result);
    }

    [Fact]
    public void Fetch_Request_Body_Readers_Throw_After_Body_Is_Used()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var request = new Request('http://example.com/data', {
    method: 'POST',
    body: 'payload'
});
request.text().then(function() {
    try {
        request.arrayBuffer();
        document.getElementById('result').textContent = 'NO_THROW';
    } catch (e) {
        document.getElementById('result').textContent = e.message;
    }
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("body is already used", result);
    }

    [Fact]
    public void Fetch_Request_Body_Readers_Set_BodyUsed_Immediately_And_Block_Same_Turn_Double_Reads()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var request = new Request('http://example.com/data', {
    method: 'POST',
    body: 'payload'
});
var firstRead = request.text();
var r = [];
r.push(request.bodyUsed === true);
try {
    request.json();
    r.push('NO_THROW');
} catch (e) {
    r.push(e.message);
}
firstRead.then(function(text) {
    r.push(text);
    document.getElementById('result').textContent = r.join('|');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|Failed to execute body reader on 'Request': body is already used.|payload", result);
    }

    [Fact]
    public void Fetch_Response_Constructor_Supports_Status_Headers_And_Text()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var response = new Response('created', {
    status: 201,
    statusText: 'Created',
    headers: { 'Content-Type': 'text/plain' },
    url: 'http://example.com/data'
});
var r = [];
r.push(response.ok);
r.push(response.status);
r.push(response.statusText);
r.push(response.headers.get('content-type'));
r.push(typeof response.clone === 'function');
response.text().then(function(text) {
    r.push(text);
    document.getElementById('result').textContent = r.join('|');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|201|Created|text/plain|true|created", result);
    }

    [Fact]
    public void Fetch_Response_Clone_Preserves_Body_Without_Consuming_Original()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var response = new Response('created', {
    headers: { 'Content-Type': 'text/plain' }
});
var clone = response.clone();
clone.text().then(function(cloneText) {
    response.text().then(function(originalText) {
        document.getElementById('result').textContent = [
            cloneText,
            originalText,
            response.bodyUsed === true,
            clone.bodyUsed === true
        ].join('|');
    });
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("created|created|true|true", result);
    }

    [Fact]
    public void Fetch_Response_Clone_Throws_After_Body_Is_Used()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var response = new Response('created', {
    headers: { 'Content-Type': 'text/plain' }
});
response.text().then(function() {
    try {
        response.clone();
        document.getElementById('result').textContent = 'NO_THROW';
    } catch (e) {
        document.getElementById('result').textContent = e.message;
    }
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("body is already used", result);
    }

    [Fact]
    public void Fetch_Response_Blob_Returns_Blob_Text_Type_Size_And_Sets_BodyUsed()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var response = new Response('created', {
    headers: { 'Content-Type': 'text/plain' }
});
response.blob().then(function(blob) {
    blob.text().then(function(text) {
        document.getElementById('result').textContent = [
            response.bodyUsed === true,
            typeof blob.text === 'function',
            blob.size,
            blob.type,
            text
        ].join('|');
    });
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|true|7|text/plain|created", result);
    }

    [Fact]
    public void Fetch_Response_FormData_Parses_Body_And_Sets_BodyUsed()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var response = new Response('name=broiler&name=oven+bird&count=2', {
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
});
response.formData().then(function(value) {
    document.getElementById('result').textContent = [
        response.bodyUsed === true,
        typeof value.get === 'function',
        value.get('name'),
        value.getAll('name').join(','),
        value.get('count')
    ].join('|');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|true|broiler|broiler,oven bird|2", result);
    }

    [Fact]
    public void Fetch_Response_Body_Readers_Throw_After_Body_Is_Used()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var response = new Response('{""name"":""broiler""}', {
    headers: { 'Content-Type': 'application/json' }
});
response.text().then(function() {
    try {
        response.json();
        document.getElementById('result').textContent = 'NO_THROW';
    } catch (e) {
        document.getElementById('result').textContent = e.message;
    }
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("body is already used", result);
    }

    [Fact]
    public void Fetch_Response_Body_Readers_Set_BodyUsed_Immediately_And_Block_Same_Turn_Double_Reads()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var response = new Response('{""name"":""broiler""}', {
    headers: { 'Content-Type': 'application/json' }
});
var firstRead = response.text();
var r = [];
r.push(response.bodyUsed === true);
try {
    response.json();
    r.push('NO_THROW');
} catch (e) {
    r.push(e.message);
}
firstRead.then(function(text) {
    r.push(text);
    document.getElementById('result').textContent = r.join('|');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|Failed to execute body reader on 'Response': body is already used.|", result);
    }

    [Fact]
    public void Fetch_Response_Json_InvalidJson_Throws_Clear_Error()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!DOCTYPE html><html><body></body></html>", "file:///test.html");

        var exception = Assert.Throws<JSException>(() =>
            context.Eval("var response = new Response('{invalid json'); response.json().then(function() {});"));

        Assert.Contains("Failed to parse response body as JSON:", exception.Message);
        Assert.True(context.Eval("response.bodyUsed").BooleanValue);
    }

    // ────────────────── fetch() method support ──────────────────

    [Fact]
    public void Fetch_Supports_Options_Method()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
try {
    fetch('http://example.com', { method: 'POST' }).then(function(response) {
        document.getElementById('result').textContent = 'POST_OK';
    });
} catch(e) {
    document.getElementById('result').textContent = 'ERROR: ' + e.message;
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("POST_OK", result);
    }

    [Fact]
    public void Fetch_Catch_Does_Not_Throw()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
try {
    fetch('http://example.com')
        .then(function(r) { return r; })
        .catch(function(e) { return e; });
    document.getElementById('result').textContent = 'CATCH_OK';
} catch(e) {
    document.getElementById('result').textContent = 'ERROR: ' + e.message;
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("CATCH_OK", result);
    }

    [Fact]
    public void Fetch_Accepts_Request_Instance_And_Headers_Object()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var request = new Request('http://example.com/request-object', {
    method: 'POST',
    headers: new Headers({ accept: 'application/json' }),
    body: 'payload'
});
fetch(request).then(function(response) {
    var r = [];
    r.push(response.url === 'http://example.com/request-object');
    r.push(typeof response.headers.get === 'function');
    document.getElementById('result').textContent = r.join('|');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|true", result);
    }

    [Fact]
    public void Fetch_Request_Constructor_Preserves_AbortSignal()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var controller = new AbortController();
var request = new Request('http://example.com/request-object', {
    signal: controller.signal
});
document.getElementById('result').textContent = [
    request.signal === controller.signal,
    request.signal.aborted === false
].join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|true", result);
    }

    [Fact]
    public void Fetch_With_PreAborted_Signal_Rejects_With_AbortError()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var controller = new AbortController();
controller.abort();
fetch('http://example.com/aborted', { signal: controller.signal })
    .then(function() {
        document.getElementById('result').textContent = 'THEN';
    })
    .catch(function(error) {
        document.getElementById('result').textContent = [
            error.name,
            error.message
        ].join('|');
    });
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("AbortError|The operation was aborted.", result);
    }

    [Fact]
    public void Fetch_AbortSignal_Dispatches_Abort_Event_Once_And_Preserves_Custom_Reason()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var controller = new AbortController();
var removedCalls = 0;
function removedListener() { removedCalls++; }
controller.signal.addEventListener('abort', removedListener);
controller.signal.removeEventListener('abort', removedListener);
controller.signal.addEventListener('abort', function(event) {
    document.getElementById('result').textContent = [
        event.type,
        this === controller.signal,
        controller.signal.aborted,
        controller.signal.reason,
        removedCalls
    ].join('|');
});
controller.abort('custom-reason');
controller.abort('second-reason');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("abort|true|true|custom-reason|0", result);
    }

    [Fact]
    public void Fetch_AbortSignal_ThrowIfAborted_Throws_Custom_Reason()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var controller = new AbortController();
controller.abort('custom-reason');
try {
    controller.signal.throwIfAborted();
    document.getElementById('result').textContent = 'NO_THROW';
} catch (e) {
    document.getElementById('result').textContent = String(e);
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("custom-reason", result);
    }

    // ────────────────── XMLHttpRequest enhancements ──────────────────

    [Fact]
    public void XHR_Has_GetResponseHeader_Method()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
var r = [];
r.push(typeof xhr.getResponseHeader === 'function');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void XHR_Has_GetAllResponseHeaders_Method()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
var r = [];
r.push(typeof xhr.getAllResponseHeaders === 'function');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void XHR_GetResponseHeader_Returns_Null_Before_Send()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
xhr.open('GET', 'http://example.com');
var v = xhr.getResponseHeader('Content-Type');
document.getElementById('result').textContent = String(v);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("null", result);
    }

    [Fact]
    public void XHR_Has_OverrideMimeType_Method()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
var r = [];
r.push(typeof xhr.overrideMimeType === 'function');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void XHR_Has_Abort_Method()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
var r = [];
r.push(typeof xhr.abort === 'function');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void XHR_Abort_Resets_State()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
xhr.open('GET', 'http://example.com');
var r = [];
r.push(xhr.readyState === 1);
xhr.abort();
r.push(xhr.readyState === 0);
r.push(xhr.status === 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void XHR_Has_ResponseType_Property()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
var r = [];
r.push(xhr.responseType === '');
r.push(xhr.responseURL === '');
r.push(xhr.responseXML === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void XHR_Has_Event_Handler_Properties()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
var r = [];
r.push(xhr.onload === null);
r.push(xhr.onerror === null);
r.push(xhr.onabort === null);
r.push(xhr.onprogress === null);
r.push(xhr.onloadstart === null);
r.push(xhr.onloadend === null);
r.push(xhr.ontimeout === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void XHR_Has_EventTarget_Methods()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
var r = [];
r.push(typeof xhr.addEventListener === 'function');
r.push(typeof xhr.removeEventListener === 'function');
r.push(typeof xhr.dispatchEvent === 'function');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void XHR_Has_Upload_EventTarget()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
var r = [];
r.push(!!xhr.upload);
r.push(typeof xhr.upload.addEventListener === 'function');
r.push(typeof xhr.upload.removeEventListener === 'function');
r.push(typeof xhr.upload.dispatchEvent === 'function');
r.push(xhr.upload.onloadstart === null);
r.push(xhr.upload.onprogress === null);
r.push(xhr.upload.onload === null);
r.push(xhr.upload.onloadend === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void XHR_Has_Static_State_Constants()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
r.push(XMLHttpRequest.UNSENT === 0);
r.push(XMLHttpRequest.OPENED === 1);
r.push(XMLHttpRequest.HEADERS_RECEIVED === 2);
r.push(XMLHttpRequest.LOADING === 3);
r.push(XMLHttpRequest.DONE === 4);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void XHR_Open_Fires_ReadyStateChange()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
var fired = false;
xhr.onreadystatechange = function() { fired = true; };
xhr.open('GET', 'http://example.com');
var r = [];
r.push(fired);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void XHR_Open_Resets_ResponseText()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
xhr.open('GET', 'http://example.com');
var r = [];
r.push(xhr.responseText === '');
r.push(xhr.responseURL === '');
r.push(typeof xhr.getAllResponseHeaders === 'function');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void XHR_WithCredentials_Default_False()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
var r = [];
r.push(xhr.withCredentials === false);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void XHR_ReadyStateChange_Fires_For_Loading_State_Before_Done()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            resolve(new Response('payload', {
                status: 200,
                headers: { 'Content-Type': 'text/plain' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
var readyStates = [];
xhr.onreadystatechange = function() {
    readyStates.push(xhr.readyState);
};
xhr.open('GET', 'http://example.com/data');
xhr.send();
fetch = window.fetch = originalFetch;
document.getElementById('result').textContent = readyStates.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("1,2,3,4", result);
    }

    [Fact]
    public void XHR_AddEventListener_Dispatches_Lifecycle_And_Progress_Events()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            resolve(new Response('payload', {
                status: 200,
                headers: { 'Content-Type': 'text/plain' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
var events = [];
xhr.addEventListener('readystatechange', function(event) {
    events.push('rs:' + xhr.readyState + ':' + (event.target === xhr) + ':' + (event.currentTarget === xhr));
});
xhr.addEventListener('loadstart', function(event) {
    events.push(['loadstart', event.lengthComputable, event.loaded, event.total, event.target === xhr].join(':'));
});
xhr.addEventListener('progress', function(event) {
    events.push(['progress', event.lengthComputable, event.loaded, event.total, event.currentTarget === xhr].join(':'));
});
xhr.addEventListener('load', function(event) {
    events.push(['load', event.lengthComputable, event.loaded, event.total, event.target === xhr].join(':'));
});
xhr.addEventListener('loadend', function(event) {
    events.push(['loadend', event.lengthComputable, event.loaded, event.total, event.target === xhr].join(':'));
    document.getElementById('result').textContent = events.join('|');
});
xhr.open('GET', 'http://example.com/data');
xhr.send();
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("rs:1:true:true", result);
        Assert.Contains("loadstart:false:0:0:true", result);
        Assert.Contains("rs:2:true:true", result);
        Assert.Contains("rs:3:true:true", result);
        Assert.Contains("progress:true:7:7:true", result);
        Assert.Contains("rs:4:true:true", result);
        Assert.Contains("load:true:7:7:true", result);
        Assert.Contains("loadend:true:7:7:true", result);
    }

    [Fact]
    public void XHR_Load_Event_Preserves_Property_Handler_Alongside_Listeners()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            resolve(new Response('payload', {
                status: 200,
                headers: { 'Content-Type': 'text/plain' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
var order = [];
xhr.onload = function() { order.push('property'); };
xhr.addEventListener('load', function() { order.push('listener'); });
xhr.addEventListener('loadend', function() {
    document.getElementById('result').textContent = order.join(',');
});
xhr.open('GET', 'http://example.com/data');
xhr.send();
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("property,listener", result);
    }

    [Fact]
    public void XHR_Upload_AddEventListener_Dispatches_Upload_Progress_Events()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function(url, opts) {
    return {
        then: function(resolve) {
            resolve(new Response('ok', {
                status: 200,
                headers: { 'Content-Type': 'text/plain' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
var events = [];
xhr.upload.addEventListener('loadstart', function(event) {
    events.push(['upload-loadstart', event.lengthComputable, event.loaded, event.total, event.target === xhr.upload, event.currentTarget === xhr.upload].join(':'));
});
xhr.upload.addEventListener('progress', function(event) {
    events.push(['upload-progress', event.lengthComputable, event.loaded, event.total, event.target === xhr.upload, event.currentTarget === xhr.upload].join(':'));
});
xhr.upload.addEventListener('load', function(event) {
    events.push(['upload-load', event.lengthComputable, event.loaded, event.total, event.target === xhr.upload, event.currentTarget === xhr.upload].join(':'));
});
xhr.upload.addEventListener('loadend', function(event) {
    events.push(['upload-loadend', event.lengthComputable, event.loaded, event.total, event.target === xhr.upload, event.currentTarget === xhr.upload].join(':'));
    document.getElementById('result').textContent = events.join('|');
});
xhr.open('POST', 'http://example.com/data');
xhr.send('payload');
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("upload-loadstart:false:0:0:true:true", result);
        Assert.Contains("upload-progress:true:7:7:true:true", result);
        Assert.Contains("upload-load:true:7:7:true:true", result);
        Assert.Contains("upload-loadend:true:7:7:true:true", result);
    }

    [Fact]
    public void XHR_Upload_Load_Event_Preserves_Property_Handler_Alongside_Listeners()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            resolve(new Response('ok', {
                status: 200,
                headers: { 'Content-Type': 'text/plain' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
var order = [];
xhr.upload.onload = function() { order.push('property'); };
xhr.upload.addEventListener('load', function() { order.push('listener'); });
xhr.upload.addEventListener('loadend', function() {
    document.getElementById('result').textContent = order.join(',');
});
xhr.open('POST', 'http://example.com/data');
xhr.send('payload');
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("property,listener", result);
    }

    // ────────────────── Content-Type handling ──────────────────

    [Fact]
    public void Iframe_Non_HTML_Src_ContentDocument_Has_No_P_FAIL()
    {
        // Acid3 test 14: iframe with src="empty.png" should NOT have <p>FAIL</p> in contentDocument
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
document.write('<iframe src=""empty.png"">FAIL</iframe>');
var iframes = document.getElementsByTagName('iframe');
var iframe = iframes[0];
var r = [];
r.push(iframe ? 'has-iframe' : 'no-iframe');
if (iframe && iframe.contentDocument) {
    var ps = iframe.contentDocument.getElementsByTagName('p');
    r.push('p-count:' + ps.length);
    var hasFail = false;
    for (var i = 0; i < ps.length; i++) {
        if (ps[i].firstChild && ps[i].firstChild.data === 'FAIL') hasFail = true;
    }
    r.push('has-fail:' + hasFail);
} else {
    r.push('no-contentDocument');
}
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("has-iframe", result);
        Assert.DoesNotContain("has-fail:true", result);
    }

    [Fact]
    public void Iframe_Text_Plain_Src_ContentDocument_Has_No_P_FAIL()
    {
        // Acid3 test 15: iframe with src="empty.txt" should NOT have <p>FAIL</p> in contentDocument
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
document.write('<iframe src=""empty.txt"">FAIL</iframe>');
var iframes = document.getElementsByTagName('iframe');
var iframe = iframes[0];
var r = [];
r.push(iframe ? 'has-iframe' : 'no-iframe');
if (iframe && iframe.contentDocument) {
    var ps = iframe.contentDocument.getElementsByTagName('p');
    r.push('p-count:' + ps.length);
    var hasFail = false;
    for (var i = 0; i < ps.length; i++) {
        if (ps[i].firstChild && ps[i].firstChild.data === 'FAIL') hasFail = true;
    }
    r.push('has-fail:' + hasFail);
} else {
    r.push('no-contentDocument');
}
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("has-iframe", result);
        Assert.DoesNotContain("has-fail:true", result);
    }

    // ────────────────── <object> element handling ──────────────────

    [Fact]
    public void Object_Data_Property_Settable()
    {
        // Acid3 test 16: object.data should be settable without exception
        var html = @"<!DOCTYPE html>
<html><body>
<map name=""""></map>
<div id=""result""></div>
<script>
try {
    var oC = document.createElement('object');
    oC.appendChild(document.createTextNode('FAIL'));
    var oB = document.createElement('object');
    var oA = document.createElement('object');
    oA.data = 'support-a.png';
    oB.data = 'support-b.png';
    oB.appendChild(oC);
    oC.data = 'support-c.png';
    oA.appendChild(oB);
    document.getElementsByTagName('map')[0].appendChild(oA);
    document.getElementById('result').textContent = 'PASS';
} catch(e) {
    document.getElementById('result').textContent = 'FAIL: ' + e.message;
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("PASS", result);
    }

    [Fact]
    public void Object_Type_Property_ReadWrite()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var obj = document.createElement('object');
var r = [];
r.push(obj.type === '');
obj.type = 'image/png';
r.push(obj.type === 'image/png');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Object_Nested_With_Data_No_Exception()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""container""></div>
<div id=""result""></div>
<script>
try {
    var outer = document.createElement('object');
    outer.data = 'outer.png';
    var inner = document.createElement('object');
    inner.data = 'inner.png';
    inner.appendChild(document.createTextNode('fallback'));
    outer.appendChild(inner);
    document.getElementById('container').appendChild(outer);
    document.getElementById('result').textContent = 'PASS';
} catch(e) {
    document.getElementById('result').textContent = 'FAIL: ' + e.message;
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("PASS", result);
    }

    [Fact]
    public void Object_Data_Change_Invalidates_SubDocument()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var obj = document.createElement('object');
document.body.appendChild(obj);
obj.data = 'first.html';
var doc1 = obj.contentDocument;
obj.data = 'second.html';
var doc2 = obj.contentDocument;
var r = [];
r.push(doc1 !== null);
r.push(doc2 !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────── Same-origin policy ──────────────────

    [Fact]
    public void Iframe_CrossOrigin_ContentDocument_Returns_Null()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""xo"" src=""https://evil.example.com/page.html""></iframe>
<div id=""result""></div>
<script>
var iframe = document.getElementById('xo');
var doc = iframe.contentDocument;
document.getElementById('result').textContent = String(doc);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "http://mysite.example.com/index.html");
        Assert.Contains("null", result);
    }

    [Fact]
    public void Iframe_SameOrigin_ContentDocument_Returns_Document()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""so"" src=""page2.html""></iframe>
<div id=""result""></div>
<script>
var iframe = document.getElementById('so');
var doc = iframe.contentDocument;
var r = [];
r.push(doc !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void Iframe_FileScheme_ContentDocument_Is_SameOrigin()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""fi"" src=""file:///other/page.html""></iframe>
<div id=""result""></div>
<script>
var iframe = document.getElementById('fi');
var doc = iframe.contentDocument;
var r = [];
r.push(doc !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void Iframe_CrossOrigin_ContentWindow_Returns_Null()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""xow"" src=""https://evil.example.com/page.html""></iframe>
<div id=""result""></div>
<script>
var iframe = document.getElementById('xow');
var win = iframe.contentWindow;
document.getElementById('result').textContent = String(win);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "http://mysite.example.com/index.html");
        Assert.Contains("null", result);
    }

    // ────────────────── iframe src property ──────────────────

    [Fact]
    public void Iframe_Src_Property_ReadWrite()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""ifr"" src=""page.html""></iframe>
<div id=""result""></div>
<script>
var iframe = document.getElementById('ifr');
var r = [];
r.push(iframe.src === 'page.html');
iframe.src = 'other.html';
r.push(iframe.src === 'other.html');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────── Notifications (Acid3 cross-file test) ──────────────────

    [Fact]
    public void Notifications_Object_Not_Set_For_PNG_Iframe()
    {
        // Acid3 test 14 pattern: notifications['empty.png'] should be falsy
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var notifications = {};
function notify(file) { notifications[file] = 1; }
document.write('<iframe src=""empty.png"">FAIL</iframe>');
var r = [];
r.push(!notifications['empty.png']);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void Notifications_Object_Not_Set_For_TXT_Iframe()
    {
        // Acid3 test 15 pattern: notifications['empty.txt'] should be falsy
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var notifications = {};
function notify(file) { notifications[file] = 1; }
document.write('<iframe src=""empty.txt"">FAIL</iframe>');
var r = [];
r.push(!notifications['empty.txt']);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    // ────────────────── Error handling ──────────────────

    [Fact]
    public void Fetch_Error_Response_Has_Headers_Object()
    {
        // When fetch fails, the error response should still have a headers object
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
fetch('http://nonexistent.invalid.domain.test/page').then(function(response) {
    var r = [];
    r.push(typeof response.headers === 'object');
    r.push(typeof response.headers.get === 'function');
    r.push(typeof response.headers.has === 'function');
    document.getElementById('result').textContent = r.join(',');
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void XHR_GetAllResponseHeaders_Returns_String_Before_Send()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var xhr = new XMLHttpRequest();
xhr.open('GET', 'http://example.com');
var headers = xhr.getAllResponseHeaders();
var r = [];
r.push(typeof headers === 'string');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void XHR_OverrideMimeType_Populates_ResponseXml_For_Default_Text_Response()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            resolve(new Response('<section id=""payload""><span>OK</span></section>', {
                status: 200,
                headers: { 'Content-Type': 'text/plain' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
xhr.open('GET', 'http://example.com/data');
xhr.overrideMimeType('text/html');
xhr.onload = function() {
    var payload = xhr.responseXML && xhr.responseXML.getElementById('payload');
    document.getElementById('result').textContent = [
        xhr.response === xhr.responseText,
        xhr.responseText.indexOf('payload') >= 0,
        xhr.responseXML !== null,
        !!payload,
        payload && payload.textContent
    ].join('|');
};
xhr.send();
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true|true|OK", result);
    }

    [Fact]
    public void XHR_OverrideMimeType_Leaves_ResponseXml_Null_For_Plain_Text_Override()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            resolve(new Response('plain text payload', {
                status: 200,
                headers: { 'Content-Type': 'text/html' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
xhr.open('GET', 'http://example.com/data');
xhr.overrideMimeType('text/plain');
xhr.onload = function() {
    document.getElementById('result').textContent = [
        xhr.response === xhr.responseText,
        xhr.responseText,
        xhr.responseXML === null
    ].join('|');
};
xhr.send();
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|plain text payload|true", result);
    }

    [Fact]
    public void XHR_ArrayBuffer_ResponseType_Uses_Fetch_ArrayBuffer_Result()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            resolve(new Response('AB', {
                status: 200,
                headers: { 'Content-Type': 'application/octet-stream' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
xhr.open('GET', 'http://example.com/data');
xhr.responseType = 'arraybuffer';
xhr.onload = function() {
    var view = new Uint8Array(xhr.response);
    document.getElementById('result').textContent = [
        xhr.response instanceof ArrayBuffer,
        xhr.responseText === '',
        view[0],
        view[1]
    ].join('|');
};
xhr.send();
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|65|66", result);
    }

    [Fact]
    public void XHR_Blob_ResponseType_Uses_Fetch_Blob_Result()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            resolve(new Response('AB', {
                status: 200,
                headers: { 'Content-Type': 'application/octet-stream' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
xhr.open('GET', 'http://example.com/data');
xhr.responseType = 'blob';
xhr.onload = function() {
    xhr.response.text().then(function(text) {
        document.getElementById('result').textContent = [
            typeof xhr.response.text === 'function',
            xhr.response.size,
            xhr.response.type,
            xhr.responseText === '',
            text
        ].join('|');
    });
};
xhr.send();
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|2|application/octet-stream|true|AB", result);
    }

    [Fact]
    public void XHR_Json_ResponseType_Uses_Fetch_Json_Result()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            resolve(new Response('{""ok"":true,""count"":2}', {
                status: 200,
                headers: { 'Content-Type': 'application/json' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
xhr.open('GET', 'http://example.com/data');
xhr.responseType = 'json';
xhr.onload = function() {
    document.getElementById('result').textContent = [
        xhr.response.ok === true,
        xhr.response.count,
        xhr.responseText === ''
    ].join('|');
};
xhr.send();
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|2|true", result);
    }

    [Fact]
    public void XHR_Json_ResponseType_Invalid_Json_Yields_Null_And_Completes_Load()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            resolve(new Response('{invalid json', {
                status: 200,
                headers: { 'Content-Type': 'application/json' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
var loadCalled = false;
var errorCalled = false;
xhr.open('GET', 'http://example.com/data');
xhr.responseType = 'json';
xhr.onload = function() {
    loadCalled = true;
};
xhr.onerror = function() {
    errorCalled = true;
};
xhr.onloadend = function() {
    document.getElementById('result').textContent = [
        loadCalled,
        errorCalled,
        xhr.response === null,
        xhr.responseText === '',
        xhr.readyState === 4
    ].join('|');
};
xhr.send();
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|false|true|true|true", result);
    }

    [Fact]
    public void XHR_Rejected_Fetch_Triggers_Error_And_LoadEnd()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve, reject) {
            if (typeof reject === 'function') {
                reject(new Error('network failed'));
            }
        }
    };
};

var xhr = new XMLHttpRequest();
var loadCalled = false;
var errorCalled = false;
xhr.open('GET', 'http://example.com/data');
xhr.onload = function() { loadCalled = true; };
xhr.onerror = function() { errorCalled = true; };
xhr.onloadend = function() {
    document.getElementById('result').textContent = [
        loadCalled,
        errorCalled,
        xhr.readyState === 4,
        xhr.status === 0,
        xhr.response === null,
        xhr.responseText === ''
    ].join('|');
};
xhr.send();
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("false|true|true|true|true|true", result);
    }

    [Fact]
    public void XHR_Rejected_BodyReader_Triggers_Error_And_LoadEnd()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            var response = new Response('payload', {
                status: 200,
                headers: { 'Content-Type': 'text/plain' }
            });
            response.text = function() {
                return {
                    then: function(resolve, reject) {
                        if (typeof reject === 'function') {
                            reject(new Error('read failed'));
                        }
                    }
                };
            };
            resolve(response);
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
var readyStates = [];
var loadCalled = false;
var errorCalled = false;
xhr.open('GET', 'http://example.com/data');
xhr.onreadystatechange = function() {
    readyStates.push(xhr.readyState);
};
xhr.onload = function() { loadCalled = true; };
xhr.onerror = function() { errorCalled = true; };
xhr.onloadend = function() {
    document.getElementById('result').textContent = [
        loadCalled,
        errorCalled,
        readyStates.join(','),
        xhr.status === 0,
        xhr.response === null,
        xhr.responseText === ''
    ].join('|');
};
xhr.send();
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("false|true|2,4|true|true|true", result);
    }

    [Fact]
    public void XHR_Document_ResponseType_Uses_Fetch_Text_Result_As_Document()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            resolve(new Response('<section id=""payload""><span>OK</span></section>', {
                status: 200,
                headers: { 'Content-Type': 'text/html' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
xhr.open('GET', 'http://example.com/data');
xhr.responseType = 'document';
xhr.onload = function() {
    var doc = xhr.response;
    var payload = doc && doc.getElementById('payload');
    document.getElementById('result').textContent = [
        xhr.response === xhr.responseXML,
        xhr.responseText === '',
        doc !== null,
        !!payload,
        payload && payload.textContent
    ].join('|');
};
xhr.send();
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true|true|OK", result);
    }

    [Fact]
    public void XHR_Document_ResponseType_Stays_Null_For_NonDocument_MimeType()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            resolve(new Response('plain text payload', {
                status: 200,
                headers: { 'Content-Type': 'text/plain' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
xhr.open('GET', 'http://example.com/data');
xhr.responseType = 'document';
xhr.onload = function() {
    document.getElementById('result').textContent = [
        xhr.response === null,
        xhr.responseXML === null,
        xhr.responseText === ''
    ].join('|');
};
xhr.send();
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true", result);
    }

    [Fact]
    public void XHR_Document_ResponseType_Uses_OverrideMimeType_For_Text_Response()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var originalFetch = fetch;
fetch = window.fetch = function() {
    return {
        then: function(resolve) {
            resolve(new Response('<section id=""payload""><span>OK</span></section>', {
                status: 200,
                headers: { 'Content-Type': 'text/plain' }
            }));
            return { catch: function() {} };
        }
    };
};

var xhr = new XMLHttpRequest();
xhr.open('GET', 'http://example.com/data');
xhr.responseType = 'document';
xhr.overrideMimeType('text/html');
xhr.onload = function() {
    var payload = xhr.response && xhr.response.getElementById('payload');
    document.getElementById('result').textContent = [
        xhr.response === xhr.responseXML,
        xhr.responseText === '',
        xhr.response !== null,
        !!payload,
        payload && payload.textContent
    ].join('|');
};
xhr.send();
fetch = window.fetch = originalFetch;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|true|true|OK", result);
    }
}
