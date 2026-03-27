using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class TmpAcid3DebugTest
{
    private readonly ITestOutputHelper _out;
    public TmpAcid3DebugTest(ITestOutputHelper out_) { _out = out_; }
    
    [Fact]
    public void DumpProcessedAcid3Html()
    {
        var html = File.ReadAllText("/home/runner/work/Broiler/Broiler/acid/acid3/acid3.html");
        var postJs = CaptureService.ExecuteScriptsWithDom(html, "file:///acid3/acid3.html");
        var postProc = Broiler.App.Rendering.HtmlPostProcessor.Process(postJs);
        File.WriteAllText("/tmp/acid3_post_proc.html", postProc);
        
        // Extract style block
        int styleStart = postProc.IndexOf("<style");
        int styleEnd = postProc.IndexOf("</style>") + 8;
        if (styleStart >= 0 && styleEnd > styleStart)
        {
            var styleBlock = postProc.Substring(styleStart, styleEnd - styleStart);
            _out.WriteLine("=== STYLE BLOCK ===");
            _out.WriteLine(styleBlock.Substring(0, Math.Min(3000, styleBlock.Length)));
        }
        
        // Bucket HTML
        int bucketsStart = postProc.IndexOf("class=\"buckets\"");
        if (bucketsStart >= 0)
        {
            int ctxStart = Math.Max(0, bucketsStart - 50);
            int ctxEnd = Math.Min(postProc.Length, bucketsStart + 1000);
            _out.WriteLine("\n=== BUCKETS HTML ===");
            _out.WriteLine(postProc.Substring(ctxStart, ctxEnd - ctxStart));
        }
        
        Assert.True(postProc.Length > 0);
    }
}
