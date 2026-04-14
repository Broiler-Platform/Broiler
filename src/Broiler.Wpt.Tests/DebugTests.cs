using System;
using System.IO;
using System.Text.RegularExpressions;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Broiler.Wpt;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Wpt.Tests;

public class DebugTests
{
    private readonly ITestOutputHelper _output;
    public DebugTests(ITestOutputHelper output) { _output = output; }

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !File.Exists(Path.Combine(dir, "Broiler.slnx")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new Exception("Repo root not found");
    }

    [Fact]
    public void Debug_Serialized_Propagation002_ProperFlow()
    {
        var root = FindRepoRoot();
        var wptRoot = Path.Combine(root, "tests", "wpt");
        var test = Path.Combine(wptRoot, "css", "css-backgrounds", "background-color-root-propagation-002.html");
        var html = File.ReadAllText(test);
        var url = new Uri(Path.GetFullPath(test)).AbsoluteUri;

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, url);

        // Step 1: Inject BrowserApiStubs
        context.Eval(@"
(function() {
  window.requestAnimationFrame = function(cb) { cb(0); return 0; };
  window.cancelAnimationFrame = function(id) {};
  if (typeof takeScreenshot === 'undefined') {
    window.takeScreenshot = function() {
      if (document.documentElement) {
        document.documentElement.classList.remove('reftest-wait');
      }
    };
  }
})();
");

        // Step 2: Extract and execute inline scripts
        var scriptPattern = new Regex(@"<script[^>]*>(?<content>.*?)</script>", RegexOptions.Singleline);
        foreach (Match m in scriptPattern.Matches(html))
        {
            var content = m.Groups["content"].Value.Trim();
            if (!string.IsNullOrEmpty(content))
            {
                try
                {
                    _output.WriteLine($"Executing script: {content.Substring(0, Math.Min(content.Length, 80))}...");
                    context.Eval(content);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Script error: {ex.Message}");
                }
            }
        }

        // Step 3: Fire onload
        _output.WriteLine("Firing window load event...");
        bridge.FireWindowLoadEvent();

        // Step 4: Flush timers
        bridge.FlushTimers();

        // Step 5: Serialize
        var serialized = bridge.SerializeToHtml();
        _output.WriteLine("=== Serialized HTML ===");
        _output.WriteLine(serialized);

        // Check critical assertions
        var htmlTag = Regex.Match(serialized, @"<html[^>]*>");
        _output.WriteLine($"HTML tag: {htmlTag.Value}");
        _output.WriteLine($"Has display:none style on html: {htmlTag.Value.Contains("display") && htmlTag.Value.Contains("none")}");
        _output.WriteLine($"Has reftest-wait class: {htmlTag.Value.Contains("reftest-wait")}");
    }

    [Fact]
    public void Debug_Serialized_Animation_ProperFlow()
    {
        var root = FindRepoRoot();
        var wptRoot = Path.Combine(root, "tests", "wpt");
        var test = Path.Combine(wptRoot, "css", "css-backgrounds", "animations", "background-color-animation-in-body.html");
        var html = File.ReadAllText(test);
        var url = new Uri(Path.GetFullPath(test)).AbsoluteUri;

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, url);

        context.Eval(@"
(function() {
  window.requestAnimationFrame = function(cb) { cb(0); return 0; };
  window.cancelAnimationFrame = function(id) {};
})();
");

        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();
        bridge.ResolveAnimationSnapshots();
        bridge.ResolveAnchorPositions();

        var serialized = bridge.SerializeToHtml();
        _output.WriteLine("=== Serialized Animation HTML ===");
        _output.WriteLine(serialized);

        // Check for resolved background-color in body tag
        var bodyTag = Regex.Match(serialized, @"<body[^>]*>");
        _output.WriteLine($"Body tag: {bodyTag.Value}");
        _output.WriteLine($"Has background-color inline style: {bodyTag.Value.Contains("background-color")}");
    }
}
