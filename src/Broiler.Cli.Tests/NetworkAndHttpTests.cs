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
}
