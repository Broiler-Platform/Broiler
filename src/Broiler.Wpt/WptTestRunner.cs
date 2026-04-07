using System.Text.RegularExpressions;
using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;
using SkiaSharp;

namespace Broiler.Wpt;

/// <summary>
/// Result of rendering and comparing a single WPT test case.
/// </summary>
internal sealed class WptTestResult
{
    /// <summary>Full path to the test file.</summary>
    public required string TestPath { get; init; }

    /// <summary>Whether the rendering matched the reference.</summary>
    public bool Passed { get; init; }

    /// <summary>True if the test was skipped (e.g. unsupported format).</summary>
    public bool Skipped { get; init; }

    /// <summary>Optional reason for skip or failure.</summary>
    public string? Message { get; init; }

    /// <summary>
    /// Percent match between the rendered output and the reference image
    /// (0–100). Null when no comparison was performed (e.g. skipped or
    /// error before the pixel comparison stage).
    /// </summary>
    public double? MatchPercent { get; init; }
}

/// <summary>
/// Discovers and executes web-platform-tests by rendering each HTML file
/// through the Broiler HTML/JavaScript stack and comparing the output to
/// a Chromium/Playwright reference image.
/// </summary>
internal sealed class WptTestRunner
{
    /// <summary>
    /// File extensions treated as test files.
    /// </summary>
    private static readonly HashSet<string> TestExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html",
        ".htm",
        ".xhtml",
    };

    /// <summary>
    /// Determines whether a test file is a WPT "crash test".
    /// Crash tests only verify that the renderer does not crash; no pixel
    /// comparison is performed.  A test is considered a crash test if:
    /// <list type="bullet">
    ///   <item>It resides under a <c>crashtests</c> directory, or</item>
    ///   <item>Its file name (without extension) ends with <c>-crash</c>.</item>
    /// </list>
    /// </summary>
    internal static bool IsCrashTest(string testPath)
    {
        // Normalize separators so the check works on all platforms.
        string normalized = testPath.Replace('\\', '/');

        // Check for a "crashtests" path segment.
        if (normalized.Contains("/crashtests/", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for a "-crash" suffix in the file name (before the extension).
        string nameWithoutExt = Path.GetFileNameWithoutExtension(testPath);
        if (nameWithoutExt.EndsWith("-crash", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Regex to extract inline and external <c>&lt;script&gt;</c> blocks.
    /// Mirrors the pattern used by <see cref="Broiler.Cli.CaptureService"/>.
    /// </summary>
    private static readonly Regex ScriptTagPattern = new(
        @"<script(?<attrs>[^>]*)>(?<content>[\s\S]*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly int _width;
    private readonly int _height;

    public WptTestRunner(int width = 1024, int height = 768)
    {
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Recursively discovers all test files under <paramref name="wptRoot"/>.
    /// </summary>
    internal static IEnumerable<string> DiscoverTests(string wptRoot)
    {
        return Directory.EnumerateFiles(wptRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => TestExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Runs a single test: renders the HTML with the Broiler stack and
    /// compares the result against a Chromium/Playwright reference image.
    /// </summary>
    internal WptTestResult RunTest(string testPath, string referenceDir, string? wptRoot = null)
    {
        if (!File.Exists(testPath))
        {
            return new WptTestResult
            {
                TestPath = testPath,
                Passed = false,
                Message = "Test file not found.",
            };
        }

        // Derive the reference image path by mirroring the test path
        // structure under the reference directory.  When wptRoot is
        // provided the full sub-directory hierarchy is preserved so
        // that tests in different directories don't collide.
        string relativePath;
        if (wptRoot is not null)
        {
            relativePath = Path.GetRelativePath(wptRoot, testPath);
        }
        else
        {
            relativePath = Path.GetFileName(testPath);
        }

        // Reference images use .png extension regardless of test format.
        string referencePath = Path.Combine(
            referenceDir,
            Path.ChangeExtension(relativePath, ".png"));

        string html;
        try
        {
            html = File.ReadAllText(testPath);
        }
        catch (Exception ex)
        {
            return new WptTestResult
            {
                TestPath = testPath,
                Passed = false,
                Message = $"Failed to read test file: {ex.Message}",
            };
        }

        // Execute inline scripts via DomBridge.
        try
        {
            html = ExecuteScriptsWithDom(html, new Uri(Path.GetFullPath(testPath)).AbsoluteUri);
        }
        catch (Exception ex)
        {
            return new WptTestResult
            {
                TestPath = testPath,
                Passed = false,
                Message = $"Script execution failed: {ex.Message}",
            };
        }

        // Post-process HTML (strip scripts, clean up for rendering).
        html = HtmlPostProcessor.Process(html);

        // Render via Broiler HTML stack.
        SKBitmap rendered;
        try
        {
            rendered = HtmlRender.RenderToImage(html, _width, _height, SKColors.White);
        }
        catch (Exception ex)
        {
            return new WptTestResult
            {
                TestPath = testPath,
                Passed = false,
                Message = $"Rendering failed: {ex.Message}",
            };
        }

        using (rendered)
        {
            // WPT crash tests only verify the renderer doesn't crash.
            // If rendering succeeded, the test passes — skip pixel comparison.
            if (IsCrashTest(testPath))
            {
                return new WptTestResult
                {
                    TestPath = testPath,
                    Passed = true,
                    Message = "Crash test: rendering completed without error.",
                };
            }

            // If no reference image exists, the test is skipped.
            if (!File.Exists(referencePath))
            {
                return new WptTestResult
                {
                    TestPath = testPath,
                    Skipped = true,
                    Message = $"No reference image at: {referencePath}",
                };
            }

            using var reference = SKBitmap.Decode(referencePath);
            if (reference is null)
            {
                return new WptTestResult
                {
                    TestPath = testPath,
                    Passed = false,
                    Message = $"Failed to decode reference image: {referencePath}",
                };
            }

            using var diff = PixelDiffRunner.Compare(rendered, reference);

            // Compute percent match for every comparison so it can be
            // included in the logfile output and used for sorting.
            double matchPct = (1.0 - diff.DiffRatio) * 100;

            if (diff.IsMatch)
            {
                return new WptTestResult
                {
                    TestPath = testPath,
                    Passed = true,
                    MatchPercent = matchPct,
                };
            }

            return new WptTestResult
            {
                TestPath = testPath,
                Passed = false,
                MatchPercent = matchPct,
                Message = $"Pixel mismatch: {matchPct:F1}% match ({diff.DiffPixelCount}/{diff.TotalPixelCount} pixels differ)",
            };
        }
    }

    /// <summary>
    /// Runs all discovered tests under <paramref name="wptRoot"/>, comparing
    /// each against reference images in <paramref name="referenceDir"/>.
    /// Yields results as they complete.
    /// </summary>
    internal IEnumerable<WptTestResult> RunAll(string wptRoot, string referenceDir)
    {
        foreach (var testPath in DiscoverTests(wptRoot))
        {
            yield return RunTest(testPath, referenceDir, wptRoot);
        }
    }

    /// <summary>
    /// Extracts and executes inline/external scripts via the DomBridge,
    /// returning the post-execution HTML with DOM mutations applied.
    /// </summary>
    private static string ExecuteScriptsWithDom(string html, string url)
    {
        var scripts = new List<string>();
        var deferredScripts = new List<string>();

        foreach (Match match in ScriptTagPattern.Matches(html))
        {
            var attrs = match.Groups["attrs"].Value;

            // Skip non-JavaScript types (e.g. type="text/template").
            if (attrs.Contains("type=", StringComparison.OrdinalIgnoreCase))
            {
                var typeMatch = Regex.Match(attrs, @"type\s*=\s*[""']?([^""'\s>]+)", RegexOptions.IgnoreCase);
                if (typeMatch.Success)
                {
                    var type = typeMatch.Groups[1].Value;
                    if (!string.IsNullOrEmpty(type)
                        && !type.Equals("text/javascript", StringComparison.OrdinalIgnoreCase)
                        && !type.Equals("application/javascript", StringComparison.OrdinalIgnoreCase)
                        && !type.Equals("module", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
            }

            bool isDefer = attrs.Contains("defer", StringComparison.OrdinalIgnoreCase);

            // Inline script
            var content = match.Groups["content"].Value.Trim();
            if (string.IsNullOrEmpty(content)) continue;

            if (isDefer)
                deferredScripts.Add(content);
            else
                scripts.Add(content);
        }

        if (scripts.Count == 0 && deferredScripts.Count == 0)
            return html;

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, url);

        foreach (var script in scripts)
        {
            try
            {
                context.Eval(script);
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "WptTestRunner.ExecuteScriptsWithDom",
                    $"Script execution error: {ex.Message}", ex);
            }
        }

        foreach (var script in deferredScripts)
        {
            try
            {
                context.Eval(script);
            }
            catch (Exception ex)
            {
                RenderLogger.LogError(LogCategory.JavaScript, "WptTestRunner.ExecuteScriptsWithDom",
                    $"Deferred script error: {ex.Message}", ex);
            }
        }

        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        return bridge.SerializeToHtml();
    }
}
