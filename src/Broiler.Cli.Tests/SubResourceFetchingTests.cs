namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 2 of Acid3 Compliance v3: Sub-Resource Fetching &amp; Content-Type Handling.
/// Covers file:// sub-resource fetching, content-type-aware contentDocument,
/// &lt;object&gt; fallback handling, and external script loading from file:// URLs.
/// </summary>
public class SubResourceFetchingTests
{
    // ------------------------------------------------------------------
    //  2.1 File:// sub-resource fetching
    // ------------------------------------------------------------------

    [Fact]
    public void Iframe_FileUrl_Html_Loads_ContentDocument()
    {
        // Create a temporary HTML file to serve as an iframe source
        var tmpDir = Path.Combine(Path.GetTempPath(), "broiler_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        var subHtmlPath = Path.Combine(tmpDir, "sub.html");
        File.WriteAllText(subHtmlPath, "<html><body><p id=\"inner\">Hello Sub</p></body></html>");
        try
        {
            var pageUrl = new Uri(Path.Combine(tmpDir, "page.html")).AbsoluteUri;
            var html = $@"<!DOCTYPE html>
<html><body>
<iframe id=""fr"" src=""sub.html""></iframe>
<div id=""out""></div>
<script>
var fr = document.getElementById('fr');
var doc = fr.contentDocument;
var r = [];
r.push(doc !== null);
r.push(doc.body !== null);
r.push(doc.getElementById('inner') !== null);
r.push(doc.getElementById('inner').textContent);
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

            var result = CaptureService.ExecuteScriptsWithDom(html, pageUrl);
            Assert.Contains("true|true|true|Hello Sub", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void Iframe_FileUrl_Relative_Resolves_Against_Base()
    {
        // Ensure relative URLs are resolved against the page's file:// URL
        var tmpDir = Path.Combine(Path.GetTempPath(), "broiler_test_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tmpDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "child.html"),
            "<html><body><span id=\"msg\">From Child</span></body></html>");
        try
        {
            var pageUrl = new Uri(Path.Combine(tmpDir, "page.html")).AbsoluteUri;
            var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""fr"" src=""subdir/child.html""></iframe>
<div id=""out""></div>
<script>
var fr = document.getElementById('fr');
var doc = fr.contentDocument;
var r = [];
r.push(doc !== null);
r.push(doc.body !== null);
var span = doc.getElementById('msg');
r.push(span !== null);
r.push(span ? span.textContent : 'missing');
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

            var result = CaptureService.ExecuteScriptsWithDom(html, pageUrl);
            Assert.Contains("true|true|true|From Child", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    // ------------------------------------------------------------------
    //  2.2 Content-type-aware contentDocument
    // ------------------------------------------------------------------

    [Fact]
    public void Iframe_ImagePng_Gets_Minimal_Empty_Document()
    {
        // An iframe pointing to an image should get a minimal document with empty body
        var tmpDir = Path.Combine(Path.GetTempPath(), "broiler_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        // Create a fake PNG file (just needs to exist with .png extension)
        File.WriteAllBytes(Path.Combine(tmpDir, "test.png"), [0x89, 0x50, 0x4E, 0x47]);
        try
        {
            var pageUrl = new Uri(Path.Combine(tmpDir, "page.html")).AbsoluteUri;
            var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""fr"" src=""test.png"">FAIL</iframe>
<div id=""out""></div>
<script>
var fr = document.getElementById('fr');
var doc = fr.contentDocument;
var r = [];
r.push(doc !== null);
r.push(doc.documentElement !== null);
r.push(doc.body !== null);
// Image resource should have empty body with no FAIL text
var bodyText = doc.body.textContent || '';
r.push(bodyText.indexOf('FAIL') === -1);
r.push(bodyText === '');
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

            var result = CaptureService.ExecuteScriptsWithDom(html, pageUrl);
            Assert.Contains("true,true,true,true,true", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void Iframe_TextPlain_Gets_Document_With_Text_Content()
    {
        // An iframe pointing to a text/plain file should get a document with the text content
        var tmpDir = Path.Combine(Path.GetTempPath(), "broiler_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        File.WriteAllText(Path.Combine(tmpDir, "readme.txt"), "Hello plain text");
        try
        {
            var pageUrl = new Uri(Path.Combine(tmpDir, "page.html")).AbsoluteUri;
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
// text/plain should have pre-formatted text content in body
var bodyText = doc.body.textContent || '';
r.push(bodyText.indexOf('FAIL') === -1);
r.push(bodyText.indexOf('Hello plain text') !== -1);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

            var result = CaptureService.ExecuteScriptsWithDom(html, pageUrl);
            Assert.Contains("true,true,true,true", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void Iframe_Html_Gets_Full_Parsed_Document()
    {
        // An iframe pointing to an HTML file should get a fully parsed DOM
        var tmpDir = Path.Combine(Path.GetTempPath(), "broiler_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        File.WriteAllText(Path.Combine(tmpDir, "page.html"),
            "<!DOCTYPE html><html><head><title>Sub</title></head><body><div id=\"content\">Loaded</div></body></html>");
        try
        {
            var pageUrl = new Uri(Path.Combine(tmpDir, "index.html")).AbsoluteUri;
            var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""fr"" src=""page.html""></iframe>
<div id=""out""></div>
<script>
var fr = document.getElementById('fr');
var doc = fr.contentDocument;
var r = [];
r.push(doc !== null);
r.push(doc.body !== null);
r.push(doc.documentElement !== null);
var content = doc.getElementById('content');
r.push(content !== null);
r.push(content ? content.textContent : 'missing');
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

            var result = CaptureService.ExecuteScriptsWithDom(html, pageUrl);
            Assert.Contains("true|true|true|true|Loaded", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    // ------------------------------------------------------------------
    //  2.3 <object> fallback handling
    // ------------------------------------------------------------------

    [Fact]
    public void Object_Http404_ContentDocument_Returns_Null()
    {
        // When an <object> data URL returns HTTP 404, contentDocument should be null
        // (fallback content should be visible)
        var html = @"<!DOCTYPE html>
<html><body>
<object id=""obj"" data=""http://localhost:1/nonexistent.html"" type=""text/html"">
  <p>Fallback visible</p>
</object>
<div id=""out""></div>
<script>
var obj = document.getElementById('obj');
var r = [];
r.push(obj.contentDocument === null);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "http://localhost:1/page.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void Object_Successful_Load_Has_ContentDocument()
    {
        // When an <object> data URL loads successfully, contentDocument should not be null
        var tmpDir = Path.Combine(Path.GetTempPath(), "broiler_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        File.WriteAllText(Path.Combine(tmpDir, "obj.html"),
            "<html><body><p id=\"loaded\">Object Content</p></body></html>");
        try
        {
            var pageUrl = new Uri(Path.Combine(tmpDir, "page.html")).AbsoluteUri;
            var html = @"<!DOCTYPE html>
<html><body>
<object id=""obj"" data=""obj.html"" type=""text/html"">
  <p>Fallback</p>
</object>
<div id=""out""></div>
<script>
var obj = document.getElementById('obj');
var r = [];
r.push(obj.contentDocument !== null);
r.push(obj.contentDocument.body !== null);
var loaded = obj.contentDocument.getElementById('loaded');
r.push(loaded !== null);
r.push(loaded ? loaded.textContent : 'missing');
document.getElementById('out').textContent = r.join('|');
</script>
</body></html>";

            var result = CaptureService.ExecuteScriptsWithDom(html, pageUrl);
            Assert.Contains("true|true|true|Object Content", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    // ------------------------------------------------------------------
    //  2.4 External <script src=""> loading from file://
    // ------------------------------------------------------------------

    [Fact]
    public void External_Script_FileUrl_Executes()
    {
        // External scripts referenced via relative URL should be fetched from file://
        var tmpDir = Path.Combine(Path.GetTempPath(), "broiler_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        File.WriteAllText(Path.Combine(tmpDir, "lib.js"),
            "document.getElementById('out').textContent = 'SCRIPT_LOADED';");
        try
        {
            var pageUrl = new Uri(Path.Combine(tmpDir, "page.html")).AbsoluteUri;
            var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script src=""lib.js""></script>
</body></html>";

            var result = CaptureService.ExecuteScriptsWithDom(html, pageUrl);
            Assert.Contains("SCRIPT_LOADED", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void External_Script_FileUrl_Ordering_Preserved()
    {
        // Multiple external scripts from file:// should execute in document order
        var tmpDir = Path.Combine(Path.GetTempPath(), "broiler_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        File.WriteAllText(Path.Combine(tmpDir, "a.js"),
            "document.getElementById('out').textContent = 'A';");
        File.WriteAllText(Path.Combine(tmpDir, "b.js"),
            "document.getElementById('out').textContent += 'B';");
        try
        {
            var pageUrl = new Uri(Path.Combine(tmpDir, "page.html")).AbsoluteUri;
            var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script src=""a.js""></script>
<script src=""b.js""></script>
<script>document.getElementById('out').textContent += 'C';</script>
</body></html>";

            var result = CaptureService.ExecuteScriptsWithDom(html, pageUrl);
            Assert.Contains("ABC", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void FetchExternalScript_FileUrl_Returns_Content()
    {
        // FetchExternalScript should read from file:// URLs
        var tmpDir = Path.Combine(Path.GetTempPath(), "broiler_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        File.WriteAllText(Path.Combine(tmpDir, "script.js"), "var x = 42;");
        try
        {
            var fileUrl = new Uri(Path.Combine(tmpDir, "script.js")).AbsoluteUri;
            var result = CaptureService.FetchExternalScript(fileUrl, "file:///page.html");
            Assert.Equal("var x = 42;", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void FetchExternalScript_FileUrl_NonExistent_Returns_Empty()
    {
        // FetchExternalScript should return empty for non-existent file:// URLs
        var result = CaptureService.FetchExternalScript("file:///nonexistent/script.js", "file:///page.html");
        Assert.Equal(string.Empty, result);
    }

    // ------------------------------------------------------------------
    //  2.5 Content-type detection edge cases
    // ------------------------------------------------------------------

    [Fact]
    public void Iframe_Css_File_Gets_Minimal_Document()
    {
        // CSS files should NOT be parsed as HTML
        var tmpDir = Path.Combine(Path.GetTempPath(), "broiler_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        File.WriteAllText(Path.Combine(tmpDir, "style.css"), "body { color: red; }");
        try
        {
            var pageUrl = new Uri(Path.Combine(tmpDir, "page.html")).AbsoluteUri;
            var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""fr"" src=""style.css""></iframe>
<div id=""out""></div>
<script>
var fr = document.getElementById('fr');
var doc = fr.contentDocument;
var r = [];
r.push(doc !== null);
r.push(doc.body !== null);
// CSS file should get a text content document, not parsed as HTML
var bodyText = doc.body.textContent || '';
r.push(bodyText.indexOf('body') !== -1);
document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

            var result = CaptureService.ExecuteScriptsWithDom(html, pageUrl);
            Assert.Contains("true,true,true", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void ContentType_Detection_From_Extension()
    {
        // Verify MIME type detection from file extensions used in sub-resource handling
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""fr1"" src=""photo.png"">FAIL</iframe>
<iframe id=""fr2"" src=""readme.txt"">FAIL</iframe>
<iframe id=""fr3"" src=""page.html"">FAIL</iframe>
<div id=""out""></div>
<script>
var r = [];
// Image should get empty body (binary content)
var doc1 = document.getElementById('fr1').contentDocument;
r.push(doc1 !== null);
r.push((doc1.body.textContent || '').indexOf('FAIL') === -1);

// Text should get empty body (no parsing, but no FAIL text from parent)
var doc2 = document.getElementById('fr2').contentDocument;
r.push(doc2 !== null);
r.push((doc2.body.textContent || '').indexOf('FAIL') === -1);

// HTML should get an empty document (file not found)
var doc3 = document.getElementById('fr3').contentDocument;
r.push(doc3 !== null);
r.push(doc3.body !== null);

document.getElementById('out').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }
}
