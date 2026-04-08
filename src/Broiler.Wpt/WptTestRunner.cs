using System.Text.RegularExpressions;
using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;
using SkiaSharp;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Broiler.Wpt;

/// <summary>
/// Categorizes the root cause of a WPT test failure for fast triage.
/// </summary>
internal enum FailureCategory
{
    /// <summary>No failure (test passed or was skipped).</summary>
    None,
    /// <summary>The test file could not be read from disk.</summary>
    FileIO,
    /// <summary>JavaScript execution threw an exception.</summary>
    ScriptError,
    /// <summary>The Broiler rendering pipeline threw an exception.</summary>
    RenderingError,
    /// <summary>The reference image could not be decoded.</summary>
    ReferenceDecodeError,
    /// <summary>Rendered output did not match the reference image.</summary>
    PixelMismatch,
    /// <summary>Catch-all for failures that don't fit another category.</summary>
    Unknown,
}

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

    /// <summary>
    /// Root cause category for failed tests.  <see cref="FailureCategory.None"/>
    /// for passing or skipped tests.
    /// </summary>
    public FailureCategory Category { get; init; }

    /// <summary>
    /// Stack trace captured when the failure originated from an exception.
    /// Null when no exception was thrown.
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Detailed diagnostics for <see cref="FailureCategory.PixelMismatch"/>
    /// failures.  Null for all other categories.
    /// </summary>
    public MismatchDiagnostics? MismatchDiagnostics { get; init; }

    /// <summary>
    /// Serialises this result to a JSON-friendly dictionary.
    /// </summary>
    internal Dictionary<string, object?> ToJsonObject()
    {
        var obj = new Dictionary<string, object?>
        {
            ["testPath"] = TestPath,
            ["passed"] = Passed,
            ["skipped"] = Skipped,
            ["matchPercent"] = MatchPercent,
            ["category"] = Category.ToString(),
            ["message"] = Message,
        };

        if (MismatchDiagnostics is { } diag)
        {
            obj["mismatchDiagnostics"] = new Dictionary<string, object?>
            {
                ["subCategory"] = diag.Category.ToString(),
                ["averageChannelDelta"] = diag.AverageChannelDelta,
                ["maxChannelDelta"] = diag.MaxChannelDelta,
                ["affectedRows"] = diag.AffectedRows,
                ["affectedColumns"] = diag.AffectedColumns,
                ["summary"] = diag.Summary,
            };
        }

        return obj;
    }
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
    /// Determines whether the given test path is a WPT crash test.
    /// Crash tests are identified by:
    /// <list type="bullet">
    ///   <item>The path contains a <c>/crashtests/</c> directory segment.</item>
    ///   <item>The filename (without extension) ends with <c>-crash</c>.</item>
    /// </list>
    /// Crash tests pass when rendering completes without throwing; no pixel
    /// comparison is required.
    /// </summary>
    internal static bool IsCrashTest(string testPath)
    {
        // Normalise separators so the check works on both Unix and Windows paths.
        if (testPath.Contains("/crashtests/", StringComparison.OrdinalIgnoreCase) ||
            testPath.Contains("\\crashtests\\", StringComparison.OrdinalIgnoreCase))
            return true;

        var name = Path.GetFileNameWithoutExtension(testPath);
        return name.EndsWith("-crash", StringComparison.OrdinalIgnoreCase);
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
                Category = FailureCategory.FileIO,
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
                Category = FailureCategory.FileIO,
                StackTrace = ex.StackTrace,
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
                Category = FailureCategory.ScriptError,
                StackTrace = ex.StackTrace,
            };
        }

        // Post-process HTML (strip scripts, clean up for rendering).
        html = HtmlPostProcessor.Process(html);

        // Derive the base URL from the test file so that relative sub-resource
        // paths (background images, stylesheets, etc.) resolve correctly.
        var testBaseUrl = new Uri(Path.GetFullPath(testPath)).AbsoluteUri;

        // Render via Broiler HTML stack.
        SKBitmap rendered;
        try
        {
            rendered = HtmlRender.RenderToImage(html, _width, _height, SKColors.White, baseUrl: testBaseUrl);
        }
        catch (Exception ex)
        {
            return new WptTestResult
            {
                TestPath = testPath,
                Passed = false,
                Message = $"Rendering failed: {ex.Message}",
                Category = FailureCategory.RenderingError,
                StackTrace = ex.StackTrace,
            };
        }

        using (rendered)
        {
            // WPT crash tests only verify the renderer doesn't crash.
            // No pixel comparison is needed; the test passes if rendering
            // completed without throwing.
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
                    Category = FailureCategory.ReferenceDecodeError,
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

            // Classify the mismatch to provide actionable diagnostics.
            var diagnostics = MismatchClassifier.Classify(
                diff,
                rendered.Width, rendered.Height,
                reference.Width, reference.Height);

            return new WptTestResult
            {
                TestPath = testPath,
                Passed = false,
                MatchPercent = matchPct,
                Message = $"Pixel mismatch: {matchPct:F1}% match ({diff.DiffPixelCount}/{diff.TotalPixelCount} pixels differ) — {diagnostics.Summary}",
                Category = FailureCategory.PixelMismatch,
                MismatchDiagnostics = diagnostics,
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
