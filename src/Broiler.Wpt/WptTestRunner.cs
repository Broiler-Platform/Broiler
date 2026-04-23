using System.Text.RegularExpressions;
using Broiler.HtmlBridge;
using Broiler.HTML.Core.Core.Entities;
using Broiler.HTML.Image;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;
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
    /// <summary>The test exceeded the configured execution timeout.</summary>
    Timeout,
    /// <summary>The reference image could not be decoded.</summary>
    ReferenceDecodeError,
    /// <summary>Rendered output did not match the reference image.</summary>
    PixelMismatch,
    /// <summary>Catch-all for failures that don't fit another category.</summary>
    Unknown,
}

/// <summary>
/// Machine-readable reason for skipped WPT cases.
/// </summary>
internal enum SkipReason
{
    None,
    MissingReferenceImage,
    UnsupportedMediaPlayback,
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
    /// Machine-readable reason for skipped tests. <see cref="SkipReason.None"/>
    /// for passed/failed tests.
    /// </summary>
    public SkipReason SkipReason { get; init; }

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
    internal Dictionary<string, object?> ToJsonObject(string? wptPath = null)
    {
        var obj = new Dictionary<string, object?>
        {
            ["testPath"] = TestPath,
            ["passed"] = Passed,
            ["skipped"] = Skipped,
            ["skipReason"] = SkipReason != SkipReason.None ? SkipReason.ToString() : null,
            ["matchPercent"] = MatchPercent,
            ["category"] = Category.ToString(),
            ["message"] = Message,
            ["stackTrace"] = StackTrace,
        };

        if (!string.IsNullOrWhiteSpace(wptPath))
        {
            var relativeTestPath = Path.GetRelativePath(wptPath, TestPath).Replace('\\', '/');
            if (!relativeTestPath.StartsWith("../", StringComparison.Ordinal) &&
                !relativeTestPath.Equals("..", StringComparison.Ordinal))
            {
                obj["relativeTestPath"] = relativeTestPath;
            }
        }

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
    /// Regex that detects <c>&lt;video&gt;</c> elements with a <c>&lt;source&gt;</c>
    /// child pointing to an external media file.  Broiler cannot decode video
    /// streams, so tests that depend on video playback produce fundamentally
    /// different output from browsers and should be skipped.
    /// </summary>
    private static readonly Regex VideoWithSourcePattern = new(
        @"<video\b[^>]*>[\s\S]*?<source\b[^>]*\bsrc\s*=",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Regex to extract inline and external <c>&lt;script&gt;</c> blocks.
    /// Mirrors the pattern used by <see cref="Broiler.Cli.CaptureService"/>.
    /// </summary>
    private static readonly Regex ScriptTagPattern = new(
        @"<script(?<attrs>[^>]*)>(?<content>[\s\S]*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Regex to extract the <c>src</c> attribute from a script element.
    /// </summary>
    private static readonly Regex ScriptSrcPattern = new(
        @"src\s*=\s*[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Minimal stubs for WPT testharness.js and check-layout-th.js so that
    /// inline test scripts can execute without errors.  Only the functions
    /// that cause script failures are stubbed; the stubs execute callbacks
    /// synchronously so that DOM-mutating side-effects (e.g. setting
    /// <c>element.style.positionArea</c>) are visible after JS execution.
    /// </summary>
    private const string TestharnessStubs = @"
(function() {
  var __broilerPromiseTests = [];
  var __broilerWindowLoaded = false;
  if (typeof test === 'undefined') {
    window.test = function(func, name) { try { func(); } catch(e) {} };
  }
  if (typeof async_test === 'undefined') {
    window.async_test = function(func, name) {
      var t = { step: function(f){try{f();}catch(e){}}, done: function(){}, step_func: function(f){return f;}, step_func_done: function(f){return f;}, step_timeout: function(f,d){try{f();}catch(e){}} };
      if (func) { try { func(t); } catch(e) {} }
      return t;
    };
  }
  if (typeof promise_test === 'undefined') {
    function __broilerRunPromiseTest(func) {
      try {
        var result = func();
        if (result && typeof result.then === 'function') {
          result.then(function(){}, function(){});
        }
      } catch (e) {}
    }

    // Real testharness.js defers promise_test execution until after page load.
    // Queue callbacks here so load-driven harness pages can still mutate the
    // DOM before Broiler serializes the final result.
    window.promise_test = function(func, name) {
      if (typeof func !== 'function') {
        return;
      }

      if (__broilerWindowLoaded) {
        __broilerRunPromiseTest(func);
        return;
      }

      __broilerPromiseTests.push(func);
    };

    window.addEventListener('load', function() {
      __broilerWindowLoaded = true;
      while (__broilerPromiseTests.length) {
        __broilerRunPromiseTest(__broilerPromiseTests.shift());
      }
    });
  }
  if (typeof assert_equals === 'undefined') {
    window.assert_equals = function(a, b, msg) {};
  }
  if (typeof assert_true === 'undefined') {
    window.assert_true = function(v, msg) {};
  }
  if (typeof assert_false === 'undefined') {
    window.assert_false = function(v, msg) {};
  }
  if (typeof assert_unreached === 'undefined') {
    window.assert_unreached = function(msg) {};
  }
  if (typeof assert_approx_equals === 'undefined') {
    window.assert_approx_equals = function(a, b, eps, msg) {};
  }
  if (typeof assert_less_than === 'undefined') {
    window.assert_less_than = function(a, b, msg) {};
  }
  if (typeof assert_greater_than === 'undefined') {
    window.assert_greater_than = function(a, b, msg) {};
  }
  if (typeof setup === 'undefined') {
    window.setup = function(obj) {};
  }
  if (typeof done === 'undefined') {
    window.done = function() {};
  }
  if (typeof checkLayout === 'undefined') {
    window.checkLayout = function(selector, callDone) {};
  }
})();
";

    /// <summary>
    /// Minimal stubs for browser APIs that the DomBridge JavaScript context
    /// does not natively provide.  These are injected unconditionally before
    /// any test scripts execute so that common Web APIs (such as
    /// <c>requestAnimationFrame</c>) are available even when testharness.js
    /// is not referenced.  WPT tests that use <c>requestAnimationFrame</c>
    /// inside <c>window.onload</c> handlers depend on this.
    /// </summary>
    private const string BrowserApiStubs = @"
(function() {
  var __broilerCustomElementRegistry = Object.create(null);
  var __broilerCurrentCustomElementName = null;
  var __broilerNativeCreateElement = document.createElement.bind(document);
  function __broilerCreateAnimationResult() {
    return {
      finished: Promise.resolve(),
      cancel: function() {},
      play: function() {},
      pause: function() {}
    };
  }
  function __broilerEnsureAnimate(element) {
    if (element && typeof element.animate === 'undefined') {
      element.animate = function() {
        return __broilerCreateAnimationResult();
      };
    }
    return element;
  }

  // Always override requestAnimationFrame with a synchronous stub.
  // DomBridge registers a native async rAF that queues callbacks into
  // _rafCallbacks for later execution via FlushTimerStep().  However,
  // style mutations (e.g. element.style.display = 'none') made inside
  // those async callbacks do not persist correctly on the C# DomElement.
  // By unconditionally replacing rAF with a synchronous implementation,
  // callbacks execute immediately during Eval / FireWindowLoadEvent,
  // which avoids the FlushTimerStep persistence bug.
  window.requestAnimationFrame = function(cb) { cb(0); return 0; };
  window.cancelAnimationFrame = function(id) {};
  if (typeof takeScreenshot === 'undefined') {
    window.takeScreenshot = function() {
      if (document.documentElement) {
        document.documentElement.classList.remove('reftest-wait');
      }
    };
  }
  if (typeof waitForAtLeastOneFrame === 'undefined') {
    window.waitForAtLeastOneFrame = function() {
      return Promise.resolve();
    };
  }
  __broilerEnsureAnimate(document.documentElement);
  __broilerEnsureAnimate(document.body);
  var test_driver = window.test_driver || {};
  window.test_driver = test_driver;
  if (typeof test_driver.Actions === 'undefined') {
    function Actions() {
      this._pointers = Object.create(null);
    }

    Actions.prototype.addPointer = function(name, type) {
      this._pointers[String(name || 'pointer')] = {
        type: String(type || 'mouse'),
        moves: []
      };
      return this;
    };

    Actions.prototype.pointerMove = function(x, y, options) {
      var sourceName = options && options.sourceName
        ? String(options.sourceName)
        : Object.keys(this._pointers)[0] || 'pointer';
      if (!this._pointers[sourceName]) {
        this.addPointer(sourceName, 'mouse');
      }

      this._pointers[sourceName].moves.push({
        x: Number(x) || 0,
        y: Number(y) || 0
      });
      return this;
    };

    Actions.prototype.pointerDown = function() { return this; };
    Actions.prototype.pointerUp = function() { return this; };
    Actions.prototype.pause = function() { return this; };
    Actions.prototype.keyDown = function() { return this; };
    Actions.prototype.keyUp = function() { return this; };
    Actions.prototype.scroll = function() { return this; };
    Actions.prototype.send = function() {
      var touchPointers = Object.keys(this._pointers)
        .map((name) => this._pointers[name])
        .filter((pointer) => pointer.type === 'touch' && pointer.moves.length > 0);

      if (touchPointers.length >= 2 &&
          window.visualViewport &&
          typeof visualViewport.scale !== 'undefined') {
        var first = touchPointers[0];
        var second = touchPointers[1];
        var firstStart = first.moves[0];
        var secondStart = second.moves[0];
        var firstEnd = first.moves[first.moves.length - 1];
        var secondEnd = second.moves[second.moves.length - 1];
        var startDistance = Math.hypot(firstStart.x - secondStart.x, firstStart.y - secondStart.y);
        var endDistance = Math.hypot(firstEnd.x - secondEnd.x, firstEnd.y - secondEnd.y);

        if (endDistance > startDistance + 1) {
          visualViewport.scale = Math.max(Number(visualViewport.scale) || 1, 2);
        }
      }

      return Promise.resolve();
    };

    test_driver.Actions = Actions;
  }
  if (typeof HTMLElement === 'undefined') {
    window.HTMLElement = function HTMLElement() {
      return __broilerEnsureAnimate(__broilerNativeCreateElement(__broilerCurrentCustomElementName || 'div'));
    };
  }
  if (typeof customElements === 'undefined') {
    function __broilerUpgradeElement(tagName, ctor, sourceElement) {
      __broilerCurrentCustomElementName = tagName;
      var upgraded = null;
      try {
        upgraded = new ctor();
      } catch (e) {
        upgraded = null;
      }
      __broilerCurrentCustomElementName = null;

      if (!upgraded) {
        return sourceElement;
      }

      __broilerEnsureAnimate(upgraded);

      if (sourceElement && upgraded !== sourceElement) {
        if (sourceElement.attributes) {
          for (var i = 0; i < sourceElement.attributes.length; i++) {
            var attr = sourceElement.attributes[i];
            upgraded.setAttribute(attr.name, attr.value);
          }
        }

        while (sourceElement.firstChild) {
          upgraded.appendChild(sourceElement.firstChild);
        }

        if (sourceElement.parentNode) {
          sourceElement.parentNode.replaceChild(upgraded, sourceElement);
        }

        if (sourceElement.id && window[sourceElement.id] === sourceElement) {
          window[sourceElement.id] = upgraded;
        }
      }

      upgraded.__broilerCustomElementTagName = tagName;
      return upgraded;
    }

    document.createElement = function(name) {
      var tagName = String(name || '').toLowerCase();
      if (__broilerCurrentCustomElementName === tagName) {
        return __broilerNativeCreateElement(tagName);
      }

      var ctor = __broilerCustomElementRegistry[tagName];
      if (!ctor) {
        return __broilerEnsureAnimate(__broilerNativeCreateElement(tagName));
      }

      return __broilerUpgradeElement(tagName, ctor, null);
    };

    window.customElements = {
      define: function(name, ctor) {
        var tagName = String(name || '').toLowerCase();
        __broilerCustomElementRegistry[tagName] = ctor;

        var existing = document.querySelectorAll(tagName);
        for (var i = 0; i < existing.length; i++) {
          __broilerUpgradeElement(tagName, ctor, existing[i]);
        }
      },
      get: function(name) {
        return __broilerCustomElementRegistry[String(name || '').toLowerCase()];
      },
      whenDefined: function(name) {
        return Promise.resolve(__broilerCustomElementRegistry[String(name || '').toLowerCase()]);
      }
    };
  }
})();
";

    private readonly int _width;
    private readonly int _height;

    public WptTestRunner(int width = 1024, int height = 768)
    {
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Directory segments that indicate a file is not an actual test.
    /// Per WPT conventions, <c>reference/</c> and <c>reftest/</c> hold
    /// reference comparison files, <c>support/</c> holds shared resources,
    /// and <c>test-plan/</c> holds specification documentation.
    /// </summary>
    private static readonly string[] NonTestDirSegments =
    {
        "/reference/", "\\reference\\",
        "/references/", "\\references\\",
        "/reftest/", "\\reftest\\",
        "/support/", "\\support\\",
        "/test-plan/", "\\test-plan\\",
    };

    /// <summary>
    /// Recursively discovers all test files under <paramref name="wptRoot"/>,
    /// excluding non-test files per WPT conventions (reference files, support
    /// resources, test-plan documentation, and ReSpec source files).
    /// </summary>
    internal static IEnumerable<string> DiscoverTests(string wptRoot)
    {
        return Directory.EnumerateFiles(wptRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => TestExtensions.Contains(Path.GetExtension(f)) && !IsNonTestFile(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Discovers test files under <paramref name="wptRoot"/> that match
    /// the given <paramref name="subsetPatterns"/>.  Patterns may contain
    /// <c>*</c> and <c>?</c> wildcards (glob-style) and are matched
    /// against each file's path relative to <paramref name="wptRoot"/>
    /// using forward-slash separators.  Multiple patterns can be supplied;
    /// a file is included when it matches <em>any</em> of the patterns.
    /// </summary>
    internal static IEnumerable<string> DiscoverTests(string wptRoot, IReadOnlyList<string> subsetPatterns)
    {
        if (subsetPatterns.Count == 0)
            return DiscoverTests(wptRoot);

        return DiscoverTests(wptRoot)
            .Where(f =>
            {
                var rel = Path.GetRelativePath(wptRoot, f).Replace('\\', '/');
                return MatchesAnyPattern(rel, subsetPatterns);
            });
    }

    /// <summary>
    /// Parses a semicolon-separated subset string into individual patterns,
    /// trimming whitespace and discarding empty entries.
    /// </summary>
    internal static string[] ParseSubsetPatterns(string subset)
    {
        if (string.IsNullOrWhiteSpace(subset))
            return [];

        return subset.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="relativePath"/>
    /// matches any of the supplied glob patterns.  Each pattern is treated
    /// as a prefix — a file matches when its relative path starts with the
    /// pattern (after wildcard expansion).  A trailing <c>**</c> is
    /// implicitly appended when the pattern does not end with a wildcard
    /// character, so <c>css/CSS2</c> matches <c>css/CSS2/test.html</c>.
    /// </summary>
    internal static bool MatchesAnyPattern(string relativePath, IReadOnlyList<string> patterns)
    {
        foreach (var raw in patterns)
        {
            if (MatchesPattern(relativePath, raw))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Matches a single glob-like pattern against a relative path.
    /// <list type="bullet">
    ///   <item><c>*</c> matches zero or more characters except <c>/</c>.</item>
    ///   <item><c>?</c> matches exactly one character except <c>/</c>.</item>
    /// </list>
    /// If the pattern contains no wildcard characters, or ends without a
    /// wildcard, it is treated as a directory prefix (i.e. an implicit
    /// <c>/**</c> is appended) so that <c>css/CSS2</c> matches all files
    /// under that directory.
    /// </summary>
    internal static bool MatchesPattern(string relativePath, string pattern)
    {
        // Normalise separators in the pattern to forward slash.
        var normalized = pattern.Replace('\\', '/').TrimEnd('/');

        if (string.IsNullOrEmpty(normalized))
            return true;

        bool hasWildcard = normalized.Contains('*') || normalized.Contains('?');

        if (!hasWildcard)
        {
            // Exact directory prefix: path must start with "pattern/" or equal the pattern.
            return relativePath.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase)
                || relativePath.Equals(normalized, StringComparison.OrdinalIgnoreCase);
        }

        // Convert glob pattern to a regex that matches the full relative path
        // as a prefix (or the entire path).
        var regexPattern = GlobToRegex(normalized);
        return Regex.IsMatch(relativePath, regexPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Converts a glob-style pattern into a regular expression string.
    /// The regex is anchored at the start and always appends an optional
    /// <c>(/.*)?$</c> suffix so that files beneath the matched prefix
    /// are included (e.g. <c>css/css-*</c> matches
    /// <c>css/css-flexbox/test.html</c>).
    /// </summary>
    private static string GlobToRegex(string glob)
    {
        var sb = new System.Text.StringBuilder("^");

        for (int i = 0; i < glob.Length; i++)
        {
            char c = glob[i];
            switch (c)
            {
                case '*':
                    sb.Append("[^/]*");
                    break;
                case '?':
                    sb.Append("[^/]");
                    break;
                default:
                    sb.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        sb.Append("(/.*)?$");
        return sb.ToString();
    }

    /// <summary>
    /// Determines whether the given file path is a WPT non-test file that
    /// should be excluded from test execution.  Non-test files include:
    /// <list type="bullet">
    ///   <item>Files in <c>reference/</c>, <c>reftest/</c>, or <c>support/</c> directories.</item>
    ///   <item>Files in <c>test-plan/</c> directories (spec documentation).</item>
    ///   <item>Files ending in <c>-ref.html/.htm/.xhtml</c> or <c>-notref.html/.htm/.xhtml</c>
    ///         (WPT reference/mismatch reference files).</item>
    ///   <item>Files with a <c>.src.html</c> extension (ReSpec source files).</item>
    /// </list>
    /// </summary>
    internal static bool IsNonTestFile(string filePath)
    {
        // Check for non-test directory segments.
        foreach (var segment in NonTestDirSegments)
        {
            if (filePath.Contains(segment, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // ReSpec source files: *.src.html
        var fileName = Path.GetFileName(filePath);
        if (fileName.EndsWith(".src.html", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".src.htm", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".src.xhtml", StringComparison.OrdinalIgnoreCase))
            return true;

        // WPT reference / mismatch reference files: *-ref.html, *-notref.html
        var nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
        if (nameWithoutExt.EndsWith("-ref", StringComparison.OrdinalIgnoreCase) ||
            nameWithoutExt.EndsWith("-notref", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Determines whether the given HTML content requires media playback
    /// (e.g. <c>&lt;video&gt;</c> with an external <c>&lt;source&gt;</c>).
    /// Broiler cannot decode video/audio streams, so these tests produce
    /// fundamentally different output and should be skipped.
    /// </summary>
    internal static bool IsMediaPlaybackTest(string htmlContent)
    {
        return VideoWithSourcePattern.IsMatch(htmlContent);
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

        // Skip tests that require media playback (video/audio streams)
        // which Broiler cannot decode.
        if (IsMediaPlaybackTest(html))
        {
            return new WptTestResult
            {
                TestPath = testPath,
                Skipped = true,
                SkipReason = SkipReason.UnsupportedMediaPlayback,
                Message = "Test requires media playback (video/audio) which is not supported.",
            };
        }

        // Execute inline scripts via DomBridge.
        try
        {
            html = ExecuteScriptsWithDom(
                html,
                new Uri(Path.GetFullPath(testPath)).AbsoluteUri,
                wptRoot,
                batchStyleInvalidations: IsCrashTest(testPath));
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

        // Pre-load WPT fonts when wptRoot is known.  WPT test files reference
        // fonts via root-relative URLs (e.g. @import "/fonts/ahem.css") that
        // resolve to {wptRoot}/fonts/ on disk.  Registering known WPT fonts
        // directly with the adapter guarantees CSS font-family references work
        // even when the @font-face stylesheet import path resolution fails.
        if (wptRoot != null)
            EnsureWptFontsLoaded(wptRoot);

        // Derive the base URL from the test file so that relative sub-resource
        // paths (background images, stylesheets, etc.) resolve correctly.
        var testBaseUrl = new Uri(Path.GetFullPath(testPath)).AbsoluteUri;

        // Build a stylesheet load handler that resolves root-relative WPT
        // paths (e.g. "/fonts/ahem.css") against the WPT root directory so
        // that @import rules that use WPT-server-relative paths are honoured.
        EventHandler<HtmlStylesheetLoadEventArgs> stylesheetHandler = null;
        if (wptRoot != null)
        {
            var capturedWptRoot = wptRoot;
            stylesheetHandler = (_, args) =>
            {
                var src = args.Src;
                if (src != null && src.StartsWith("/", StringComparison.Ordinal))
                {
                    var rel = src.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var local = Path.Combine(capturedWptRoot, rel);
                    if (File.Exists(local))
                        args.SetSrc = local;
                }
            };
        }

        // Render via Broiler HTML stack.
        SKBitmap rendered;
        try
        {
            rendered = HtmlRender.RenderToImage(html, _width, _height, SKColors.White,
                stylesheetLoad: stylesheetHandler, baseUrl: testBaseUrl);
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
                    SkipReason = SkipReason.MissingReferenceImage,
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
    /// Runs a WPT <c>rel="match"</c> test by rendering both the test HTML and
    /// its reference HTML with the Broiler stack, then comparing the two bitmaps.
    /// This avoids font-rendering / scrollbar differences between Chromium and
    /// Broiler that make cross-engine comparison imprecise.
    /// </summary>
    /// <param name="testPath">Path to the test HTML file.</param>
    /// <param name="refHtmlPath">Path to the WPT reference HTML file.</param>
    /// <param name="wptRoot">Optional WPT root directory for font loading.</param>
    internal WptTestResult RunMatchTest(string testPath, string refHtmlPath, string? wptRoot = null)
    {
        if (!File.Exists(testPath))
            return new WptTestResult { TestPath = testPath, Passed = false,
                Message = "Test file not found.", Category = FailureCategory.FileIO };
        if (!File.Exists(refHtmlPath))
            return new WptTestResult { TestPath = testPath, Passed = false,
                Message = $"Reference HTML not found: {refHtmlPath}", Category = FailureCategory.FileIO };

        if (wptRoot != null)
            EnsureWptFontsLoaded(wptRoot);

        SKBitmap? rendered = null;
        SKBitmap? reference = null;
        try
        {
            rendered = RenderHtmlFile(testPath, wptRoot);
            reference = RenderHtmlFile(refHtmlPath, wptRoot);

            using var diff = PixelDiffRunner.Compare(rendered, reference);
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
        catch (Exception ex)
        {
            return new WptTestResult
            {
                TestPath = testPath,
                Passed = false,
                Message = $"Match test failed: {ex.Message}",
                Category = FailureCategory.RenderingError,
                StackTrace = ex.StackTrace,
            };
        }
        finally
        {
            rendered?.Dispose();
            reference?.Dispose();
        }
    }

    /// <summary>
    /// Renders an HTML file through the full Broiler pipeline (script execution,
    /// anchor resolution, rendering) and returns the resulting bitmap.
    /// </summary>
    private SKBitmap RenderHtmlFile(string htmlPath, string? wptRoot)
    {
        var html = File.ReadAllText(htmlPath);

        if (IsMediaPlaybackTest(html))
            throw new InvalidOperationException("Test requires media playback.");

        var testBaseUrl = new Uri(Path.GetFullPath(htmlPath)).AbsoluteUri;

        // Set local base path for sub-resource resolution.
        html = ExecuteScriptsWithDom(html, testBaseUrl, wptRoot);
        html = HtmlPostProcessor.Process(html);

        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetHandler = null;
        if (wptRoot != null)
        {
            var capturedWptRoot = wptRoot;
            stylesheetHandler = (_, args) =>
            {
                var src = args.Src;
                if (src != null && src.StartsWith("/", StringComparison.Ordinal))
                {
                    var rel = src.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var local = Path.Combine(capturedWptRoot, rel);
                    if (File.Exists(local))
                        args.SetSrc = local;
                }
            };
        }

        return HtmlRender.RenderToImage(html, _width, _height, SKColors.White,
            stylesheetLoad: stylesheetHandler, baseUrl: testBaseUrl);
    }

    /// <summary>
    /// Public wrapper for <see cref="RenderHtmlFile"/> that allows test code
    /// to render a single HTML file through the full WPT pipeline and inspect
    /// the resulting bitmap directly (e.g. for visual-inspection tests that
    /// lack a <c>rel="match"</c> reference).
    /// </summary>
    internal SKBitmap RenderHtmlFilePublic(string htmlPath, string? wptRoot)
    {
        if (wptRoot != null)
            EnsureWptFontsLoaded(wptRoot);
        return RenderHtmlFile(htmlPath, wptRoot);
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
    /// Runs discovered tests that match <paramref name="subsetPatterns"/>
    /// under <paramref name="wptRoot"/>, comparing each against reference
    /// images in <paramref name="referenceDir"/>.
    /// </summary>
    internal IEnumerable<WptTestResult> RunAll(string wptRoot, string referenceDir, IReadOnlyList<string> subsetPatterns)
    {
        foreach (var testPath in DiscoverTests(wptRoot, subsetPatterns))
        {
            yield return RunTest(testPath, referenceDir, wptRoot);
        }
    }

    /// <summary>
    /// Pre-loads well-known WPT fonts from <paramref name="wptRoot"/>
    /// into the rendering adapter so that <c>font-family</c> CSS references
    /// work correctly without relying on the @font-face stylesheet import
    /// mechanism (which uses root-relative URLs that cannot be resolved for
    /// <c>file://</c> base URLs).
    /// </summary>
    private static void EnsureWptFontsLoaded(string wptRoot)
    {
        var ahemPath = Path.Combine(wptRoot, "fonts", "Ahem.ttf");
        if (File.Exists(ahemPath))
            HtmlRender.LoadFontFromFile(ahemPath, "Ahem");
    }

    /// <summary>
    /// Extracts and executes inline/external scripts via the DomBridge,
    /// returning the post-execution HTML with DOM mutations applied.
    /// </summary>
    private static string ExecuteScriptsWithDom(
        string html,
        string url,
        string? wptRoot = null,
        bool batchStyleInvalidations = false)
    {
        var scripts = new List<string>();
        var deferredScripts = new List<string>();
        var microTasks = new MicroTaskQueue();

        // Determine the base directory for resolving relative script paths.
        string? testDir = null;
        if (url.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            var localPath = new Uri(url).LocalPath;
            testDir = Path.GetDirectoryName(localPath);
        }

        bool needsStubs = false;

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

            // Check for external script src attribute.
            var srcMatch = ScriptSrcPattern.Match(attrs);
            if (srcMatch.Success)
            {
                var src = srcMatch.Groups[1].Value;

                // Testharness / check-layout scripts → inject stubs.
                if (src.Contains("testharness", StringComparison.OrdinalIgnoreCase) ||
                    src.Contains("check-layout", StringComparison.OrdinalIgnoreCase))
                {
                    needsStubs = true;
                    continue;
                }

                var localScript = ResolveExternalScriptPath(src, testDir, wptRoot);
                if (localScript != null && File.Exists(localScript))
                {
                    var scriptContent = File.ReadAllText(localScript);
                    if (isDefer)
                        deferredScripts.Add(scriptContent);
                    else
                        scripts.Add(scriptContent);
                }

                continue;
            }

            // Inline script
            var content = match.Groups["content"].Value.Trim();
            if (string.IsNullOrEmpty(content)) continue;

            if (isDefer)
                deferredScripts.Add(content);
            else
                scripts.Add(content);
        }

        // Insert testharness stubs at the beginning if needed.
        if (needsStubs)
            scripts.Insert(0, TestharnessStubs);

        // Always inject browser API stubs (requestAnimationFrame, etc.)
        // so that tests relying on common Web APIs work even without
        // testharness.js.  Insert at position 0 so they are available
        // before any test script code runs.
        scripts.Insert(0, BrowserApiStubs);

        if (scripts.Count == 0 && deferredScripts.Count == 0)
        {
            // Even with no inline scripts, we still need to process anchor
            // positioning, animation snapshots, etc. via the DomBridge.
            using var context2 = new JSContext();
            var bridge2 = new DomBridge();
            bridge2.Attach(context2, html, url);
            // Inject browser API stubs so onload handlers etc. can reference them.
            try { context2.Eval(BrowserApiStubs); } catch { /* best-effort */ }
            bridge2.FireWindowLoadEvent();
            bridge2.FlushTimers();
            bridge2.ResolveAnimationSnapshots();
            bridge2.ResolveAnchorPositions();
            return bridge2.SerializeToHtml();
        }

        using var context = new JSContext();
        var bridge = new DomBridge();
        context["queueMicrotask"] = new JSFunction((in Arguments a) =>
        {
            if (a.Length > 0 && a[0] is JSFunction fn)
            {
                microTasks.Enqueue(() =>
                {
                    try { fn.InvokeFunction(new Arguments(JSUndefined.Value)); }
                    catch (Exception ex)
                    {
                        RenderLogger.LogError(LogCategory.JavaScript, "WptTestRunner.queueMicrotask",
                            $"Callback error: {ex.Message}", ex);
                    }
                });
            }

            return JSUndefined.Value;
        }, "queueMicrotask", 1);
        bridge.Attach(context, html, url);
        try { context.Eval("window.queueMicrotask = queueMicrotask;"); } catch { /* best-effort */ }

        // Register DOM elements with IDs as globals (HTML5 named access).
        bridge.RegisterNamedElementGlobals(context);

        void DrainAsyncWork()
        {
            // Match the broader DomBridge timer drain cap so promise/timer chains
            // used by WPT can settle without risking an infinite loop.
            const int maxIterations = 1000;
            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                bool hadWork = false;

                if (microTasks.Count > 0)
                {
                    microTasks.Drain();
                    hadWork = true;
                }

                if (bridge.HasPendingTimers)
                {
                    bridge.FlushTimerStep();
                    hadWork = true;
                }

                if (!hadWork)
                    break;
            }
        }

        if (batchStyleInvalidations)
            bridge.BeginStyleInvalidationBatch();

        try
        {
            foreach (var script in scripts)
            {
                try
                {
                    context.Eval(script);
                    DrainAsyncWork();
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
                    DrainAsyncWork();
                }
                catch (Exception ex)
                {
                    RenderLogger.LogError(LogCategory.JavaScript, "WptTestRunner.ExecuteScriptsWithDom",
                        $"Deferred script error: {ex.Message}", ex);
                }
            }

            bridge.FireWindowLoadEvent();
            DrainAsyncWork();
        }
        finally
        {
            if (batchStyleInvalidations)
                bridge.EndStyleInvalidationBatch();
        }

        // Resolve CSS animation snapshots: for elements with animation + negative
        // delay, compute the animated property values at t=0 and write them as
        // inline styles so the static renderer can produce the correct output.
        bridge.ResolveAnimationSnapshots();

        // Resolve CSS anchor positioning: for elements that use anchor()
        // functions, compute the anchored position from the target anchor
        // element's known CSS position and dimensions.  Also inserts
        // backdrop elements for modal <dialog> elements.
        bridge.ResolveAnchorPositions();

        return bridge.SerializeToHtml();
    }

    private static string? ResolveExternalScriptPath(string src, string? testDir, string? wptRoot)
    {
        if (string.IsNullOrWhiteSpace(src) ||
            src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return null;

        if (src.StartsWith("/", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(wptRoot))
                return null;

            var relativePath = src.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(wptRoot, relativePath);
        }

        if (string.IsNullOrWhiteSpace(testDir))
            return null;

        return Path.Combine(testDir, src.Replace('/', Path.DirectorySeparatorChar));
    }
}
