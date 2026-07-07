namespace Broiler.Cli.Tests;

public class Acid3DebugTest
{
    [Fact]
    public void DebugCreateElement_InvalidNames_Throw()
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
        Console.WriteLine("=== CAPTURED HTML ===");
        Console.WriteLine(result);
        Console.WriteLine("=== END HTML ===");
        
        // Print each line
        var lines = result.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        Console.WriteLine($"=== FOUND {lines.Length} LINES ===");
        foreach(var line in lines)
        {
            if (line.Contains("P,P") || line.Contains("results") || line.Contains("textContent"))
            {
                Console.WriteLine($"  [{line}]");
            }
        }

        Assert.Contains("P,P,P", result);
    }
}
