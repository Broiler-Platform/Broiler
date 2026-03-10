namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for the Phase 1 Acid3 compliance changes: JavaScript execution in the
/// CLI image-capture path via <see cref="CaptureService"/>.
/// </summary>
public class CaptureServiceJsTests
{
    [Fact]
    public void ExecuteScriptsWithDom_Returns_Original_Html_When_No_Scripts()
    {
        var html = "<html><body><p>Hello</p></body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // With no scripts, the original HTML is returned unchanged.
        Assert.Equal(html, result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_Runs_Inline_Script()
    {
        var html = @"<!DOCTYPE html>
<html>
<head><title>Test</title></head>
<body>
<div id=""target""></div>
<script>
var div = document.getElementById('target');
var p = document.createElement('p');
p.textContent = 'generated';
div.appendChild(p);
</script>
</body>
</html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("<p>generated</p>", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_DocumentBody_Is_Accessible()
    {
        var html = @"<!DOCTYPE html>
<html>
<head></head>
<body>
<script>
var p = document.createElement('p');
p.textContent = 'body-child';
document.body.appendChild(p);
</script>
</body>
</html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("<p>body-child</p>", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_Handles_CreateElement_And_SetAttribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var span = document.createElement('span');
span.setAttribute('class', 'highlight');
span.textContent = 'styled';
document.body.appendChild(span);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("highlight", result);
        Assert.Contains("styled", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_Handles_Script_Errors_Gracefully()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""before""></div>
<script>throw new Error('intentional');</script>
<script>
var el = document.getElementById('before');
var p = document.createElement('p');
p.textContent = 'after-error';
el.appendChild(p);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // The second script should still execute despite the first throwing.
        Assert.Contains("after-error", result);
    }

    [Fact]
    public void DecodeDataUri_Decodes_Percent_Encoded()
    {
        var uri = "data:text/javascript,d1%20%3D%20'one'%3B";

        var result = CaptureService.DecodeDataUri(uri);

        Assert.Equal("d1 = 'one';", result);
    }

    [Fact]
    public void DecodeDataUri_Decodes_Base64()
    {
        // "d2 = 'two';" in base64 is "ZDIgPSAndHdvJzs="
        var uri = "data:text/javascript;base64,ZDIgPSAndHdvJzs=";

        var result = CaptureService.DecodeDataUri(uri);

        Assert.Equal("d2 = 'two';", result);
    }

    [Fact]
    public void DecodeDataUri_Decodes_Percent_Encoded_Base64()
    {
        // Acid3 uses percent-encoded base64 payloads
        var uri = "data:text/javascript;base64,ZDIgPSAndHdvJzs%3D";

        var result = CaptureService.DecodeDataUri(uri);

        Assert.Equal("d2 = 'two';", result);
    }

    [Fact]
    public void DecodeDataUri_Returns_Empty_For_Invalid_Uri()
    {
        Assert.Equal(string.Empty, CaptureService.DecodeDataUri("not-a-data-uri"));
        Assert.Equal(string.Empty, CaptureService.DecodeDataUri("data:no-comma"));
    }

    [Fact]
    public void ExecuteScriptsWithDom_Runs_Data_Uri_Scripts()
    {
        // d1 is set by a data: URI script, then used in an inline script
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>var d1 = 'fail';</script>
<script type=""text/javascript"" src=""data:text/javascript,d1%20%3D%20'pass'%3B""></script>
<script>
var el = document.getElementById('out');
var p = document.createElement('p');
p.textContent = d1;
el.appendChild(p);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("pass", result);
        Assert.DoesNotContain(">fail<", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_Document_Write_Appends_Content()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>document.write('<p id=""written"">from-write</p>');</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("from-write", result);
    }

    [Fact]
    public void ExecuteScriptsWithDom_Preserves_Existing_Content()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<p>existing</p>
<script>
var p = document.createElement('p');
p.textContent = 'added';
document.body.appendChild(p);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("existing", result);
        Assert.Contains("added", result);
    }
}
