using System.Text.RegularExpressions;
using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;
using System.Text.Json;
using System.Text.Json.Serialization;
using Broiler.HTML.Core.Entities;
using Broiler.HtmlBridge.Logging;
using Broiler.HtmlBridge.Scripting;
using BColor = Broiler.Graphics.BColor;

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

    /// <summary>
    /// A WPT <em>manual</em> test (filename ends in <c>-manual</c>). These
    /// require human interaction and cannot be validated by an automated pixel
    /// harness, so — like the upstream wptrunner, which excludes them unless
    /// <c>--include-manual</c> — Broiler reports them as skipped rather than
    /// failed. See <see cref="WptTestRunner.IsManualTest"/>.
    /// </summary>
    ManualTest,
}

/// <summary>
/// First-class classification of a WPT test file by its kind, independent of the
/// pass/fail outcome (diagnostic #8). Surfacing these as their own buckets keeps a
/// regression in the classification visible — e.g. if manual-test detection breaks,
/// 59 tests would silently flip into the failure total — rather than hidden.
/// </summary>
internal enum TestKind
{
    /// <summary>An ordinary automated test compared against a reference image.</summary>
    Regular,

    /// <summary>Filename ends in <c>-manual</c>; requires human interaction (skipped).</summary>
    Manual,

    /// <summary>Tests a not-yet-standardised feature (<c>.tentative</c> in the name or a <c>tentative/</c> dir).</summary>
    Tentative,

    /// <summary>Passes if rendering does not crash (<c>-crash</c> suffix or <c>crashtests/</c> dir).</summary>
    CrashTest,
}

/// <summary>
/// A single failed <c>check-layout-th.js</c> geometry assertion: the element, the
/// checked property, and the expected vs. computed value.
/// </summary>
internal readonly record struct LayoutAssertionFailure(
    string Element, string Property, double Expected, double Actual)
{
    /// <summary>One-line, directly-actionable description (e.g. <c>span.abspos[title=start] expected offset-y=0, got 13</c>).</summary>
    public string Describe() =>
        $"{Element} expected {Property}={Format(Expected)}, got {Format(Actual)}";

    private static string Format(double value) =>
        value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Result of rendering and comparing a single WPT test case.
/// </summary>
internal sealed class WptTestResult
{
    /// <summary>
    /// Stable machine-readable identifier of the backend that produced the rendered image.
    /// </summary>
    public string RenderBackendId { get; init; } = BGraphicsBackend.CurrentId;

    /// <summary>
    /// Human-readable name of the backend that produced the rendered image.
    /// </summary>
    public string RenderBackendDisplayName { get; init; } = BGraphicsBackend.CurrentDisplayName;

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
    /// Non-uniform per-band displacement phrase (diagnostic #11) when the
    /// mismatched region splits into vertical bands that moved by different
    /// amounts — the line-spacing / <c>&lt;br&gt;</c>-flow signature that the
    /// single global <see cref="MismatchDiagnostics.Displacement"/> centroid hides.
    /// Null for a uniform or single-band shift.
    /// </summary>
    public string? DisplacementProfile { get; init; }

    /// <summary>
    /// Reference sanity check (diagnostic #14): set when a pixel-mismatch failure
    /// against the committed reference PNG is accompanied by Broiler's own render
    /// matching the test's <c>rel="match"</c> reference <em>HTML</em>. That pairing
    /// means Broiler renders the reftest correctly (test ≈ ref) and the committed
    /// PNG is the outlier — the failure is a stale/incorrect reference, not a
    /// Broiler bug. Null when the check was not run, no <c>rel="match"</c>
    /// reference exists, or Broiler does not match the reference HTML.
    /// </summary>
    public string? SuspectReference { get; init; }

    /// <summary>
    /// <c>check-layout-th.js</c> geometry assertions whose computed value diverged
    /// from the test's <c>data-offset-*</c> / <c>data-expected-*</c> attribute, with
    /// the expected and actual values. Null/empty when the test carries no such
    /// assertions or all of them matched. Turns an opaque pixel mismatch into a
    /// precise, font-independent layout diagnostic (diagnostic #4).
    /// </summary>
    public IReadOnlyList<LayoutAssertionFailure>? LayoutAssertionFailures { get; init; }

    /// <summary>
    /// Path to the saved rendered-output PNG for a failing comparison, when failure
    /// image capture is enabled (diagnostic #6). Null otherwise.
    /// </summary>
    public string? RenderedImagePath { get; init; }

    /// <summary>Path to the saved reference PNG for a failing comparison. Null when not captured.</summary>
    public string? ReferenceImagePath { get; init; }

    /// <summary>
    /// Path to the saved diff PNG (changed pixels highlighted in magenta) for a
    /// failing comparison. Null when not captured or when no diff bitmap exists
    /// (e.g. a size mismatch).
    /// </summary>
    public string? DiffImagePath { get; init; }

    /// <summary>
    /// <c>&lt;link rel="help"&gt;</c> targets declared by the test — the spec
    /// section(s) it exercises. Null/empty when none (diagnostic #9).
    /// </summary>
    public IReadOnlyList<string>? HelpLinks { get; init; }

    /// <summary>
    /// The test's <c>&lt;meta name="assert"&gt;</c> text — what it claims to verify.
    /// Reading it is the fastest way to tell whether, say, a <c>css-align</c> failure
    /// is actually a paint/parse bug. Null when the test declares no assertion.
    /// </summary>
    public string? Assertion { get; init; }

    /// <summary>
    /// Serialises this result to a JSON-friendly dictionary.
    /// </summary>
    internal Dictionary<string, object?> ToJsonObject(string? wptPath = null)
    {
        var obj = new Dictionary<string, object?>
        {
            ["testPath"] = TestPath,
            ["renderBackendId"] = RenderBackendId,
            ["renderBackendDisplayName"] = RenderBackendDisplayName,
            ["passed"] = Passed,
            ["skipped"] = Skipped,
            ["matchPercent"] = MatchPercent,
            ["category"] = Category.ToString(),
            ["message"] = Message,
            ["stackTrace"] = StackTrace,
        };

        if (SkipReason != SkipReason.None)
            obj["skipReason"] = SkipReason.ToString();

        var testKind = WptTestRunner.ClassifyTestKind(TestPath);
        if (testKind != TestKind.Regular)
            obj["testKind"] = testKind.ToString();

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
                ["boundingBox"] = new Dictionary<string, object?>
                {
                    ["left"] = diag.BoundingLeft,
                    ["top"] = diag.BoundingTop,
                    ["width"] = diag.BoundingWidth,
                    ["height"] = diag.BoundingHeight,
                },
                ["displacement"] = diag.Displacement,
                ["summary"] = diag.Summary,
            };
        }

        if (LayoutAssertionFailures is { Count: > 0 } layoutFailures)
        {
            obj["layoutAssertionFailures"] = layoutFailures
                .Select(f => new Dictionary<string, object?>
                {
                    ["element"] = f.Element,
                    ["property"] = f.Property,
                    ["expected"] = f.Expected,
                    ["actual"] = f.Actual,
                })
                .ToList();
        }

        if (RenderedImagePath is not null || ReferenceImagePath is not null || DiffImagePath is not null)
        {
            obj["failureImages"] = new Dictionary<string, object?>
            {
                ["rendered"] = RenderedImagePath,
                ["reference"] = ReferenceImagePath,
                ["diff"] = DiffImagePath,
            };
        }

        if (HelpLinks is { Count: > 0 } || Assertion is not null)
        {
            obj["testMetadata"] = new Dictionary<string, object?>
            {
                ["helpLinks"] = HelpLinks,
                ["assert"] = Assertion,
            };
        }

        if (SuspectReference is not null)
            obj["suspectReference"] = SuspectReference;

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
    /// When set, the stub <c>promise_test</c> does NOT run its bodies before the
    /// render snapshot. WPT <c>promise_test</c> callbacks are assertions that
    /// commonly mutate layout (scrollTo, display toggling) through post-load rAF
    /// chains; Chromium's reference generator screenshots at <c>load</c>, before
    /// they run, so executing them here advances the captured DOM past the
    /// reference point (the css-anchor-position scroll cluster). Default
    /// <c>false</c> preserves the historical behaviour; overridden by the
    /// <c>BROILER_WPT_DEFER_PROMISE_TESTS</c> environment variable and the
    /// <c>--defer-promise-tests</c> CLI flag.
    /// </summary>
    internal static bool DeferPromiseTests { get; set; } =
        Environment.GetEnvironmentVariable("BROILER_WPT_DEFER_PROMISE_TESTS") is "1" or "true" or "TRUE";

    /// <summary>
    /// File extensions treated as test files.
    /// </summary>
    private static readonly HashSet<string> TestExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html",
        ".htm",
        ".xht",
        ".xhtml",
    };

    /// <summary>
    /// Conservative JavaScript dependency check shared in behaviour with the
    /// Broiler.HTML non-JS WPT runner.
    /// </summary>
    private static readonly Regex JavaScriptDependencyPattern = new(
        @"<script\b|\bon[a-z]+\s*=\s*[""']|javascript:|testharness\.js|testdriver\.js|reftest-wait",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
      // Chromium-matching mode: the reference generator screenshots at `load`,
      // before any promise_test body runs. Those bodies are assertions that
      // frequently mutate layout (scrollTo, display toggling) via rAF chains;
      // running them here would advance the captured DOM past the reference point.
      // Leave them un-run so the snapshot reflects the initial post-load layout.
      if (window.__broilerDeferPromiseTests) {
        return;
      }
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

    private static readonly string[] TestharnessGlobalNames =
    [
        "test",
        "async_test",
        "promise_test",
        "assert_equals",
        "assert_true",
        "assert_false",
        "assert_unreached",
        "assert_approx_equals",
        "assert_less_than",
        "assert_greater_than",
        "setup",
        "done",
        "checkLayout"
    ];

    private readonly int _width;
    private readonly int _height;
    private readonly string? _failureImageDir;
    private readonly bool _verifyReferenceHtml;

    public WptTestRunner(int width = 1024, int height = 768, string? failureImageDir = null,
        bool verifyReferenceHtml = false)
    {
        _width = width;
        _height = height;
        _failureImageDir = failureImageDir;
        _verifyReferenceHtml = verifyReferenceHtml;
    }

    /// <summary>
    /// Saves the rendered, reference, and diff bitmaps for a failing comparison
    /// under <see cref="_failureImageDir"/> (diagnostic #6), mirroring the test's
    /// path as a sub-directory. Returns the written paths (any may be null when not
    /// captured). Best-effort: an I/O error is logged and does not fail the test.
    /// </summary>
    private (string? Rendered, string? Reference, string? Diff) SaveFailureImages(
        string testPath, string? wptRoot, BBitmap rendered, BBitmap reference, BBitmap? diff)
    {
        if (string.IsNullOrEmpty(_failureImageDir))
            return (null, null, null);

        try
        {
            // Mirror the test's relative path (without extension) as a folder so the
            // three images for one test sit together and don't collide across the tree.
            var relative = wptRoot is not null
                ? Path.GetRelativePath(wptRoot, testPath)
                : Path.GetFileName(testPath);
            var withoutExtension = Path.Combine(
                Path.GetDirectoryName(relative) ?? string.Empty,
                Path.GetFileNameWithoutExtension(relative));
            var caseDir = Path.Combine(_failureImageDir, withoutExtension);
            Directory.CreateDirectory(caseDir);

            var renderedPath = Path.Combine(caseDir, "rendered.png");
            var referencePath = Path.Combine(caseDir, "reference.png");
            rendered.Save(renderedPath);
            reference.Save(referencePath);

            string? diffPath = null;
            if (diff is not null)
            {
                diffPath = Path.Combine(caseDir, "diff.png");
                diff.Save(diffPath);
            }

            return (renderedPath, referencePath, diffPath);
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.HtmlRenderer, "WptTestRunner.SaveFailureImages",
                $"Failed to save failure images for {testPath}: {ex.Message}", ex);
            return (null, null, null);
        }
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
        "/resources/", "\\resources\\",
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
    /// Discovers tests and, when requested, excludes documents that depend on
    /// JavaScript using the same conservative policy as Broiler.HTML's
    /// <c>wpt-non-js</c> runner.
    /// </summary>
    internal static IEnumerable<string> DiscoverTests(string wptRoot, bool nonJavaScriptOnly)
    {
        var tests = DiscoverTests(wptRoot);
        return nonJavaScriptOnly
            ? tests.Where(testPath => !RequiresJavaScript(File.ReadAllText(testPath)))
            : tests;
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
        => DiscoverTests(wptRoot, subsetPatterns, nonJavaScriptOnly: false);

    /// <summary>
    /// Discovers tests matching the supplied subset and optional non-JS policy.
    /// </summary>
    internal static IEnumerable<string> DiscoverTests(
        string wptRoot,
        IReadOnlyList<string> subsetPatterns,
        bool nonJavaScriptOnly)
    {
        if (subsetPatterns.Count == 0)
            return DiscoverTests(wptRoot, nonJavaScriptOnly);

        return DiscoverTests(wptRoot, nonJavaScriptOnly)
            .Where(f =>
            {
                var rel = Path.GetRelativePath(wptRoot, f).Replace('\\', '/');
                return MatchesAnyPattern(rel, subsetPatterns);
            });
    }

    /// <summary>
    /// Returns whether markup depends on JavaScript or the WPT JavaScript
    /// harness and therefore must not be included in a non-JS visual run.
    /// </summary>
    internal static bool RequiresJavaScript(string htmlContent)
        => JavaScriptDependencyPattern.IsMatch(htmlContent);

    /// <summary>
    /// Sentinel <c>--shard-index</c> value meaning "run every shard" (no
    /// sharding filter is applied).
    /// </summary>
    internal const int AllShards = -1;

    /// <summary>
    /// Filters <paramref name="tests"/> down to the single shard identified by
    /// <paramref name="shardIndex"/> out of <paramref name="shardCount"/>.
    /// Shard assignment is a content-independent FNV-1a hash of each test's
    /// path relative to <paramref name="wptRoot"/> (forward-slash separators),
    /// so it is stable across runs and — crucially — reproducible byte-for-byte
    /// by the JavaScript reference generator, which shards the same way. That
    /// lets shard <c>N</c> generate references for exactly the tests shard
    /// <c>N</c> later runs. When <paramref name="shardIndex"/> is
    /// <see cref="AllShards"/> the input is returned unchanged.
    /// </summary>
    internal static IEnumerable<string> ApplyShard(
        IEnumerable<string> tests,
        string wptRoot,
        int shardCount,
        int shardIndex)
    {
        if (shardCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(shardCount), shardCount, "shardCount must be greater than 0.");

        if (shardIndex == AllShards || shardCount == 1)
            return tests;

        if (shardIndex < 0 || shardIndex >= shardCount)
            throw new ArgumentOutOfRangeException(
                nameof(shardIndex),
                shardIndex,
                $"shardIndex must be {AllShards} or between 0 and {shardCount - 1}.");

        return tests.Where(testPath =>
        {
            var relativePath = Path.GetRelativePath(wptRoot, testPath).Replace('\\', '/');
            return GetShardIndex(relativePath, shardCount) == shardIndex;
        });
    }

    /// <summary>
    /// Returns the deterministic shard index in <c>[0, shardCount)</c> for a
    /// forward-slash relative path, using a 32-bit FNV-1a hash of its UTF-8
    /// bytes. This algorithm is intentionally simple so the reference generator
    /// (<c>scripts/generate-wpt-references.js</c>) can reproduce it exactly.
    /// </summary>
    internal static int GetShardIndex(string relativePath, int shardCount)
    {
        const uint fnvOffsetBasis = 2166136261;
        const uint fnvPrime = 16777619;

        uint hash = fnvOffsetBasis;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(relativePath))
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return (int)(hash % (uint)shardCount);
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
    ///   <item>Files ending in <c>-ref.html/.htm/.xht/.xhtml</c> or <c>-notref.html/.htm/.xht/.xhtml</c>
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
            fileName.EndsWith(".src.xht", StringComparison.OrdinalIgnoreCase) ||
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
    /// Determines whether the given test path is a WPT <em>manual</em> test.
    /// Per WPT convention a test is manual when its filename (without the
    /// extension) ends in <c>-manual</c> — e.g.
    /// <c>animation-delay-001-manual.html</c>. Such tests require human
    /// interaction (they have no automated pass condition) and therefore cannot
    /// be pixel-compared against a Chromium screenshot; the upstream wptrunner
    /// skips them unless <c>--include-manual</c> is passed.
    /// <para>
    /// DIAGNOSTIC NOTE (WPT issue #1100): before this check existed, all 59
    /// <c>-manual</c> tests under <c>css/css-animations</c> were rendered and
    /// reported as PixelMismatch failures, inflating the failure count by ~12%.
    /// If manual tests reappear as failures, or a legitimately-automated test is
    /// wrongly skipped, this helper and its call site in <c>RunSingleTest</c> are
    /// the place to look. (The rarer <c>&lt;meta name="flags" content="manual"&gt;</c>
    /// form is not detected here — the filename suffix is WPT's primary signal
    /// and covers the observed cases.)
    /// </para>
    /// </summary>
    internal static bool IsManualTest(string testPath)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(testPath);
        return nameWithoutExt.EndsWith("-manual", StringComparison.OrdinalIgnoreCase);
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
    /// Determines whether the given test path is a WPT <em>tentative</em> test —
    /// one that exercises a feature whose specification is not yet stable. Per WPT
    /// convention this is signalled by <c>.tentative</c> in the file name (e.g.
    /// <c>foo.tentative.html</c>) or by a <c>tentative/</c> directory segment.
    /// </summary>
    internal static bool IsTentativeTest(string testPath)
    {
        if (testPath.Contains("/tentative/", StringComparison.OrdinalIgnoreCase) ||
            testPath.Contains("\\tentative\\", StringComparison.OrdinalIgnoreCase))
            return true;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(testPath);
        return nameWithoutExt.EndsWith(".tentative", StringComparison.OrdinalIgnoreCase) ||
               nameWithoutExt.Contains(".tentative.", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly Regex LinkTagPattern = new(@"<link\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MetaTagPattern = new(@"<meta\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RelHelpPattern = new(@"\brel\s*=\s*[""']?\s*help\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RelMatchPattern = new(@"\brel\s*=\s*[""']?\s*match\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NameAssertPattern = new(@"\bname\s*=\s*[""']?\s*assert\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HrefValuePattern = new(@"\bhref\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ContentValuePattern = new(@"\bcontent\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// What a test declares about itself: its <c>&lt;link rel="help"&gt;</c> spec
    /// targets and its <c>&lt;meta name="assert"&gt;</c> claim (diagnostic #9).
    /// </summary>
    internal readonly record struct TestMetadata(IReadOnlyList<string>? HelpLinks, string? Assertion);

    /// <summary>
    /// Extracts the <c>&lt;link rel="help"&gt;</c> hrefs and the joined
    /// <c>&lt;meta name="assert"&gt;</c> content from a test's HTML. Surfacing these
    /// in the report shows what a failing test <em>claims</em> to verify — the
    /// fastest way to spot that a nominal layout failure is really a paint/parse bug.
    /// </summary>
    internal static TestMetadata ExtractTestMetadata(string html)
    {
        var helpLinks = new List<string>();
        foreach (Match link in LinkTagPattern.Matches(html))
        {
            if (!RelHelpPattern.IsMatch(link.Value))
                continue;
            if (TryExtractAttributeValue(HrefValuePattern, link.Value, out var href))
                helpLinks.Add(href);
        }

        var assertions = new List<string>();
        foreach (Match meta in MetaTagPattern.Matches(html))
        {
            if (!NameAssertPattern.IsMatch(meta.Value))
                continue;
            if (TryExtractAttributeValue(ContentValuePattern, meta.Value, out var content))
                assertions.Add(System.Net.WebUtility.HtmlDecode(content));
        }

        return new TestMetadata(
            helpLinks.Count > 0 ? helpLinks : null,
            assertions.Count > 0 ? string.Join(" / ", assertions) : null);
    }

    /// <summary>
    /// Extracts the first <c>&lt;link rel="match" href="…"&gt;</c> target — the
    /// reftest's reference HTML — or null when the test is not a rel=match reftest.
    /// </summary>
    internal static string? ExtractMatchHref(string html)
    {
        foreach (Match link in LinkTagPattern.Matches(html))
        {
            if (!RelMatchPattern.IsMatch(link.Value))
                continue;
            if (TryExtractAttributeValue(HrefValuePattern, link.Value, out var href))
                return href;
        }
        return null;
    }

    /// <summary>
    /// Reference sanity check (diagnostic #14). When a test fails its committed
    /// reference-PNG comparison, render the test's own <c>rel="match"</c> reference
    /// HTML and compare it to the already-computed test render. If Broiler matches
    /// its reference HTML, the reftest actually passes (test ≈ ref) and the
    /// committed PNG is the stale/incorrect outlier — return a note saying so.
    /// Returns null when there is no rel=match reference, it cannot be resolved or
    /// rendered, or Broiler does not match it. Best-effort: never throws.
    /// </summary>
    private string? VerifyAgainstReferenceHtml(string testPath, string html, string? wptRoot, BBitmap testRender)
    {
        try
        {
            var href = ExtractMatchHref(html);
            if (string.IsNullOrWhiteSpace(href))
                return null;

            // Strip any fragment/query and resolve relative to the test file
            // (a root-relative "/foo" maps under the WPT root).
            href = href.Split('#', '?')[0].Trim();
            if (href.Length == 0)
                return null;

            string refPath = href.StartsWith("/", StringComparison.Ordinal) && wptRoot != null
                ? Path.GetFullPath(Path.Combine(wptRoot, href.TrimStart('/')))
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testPath)!, href));
            if (!File.Exists(refPath))
                return null;

            using var refRender = RenderHtmlFileBitmap(refPath, wptRoot);
            using var diff = PixelDiffRunner.Compare(testRender, refRender);
            if (!diff.IsMatch)
                return null;

            double pct = (1.0 - diff.DiffRatio) * 100;
            return $"⚠ suspect reference: Broiler matches its rel=match reference HTML " +
                   $"({pct:F1}%) but not the committed reference PNG — the committed reference " +
                   "is likely stale/incorrect, not a Broiler bug";
        }
        catch
        {
            return null;
        }
    }

    private static bool TryExtractAttributeValue(Regex pattern, string tag, out string value)
    {
        var match = pattern.Match(tag);
        if (match.Success)
        {
            // Groups 1-3 are the double-quoted / single-quoted / unquoted alternatives.
            for (var i = 1; i < match.Groups.Count; i++)
            {
                if (match.Groups[i].Success && match.Groups[i].Value.Trim().Length > 0)
                {
                    value = match.Groups[i].Value.Trim();
                    return true;
                }
            }
        }

        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Classifies a test file into its <see cref="TestKind"/>. Crash and manual are
    /// checked first because they drive distinct handling (auto-pass / skip); a test
    /// is otherwise tentative or regular. Outcome-independent — it keys purely off
    /// the path so the report can bucket every result by kind.
    /// </summary>
    internal static TestKind ClassifyTestKind(string testPath)
    {
        if (IsCrashTest(testPath))
            return TestKind.CrashTest;
        if (IsManualTest(testPath))
            return TestKind.Manual;
        if (IsTentativeTest(testPath))
            return TestKind.Tentative;
        return TestKind.Regular;
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

        // Skip WPT manual tests: they require human interaction and have no
        // automated pass condition, so a pixel comparison against a Chromium
        // screenshot is meaningless. Reported as skipped, not failed.
        if (IsManualTest(testPath))
        {
            return new WptTestResult
            {
                TestPath = testPath,
                Skipped = true,
                SkipReason = SkipReason.ManualTest,
                Message = "WPT manual test (filename ends in -manual); requires human interaction.",
            };
        }

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

        // What the test claims to verify (spec links + assertion); attached to
        // failures so triage can read intent without opening the file (#9).
        var metadata = ExtractTestMetadata(html);

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
        IReadOnlyList<LayoutAssertionFailure>? layoutAssertionFailures = null;
        try
        {
            var executed = ExecuteScriptsWithDom(
                html,
                new Uri(Path.GetFullPath(testPath)).AbsoluteUri,
                wptRoot,
                batchStyleInvalidations: IsCrashTest(testPath));
            html = executed.Html;
            layoutAssertionFailures = ComputeLayoutAssertionFailures(executed.LayoutAssertions);
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
                HelpLinks = metadata.HelpLinks,
                Assertion = metadata.Assertion,
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
        EventHandler<HtmlImageLoadEventArgs> imageHandler = null;
        if (wptRoot != null)
        {
            var capturedWptRoot = wptRoot;
            stylesheetHandler = (_, args) =>
            {
                var local = TryResolveWptRootRelativePath(args.Src, capturedWptRoot);
                if (local != null)
                    args.SetSrc = local;
            };
            imageHandler = (_, args) =>
            {
                var local = TryResolveWptRootRelativePath(args.Src, capturedWptRoot);
                if (local != null)
                    args.Callback(local);
            };
        }

        // Render via Broiler HTML stack.
        BBitmap rendered;
        try
        {
            rendered = HtmlRender.RenderToImageWithStyleSet(html, _width, _height,
                backgroundColor: BColor.White,
                stylesheetLoad: stylesheetHandler, imageLoad: imageHandler, baseUrl: testBaseUrl);
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
                HelpLinks = metadata.HelpLinks,
                Assertion = metadata.Assertion,
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

            BBitmap reference;
            try
            {
                reference = BBitmap.Decode(referencePath);
            }
            catch (Exception ex)
            {
                return new WptTestResult
                {
                    TestPath = testPath,
                    Passed = false,
                    Message = $"Failed to decode reference image: {referencePath} ({ex.Message})",
                    Category = FailureCategory.ReferenceDecodeError,
                    StackTrace = ex.StackTrace,
                    HelpLinks = metadata.HelpLinks,
                    Assertion = metadata.Assertion,
                };
            }

            using (reference)
            {
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
                        LayoutAssertionFailures = layoutAssertionFailures,
                    };
                }

                // Classify the mismatch to provide actionable diagnostics.
                var diagnostics = MismatchClassifier.Classify(
                    diff,
                    rendered.Width, rendered.Height,
                    reference.Width, reference.Height);

                // Per-band displacement profile (#11): resolve the shift along the
                // vertical axis so a band-localised shift (the line-spacing / <br>
                // signature the global centroid blurs away) is surfaced explicitly.
                var bands = DisplacementBandAnalyzer.Analyze(diff.Mismatches);
                var displacementProfile = DisplacementBandAnalyzer.DescribeNonUniform(bands);
                var message = $"Pixel mismatch: {matchPct:F1}% match ({diff.DiffPixelCount}/{diff.TotalPixelCount} pixels differ) — {diagnostics.Summary}";
                if (displacementProfile != null)
                    message = $"{message} [{displacementProfile}]";

                // Reference sanity check (#14): when the committed PNG comparison
                // fails, optionally render the test's own rel=match reference HTML
                // and see whether Broiler's render actually matches it. If it does,
                // the committed PNG — not Broiler — is the outlier (stale/incorrect
                // reference), which we surface so triage doesn't chase a non-bug.
                string? suspectReference = _verifyReferenceHtml
                    ? VerifyAgainstReferenceHtml(testPath, html, wptRoot, rendered)
                    : null;
                if (suspectReference != null)
                    message = $"{message} [{suspectReference}]";

                // Capture rendered / reference / diff PNGs for visual triage (#6).
                var images = SaveFailureImages(testPath, wptRoot, rendered, reference, diff.DiffBitmap);

                return new WptTestResult
                {
                    TestPath = testPath,
                    Passed = false,
                    MatchPercent = matchPct,
                    Message = message,
                    Category = FailureCategory.PixelMismatch,
                    MismatchDiagnostics = diagnostics,
                    DisplacementProfile = displacementProfile,
                    SuspectReference = suspectReference,
                    LayoutAssertionFailures = layoutAssertionFailures,
                    RenderedImagePath = images.Rendered,
                    ReferenceImagePath = images.Reference,
                    DiffImagePath = images.Diff,
                    HelpLinks = metadata.HelpLinks,
                    Assertion = metadata.Assertion,
                };
            }
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

        BBitmap? rendered = null;
        BBitmap? reference = null;
        try
        {
            rendered = RenderHtmlFileBitmap(testPath, wptRoot);
            reference = RenderHtmlFileBitmap(refHtmlPath, wptRoot);

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

    private static string? TryResolveWptRootRelativePath(string? src, string wptRoot)
    {
        if (src == null || !src.StartsWith("/", StringComparison.Ordinal))
            return null;

        var rel = src.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var local = Path.Combine(wptRoot, rel);
        return File.Exists(local) ? local : null;
    }

    /// <summary>
    /// Renders an HTML file through the full Broiler pipeline (script execution,
    /// anchor resolution, rendering) and returns the resulting bitmap.
    /// </summary>
    private BBitmap RenderHtmlFileBitmap(string htmlPath, string? wptRoot)
    {
        var html = File.ReadAllText(htmlPath);

        if (IsMediaPlaybackTest(html))
            throw new InvalidOperationException("Test requires media playback.");

        var testBaseUrl = new Uri(Path.GetFullPath(htmlPath)).AbsoluteUri;

        // Set local base path for sub-resource resolution.
        html = ExecuteScriptsWithDom(html, testBaseUrl, wptRoot).Html;
        html = HtmlPostProcessor.Process(html);

        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetHandler = null;
        EventHandler<HtmlImageLoadEventArgs>? imageHandler = null;
        if (wptRoot != null)
        {
            var capturedWptRoot = wptRoot;
            stylesheetHandler = (_, args) =>
            {
                var local = TryResolveWptRootRelativePath(args.Src, capturedWptRoot);
                if (local != null)
                    args.SetSrc = local;
            };
            imageHandler = (_, args) =>
            {
                var local = TryResolveWptRootRelativePath(args.Src, capturedWptRoot);
                if (local != null)
                    args.Callback(local);
            };
        }

        return HtmlRender.RenderToImageWithStyleSet(html, _width, _height,
            backgroundColor: BColor.White,
            stylesheetLoad: stylesheetHandler, imageLoad: imageHandler, baseUrl: testBaseUrl);
    }

    internal BBitmap RenderHtmlFileBitmapPublic(string htmlPath, string? wptRoot)
    {
        if (wptRoot != null)
            EnsureWptFontsLoaded(wptRoot);
        return RenderHtmlFileBitmap(htmlPath, wptRoot);
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
    /// <summary>
    /// Per-axis pixel tolerance below which a computed geometry value is treated as
    /// matching its <c>data-*</c> expectation. The bridge metrics are CSS-based
    /// estimates, so a sub-pixel difference is not a real layout discrepancy.
    /// </summary>
    private const double LayoutAssertionTolerancePx = 1.0;

    /// <summary>
    /// Filters the evaluated <c>check-layout-th.js</c> assertions down to the ones
    /// whose computed value diverges from the expected value beyond
    /// <see cref="LayoutAssertionTolerancePx"/>. Returns <c>null</c> when everything
    /// matched (or there were no assertions) so the result carries no empty list.
    /// </summary>
    private static IReadOnlyList<LayoutAssertionFailure>? ComputeLayoutAssertionFailures(
        IReadOnlyList<DomBridge.CheckLayoutAssertion> assertions)
    {
        if (assertions.Count == 0)
            return null;

        var failures = new List<LayoutAssertionFailure>();
        foreach (var a in assertions)
        {
            if (double.IsNaN(a.Actual) || Math.Abs(a.Expected - a.Actual) > LayoutAssertionTolerancePx)
                failures.Add(new LayoutAssertionFailure(a.Element, a.Property, a.Expected, a.Actual));
        }

        return failures.Count > 0 ? failures : null;
    }

    private static (string Html, IReadOnlyList<DomBridge.CheckLayoutAssertion> LayoutAssertions) ExecuteScriptsWithDom(
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

        // Expose the promise_test deferral setting to the stubs (inserted before
        // them so the promise_test stub can honour it). See TestharnessStubs.
        scripts.Insert(0, FormattableString.Invariant(
            $"window.__broilerDeferPromiseTests = {(DeferPromiseTests ? "true" : "false")};"));

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
            var assertions2 = bridge2.EvaluateCheckLayoutAssertions();
            return (bridge2.SerializeToHtml(), assertions2);
        }

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.TaskCheckpointCallback = () => microTasks.Drain();
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
            for (var iteration = 0; iteration < DomBridge.AsyncDrainIterationLimit; iteration++)
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
                    PromoteWindowGlobalsToContext(context);
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
                    PromoteWindowGlobalsToContext(context);
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

        var layoutAssertions = bridge.EvaluateCheckLayoutAssertions();
        return (bridge.SerializeToHtml(), layoutAssertions);
    }

    private static void PromoteWindowGlobalsToContext(JSContext context)
    {
        foreach (var name in TestharnessGlobalNames)
        {
            try
            {
                var value = context.Eval($"typeof window['{name}'] !== 'undefined' ? window['{name}'] : undefined");
                if (value != null && value != JSUndefined.Value)
                    context[name] = value;
            }
            catch
            {
                // Best-effort only — some globals may not exist for a given page.
            }
        }
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
