namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 3 of Acid3 Compliance v2: HTTP &amp; Sub-Resource Loading.
/// Covers iframe content loading, object element handling, dynamic resource
/// injection, and external script loading.
/// </summary>
public class HttpSubResourceTests
{
    // ------------------------------------------------------------------
    //  3.1 iframe content loading
    // ------------------------------------------------------------------

    [Fact]
    public void Iframe_AboutBlank_Has_ContentDocument()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""fr"" src=""about:blank""></iframe>
<div id=""out""></div>
<script>
var fr = document.getElementById('fr');
var r = [];
r.push(fr.contentDocument !== null);
r.push(fr.contentDocument.body !== null);
r.push(fr.contentWindow !== null);
r.push(fr.contentWindow.location.href);
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|true|true|about:blank", result);
    }

    [Fact]
    public void Iframe_Srcdoc_DefaultView_And_WindowScroll_Use_Subdocument_Root()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""fr"" srcdoc='<!DOCTYPE html><html><body style=""margin:0""><div id=""target"" style=""width:2000px;height:1000px""></div></body></html>'></iframe>
<div id=""out""></div>
<script>
var fr = document.getElementById('fr');
var doc = fr.contentDocument;
var win = fr.contentWindow;
var scroller = doc.scrollingElement;
win.scrollTo({ left: 40, top: 50 });
win.scrollBy({ left: 10, top: 15 });
document.getElementById('out').textContent = [
  doc !== null,
  doc.getElementById('target') !== null,
  scroller === doc.documentElement,
  doc.defaultView === win,
  win.document === doc,
  win.location.href,
  win.scrollX + ',' + win.scrollY,
  win.pageXOffset + ',' + win.pageYOffset,
  scroller.scrollLeft + ',' + scroller.scrollTop
].join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|true|true|true|true|about:srcdoc|50,65|50,65|50,65", result);
    }

    [Fact]
    public void Iframe_Srcdoc_Load_Event_Fires_After_Listener_Registration()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""fr"" srcdoc='<!DOCTYPE html><html><body><div id=""target""></div></body></html>'></iframe>
<div id=""out""></div>
<script>
var fr = document.getElementById('fr');
fr.addEventListener('load', function () {
  var doc = fr.contentDocument;
  document.getElementById('out').textContent = String(doc !== null && doc.getElementById('target') !== null);
});
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true", result);
    }

    [Fact]
    public void Iframe_Nested_Subdocument_Scripts_Resolve_Relative_Iframe_Sources()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"broiler-nested-iframe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            File.WriteAllText(Path.Combine(tempRoot, "leaf.html"), """
<!DOCTYPE html>
<div id="leaf"></div>
<script>
document.getElementById('leaf').textContent = location.search || 'missing';
</script>
""");
            File.WriteAllText(Path.Combine(tempRoot, "nested.html"), """
<!DOCTYPE html>
<iframe id="target"></iframe>
<script>
document.getElementById('target').src = 'leaf.html?scale=3';
</script>
""");

            var html = """
<!DOCTYPE html>
<html><body>
<iframe id="outer" src="nested.html"></iframe>
<div id="out"></div>
<script>
window.onload = function () {
  var outer = document.getElementById('outer');
  var inner = outer.contentDocument.getElementById('target');
  var leaf = inner.contentDocument.getElementById('leaf');
  document.getElementById('out').textContent = [
    inner.contentWindow.location.href,
    inner.contentWindow.location.search,
    leaf ? leaf.textContent : 'missing'
  ].join('|');
};
</script>
</body></html>
""";

            var result = CaptureService.ExecuteScriptsWithDom(
                html,
                "file:///test.html",
                localResourceBasePath: tempRoot);

            Assert.Contains("leaf.html?scale=3|?scale=3|?scale=3", result);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Iframe_NonHtml_Src_Gets_Minimal_Document()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""fr"" src=""image.png"">FAIL</iframe>
<div id=""out""></div>
<script>
var fr = document.getElementById('fr');
var doc = fr.contentDocument;
var r = [];
r.push(doc !== null);
r.push(doc.documentElement !== null);
r.push(doc.body !== null);
// Non-HTML resource should have empty body with no FAIL text
var bodyText = doc.body.textContent || '';
r.push(bodyText.indexOf('FAIL') === -1);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void Iframe_TextPlain_Src_Gets_Minimal_Document()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""fr"" src=""readme.txt"">FAIL</iframe>
<div id=""out""></div>
<script>
var fr = document.getElementById('fr');
var doc = fr.contentDocument;
var r = [];
r.push(doc !== null);
r.push(doc.body !== null);
// text/plain resource should not parse as HTML
var bodyText = doc.body.textContent || '';
r.push(bodyText.indexOf('FAIL') === -1);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true,true", result);
    }

    // ------------------------------------------------------------------
    //  3.2 <object> element handling
    // ------------------------------------------------------------------

    [Fact]
    public void Object_ContentDocument_Accessible_With_SameOrigin_Data()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<object id=""obj"" data=""page.html"" type=""text/html"">
  <p>Fallback content</p>
</object>
<div id=""out""></div>
<script>
var obj = document.getElementById('obj');
var r = [];
r.push(obj.contentDocument !== null);
r.push(obj.data !== '');
r.push(obj.type);
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|", result);
        Assert.Contains("|text/html", result);
    }

    [Fact]
    public void Object_Without_Data_Has_ContentDocument()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var obj = document.createElement('object');
document.body.appendChild(obj);
var r = [];
r.push(obj.contentDocument !== null);
r.push(obj.contentDocument.body !== null);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true", result);
    }

    // ------------------------------------------------------------------
    //  3.3 Dynamic resource injection
    // ------------------------------------------------------------------

    [Fact]
    public void Dynamic_Iframe_Has_ContentDocument()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var iframe = document.createElement('iframe');
document.body.appendChild(iframe);
var r = [];
r.push(iframe.contentDocument !== null);
r.push(iframe.contentDocument.body !== null);
r.push(iframe.contentDocument.documentElement !== null);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Dynamic_Iframe_Src_Change_Resets_Document()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var iframe = document.createElement('iframe');
document.body.appendChild(iframe);
var doc1 = iframe.contentDocument;
iframe.src = 'other.html';
var doc2 = iframe.contentDocument;
var r = [];
r.push(doc1 !== null);
r.push(doc2 !== null);
// After src change, the sub-document cache is invalidated
r.push(doc1 !== doc2);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Dynamic_Iframe_AboutBlank_Initial()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var iframe = document.createElement('iframe');
iframe.src = 'about:blank';
document.body.appendChild(iframe);
var r = [];
r.push(iframe.contentWindow !== null);
r.push(iframe.contentWindow.location.href);
r.push(iframe.contentDocument !== null);
r.push(iframe.contentDocument.body !== null);
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|about:blank|true|true", result);
    }

    // ------------------------------------------------------------------
    //  3.4 External script loading
    // ------------------------------------------------------------------

    [Fact]
    public void External_DataUri_Script_Executes()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script src=""data:text/javascript,document.getElementById('out').textContent='LOADED'""></script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("LOADED", result);
    }

    [Fact]
    public void External_Script_Ordering_Preserved()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>document.getElementById('out').textContent = 'A';</script>
<script src=""data:text/javascript,document.getElementById('out').textContent %2B= 'B'""></script>
<script>document.getElementById('out').textContent += 'C';</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("ABC", result);
    }

    // ------------------------------------------------------------------
    //  3.5 Content-Type based handling
    // ------------------------------------------------------------------

    [Fact]
    public void Iframe_ContentDocument_DocumentWrite_Works()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""fr""></iframe>
<div id=""out""></div>
<script>
var fr = document.getElementById('fr');
var doc = fr.contentDocument;
doc.open();
doc.write('<p>Hello from write</p>');
doc.close();
var r = [];
r.push(doc !== null);
r.push(doc.body !== null);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true", result);
    }

    [Fact]
    public void FetchExternalScript_Returns_Empty_For_Invalid_Url()
    {
        // Test that FetchExternalScript handles invalid URLs gracefully
        var result = CaptureService.FetchExternalScript("not-a-valid-url", "also-not-valid");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FetchExternalScript_Resolves_Relative_Urls()
    {
        // Test that FetchExternalScript resolves relative URLs against base URL
        // This will fail to fetch (no server) but should not throw
        var result = CaptureService.FetchExternalScript("script.js", "http://localhost:99999/page.html");
        // Should return empty (connection refused) but not throw
        Assert.Equal(string.Empty, result);
    }
}
