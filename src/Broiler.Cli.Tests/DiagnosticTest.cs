using System;
using System.IO;
using System.Text.RegularExpressions;
using Broiler.Cli;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class DiagnosticTest
{
    private readonly ITestOutputHelper _output;
    public DiagnosticTest(ITestOutputHelper output) => _output = output;
    
    [Fact]
    public void Acid3_Test4_Identity_Debug()
    {
        var acid3Path = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
            "acid", "acid3", "acid3.html"));
        Assert.True(File.Exists(acid3Path));
        
        var html = File.ReadAllText(acid3Path);
        
        var debugScript = @"
<script>
var dbg = [];
try {
    // Check document structure
    var de = document.documentElement;
    dbg.push('docEl=' + de.tagName);
    dbg.push('docEl_children=' + de.childNodes.length);
    for (var ci = 0; ci < de.childNodes.length; ci++) {
        var kid = de.childNodes[ci];
        var desc = kid.nodeType == 1 ? kid.tagName + (kid.id ? '#' + kid.id : '') : 'type' + kid.nodeType;
        dbg.push('docEl_child' + ci + '=' + desc + ' (' + (kid.childNodes ? kid.childNodes.length : '?') + ' children)');
    }
    
    // Check head
    if (document.head) {
        dbg.push('head_children=' + document.head.childNodes.length);
    } else {
        dbg.push('NO_HEAD');
    }
    
    // Check body direct children (first 20)
    dbg.push('body_children=' + document.body.childNodes.length);
    for (var ci = 0; ci < Math.min(20, document.body.childNodes.length); ci++) {
        var kid = document.body.childNodes[ci];
        var desc = '';
        if (kid.nodeType == 3) {
            var txt = kid.data.substring(0,30).replace(/\s+/g,' ').replace(/&/g,'&amp;');
            desc = 'TEXT(' + txt.length + ')';
        } else if (kid.nodeType == 1) {
            desc = kid.tagName + (kid.id ? '#' + kid.id : '') + (kid.className ? '.' + kid.className : '');
        } else {
            desc = 'type' + kid.nodeType;
        }
        dbg.push('body_child' + ci + '=' + desc);
    }
    
    // Check scripts
    var bodyScripts = 0;
    for (var ci = 0; ci < document.body.childNodes.length; ci++) {
        if (document.body.childNodes[ci].tagName === 'SCRIPT') bodyScripts++;
    }
    dbg.push('body_scripts=' + bodyScripts);
    
} catch(e) {
    dbg.push('ERROR:' + e.message);
}
var dbgEl = document.createElement('div');
dbgEl.id = 'debug-output';
dbgEl.setAttribute('data-dbg', dbg.join('|'));
document.body.appendChild(dbgEl);
</script>";
        
        html = html.Replace("</body>", debugScript + "</body>");
        
        var url = "http://acid3.acidtests.org/acid3.html";
        var result = CaptureService.ExecuteScriptsWithDom(html, url);
        
        var scoreMatch = Regex.Match(result, @"id=""score""[^>]*>(\d+)<");
        if (scoreMatch.Success)
            _output.WriteLine($"ACID3_SCORE={scoreMatch.Groups[1].Value}");
        
        var dbgMatch = Regex.Match(result, @"data-dbg=""([^""]+)""");
        if (dbgMatch.Success)
        {
            _output.WriteLine("=== DEBUG OUTPUT ===");
            foreach (var item in dbgMatch.Groups[1].Value.Split('|'))
                _output.WriteLine(item);
        }
    }
}
