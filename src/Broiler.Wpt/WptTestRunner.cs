using System.Text.RegularExpressions;
using Broiler.HTML.Image;
using Broiler.Media.Image;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;
using Broiler.HTML.Core.Entities;
using Broiler.HtmlBridge.Logging;
using Broiler.HtmlBridge;
using Broiler.Graphics;
using Broiler.HtmlBridge.Scripting;
using Broiler.HtmlBridge.Dom;

namespace Broiler.Wpt;

// Disambiguate the unqualified `Regex` type: the Broiler.JS engine now exposes a top-level
// `Broiler.Regex` namespace which, from this `Broiler.*` namespace, otherwise shadows
// System.Text.RegularExpressions.Regex by simple-name lookup (CS0118). The alias must sit
// inside the Broiler.Wpt namespace scope so it is resolved before the enclosing `Broiler`
// namespace's `Regex` member. This file uses only the .NET Regex.
using Regex = System.Text.RegularExpressions.Regex;

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
    /// <summary>The test was aborted after exceeding the configured per-test RAM cap.</summary>
    MemoryLimitExceeded,
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
    UnsupportedWptVariant,

    /// <summary>
    /// Reftest mode only: the test declares no usable <c>&lt;link rel="match"&gt;</c> /
    /// <c>&lt;link rel="mismatch"&gt;</c> reference, or every href it declares names a
    /// file that is not on disk. Reftest mode has nothing to compare against, so the
    /// case is reported as skipped rather than failed. See
    /// <see cref="WptTestRunner.RunReferenceTest"/>.
    /// </summary>
    NoReferenceDeclared,

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
    /// How closely Broiler's render matched the test's own <c>rel="match"</c> reference
    /// <em>HTML</em>, as a percentage, whenever <c>--verify-reference</c> ran that comparison —
    /// whether or not it cleared the pass threshold. Null when the check did not run or the test
    /// declares no reference.
    /// </summary>
    /// <remarks>
    /// The score used to be computed and then discarded unless it passed the gate, so a test at 94%
    /// against its own reference and 0.8% against the committed golden was reported as though
    /// nothing at all were known about it — indistinguishable from one that is wrong against both.
    /// Those are opposite triage outcomes: the first says the golden is the outlier and "fixing" it
    /// would mean rendering *less* than the test asks for; the second is a real engine gap. Keeping
    /// the number separates them without a second threshold to tune.
    /// </remarks>
    public double? ReferenceMatchPercent { get; init; }

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

        if (ReferenceMatchPercent is not null)
            obj["referenceMatchPercent"] = ReferenceMatchPercent;

        return obj;
    }
}

/// <summary>
/// Discovers and executes web-platform-tests by rendering each HTML file
/// through the Broiler HTML/JavaScript stack and comparing the output to
/// a Chromium/Playwright reference image.
/// </summary>
internal sealed partial class WptTestRunner
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
    /// Native anchor-placement cutover lever (HtmlBridge complexity-reduction roadmap
    /// Phase 5, P5.8d.2b / Phase 4 item-2 step 2). **Default <c>true</c>** since the flip
    /// was validated across the full css corpus (lever-on 36 fails vs 38 default-off, zero
    /// new failures, 2 fixed). When on, the runner puts the DomBridge into native anchor
    /// mode (so MVP-subset boxes keep their CSS instead of being pre-baked) and enables the
    /// Broiler.Layout engine's placement post-pass (<c>NativeAnchorPlacement.Enabled</c>)
    /// around the final render, so the engine — not the bridge — positions those boxes.
    /// The baked path is kept reachable for rollback/comparison: set
    /// <c>BROILER_WPT_NATIVE_ANCHOR=0</c> (or <c>false</c>/<c>off</c>) to force it off.
    /// </summary>
    internal static bool NativeAnchorPlacement { get; set; } =
        Environment.GetEnvironmentVariable("BROILER_WPT_NATIVE_ANCHOR") is not ("0" or "false" or "FALSE" or "off");

    /// <summary>
    /// Native <c>::backdrop</c> lever (Phase 5 native dialog/backdrop track). The native
    /// <c>::backdrop</c> box generation (<c>Broiler.HTML DomParser.GenerateNativeBackdrops</c> +
    /// <c>PaintWalker</c> top-layer paint) is <em>pinned</em> in the submodule, so the engine
    /// honours the <c>data-broiler-backdrop</c> / <c>data-broiler-top-layer</c> markers — the
    /// default <see cref="DomBridge"/> now stamps them (native) rather than emulating a scrim, and
    /// the end-to-end render is covered by <c>NativeBackdropRenderTests</c>. This WPT lever stays
    /// Default OFF here only until the full WPT <c>::backdrop</c> reftest corpus has been swept
    /// under it; with it off the runner keeps synthesizing the backdrop <c>&lt;div&gt;</c> (the
    /// established CI-green fallback). Force on with <c>BROILER_WPT_NATIVE_BACKDROP=1</c>.
    /// </summary>
    internal static bool NativeBackdrop { get; set; } =
        Environment.GetEnvironmentVariable("BROILER_WPT_NATIVE_BACKDROP") is ("1" or "true" or "TRUE" or "on");

    /// <summary>
    /// Native CSS <c>zoom</c> render lever (Phase 5 zoom endgame — render-side A/B validation). Default
    /// OFF: the serialize path bakes <c>zoom</c> into the DOM (<c>ApplyZoomSerializationStyles</c>) and the
    /// render lays out the pre-scaled HTML 1:1, exactly as today. When on, the runner enables the engine's
    /// used-value <c>zoom</c> model (<c>Broiler.Layout.Engine.NativeZoom</c>) around the whole
    /// serialize+render, so the bridge bake is skipped (<c>DomBridge.ZoomBakeActive</c>) and the engine
    /// scales used values during layout. Used to validate that the engine render reproduces the correct
    /// (reference) pixels for the zoom corpus — the render-side half of the increment-6 cutover's A/B gate.
    /// </summary>
    internal static bool NativeZoom { get; set; }

    /// <summary>
    /// Render the script-mutated document directly instead of serialising it to HTML and
    /// re-parsing that. Force on with <c>BROILER_WPT_DOM_RENDER=1</c>; force the round trip back
    /// with <c>BROILER_WPT_DOM_RENDER=0</c>, the same shape as the other render levers here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The round trip loses whatever markup cannot express, and HTML tree construction is not a
    /// faithful inverse of a DOM: it unconditionally creates a <c>&lt;body&gt;</c>, so a document a
    /// script deliberately left without one comes back with one. WPT's
    /// <c>quirks/tables-inherit-color-from-body-quirk-004…-007</c> exist to check exactly that case
    /// and cannot be decided through the string path at all — the render is not wrong, it is of a
    /// different document.
    /// </para>
    /// <para>
    /// Both paths take the same projected document (<c>DomBridge.GetRenderDocument</c>) as their
    /// input; the lever only decides whether it is serialised first. That is what makes the A/B
    /// comparison meaningful rather than a comparison of two pipelines.
    /// </para>
    /// <para>
    /// <b>Default ON</b>, on a paired A/B over <b>~3 400 reftests</b> in which <b>3 tests changed,
    /// all of them fixed, and none regressed</b>: a 2 206-test set spanning <c>css-break</c>,
    /// <c>css-overflow/line-clamp</c>, <c>css-box/margin-trim</c>, <c>CSS2/floats</c>,
    /// <c>css-align</c>, <c>css-sizing</c>, <c>css-display</c>, <c>css-lists</c>,
    /// <c>css-inline</c>, <c>css-rhythm</c>, <c>css-pseudo</c> and <c>quirks</c> (1 133 → 1 135),
    /// and a 1 173-test <c>css-backgrounds</c> + <c>css-images</c> set (690 → 691). That is not
    /// the whole corpus — the directories nothing has swept are why the round trip stays one
    /// environment variable away.
    /// </para>
    /// </remarks>
    internal static bool DomRender { get; set; } =
        Environment.GetEnvironmentVariable("BROILER_WPT_DOM_RENDER") is not ("0" or "false" or "FALSE" or "off");

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
  // Reference-matching (defer) mode: the reference generator does not load
  // testharness.js, so `test`/`async_test`/`promise_test` are undefined there and
  // the first call to one throws — aborting the rest of that <script> block before
  // any test body, or any post-call DOM mutation, runs. A stub that merely no-ops
  // instead of throwing lets that later code run (e.g. interpolation-testcommon.js
  // `create_tests()` removing its container of test boxes after calling test(), or
  // an HTMLLinkElement-disabled async_test toggling `<link disabled>` on), advancing
  // our captured DOM past the reference point. So in defer mode we leave these three
  // undefined too, matching the reference generator; each <script> is eval'd in its
  // own try/catch, so the throw aborts only that block (exactly as in a browser).
  // Outside defer mode they are stubbed so local renders still exercise the bodies.
  var __defer = window.__broilerDeferPromiseTests;
  if (typeof test === 'undefined' && !__defer) {
    window.test = function(func, name) {};
  }
  if (typeof async_test === 'undefined' && !__defer) {
    window.async_test = function(func, name) {
      var t = { step: function(f){try{f();}catch(e){}}, done: function(){}, step_func: function(f){return f;}, step_func_done: function(f){return f;}, step_timeout: function(f,d){try{f();}catch(e){}} };
      if (func) { try { func(t); } catch(e) {} }
      return t;
    };
  }
  if (typeof promise_test === 'undefined' && !__defer) {
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
  // `setup`/`done` follow the same defer-mode rule as test/async_test/promise_test above: the
  // reference generator does not serve testharness.js, so they are undefined there and the first
  // call throws, aborting the rest of that <script> block. Stubbing them unconditionally let our
  // side run on past the abort point and mutate the DOM the reference never saw — WPT
  // html/semantics/interactive-elements/the-dialog-element/backdrop-receives-element-events opens
  // with `setup({single_test: true})` and only then calls dialog.showModal(), so Chromium's golden
  // is a blank page while Broiler painted the dialog's red ::backdrop over the whole viewport.
  // Outside defer mode they stay stubbed so local renders still exercise the page.
  if (typeof setup === 'undefined' && !__defer) {
    window.setup = function(obj) {};
  }
  if (typeof done === 'undefined' && !__defer) {
    window.done = function() {};
  }
  if (typeof checkLayout === 'undefined') {
    window.checkLayout = function(selector, callDone) {};
  }
})();
";

    /// <summary>
    /// The runner's <c>test_driver</c> implementation — the parts of the testdriver API that
    /// can mean something without a WebDriver session behind them.
    /// <para>
    /// Injected twice, and the second time is the point. It rides along with
    /// <see cref="BrowserApiStubs"/> so a test that never pulls in testdriver.js still gets a
    /// <c>test_driver</c>, and it is re-injected immediately after the real
    /// <c>/resources/testdriver.js</c> whenever a test does load it (see
    /// <see cref="IsTestDriverScript"/>). Without that second injection the real file — which the
    /// runner serves verbatim out of the checkout, and which loads *after* the stubs — replaced
    /// every method here with one that funnels through <c>test_driver_internal</c>. WPT's in-tree
    /// <c>testdriver-vendor.js</c> is an empty file, so nothing implements that side and the
    /// promises never settle.
    /// </para>
    /// <para>
    /// Each method therefore assigns unconditionally rather than filling a gap: the definition it
    /// overwrites is the real testdriver's, and in this environment that one cannot work.
    /// </para>
    /// </summary>
    private const string TestDriverStubs = @"
(function() {
  var test_driver = window.test_driver || {};
  window.test_driver = test_driver;
  // testdriver `bless(name, action)`: in wptrunner this grants transient user activation and then
  // runs the action, so a test can call an API that requires a user gesture — requestFullscreen()
  // in fullscreen/rendering, showPicker() in the customizable-select tests. There is no user here
  // and nothing checks activation, so the activation half is a no-op and the action is what
  // matters: without it the callback never runs and the test renders its un-activated state.
  // Returns a promise resolving to the action's result, which is what callers await.
  //
  // The real testdriver.js instead appends a `This test requires user interaction. Please click
  // here to allow <intent>.` button to the document and awaits a click on it delivered through
  // WebDriver — so leaving it in place both stranded the action *and* painted that button into
  // the reftest screenshot.
  test_driver.bless = function(name, action) {
    try {
      return Promise.resolve(typeof action === 'function' ? action() : undefined);
    } catch (e) {
      return Promise.reject(e);
    }
  };
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
    private const string BrowserApiStubs = BrowserApiStubsCore + TestDriverStubs;

    /// <summary>
    /// Everything in <see cref="BrowserApiStubs"/> except the testdriver half, which is a separate
    /// source only because the runner also injects it a second time (see
    /// <see cref="TestDriverStubs"/>). Nothing should reference this directly.
    /// </summary>
    private const string BrowserApiStubsCore = @"
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
  // Bounded, because synchronous means a self-rescheduling rAF loop recurses natively
  // through this stub rather than yielding to a queue: `function f(){ ...; rAF(f); }`
  // becomes f -> stub -> f -> stub -> ... with no return until it converges. The real
  // rAF this replaces is queue-based and capped (BrowserEventLoop.DrainAll runs at most
  // maxIterations=1000); the stub threw that cap away with the queue. A loop whose exit
  // condition this engine never satisfies then ran until the CLR stack gave out, and
  // since every level re-read layout it cost two full document layouts per level:
  // css-fonts/variations/font-opentype-collections.html spent 88s that way against the
  // runner's 30s budget, and any test with the same shape would too.
  // Dropping the callback once the budget is spent lets the page render the state it
  // reached, so such a test reports an honest assertion failure in well under a second
  // instead of a timeout. The limits are far above what a converging loop needs (real
  // ones chain a handful of frames) and far below CLR stack exhaustion.
  var __broilerRafDepth = 0, __broilerRafFrames = 0;
  window.requestAnimationFrame = function(cb) {
    if (__broilerRafDepth >= 64 || __broilerRafFrames >= 256) return 0;
    __broilerRafDepth++; __broilerRafFrames++;
    try { cb(0); } finally { __broilerRafDepth--; }
    return 0;
  };
  // Still a no-op: this stub has already run the callback by the time anything could cancel it.
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
  // The bridge now defines HTMLElement (see DomBridge RegisterDomInterfaceConstructors), so an
  // `is it missing?` guard would skip this shim and leave `class X extends HTMLElement` building
  // plain objects instead of elements. Probe the capability this shim actually needs — that
  // `new HTMLElement()` yields an element node — rather than the name's mere existence. The
  // bridge's version answers `instanceof` and is not constructible into an element, so the
  // override still installs; a future real implementation would pass the probe and win.
  var __broilerNeedsHtmlElementShim = true;
  try {
    if (typeof HTMLElement !== 'undefined') {
      var __broilerHtmlElementProbe = new HTMLElement();
      __broilerNeedsHtmlElementShim =
        !(__broilerHtmlElementProbe && __broilerHtmlElementProbe.nodeType === 1);
    }
  } catch (e) {
    __broilerNeedsHtmlElementShim = true;
  }
  if (__broilerNeedsHtmlElementShim) {
    var __broilerHtmlElementShim = function HTMLElement() {
      return __broilerEnsureAnimate(__broilerNativeCreateElement(__broilerCurrentCustomElementName || 'div'));
    };
    window.HTMLElement = __broilerHtmlElementShim;
    // Assigned to the global scope as well as to window. A bare `HTMLElement` in
    // `class X extends HTMLElement` resolves through the global scope, and the alias loop below
    // only copies window onto globalThis for names the global scope is *missing* — which this one
    // no longer is, now that the bridge declares it.
    try { globalThis.HTMLElement = __broilerHtmlElementShim; } catch (e) {}
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
        // Prefer getAttributeNames(): the bridge's `attributes` reports a length but does not
        // answer to numeric indexing, so the obvious loop read `undefined.name` and threw out of
        // customElements.define — taking the whole page's script with it.
        if (typeof sourceElement.getAttributeNames === 'function') {
          var names = sourceElement.getAttributeNames();
          for (var n = 0; n < names.length; n++) {
            upgraded.setAttribute(names[n], sourceElement.getAttribute(names[n]));
          }
        } else if (sourceElement.attributes) {
          for (var i = 0; i < sourceElement.attributes.length; i++) {
            var attr = sourceElement.attributes[i];
            if (attr && attr.name) {
              upgraded.setAttribute(attr.name, attr.value);
            }
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

      // `new ctor()` yields a native element (the HTMLElement base hands back a real one), and it
      // does not carry the class's prototype, so its reaction callbacks are not reachable on it.
      // Copy them across so `element.connectedCallback()` runs the component's own code with
      // `this` bound to the element — which is what it expects.
      if (ctor && ctor.prototype) {
        var __reactions = ['connectedCallback', 'disconnectedCallback',
                           'attributeChangedCallback', 'adoptedCallback'];
        for (var __r = 0; __r < __reactions.length; __r++) {
          var __reaction = __reactions[__r];
          if (typeof upgraded[__reaction] !== 'function' &&
              typeof ctor.prototype[__reaction] === 'function') {
            upgraded[__reaction] = ctor.prototype[__reaction];
          }
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
          var upgraded = __broilerUpgradeElement(tagName, ctor, existing[i]);
          // Upgrading an element that is already in the document runs its connectedCallback:
          // the custom-elements spec enqueues the upgrade reaction and then, because the
          // element is connected, the connected reaction. That callback is where a component
          // builds its shadow root, so skipping it left every such element empty — which is
          // what made the four <x-menu> hosts of WPT
          // shadow-dom/focus-navigation/delegatesFocus-highlight-sibling.html render nothing.
          // Elements created later via document.createElement are not connected yet and are
          // deliberately not called here.
          if (upgraded && typeof upgraded.connectedCallback === 'function') {
            try {
              upgraded.connectedCallback();
            } catch (e) {
            }
          }
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

  // Expose the DOM globals a page reaches for by bare name. The bridge registers these on
  // `window`, but a bare identifier does not resolve through it the way it does in a browser
  // (where window *is* the global object), so `class extends HTMLElement` threw
  // 'HTMLElement is not defined' and `customElements.define(...)` threw before any component
  // could build itself. Assigned without `var` so they land in the global scope rather than in
  // this IIFE. Each is aliased only when the bare name is genuinely missing, so a real
  // implementation always wins.
  var __broilerGlobalAliases = ['HTMLElement', 'Element', 'customElements', 'ShadowRoot',
                                'DocumentFragment', 'HTMLTemplateElement'];
  for (var __a = 0; __a < __broilerGlobalAliases.length; __a++) {
    var __name = __broilerGlobalAliases[__a];
    try {
      if (typeof globalThis[__name] === 'undefined' && typeof window[__name] !== 'undefined') {
        globalThis[__name] = window[__name];
      }
    } catch (e) {
    }
  }
})();
";

    /// <summary>
    /// Percentage of pixels that must match the reference for a test to pass when no
    /// other threshold is requested: 99% match, i.e. at most 1% of the pixels differ.
    /// </summary>
    public const double DefaultPassThresholdPercent = 99;

    private readonly int _width;
    private readonly int _height;
    private readonly string? _failureImageDir;
    private readonly bool _verifyReferenceHtml;
    private readonly bool _referenceTestsOnly;
    private readonly HTML.Core.IR.DeterministicRenderConfig _pixelDiffConfig;

    public WptTestRunner(int width = 1024, int height = 768, string? failureImageDir = null,
        bool verifyReferenceHtml = false, double passThresholdPercent = DefaultPassThresholdPercent,
        bool referenceTestsOnly = false)
    {
        _width = width;
        _height = height;
        _failureImageDir = failureImageDir;
        _verifyReferenceHtml = verifyReferenceHtml;
        _referenceTestsOnly = referenceTestsOnly;
        _pixelDiffConfig = CreatePixelDiffConfig(passThresholdPercent);
    }

    /// <summary>
    /// Translates a pass threshold expressed as a percentage of matching pixels into the
    /// diff-ratio form <see cref="PixelDiffRunner"/> compares against — 99% match becomes
    /// a 0.01 maximum diff ratio. The colour tolerance stays at the deterministic-render
    /// default; only the how-many-pixels-may-differ half is configurable.
    /// </summary>
    internal static HTML.Core.IR.DeterministicRenderConfig CreatePixelDiffConfig(double passThresholdPercent)
        => new()
        {
            PixelDiffThreshold = Math.Clamp((100.0 - passThresholdPercent) / 100.0, 0.0, 1.0),
        };

    /// <summary>
    /// Saves the rendered, reference, and diff bitmaps for a failing comparison
    /// under <see cref="_failureImageDir"/> (diagnostic #6), mirroring the test's
    /// path as a sub-directory. Returns the written paths (any may be null when not
    /// captured). Best-effort: an I/O error is logged and does not fail the test.
    /// </summary>
    private (string? Rendered, string? Reference, string? Diff) SaveFailureImages(
        string testPath, string? wptRoot, HTML.Image.BBitmap rendered, HTML.Image.BBitmap reference, HTML.Image.BBitmap? diff)
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
    /// <para>
    /// <c>tools/</c> and <c>docs/</c> are likewise excluded: upstream WPT keeps its
    /// own test-running infrastructure under <c>tools/</c> (wptrunner, wptserve, and
    /// the vendored third-party libraries under <c>tools/third_party/</c>) and its
    /// Sphinx documentation under <c>docs/</c>, and its manifest generation treats
    /// neither as a test source. Without this, vendored demo pages were discovered and
    /// pixel-compared as if they were tests — e.g. the <c>websockets</c> library's
    /// <c>tools/third_party/websockets/experiments/authentication/*.html</c> demos were
    /// reported as 0.0%-match failures in the WPT top-problems report (issue #1473),
    /// crowding real rendering bugs out of the ranking. This is the same class of
    /// corpus pollution the <see cref="IsManualTest"/> filter was added to fix.
    /// </para>
    /// </summary>
    private static readonly string[] NonTestDirSegments =
    [
        "/docs/", "\\docs\\",
        "/reference/", "\\reference\\",
        "/references/", "\\references\\",
        "/reftest/", "\\reftest\\",
        "/resources/", "\\resources\\",
        "/support/", "\\support\\",
        "/test-plan/", "\\test-plan\\",
        "/tools/", "\\tools\\",
    ];

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
    ///   <item>Files in <c>tools/</c> (WPT's own tooling and vendored third-party code)
    ///         or <c>docs/</c> (documentation) directories.</item>
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
    /// <c>animation-delay-001-manual.html</c> — or when it sits under a
    /// <c>manual/</c> directory segment, which upstream treats the same way
    /// (the directory carries the signal for a whole family of tests instead of
    /// each filename repeating it). Such tests require human interaction (they
    /// have no automated pass condition) and therefore cannot be pixel-compared
    /// against a Chromium screenshot; the upstream wptrunner skips them unless
    /// <c>--include-manual</c> is passed.
    /// <para>
    /// DIAGNOSTIC NOTE (WPT issue #1491, problem 24):
    /// <c>html/canvas/element/manual/draw-element-image/dialog-paints-in-top-layer.tentative.html</c>
    /// was discovered and scored as a Regular test. Its Chromium reference is a
    /// blank canvas — Chromium does not implement the proposed
    /// <c>draw-element-image</c> API either — so the only way to "pass" was to
    /// render nothing, while Broiler painting the dialog scored 0.0%. The
    /// directory-segment check below takes such tests out of the scored set.
    /// </para>
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
        // Normalise separators so the check works on both Unix and Windows paths,
        // mirroring IsCrashTest's "/crashtests/" and IsTentativeTest's
        // "/tentative/" segment checks.
        if (testPath.Contains("/manual/", StringComparison.OrdinalIgnoreCase) ||
            testPath.Contains("\\manual\\", StringComparison.OrdinalIgnoreCase))
            return true;

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
    private static readonly Regex RelMismatchPattern = new(@"\brel\s*=\s*[""']?\s*mismatch\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NameAssertPattern = new(@"\bname\s*=\s*[""']?\s*assert\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NameVariantPattern = new(@"\bname\s*=\s*[""']?\s*variant\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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
    /// WPT variant files are templates that the WPT server expands into one or
    /// more query-specific tests (for example <c>test.html?1-1000</c>). Broiler's
    /// pixel runner currently executes only the base file URL, so running these
    /// directly can exercise all subtests at once and compare against the wrong
    /// reference. Treat them as unsupported until the runner models variants.
    /// </summary>
    internal static bool IsWptVariantTest(string html)
    {
        foreach (Match meta in MetaTagPattern.Matches(html))
        {
            if (NameVariantPattern.IsMatch(meta.Value))
                return true;
        }

        return false;
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
    /// One reference a test declares about itself: the href of a
    /// <c>&lt;link rel="match"&gt;</c> (the render must be identical to it) or of a
    /// <c>&lt;link rel="mismatch"&gt;</c> (the render must <em>differ</em> from it).
    /// </summary>
    internal readonly record struct WptReferenceLink(string Href, bool IsMatch);

    /// <summary>
    /// Extracts every reference a test declares — both <c>rel="match"</c> and
    /// <c>rel="mismatch"</c> links, in document order. This is the reftest half of
    /// WPT: a test carrying one of these links says what its own correct rendering
    /// looks like, so it can be checked without a second engine's screenshot.
    /// Returns an empty list for a test that declares nothing.
    /// </summary>
    internal static IReadOnlyList<WptReferenceLink> ExtractReferenceLinks(string html)
    {
        var links = new List<WptReferenceLink>();
        foreach (Match link in LinkTagPattern.Matches(html))
        {
            bool isMatch = RelMatchPattern.IsMatch(link.Value);
            if (!isMatch && !RelMismatchPattern.IsMatch(link.Value))
                continue;
            if (TryExtractAttributeValue(HrefValuePattern, link.Value, out var href))
                links.Add(new WptReferenceLink(href, isMatch));
        }

        return links;
    }

    /// <summary>
    /// Resolves a reference href onto the file it names: a root-relative
    /// <c>/css/foo-ref.html</c> maps under <paramref name="wptRoot"/>, anything else
    /// resolves against the test's own directory, and any query/fragment is stripped
    /// first. Purely syntactic — the caller decides whether the file has to exist.
    /// Returns false for an empty or unusable href.
    /// </summary>
    internal static bool TryResolveReferenceHref(
        string? href, string testPath, string? wptRoot, out string referencePath)
    {
        referencePath = string.Empty;
        if (string.IsNullOrWhiteSpace(href))
            return false;

        try
        {
            var trimmed = href.Split('#', '?')[0].Trim();
            if (trimmed.Length == 0)
                return false;

            referencePath = trimmed.StartsWith("/", StringComparison.Ordinal) && wptRoot != null
                ? Path.GetFullPath(Path.Combine(wptRoot, trimmed.TrimStart('/')))
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(testPath))!, trimmed));
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            referencePath = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// Image extensions a reference href may name directly. WPT reftests normally
    /// point at a reference <em>document</em>, but a reference that is already a
    /// bitmap needs no rendering at all — it is decoded and compared as-is.
    /// </summary>
    private static readonly HashSet<string> ReferenceImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp",
    };

    /// <summary>
    /// True when a reference href names a bitmap rather than a document to render.
    /// </summary>
    internal static bool IsReferenceImagePath(string referencePath)
        => ReferenceImageExtensions.Contains(Path.GetExtension(referencePath));

    /// <summary>
    /// True when <paramref name="testPath"/> declares at least one reference —
    /// image or HTML — that is actually present on disk. This is the membership
    /// test for the reftest suite: a case that answers false has nothing to be
    /// compared against without a second engine, so reftest mode never runs it.
    /// Never throws; an unreadable file simply answers false.
    /// </summary>
    internal static bool HasResolvableReference(string testPath, string? wptRoot)
    {
        try
        {
            foreach (var link in ExtractReferenceLinks(File.ReadAllText(testPath)))
            {
                if (TryResolveReferenceHref(link.Href, testPath, wptRoot, out var referencePath)
                    && File.Exists(referencePath))
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Narrows a discovered test set to the cases reftest mode can actually run:
    /// those declaring a <c>rel="match"</c> / <c>rel="mismatch"</c> reference that
    /// exists on disk. Every other case is dropped here rather than reported as
    /// skipped, so a reftest run's totals describe only tests it could decide.
    /// </summary>
    internal static IEnumerable<string> FilterToReferenceTests(IEnumerable<string> tests, string? wptRoot)
        => tests.Where(testPath => HasResolvableReference(testPath, wptRoot));

    /// <summary>
    /// Resolves the committed golden PNG for a reftest's <c>rel="match"</c>
    /// reference file, mirroring how <see cref="RunTest"/> derives the test's
    /// own golden path (relative to <paramref name="wptRoot"/> with a
    /// <c>.png</c> extension under <paramref name="referenceDir"/>).
    /// <para>
    /// The committed golden is a Chromium screenshot of the <em>test</em> file.
    /// When the reference-generating Chromium lacks a feature the test exercises
    /// (e.g. <c>background-clip: border-area</c>) that screenshot is wrong, even
    /// though the same Chromium renders the deliberately feature-simple
    /// reference file correctly. Comparing Broiler's render against the
    /// reference file's golden therefore recovers the authoritative rendering.
    /// </para>
    /// Returns <see langword="true"/> and sets <paramref name="referenceGoldenPath"/>
    /// only when a <c>rel="match"</c> href resolves to a reference file whose
    /// golden PNG exists on disk. Never throws.
    /// </summary>
    private static bool TryResolveReferenceGoldenPath(
        string html, string testPath, string? wptRoot, string referenceDir,
        out string referenceGoldenPath)
    {
        referenceGoldenPath = string.Empty;
        try
        {
            if (!TryResolveReferenceHref(ExtractMatchHref(html), testPath, wptRoot, out var refPath))
                return false;

            string refRelative = wptRoot is not null
                ? Path.GetRelativePath(wptRoot, refPath)
                : Path.GetFileName(refPath);

            string goldenPath = Path.Combine(
                referenceDir, Path.ChangeExtension(refRelative, ".png"));

            if (!File.Exists(goldenPath))
                return false;

            referenceGoldenPath = goldenPath;
            return true;
        }
        catch
        {
            return false;
        }
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
    /// <remarks>
    /// <para>
    /// <b>Agreement is only evidence when something was drawn.</b> A test that renders a blank
    /// canvas and a reference that renders a blank canvas match at 100%, so a gap that makes
    /// Broiler paint nothing at all used to clear itself here and move off the severity ranking
    /// entirely. The 2026-08-13 triage of the flags nobody had checked by hand found 17 of 28 were
    /// exactly that: a uniform render on both sides while Chromium painted substantial content in
    /// both (<c>docs/wpt-rendering-gaps-open.md</c>).
    /// </para>
    /// <para>
    /// So a clear now needs the render to have content, judged against the committed golden: a
    /// uniform render is only agreement when the golden is uniform too. Both of the checks that
    /// document proposed are in that one condition — it rejects a uniform render, and it rejects a
    /// render with no content against a golden that has some.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Renders the test's own <c>rel="match"</c> reference and compares Broiler's render to it.
    /// </summary>
    /// <returns>
    /// The match percentage against that reference, and the note to append to the failure message
    /// when the comparison says something triage needs. Both are null when the check could not run.
    /// </returns>
    /// <remarks>
    /// The percentage is returned whether or not it clears the pass threshold. Gating on the
    /// threshold and returning nothing otherwise threw away the one measurement that tells a
    /// reference disagreement apart from an engine gap — see
    /// <see cref="WptTestResult.ReferenceMatchPercent"/>.
    /// </remarks>
    private (double? Percent, string? Note) VerifyAgainstReferenceHtml(
        string testPath, string html, string? wptRoot,
        HTML.Image.BBitmap testRender, HTML.Image.BBitmap committedGolden, double goldenPercent)
    {
        try
        {
            // Strip any fragment/query and resolve relative to the test file
            // (a root-relative "/foo" maps under the WPT root).
            if (!TryResolveReferenceHref(ExtractMatchHref(html), testPath, wptRoot, out var refPath)
                || !File.Exists(refPath))
            {
                return (null, null);
            }

            // A render that drew nothing agrees with any other blank render, so it cannot be used
            // as evidence that the golden is the outlier.
            if (IsUniform(testRender) && !IsUniform(committedGolden))
                return (null, null);

            using var refRender = RenderHtmlFileBitmap(refPath, wptRoot);
            using var diff = PixelDiffRunner.Compare(testRender, refRender, _pixelDiffConfig);
            double pct = (1.0 - diff.DiffRatio) * 100;

            if (diff.IsMatch)
            {
                return (pct,
                    $"⚠ suspect reference: Broiler matches its rel=match reference HTML " +
                    $"({pct:F1}%) but not the committed reference PNG — the committed reference " +
                    "is likely stale/incorrect, not a Broiler bug");
            }

            // Short of the gate, but far closer to the test's own reference than to the golden:
            // the two engines disagree about the reference, so most of the golden's mismatch is
            // not something the engine is getting wrong about this test.
            if (pct - goldenPercent >= ReferenceDisagreementMargin)
            {
                return (pct,
                    $"ℹ reference disagreement: Broiler is {pct:F1}% against the test's own " +
                    $"rel=match reference and {goldenPercent:F1}% against the committed PNG — " +
                    "most of this gap is the two references disagreeing, not the test's own feature");
            }

            return (pct, null);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>
    /// How far a test's score against its own reference must exceed its score against the committed
    /// golden before the gap is attributed to the references disagreeing rather than to the engine.
    /// </summary>
    /// <remarks>
    /// A margin rather than a second pass threshold, so it does not need tuning against the run's
    /// own gate: the claim it supports is comparative ("far better against one than the other"),
    /// not absolute. The cases it was sized for sit at 94% versus 0.8%.
    /// </remarks>
    private const double ReferenceDisagreementMargin = 25.0;

    /// <summary>
    /// Whether every pixel of <paramref name="bitmap"/> is the same colour — the signature of a
    /// render that drew nothing, and the one thing a pixel comparison cannot tell apart from
    /// agreement.
    /// </summary>
    internal static bool IsUniform(HTML.Image.BBitmap? bitmap)
    {
        if (bitmap is null || bitmap.Width == 0 || bitmap.Height == 0)
            return true;

        var first = bitmap.GetPixel(0, 0);
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != first)
                    return false;
            }
        }

        return true;
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
    /// <para>
    /// When the runner was constructed with <c>referenceTestsOnly</c>, the case is
    /// instead decided as a WPT reftest — both sides rendered by Broiler, no
    /// reference directory consulted. See <see cref="RunReferenceTest"/>.
    /// </para>
    /// </summary>
    internal WptTestResult RunTest(string testPath, string referenceDir, string? wptRoot = null)
    {
        if (_referenceTestsOnly)
            return RunReferenceTest(testPath, wptRoot);

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
                Message = "WPT manual test (filename ends in -manual, or a manual/ path segment); requires human interaction.",
            };
        }

        string html;
        try
        {
            html = WptSubstitution.ReadDocument(testPath, wptRoot);
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

        // The instant this test wants to be screenshotted at, read now: `html` is rewritten
        // by the script pass and the post-processor below, and the call that carries the
        // delay lives in a <script> both of them remove.
        TimeSpan presentationTime = ScreenshotPresentationTime(html);

        if (IsWptVariantTest(html))
        {
            return new WptTestResult
            {
                TestPath = testPath,
                Skipped = true,
                SkipReason = SkipReason.UnsupportedWptVariant,
                Message = "WPT variant test; Broiler's pixel runner does not expand query-specific variants yet.",
                HelpLinks = metadata.HelpLinks,
                Assertion = metadata.Assertion,
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

        // Pre-load WPT fonts BEFORE any measurement. WPT test files reference fonts
        // via root-relative URLs (e.g. @import "/fonts/ahem.css") that resolve to
        // {wptRoot}/fonts/ on disk, and the CSS generic families resolve to the
        // Liberation faces the reference Chromium uses. This MUST run before
        // ExecuteScriptsWithDom below: the DomBridge pass measures text (offset*,
        // check-layout), and the font adapter caches the typeface it resolves per
        // family — if fonts are registered only afterwards, that first measurement
        // caches the bundled fallback (Vazirmatn, far wider) for `serif`/`sans-serif`
        // and the later render reuses the stale width, wrapping text to extra lines
        // and shifting every downstream box (the css-backgrounds/background-clip tail).
        if (wptRoot != null)
            EnsureWptFontsLoaded(wptRoot);

        // Execute inline scripts via DomBridge.
        IReadOnlyList<LayoutAssertionFailure>? layoutAssertionFailures = null;
        Broiler.Dom.DomDocument? renderDocument = null;
        try
        {
            var executed = ExecuteScriptsWithDom(
                html,
                new Uri(Path.GetFullPath(testPath)).AbsoluteUri,
                wptRoot,
                batchStyleInvalidations: IsCrashTest(testPath));
            html = executed.Html ?? html;
            renderDocument = executed.Document;
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

        // Post-process (strip scripts, clean up for rendering) — on whichever form the render
        // path is going to consume.
        if (renderDocument is not null)
            WptDocumentPostProcessor.Process(renderDocument);
        else
            html = HtmlPostProcessor.Process(html);

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
            stylesheetHandler = WptResourceHandlers.Stylesheet(wptRoot);
            imageHandler = WptResourceHandlers.Image(wptRoot);
        }

        // Render via Broiler HTML stack.
        HTML.Image.BBitmap rendered;
        try
        {
            using var presentation = ImageAnimationClock.Pin(presentationTime);
            // The checkout is the document root a `/`-rooted <frame>/<iframe> src resolves against,
            // matching what the stylesheet/image handlers above already do for their own URLs — and
            // it is what WPT's own hosts serve, so a substituted `.sub` template's cross-origin
            // frame resolves to the same checkout instead of to a network fetch that cannot happen.
            using var documentRoot = Broiler.Layout.Engine.DocumentRoot.Pin(wptRoot, WptSubstitution.ServedHosts);
            rendered = RenderWithNativeAnchor(html, () => renderDocument is not null
                ? WptDocumentRenderer.RenderToImage(renderDocument, _width, _height,
                    backgroundColor: BColor.White,
                    stylesheetLoad: stylesheetHandler, imageLoad: imageHandler, baseUrl: testBaseUrl)
                : HtmlRender.RenderToImageWithStyleSet(html, _width, _height,
                    backgroundColor: BColor.White,
                    stylesheetLoad: stylesheetHandler, imageLoad: imageHandler, baseUrl: testBaseUrl));
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

            HTML.Image.BBitmap reference;
            try
            {
                reference = HTML.Image.BBitmap.Decode(referencePath);
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
                using var diff = PixelDiffRunner.Compare(rendered, reference, _pixelDiffConfig);

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

                // Reftest golden fallback: the committed golden is a Chromium
                // screenshot of the *test* file. When the reference-generating
                // Chromium lacks a feature the test exercises (e.g.
                // background-clip: border-area), that screenshot is wrong, yet the
                // same Chromium renders the feature-simple rel=match *reference*
                // file correctly. Compare against the reference's committed golden;
                // a match there means Broiler agrees with the authoritative
                // rendering and the reftest passes. Only reached on a failing
                // comparison, so it can add passes but never mask a real Broiler
                // bug (a wrong render matches neither golden).
                if (TryResolveReferenceGoldenPath(html, testPath, wptRoot, referenceDir, out var refGoldenPath))
                {
                    try
                    {
                        using var refGolden = HTML.Image.BBitmap.Decode(refGoldenPath);
                        using var refDiff = PixelDiffRunner.Compare(rendered, refGolden, _pixelDiffConfig);
                        if (refDiff.IsMatch)
                        {
                            return new WptTestResult
                            {
                                TestPath = testPath,
                                Passed = true,
                                MatchPercent = (1.0 - refDiff.DiffRatio) * 100,
                                LayoutAssertionFailures = layoutAssertionFailures,
                            };
                        }
                    }
                    catch
                    {
                        // Best-effort: a missing/undecodable reference golden
                        // falls through to the normal mismatch reporting below.
                    }
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
                var (referencePercent, referenceNote) = _verifyReferenceHtml
                    ? VerifyAgainstReferenceHtml(testPath, html, wptRoot, rendered, reference, matchPct)
                    : (null, null);
                if (referenceNote != null)
                    message = $"{message} [{referenceNote}]";

                // SuspectReference keeps its narrower meaning — Broiler *reproduced* the declared
                // reference — because the ranking uses it to drop a test out of the candidates.
                string? suspectReference =
                    referenceNote != null && referencePercent is not null && referenceNote[0] == '⚠'
                        ? referenceNote
                        : null;

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
                    ReferenceMatchPercent = referencePercent,
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
    /// Decides one WPT reftest entirely inside Broiler: the test document and the
    /// reference it declares are both rendered by this engine and compared against
    /// each other at the runner's pass threshold. Nothing outside the WPT checkout is
    /// consulted — no Chromium screenshot, no committed golden — so the only thing a
    /// result can say is whether Broiler agrees with WPT's own statement of what the
    /// test should look like.
    /// <para>
    /// A <c>rel="match"</c> reference must be reproduced pixel-for-pixel (within the
    /// threshold); a <c>rel="mismatch"</c> reference must <em>not</em> be. When a test
    /// declares several <c>rel="match"</c> references it passes on the first one it
    /// reproduces, which is WPT's own rule; the closest of the failing candidates is
    /// the one reported. A test whose references are all <c>rel="mismatch"</c> passes
    /// only when it differs from every one of them.
    /// </para>
    /// <para>
    /// A reference href that already names a bitmap is decoded rather than rendered.
    /// Reference chains (a reference that itself declares a reference) are not
    /// followed: the declared reference is rendered as written, which is what the
    /// chain asserts it looks like anyway.
    /// </para>
    /// </summary>
    /// <param name="testPath">Path to the test HTML file.</param>
    /// <param name="wptRoot">WPT checkout root, for fonts and root-relative resources.</param>
    internal WptTestResult RunReferenceTest(string testPath, string? wptRoot = null) =>
        InPageModeFor(testPath, () => RunReferenceTestCore(testPath, wptRoot));

    private WptTestResult RunReferenceTestCore(string testPath, string? wptRoot)
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

        // Manual tests need a human, exactly as in the golden-image path.
        if (IsManualTest(testPath))
        {
            return new WptTestResult
            {
                TestPath = testPath,
                Skipped = true,
                SkipReason = SkipReason.ManualTest,
                Message = "WPT manual test (filename ends in -manual, or a manual/ path segment); requires human interaction.",
            };
        }

        string html;
        try
        {
            html = WptSubstitution.ReadDocument(testPath, wptRoot);
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

        var metadata = ExtractTestMetadata(html);

        if (IsWptVariantTest(html))
        {
            return new WptTestResult
            {
                TestPath = testPath,
                Skipped = true,
                SkipReason = SkipReason.UnsupportedWptVariant,
                Message = "WPT variant test; Broiler's pixel runner does not expand query-specific variants yet.",
                HelpLinks = metadata.HelpLinks,
                Assertion = metadata.Assertion,
            };
        }

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

        var references = new List<(string Path, bool IsMatch)>();
        foreach (var link in ExtractReferenceLinks(html))
        {
            if (TryResolveReferenceHref(link.Href, testPath, wptRoot, out var referencePath)
                && File.Exists(referencePath))
            {
                references.Add((referencePath, link.IsMatch));
            }
        }

        if (references.Count == 0)
        {
            return new WptTestResult
            {
                TestPath = testPath,
                Skipped = true,
                SkipReason = SkipReason.NoReferenceDeclared,
                Message = "No rel=match/rel=mismatch reference on disk; nothing to compare against in reftest mode.",
                HelpLinks = metadata.HelpLinks,
                Assertion = metadata.Assertion,
            };
        }

        // Fonts before the first render, for the same reason the golden path loads them
        // there: the first text measurement caches the typeface it resolves per family.
        if (wptRoot != null)
            using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.Fonts))
                EnsureWptFontsLoaded(wptRoot);

        HTML.Image.BBitmap rendered;
        try
        {
            rendered = RenderHtmlFileBitmap(testPath, wptRoot);
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
            var matches = references.Where(r => r.IsMatch).ToList();
            return matches.Count > 0
                ? CompareAgainstMatchReferences(testPath, wptRoot, rendered, matches, metadata)
                : CompareAgainstMismatchReferences(testPath, wptRoot, rendered, references, metadata);
        }
    }


    /// <summary>
    /// <see cref="PixelDiffRunner.Compare"/> under a phase scope. A local helper rather than a
    /// <c>using</c> at each call site because the result is itself disposable and the two scopes
    /// would nest confusingly.
    /// </summary>
    private PixelDiffResult MeasuredCompare(HTML.Image.BBitmap rendered, HTML.Image.BBitmap reference)
    {
        using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.Compare))
            return PixelDiffRunner.Compare(rendered, reference, _pixelDiffConfig);
    }

    /// <summary>
    /// The <c>rel="match"</c> half of <see cref="RunReferenceTest"/>: pass on the first
    /// reference the render reproduces, otherwise report the closest candidate.
    /// </summary>
    private WptTestResult CompareAgainstMatchReferences(
        string testPath,
        string? wptRoot,
        HTML.Image.BBitmap rendered,
        IReadOnlyList<(string Path, bool IsMatch)> references,
        TestMetadata metadata)
    {
        WptTestResult? closestFailure = null;
        foreach (var (referencePath, _) in references)
        {
            if (!TryLoadReferenceBitmap(testPath, referencePath, wptRoot, metadata, out var reference, out var loadFailure))
            {
                // A reference that cannot be produced is itself the finding; keep it only
                // if no comparison ever happens, so a later reference can still pass.
                closestFailure ??= loadFailure;
                continue;
            }

            using (reference)
            {
                using var diff = MeasuredCompare(rendered, reference);
                double matchPct = (1.0 - diff.DiffRatio) * 100;

                if (diff.IsMatch)
                {
                    return new WptTestResult
                    {
                        TestPath = testPath,
                        Passed = true,
                        MatchPercent = matchPct,
                        Message = $"Matches {DescribeReference(referencePath, wptRoot)} at {matchPct:F1}%.",
                    };
                }

                if (closestFailure is null || matchPct > (closestFailure.MatchPercent ?? -1))
                {
                    using var diagnoseScope = WptPhaseTrace.Measure(WptPhaseTrace.Phases.Diagnose);
                    var diagnostics = MismatchClassifier.Classify(
                        diff, rendered.Width, rendered.Height, reference.Width, reference.Height);
                    var bands = DisplacementBandAnalyzer.Analyze(diff.Mismatches);
                    var displacementProfile = DisplacementBandAnalyzer.DescribeNonUniform(bands);
                    var message =
                        $"Pixel mismatch against {DescribeReference(referencePath, wptRoot)}: " +
                        $"{matchPct:F1}% match ({diff.DiffPixelCount}/{diff.TotalPixelCount} pixels differ) — {diagnostics.Summary}";
                    if (displacementProfile != null)
                        message = $"{message} [{displacementProfile}]";

                    var images = SaveFailureImages(testPath, wptRoot, rendered, reference, diff.DiffBitmap);

                    closestFailure = new WptTestResult
                    {
                        TestPath = testPath,
                        Passed = false,
                        MatchPercent = matchPct,
                        Message = message,
                        Category = FailureCategory.PixelMismatch,
                        MismatchDiagnostics = diagnostics,
                        DisplacementProfile = displacementProfile,
                        RenderedImagePath = images.Rendered,
                        ReferenceImagePath = images.Reference,
                        DiffImagePath = images.Diff,
                        HelpLinks = metadata.HelpLinks,
                        Assertion = metadata.Assertion,
                    };
                }
            }
        }

        return closestFailure!;
    }

    /// <summary>
    /// The <c>rel="mismatch"</c> half of <see cref="RunReferenceTest"/>: the render has
    /// to differ from every declared reference, so any one it reproduces is a failure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A mismatch is decided on inequality, not on the pass threshold.</b> The
    /// <c>rel="match"</c> half asks "are these the same picture?" and answers it with a
    /// tolerance — at the default threshold, up to 1% of the pixels may differ. Reusing that
    /// gate here inverts the assertion the test is making: <c>rel="mismatch"</c> says the two
    /// renders must not be the same picture, and a difference smaller than 1% of the viewport
    /// is still a difference. Under the old <c>diff.IsMatch</c> test anything below the
    /// threshold was reported as "identical", so a test that Broiler rendered *correctly*
    /// failed: the engine drew exactly the thing the test asked for, and the gate absorbed it.
    /// </para>
    /// <para>
    /// That is the normal size of what these tests assert, because a mismatch reference is
    /// deliberately minimal — it differs from the test by one glyph, one rule, one box. The
    /// <c>css/css-text/white-space/control-chars-*</c> family (63 tests) renders one 4em glyph
    /// against a reference that has none: 2 217 differing pixels on a 1024×768 page, against
    /// the 7 864 that 1% of 786 432 absorbs. 62 of the 63 failed with the glyph on screen the
    /// whole time, which is also why the family topped the suite's failure list. Upstream
    /// wptrunner compares reftest screenshots for equality and applies a tolerance only where
    /// the test itself opts in via <c>fuzzy</c> metadata, so inequality is also what WPT means
    /// by the relation.
    /// </para>
    /// <para>
    /// The per-channel <see cref="HTML.Core.IR.DeterministicRenderConfig.ColorTolerance"/> still
    /// applies, so "differs" means a visibly different pixel rather than a rounding wobble. Both
    /// sides come out of the same deterministic renderer, so two renders of the same picture are
    /// byte-identical and land on 0 differing pixels regardless.
    /// </para>
    /// </remarks>
    private WptTestResult CompareAgainstMismatchReferences(
        string testPath,
        string? wptRoot,
        HTML.Image.BBitmap rendered,
        IReadOnlyList<(string Path, bool IsMatch)> references,
        TestMetadata metadata)
    {
        foreach (var (referencePath, _) in references)
        {
            if (!TryLoadReferenceBitmap(testPath, referencePath, wptRoot, metadata, out var reference, out var loadFailure))
                return loadFailure!;

            using (reference)
            {
                using var diff = PixelDiffRunner.Compare(rendered, reference, _pixelDiffConfig);
                if (diff.DiffPixelCount > 0)
                    continue;

                // Reaching here means zero differing pixels, so the match is 100% by
                // construction — reported as such rather than recomputed from the ratio,
                // which can only be 0 at this point.
                var images = SaveFailureImages(testPath, wptRoot, rendered, reference, diff.DiffBitmap);
                return new WptTestResult
                {
                    TestPath = testPath,
                    Passed = false,
                    MatchPercent = 100,
                    Message =
                        $"Render is pixel-identical to {DescribeReference(referencePath, wptRoot)}, " +
                        "which the test declares it must differ from (rel=mismatch).",
                    Category = FailureCategory.PixelMismatch,
                    RenderedImagePath = images.Rendered,
                    ReferenceImagePath = images.Reference,
                    DiffImagePath = images.Diff,
                    HelpLinks = metadata.HelpLinks,
                    Assertion = metadata.Assertion,
                };
            }
        }

        return new WptTestResult
        {
            TestPath = testPath,
            Passed = true,
            Message = $"Differs from all {references.Count} rel=mismatch reference(s), as required.",
        };
    }

    /// <summary>
    /// Produces the bitmap a reference stands for: decoded when the href names an
    /// image, rendered by Broiler when it names a document. On failure returns the
    /// result the test should carry (a decode error and a render error are distinct
    /// categories, and neither is the test's own fault).
    /// </summary>
    private bool TryLoadReferenceBitmap(
        string testPath,
        string referencePath,
        string? wptRoot,
        TestMetadata metadata,
        out HTML.Image.BBitmap bitmap,
        out WptTestResult? failure)
    {
        bool isImage = IsReferenceImagePath(referencePath);
        try
        {
            bitmap = isImage
                ? HTML.Image.BBitmap.Decode(referencePath)
                : RenderHtmlFileBitmap(referencePath, wptRoot);
            failure = null;
            return true;
        }
        catch (Exception ex)
        {
            bitmap = null!;
            failure = new WptTestResult
            {
                TestPath = testPath,
                Passed = false,
                Message = isImage
                    ? $"Failed to decode reference image {DescribeReference(referencePath, wptRoot)}: {ex.Message}"
                    : $"Failed to render reference document {DescribeReference(referencePath, wptRoot)}: {ex.Message}",
                Category = isImage ? FailureCategory.ReferenceDecodeError : FailureCategory.RenderingError,
                StackTrace = ex.StackTrace,
                HelpLinks = metadata.HelpLinks,
                Assertion = metadata.Assertion,
            };
            return false;
        }
    }

    /// <summary>Names a reference the way a report should show it: relative to the WPT root.</summary>
    private static string DescribeReference(string referencePath, string? wptRoot)
        => (wptRoot is not null
            ? Path.GetRelativePath(wptRoot, referencePath)
            : Path.GetFileName(referencePath)).Replace('\\', '/');

    /// <summary>
    /// Runs a WPT <c>rel="match"</c> test by rendering both the test HTML and
    /// its reference HTML with the Broiler stack, then comparing the two bitmaps.
    /// This avoids font-rendering / scrollbar differences between Chromium and
    /// Broiler that make cross-engine comparison imprecise.
    /// </summary>
    /// <param name="testPath">Path to the test HTML file.</param>
    /// <param name="refHtmlPath">Path to the WPT reference HTML file.</param>
    /// <param name="wptRoot">Optional WPT root directory for font loading.</param>
    internal WptTestResult RunMatchTest(string testPath, string refHtmlPath, string? wptRoot = null) =>
        InPageModeFor(testPath, () => RunMatchTestCore(testPath, refHtmlPath, wptRoot));

    private WptTestResult RunMatchTestCore(string testPath, string refHtmlPath, string? wptRoot)
    {
        if (!File.Exists(testPath))
            return new WptTestResult { TestPath = testPath, Passed = false,
                Message = "Test file not found.", Category = FailureCategory.FileIO };
        if (!File.Exists(refHtmlPath))
            return new WptTestResult { TestPath = testPath, Passed = false,
                Message = $"Reference HTML not found: {refHtmlPath}", Category = FailureCategory.FileIO };

        if (wptRoot != null)
            EnsureWptFontsLoaded(wptRoot);

        HTML.Image.BBitmap? rendered = null;
        HTML.Image.BBitmap? reference = null;
        try
        {
            rendered = RenderHtmlFileBitmap(testPath, wptRoot);
            reference = RenderHtmlFileBitmap(refHtmlPath, wptRoot);

            using var diff = PixelDiffRunner.Compare(rendered, reference, _pixelDiffConfig);
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
    /// True when <paramref name="src"/> is an absolute http(s) URL on a host the corpus does not
    /// serve — i.e. a sub-resource the engine would fetch off the real network.
    /// </summary>
    /// <remarks>
    /// A conformance run must not touch the network, and one that does is not merely impure — it
    /// is slow in a way no per-test budget survives. Both loaders are synchronous on the layout
    /// thread (<c>HtmlRender</c> sets <c>AvoidAsyncImagesLoading</c>), so a fetch blocks the render
    /// for as long as the host takes to answer — and an unroutable one never does, so what it costs
    /// is the client's whole timeout: five seconds for a <c>&lt;link&gt;</c>, and .NET's 100-second
    /// default for an <c>&lt;img&gt;</c> until the engine-side cap lands. Neither is a render, and
    /// neither is cached, so the next layout pass pays it again.
    /// <para>
    /// Both timed-out tests of the 2026-08-16 run are this and nothing else.
    /// <c>css/CSS2/cascade-import/cascade-import-009.xht</c> links three stylesheets to a
    /// <c>delayed-file</c> CGI on a personal server that pauses 2, 5 and 8 seconds by design;
    /// <c>conformance-checkers/html/elements/img/src-isvalid.html</c> is 88 <c>&lt;img&gt;</c> with
    /// 88 distinct sources, deliberately including IP literals (<c>http://192.0x00A80001</c>) and
    /// documentation addresses (<c>http://[2001::1]</c>) that black-hole on a CI runner with real
    /// internet. One of either is enough to lose the test.
    /// </para>
    /// <para>
    /// Suppressing the load rather than redirecting it is what the corpus wants: the resource does
    /// not load, which is exactly what these tests are checking the parser does with the URL.
    /// </para>
    /// </remarks>
    internal static bool IsOffCorpusNetworkResource(string? src)
    {
        if (string.IsNullOrWhiteSpace(src)
            || !Uri.TryCreate(src, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return false;

        foreach (var served in WptSubstitution.ServedHosts)
        {
            if (string.Equals(uri.Host, served, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Maps a root-relative WPT URL (<c>/images/blue.png</c>, <c>/fonts/Ahem.ttf</c>) — or an
    /// absolute one on a host WPT serves from the same checkout
    /// (<c>http://www.web-platform.test:8000/images/blue.png</c>, which is what a substituted
    /// <c>.sub</c> template carries) — onto the file it names under <paramref name="wptRoot"/>,
    /// mirroring what a real WPT server resolves. Returns <c>null</c> when the URL is neither of
    /// those or names nothing on disk.
    /// <para>
    /// The query and fragment are stripped, and percent-escapes decoded, before touching the disk —
    /// exactly what the reference generator's <c>decodeFileUrlPath</c> does on its side. WPT routinely
    /// tags a resource with a cache-busting or identifying query
    /// (<c>/images/blue.png?inline-style</c>, <c>/fonts/Ahem.ttf?initiator-html</c>); taking the raw
    /// string as a path looked for a file literally named <c>blue.png?inline-style</c>, so every such
    /// image, font and stylesheet silently failed to load while Chromium's golden had it
    /// (WPT resource-timing/tentative/initiator-url/static-resource, resource-timing/initiator-type/misc).
    /// </para>
    /// </summary>
    internal static string? TryResolveWptRootRelativePath(string? src, string wptRoot)
    {
        // A `.sub` template's URLs are absolute and on a WPT host (`http://www.web-platform.test:8000/…`)
        // once substituted. WPT serves every one of those hosts from this same checkout, so such a
        // URL is the root-relative one with an origin in front of it.
        src = WptSubstitution.TryStripServedOrigin(src) ?? src;

        if (src == null || !src.StartsWith("/", StringComparison.Ordinal))
            return null;

        var path = src.Split('?', '#')[0];
        if (path.Length <= 1)
            return null;

        try
        {
            path = Uri.UnescapeDataString(path);
        }
        catch (UriFormatException)
        {
            // Malformed escape — fall back to the raw path rather than failing the resolve.
        }

        var rel = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var local = Path.Combine(wptRoot, rel);
        if (!File.Exists(local))
            return null;

        // Absolute, always. The resolved path is handed straight to the engine, which
        // resolves what it is given against the *document's* base URL — so a relative
        // one (which is what this returns whenever --wpt-dir is itself relative, e.g.
        // the `--wpt-dir tests/wpt/checkout` every doc and script spells out) is looked
        // for next to the test file and silently not found. File.Exists above succeeds
        // regardless, because it resolves against the process's working directory, so
        // the failure is invisible: the render simply comes out with no image.
        return Path.GetFullPath(local);
    }

    /// <summary>
    /// The <c>check-layout-th.js</c> assertions the most recent render evaluated, or an empty
    /// list when the document declared none. A <c>check-layout</c> test states its expected
    /// geometry in the markup, so this is the one signal about it that needs no reference image
    /// — which is what <c>--render --check-layout</c> reports.
    /// </summary>
    internal IReadOnlyList<DomBridge.CheckLayoutAssertion> LastLayoutAssertions { get; private set; } = [];

    /// <summary>
    /// Renders an HTML file through the full Broiler pipeline (script execution,
    /// anchor resolution, rendering) and returns the resulting bitmap.
    /// </summary>
    private HTML.Image.BBitmap RenderHtmlFileBitmap(string htmlPath, string? wptRoot)
    {
        // Zoom render lever: enable the engine's used-value `zoom` for the whole serialize+render so the
        // bridge bake is skipped (ZoomBakeActive) and the engine scales used values during layout. Default
        // off → the serialize bakes and this is byte-identical. [ThreadStatic] save/restore, so it scopes to
        // this render on this thread only.
        var previousNativeZoom = Broiler.Layout.Engine.NativeZoom.Enabled;
        if (NativeZoom)
            Broiler.Layout.Engine.NativeZoom.Enabled = true;
        try
        {
            return RenderHtmlFileBitmapCore(htmlPath, wptRoot);
        }
        finally
        {
            Broiler.Layout.Engine.NativeZoom.Enabled = previousNativeZoom;
        }
    }

    private HTML.Image.BBitmap RenderHtmlFileBitmapCore(string htmlPath, string? wptRoot)
    {
        string html;
        using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.FileRead))
            html = WptSubstitution.ReadDocument(htmlPath, wptRoot);

        if (IsMediaPlaybackTest(html))
            throw new InvalidOperationException("Test requires media playback.");

        // Read before the script pass and post-processor rewrite `html` out from under it.
        TimeSpan presentationTime = ScreenshotPresentationTime(html);

        var testBaseUrl = new Uri(Path.GetFullPath(htmlPath)).AbsoluteUri;

        // Set local base path for sub-resource resolution.
        Broiler.Dom.DomDocument? renderDocument;
        using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.Scripts))
        {
            var executed = ExecuteScriptsWithDom(html, testBaseUrl, wptRoot);
            html = executed.Html ?? html;
            renderDocument = executed.Document;
            LastLayoutAssertions = executed.LayoutAssertions;
        }

        using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.PostProcess))
        {
            if (renderDocument is not null)
                WptDocumentPostProcessor.Process(renderDocument);
            else
                html = HtmlPostProcessor.Process(html);
        }

        EventHandler<HtmlStylesheetLoadEventArgs>? stylesheetHandler = null;
        EventHandler<HtmlImageLoadEventArgs>? imageHandler = null;
        if (wptRoot != null)
        {
            stylesheetHandler = WptResourceHandlers.Stylesheet(wptRoot);
            imageHandler = WptResourceHandlers.Image(wptRoot);
        }

        using var presentation = ImageAnimationClock.Pin(presentationTime);
        // The checkout is the document root a `/`-rooted <frame>/<iframe> src resolves against,
        // matching what the stylesheet/image handlers above already do for their own URLs — and
        // it is what WPT's own hosts serve, so a substituted `.sub` template's cross-origin frame
        // resolves to the same checkout instead of to a network fetch that cannot happen.
        using var documentRoot = Broiler.Layout.Engine.DocumentRoot.Pin(wptRoot, WptSubstitution.ServedHosts);
        using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.Render))
        {
            // `@page` paint applies to paged media only, so it is read for a print test and for
            // nothing else — and for both sides of one, since PrintMedia is set from the test path
            // for the whole reftest. A document with no page background, border or padding resolves
            // to null here and renders exactly as it always has.
            //
            // The page box comes first because the decoration needs it: it is the surface a paged
            // render lays out on, and it is the basis a `@page` percentage resolves against — in the
            // unpaginated render below too, whose sheet is the runner's viewport instead.
            var declaredPage = PrintMedia
                ? WptPageBox.Resolve(html, new System.Drawing.SizeF(_width, _height))
                : default;
            var decoration = PrintMedia ? WptPageDecoration.Resolve(html, declaredPage) : null;

            if (PagedRender)
            {
                // A paged render knows its pages one by one, so it takes no document-wide guess at
                // a named page: the base box below is the unconditional rule, page one adds
                // `@page :first`, and every other page's name is read off the laid-out flow.
                var surface = new System.Drawing.SizeF(_width, _height);
                WptPageBox ResolvePage(string? name, bool first) =>
                    WptPageBox.Resolve(html, surface, firstPage: first, pageName: name ?? string.Empty);

                var basePage = ResolvePage(null, first: false);

                return RenderWithNativeAnchor(html, () => WptDocumentRenderer.RenderPaged(
                    renderDocument, html, basePage,
                    backgroundColor: BColor.White,
                    stylesheetLoad: stylesheetHandler, imageLoad: imageHandler, baseUrl: testBaseUrl,
                    decoration: decoration, resolvePage: ResolvePage));
            }

            if (decoration is not null)
            {
                // Unpaginated, so the surface stays the runner's viewport and only the `@page`'s
                // margins are taken from the rule: taking its `size` too would resize one side of a
                // comparison whose reference states no size of its own.
                var sheet = declaredPage with { BoxSize = new System.Drawing.SizeF(_width, _height) };
                return RenderWithNativeAnchor(html, () => WptDocumentRenderer.RenderDecorated(
                    renderDocument, html, sheet, decoration,
                    backgroundColor: BColor.White,
                    stylesheetLoad: stylesheetHandler, imageLoad: imageHandler, baseUrl: testBaseUrl));
            }

            return RenderWithNativeAnchor(html, () => renderDocument is not null
                ? WptDocumentRenderer.RenderToImage(renderDocument, _width, _height,
                    backgroundColor: BColor.White,
                    stylesheetLoad: stylesheetHandler, imageLoad: imageHandler, baseUrl: testBaseUrl)
                : HtmlRender.RenderToImageWithStyleSet(html, _width, _height,
                    backgroundColor: BColor.White,
                    stylesheetLoad: stylesheetHandler, imageLoad: imageHandler, baseUrl: testBaseUrl));
        }
    }

    /// <summary>
    /// Paged-print render lever. Default OFF: a <c>-print</c> reftest is rendered on the ordinary
    /// viewport surface, both sides alike, as it always has been. Force on with
    /// <c>BROILER_WPT_PAGED_PRINT=1</c> to render it as CSS Paged Media says — pages of the
    /// document's own <c>@page</c> box, cut from the flow and stacked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Off by default because paged rendering is not yet a better answer than not paginating at
    /// all. Measured over the 409 print reftests: 252 passing unpaginated, 212 paged. That is not
    /// the paging being wrong so much as it being partial — where the flow is unpaginated a test
    /// and its reference are wrong in the same way and agree, and each unimplemented piece of
    /// paged media (fragmentation of flex and table content, per-name page sizes) breaks that
    /// agreement for the pairs that rest on it.
    /// </para>
    /// <para>
    /// It is worth having behind a lever rather than not at all because it is the only way to
    /// measure any of that: with the lever off, every paged-media fix scores exactly zero.
    /// </para>
    /// </remarks>
    internal static bool PagedPrint { get; set; } =
        Environment.GetEnvironmentVariable("BROILER_WPT_PAGED_PRINT") is ("1" or "true" or "TRUE" or "on");

    /// <summary>
    /// Whether the render in progress is a paged one. Set for the whole of one reftest — the test
    /// document <em>and</em> its references — because a reftest is run in one mode, and WPT's own
    /// print references are not themselves named <c>-print</c>
    /// (<c>block-page-break-inside-avoid-1-print.html</c> matches
    /// <c>block-page-break-inside-avoid-print-ref.html</c>). Deciding per document instead would
    /// paginate one side of the comparison and not the other.
    /// </summary>
    /// <remarks>
    /// <c>[ThreadStatic]</c> for the same reason the engine's <c>NativeZoom</c> and
    /// <c>NativeAnchorPlacement</c> levers are: it scopes to the renders this thread is running,
    /// and the runner's parallelism is across worker processes and threads, never inside one test.
    /// </remarks>
    [ThreadStatic]
    private static bool PagedRender;

    /// <summary>
    /// Whether the render in progress stands in for a printed page — set for the whole of one
    /// <c>-print</c> reftest, its references included, by the same rule and for the same reason as
    /// <see cref="PagedRender"/>.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="PagedRender"/> because it answers a different question: that one
    /// asks whether to cut the flow into pages, which is still behind the
    /// <see cref="PagedPrint"/> lever, while this asks whether the document is being rendered for
    /// paged media at all — which decides whether its <c>@page</c> paints, and is true whether or
    /// not the flow is paginated. <c>page-background-image-print</c> is the pair that needs the
    /// distinction: it says outright that its background should print and not show on screen, and
    /// its reference states the same colour on <c>html</c>, so the two agree only when the page
    /// paints.
    /// </remarks>
    [ThreadStatic]
    private static bool PrintMedia;

    /// <summary>
    /// Runs <paramref name="body"/> in print media iff <paramref name="testPath"/> names a WPT
    /// print test — the <c>-print</c> suffix on the file's stem, which is the convention the suite
    /// uses to mark one and the trigger every other runner reads — and with paged rendering on iff
    /// the lever is on besides.
    /// </summary>
    private static T InPageModeFor<T>(string testPath, Func<T> body)
    {
        bool previousPaged = PagedRender;
        bool previousPrint = PrintMedia;
        PrintMedia = IsPrintTestPath(testPath);
        PagedRender = PagedPrint && PrintMedia;

        // The formatting context has to be established here rather than around the render itself:
        // a document's style sheets are cascaded while it is being built, well before the render
        // phase, and the engine memoizes the rule set that cascade produced. A media query answered
        // on the screen surface before the paged context existed would stay answered that way.
#if BROILER_CSS_PAGED_MEDIA
        using var pagedMedia = PrintMedia
            ? Broiler.CSS.Dom.CssPagedMedia.Pin(WptInitialPageArea.Width, WptInitialPageArea.Height)
            : null;
#endif
        try
        {
            return body();
        }
        finally
        {
            PagedRender = previousPaged;
            PrintMedia = previousPrint;
        }
    }

    /// <summary>Whether a path names a WPT print test: a stem ending in <c>-print</c>.</summary>
    internal static bool IsPrintTestPath(string path) =>
        Path.GetFileNameWithoutExtension(path).EndsWith("-print", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Runs <paramref name="render"/> with the Broiler.Layout engine's native
    /// anchor-placement post-pass enabled iff the runner lever is on (P5.8d.2b). The
    /// engine flag is <c>[ThreadStatic]</c>, so enabling it here scopes it to this one
    /// render on this thread — the earlier bridge geometry snapshots are unaffected.
    /// The document's <c>@position-try</c> rule bodies (parsed from <paramref name="html"/>)
    /// are handed to the engine's post-pass via the same thread-static channel, since a
    /// stylesheet at-rule never reaches the cascaded box properties the engine reads
    /// (P5.8d.2b position-try expansion).
    /// </summary>
    private static HTML.Image.BBitmap RenderWithNativeAnchor(string html, Func<HTML.Image.BBitmap> render)
    {
        if (!NativeAnchorPlacement)
            return render();

        var previousEnabled = Broiler.Layout.Engine.NativeAnchorPlacement.Enabled;
        var previousRules = Broiler.Layout.Engine.NativeAnchorPlacement.PositionTryRules;
        Broiler.Layout.Engine.NativeAnchorPlacement.Enabled = true;
        Broiler.Layout.Engine.NativeAnchorPlacement.PositionTryRules = ParsePositionTryRulesFromHtml(html);
        try
        {
            return render();
        }
        finally
        {
            Broiler.Layout.Engine.NativeAnchorPlacement.Enabled = previousEnabled;
            Broiler.Layout.Engine.NativeAnchorPlacement.PositionTryRules = previousRules;
        }
    }

    /// <summary>
    /// Extracts the document's <c>@position-try</c> rules (name → declarations) from the
    /// serialised HTML's <c>&lt;style&gt;</c> blocks, using the canonical
    /// <c>Broiler.CSS.PositionTryRule</c> parser (the same model the bridge uses). Later
    /// duplicates win, in document order — matching the bridge's accumulator.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? ParsePositionTryRulesFromHtml(string html)
    {
        if (string.IsNullOrEmpty(html) || html.IndexOf("@position-try", StringComparison.OrdinalIgnoreCase) < 0)
            return null;

        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (Match m in StyleBlockRegex().Matches(html))
        {
            var css = m.Groups["css"].Value;
            if (css.IndexOf("@position-try", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            foreach (var rule in Broiler.CSS.PositionTryRule.Parse(css))
                result[rule.Key] = rule.Value;
        }
        return result.Count > 0 ? result : null;
    }

    [GeneratedRegex(@"<style[^>]*>(?<css>.*?)</style>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleBlockRegex();

    /// <summary>
    /// The moment a test asks to be screenshotted at, as an offset from load.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>reftest-wait</c> test that calls <c>takeScreenshotDelayed(N)</c>
    /// (<c>/common/reftest-wait.js</c>) is stating that its result is only correct once N
    /// milliseconds of the page's timeline have passed — the reference generator honours that
    /// by waiting, so the runner has to render *as of* the same instant or the two disagree by
    /// construction. Anything driven by that timeline reads this: today that is animated image
    /// frame selection, through <see cref="ImageAnimationClock"/>.
    /// </para>
    /// <para>
    /// This is not a substitute for actually running the page for N milliseconds — no timers
    /// fire and no animations are ticked. It is the one number those mechanisms need to agree
    /// on, and it is read from the test source before scripts run, because the post-processor
    /// strips the <c>&lt;script&gt;</c> that carries it.
    /// </para>
    /// <para>
    /// A test with no delayed screenshot renders at zero — the first frame, which is what every
    /// render produced before this existed.
    /// </para>
    /// </remarks>
    internal static TimeSpan ScreenshotPresentationTime(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return TimeSpan.Zero;

        Match match = ScreenshotDelayRegex().Match(html);
        if (!match.Success ||
            !double.TryParse(
                match.Groups["ms"].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double milliseconds) ||
            !double.IsFinite(milliseconds) ||
            milliseconds <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    // Case-sensitive: this is a JavaScript identifier, and the surrounding word boundary keeps
    // a test's own `myTakeScreenshotDelayed` helper from being read as the WPT one.
    [GeneratedRegex(@"\btakeScreenshotDelayed\s*\(\s*(?<ms>[0-9]*\.?[0-9]+)\s*\)")]
    private static partial Regex ScreenshotDelayRegex();

    internal HTML.Image.BBitmap RenderHtmlFileBitmapPublic(string htmlPath, string? wptRoot)
    {
        if (wptRoot != null)
            EnsureWptFontsLoaded(wptRoot);

        // Through the same page-mode decision a reftest render goes through, so this renders the
        // file the way the runner would rather than a viewport-only approximation of it.
        return InPageModeFor(htmlPath, () => RenderHtmlFileBitmap(htmlPath, wptRoot));
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
    private static bool _genericFontsLoaded;

    private static void EnsureWptFontsLoaded(string wptRoot)
    {
        var ahemPath = Path.Combine(wptRoot, "fonts", "Ahem.ttf");
        if (File.Exists(ahemPath))
            HtmlRender.LoadFontFromFile(ahemPath, "Ahem");

        EnsureGenericFontsLoaded();
    }

    // The reference generator's headless Chromium resolves the CSS generic families
    // (and the default/initial font — Blink's standard family) through Blink's own
    // font preferences to the **Liberation** metric-compatible faces, NOT the system
    // fontconfig defaults. Measured against the actual reference Chromium
    // (chromium-1194): `serif` and the unstyled default both render at 274.97px for a
    // 43-char probe — Liberation Serif is 276.38px (DejaVu Serif 349.52px is far off);
    // `sans-serif` at 304.77px matches Liberation Sans 306.83px (DejaVu Sans 342.30px
    // off). Broiler previously registered DejaVu here, so its default/serif/sans text
    // came out ~25 % wider than the reference and wrapped to extra lines, shifting every
    // downstream box and mismatching whole pages (e.g. css-backgrounds/background-clip,
    // where the intro <p> wrapped an extra line and pushed the .view box ~25px down).
    // Register the Liberation faces so widths, wrapping, and line positions match the
    // reference. First existing candidate wins; missing files are skipped (no-op).
    // Monospace maps to Liberation Mono (≈ DejaVu Mono in advance) — Chrome's separate
    // 13px monospace-default-size quirk is orthogonal and not handled here.
    private static void EnsureGenericFontsLoaded()
    {
        if (_genericFontsLoaded)
            return;
        _genericFontsLoaded = true;

        LoadFirstAvailableAs("monospace",
            "/usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf");
        LoadFirstAvailableAs("serif",
            "/usr/share/fonts/truetype/liberation/LiberationSerif-Regular.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf");
        LoadFirstAvailableAs("sans-serif",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf");
    }

    private static void LoadFirstAvailableAs(string cssName, params string[] candidatePaths)
    {
        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                HtmlRender.LoadFontFromFile(path, cssName);
                return;
            }
        }
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

    /// <summary>
    /// The document a test's scripts left behind, in whichever form the render path wants it:
    /// <see cref="Html"/> for the serialise-and-reparse path, <see cref="Document"/> for the
    /// direct one (<see cref="DomRender"/>). Exactly one is populated — producing both would
    /// build the render projection twice, which is the expensive half of either.
    /// </summary>
    private readonly record struct ExecutedDocument(
        string? Html,
        Broiler.Dom.DomDocument? Document,
        IReadOnlyList<DomBridge.CheckLayoutAssertion> LayoutAssertions);

    /// <summary>
    /// The script inside an XML CDATA section, or <paramref name="content"/> unchanged when it is
    /// not wrapped in one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An XHTML test writes its inline scripts as <c>&lt;![CDATA[ … ]]&gt;</c> — an XML parser
    /// consumes the wrapper and hands the engine what is between the markers, but this scan reads
    /// the file as text and would hand the engine the markers too. <c>&lt;![CDATA[</c> is a syntax
    /// error, so the whole script threw and every one of the document's scripts was lost with it:
    /// not just the statements, but the functions an <c>onload</c> attribute goes on to call. That
    /// is why <c>css/CSS2/visufx/clip-001.xht</c> never ran the <c>clip = "auto"</c> its result
    /// depends on, and it is the same for every scripted test in the <c>.xht</c> half of the corpus.
    /// </para>
    /// <para>
    /// The commented spellings (<c>//&lt;![CDATA[</c>, <c>&lt;!-- … --&gt;</c>) are handled too:
    /// they are the same wrapper written to survive an HTML parser, and both halves are only ever
    /// noise to the engine.
    /// </para>
    /// </remarks>
    internal static string StripCdataSection(string content)
    {
        var trimmed = content.Trim();

        // `//<![CDATA[` and `// ]]>` — the JS-comment spelling, which the engine would tolerate but
        // which the plain form below is the general case of.
        trimmed = CdataOpenPattern().Replace(trimmed, string.Empty, 1);
        trimmed = CdataClosePattern().Replace(trimmed, string.Empty, 1);

        return trimmed;
    }

    [GeneratedRegex(@"\A\s*(?://\s*|<!--\s*)?<!\[CDATA\[")]
    private static partial Regex CdataOpenPattern();

    [GeneratedRegex(@"(?://\s*)?\]\]>(?:\s*-->)?\s*\z")]
    private static partial Regex CdataClosePattern();

    private static ExecutedDocument ExecuteScriptsWithDom(
        string html,
        string url,
        string? wptRoot = null,
        bool batchStyleInvalidations = false)
    {
        // This method is what drives the bridge's headless geometry pass, and that pass builds a
        // container of its own — out of reach of the handlers the render is handed afterwards. Tell
        // the policy which checkout to resolve against here, at the one boundary every caller of the
        // geometry pass goes through (RunSingleTest, RenderHtmlFileBitmapCore, a worker process).
        WptResourceHandlers.Root = wptRoot;

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

        // Inline linked stylesheets so the DomBridge passes that re-parse <style> source (view
        // transitions, animations) see them, not just the render-time cascade. Cascade-neutral.
        var scanScope = WptPhaseTrace.Measure(WptPhaseTrace.Phases.ScriptScan);

        html = InlineLinkedStylesheets(html, testDir, wptRoot);

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

                    // testdriver.js is served verbatim — it carries API surface the runner does
                    // not shim — but it also overwrites the methods the runner *does* implement
                    // with WebDriver-backed ones that cannot settle here. Re-install the runner's
                    // versions on the same script, so they win regardless of where in the document
                    // the include sits. Appending rather than inserting a list entry keeps the
                    // page-script indices (and so the phase-trace labels) matching the document.
                    if (IsTestDriverScript(src))
                        scriptContent += Environment.NewLine + TestDriverStubs;

                    if (isDefer)
                        deferredScripts.Add(scriptContent);
                    else
                        scripts.Add(scriptContent);
                }

                continue;
            }

            // Inline script
            var content = StripCdataSection(match.Groups["content"].Value).Trim();
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

        // The leading entries of `scripts` are this runner's own constant sources, not the
        // document's — the promise-test flag, BrowserApiStubs, and TestharnessStubs when the test
        // pulls in testharness.js. Counted here so the phase trace can separate what the runner
        // injects from what the page brought.
        var injectedScriptCount = 2 + (needsStubs ? 1 : 0);

        scanScope.Dispose();

        if (scripts.Count == 0 && deferredScripts.Count == 0)
        {
            // Even with no inline scripts, we still need to process anchor
            // positioning, animation snapshots, etc. via the DomBridge.
            using var microTaskContext2 = MicroTaskSynchronizationContext.Install(microTasks);
            using var context2 = new JSContext();
            using var bridge2 = new DomBridge { NativeAnchorPlacement = NativeAnchorPlacement, NativeTopLayer = NativeAnchorPlacement, NativeBackdrop = NativeBackdrop };
            bridge2.Attach(context2, html, url);
            // Inject browser API stubs so onload handlers etc. can reference them.
            try { context2.Eval(BrowserApiStubs); } catch { /* best-effort */ }
            bridge2.FireWindowLoadEvent();
            bridge2.FlushTimers();
            bridge2.ResolveAnimationSnapshots();
            bridge2.ResolveAnchorPositions();
            var assertions2 = bridge2.EvaluateCheckLayoutAssertions();
            return DomRender
                ? new ExecutedDocument(null, bridge2.GetRenderDocument(), assertions2)
                : new ExecutedDocument(bridge2.SerializeToHtml(), null, assertions2);
        }

        // Current on this thread for the whole of script execution: a promise captures its
        // synchronization context when it is created, so without this the engine resumes await
        // continuations on the thread pool, racing this thread's serialize/render.
        using var microTaskContext = MicroTaskSynchronizationContext.Install(microTasks);
        var contextScope = WptPhaseTrace.Measure(WptPhaseTrace.Phases.JsContext);
        using var context = new JSContext();
        contextScope.Dispose();
        // Disposed with this call: the bridge owns a per-document session — the headless layout
        // view and its HtmlContainer (hence the whole box tree and every sub-resource its image
        // load handlers hold), timer/animation queues, listener stores and observers. Leaving it
        // to the GC kept one of those sessions alive per rendered document.
        using var bridge = new DomBridge { NativeAnchorPlacement = NativeAnchorPlacement, NativeTopLayer = NativeAnchorPlacement, NativeBackdrop = NativeBackdrop };
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
        var attachScope = WptPhaseTrace.Measure(WptPhaseTrace.Phases.BridgeAttach);
        bridge.Attach(context, html, url);

        // Enforce the CSP style-src family (style-src / -elem / -attr) on the
        // parsed DOM before scripts run, so an inline style attribute or <style>
        // element blocked by the page's policy neither applies nor renders
        // (WPT content-security-policy/style-src*). Only style directives are
        // consulted, so this does not change script/event-handler behaviour.
        bridge.ApplyStyleContentSecurityPolicy(ContentSecurityPolicy.FromHtml(html));

        try { context.Eval("window.queueMicrotask = queueMicrotask;"); } catch { /* best-effort */ }

        // Register DOM elements with IDs as globals (HTML5 named access).
        bridge.RegisterNamedElementGlobals(context);
        attachScope.Dispose();

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
                    return; // settled within the budget
            }

            // Phase 8 item 3: the drain budget is exhausted while work is still queued (a runaway
            // microtask/timer loop). Surface it explicitly instead of stopping silently.
            RenderLogger.LogWarning(LogCategory.JavaScript, "WptTestRunner.DrainAsyncWork",
                $"Async work did not settle within {DomBridge.AsyncDrainIterationLimit} drain iterations; " +
                $"stopping with pending microtasks={microTasks.Count}, pendingTimers={bridge.HasPendingTimers}. " +
                "This usually indicates a runaway microtask/timer loop.");
        }

        if (batchStyleInvalidations)
            bridge.BeginStyleInvalidationBatch();

        var evalScope = WptPhaseTrace.Measure(WptPhaseTrace.Phases.ScriptEval);
        try
        {
            for (var si = 0; si < scripts.Count; si++)
            {
                try
                {
                    if (si < injectedScriptCount)
                    {
                        using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.EvalStubs))
                            EvalInjectedStub(context, scripts[si]);
                    }
                    else
                    {
                        // Named by its position among the *page's* scripts, so the label matches the
                        // document rather than counting the runner's injected stubs. The stubs above
                        // keep the engine default: they are compile-time constants served from the
                        // process-shared cache, whose key includes the location.
                        using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.EvalPage))
                            context.Eval(scripts[si], ScriptLabel.Inline(si - injectedScriptCount));
                    }
                    using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.EvalSync))
                        PromoteWindowGlobalsToContext(bridge);
                    using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.EvalDrain))
                        DrainAsyncWork();
                }
                catch (Exception ex)
                {
                    RenderLogger.LogError(LogCategory.JavaScript, "WptTestRunner.ExecuteScriptsWithDom",
                        $"Script execution error: {ex.Message}", ex);
                }
            }

            for (var di = 0; di < deferredScripts.Count; di++)
            {
                var deferredLabel = ScriptLabel.Deferred(di);
                try
                {
                    using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.EvalPage))
                        context.Eval(deferredScripts[di], deferredLabel);
                    using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.EvalSync))
                        PromoteWindowGlobalsToContext(bridge);
                    using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.EvalDrain))
                        DrainAsyncWork();
                }
                catch (Exception ex)
                {
                    RenderLogger.LogError(LogCategory.JavaScript, "WptTestRunner.ExecuteScriptsWithDom",
                        $"Script {deferredLabel} failed: {ex.Message}", ex);
                }
            }

            bridge.FireWindowLoadEvent();
            using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.EvalDrain))
                DrainAsyncWork();
        }
        finally
        {
            if (batchStyleInvalidations)
                bridge.EndStyleInvalidationBatch();
            evalScope.Dispose();
        }

        // Resolve CSS animation snapshots: for elements with animation + negative
        // delay, compute the animated property values at t=0 and write them as
        // inline styles so the static renderer can produce the correct output.
        var postScope = WptPhaseTrace.Measure(WptPhaseTrace.Phases.PostScript);
        bridge.ResolveAnimationSnapshots();

        // Resolve CSS anchor positioning: for elements that use anchor()
        // functions, compute the anchored position from the target anchor
        // element's known CSS position and dimensions.  Also inserts
        // backdrop elements for modal <dialog> elements.
        bridge.ResolveAnchorPositions();

        var layoutAssertions = bridge.EvaluateCheckLayoutAssertions();
        postScope.Dispose();

        using (WptPhaseTrace.Measure(WptPhaseTrace.Phases.Serialize))
        {
            // The render projection is what both paths render; only one of them turns it into
            // text first. Building it twice would double the most expensive step here, so the
            // lever picks one.
            return DomRender
                ? new ExecutedDocument(null, bridge.GetRenderDocument(), layoutAssertions)
                : new ExecutedDocument(bridge.SerializeToHtml(), null, layoutAssertions);
        }
    }

    /// <summary>
    /// Makes whatever the script that just ran added to <c>window</c> reachable unqualified, by
    /// re-running the bridge's <c>window</c> → global mirror.
    /// <para>
    /// A browser has nothing to do here — its <c>window</c> <em>is</em> the global object — but the
    /// bridge keeps the two apart, so <c>window.foo = …</c> in one <c>&lt;script&gt;</c> leaves an
    /// unqualified <c>foo()</c> in the next a <c>ReferenceError</c>, which aborts that whole script
    /// block. This used to promote a hand-written list of 13 testharness names, which covered
    /// <c>test</c>/<c>assert_*</c> and nothing else: every other WPT support library was still
    /// invisible unqualified. <c>/css/support/interpolation-testcommon.js</c> is the widest of them
    /// — it exports <c>test_interpolation</c>, <c>test_composition</c>, <c>neutralKeyframe</c> and
    /// three more, and each of the ~100 <c>*-interpolation</c> tests calls them unqualified from
    /// its inline script, so all of them rendered blank against a reference full of test boxes
    /// (issue #1552 problems 4, 18 and 22). Sweeping every own member of <c>window</c> instead of
    /// listing names keeps this correct as libraries come and go.
    /// </para>
    /// </summary>
    /// <summary>
    /// Evaluates one of this runner's own injected stub sources with the process-shared code cache
    /// installed, so it is compiled once per process rather than once per document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The injected sources are <c>BrowserApiStubs</c> (~10 KB), <c>TestharnessStubs</c> (~4.8 KB)
    /// and a one-line flag — <c>private const string</c> fields of this class, so a fixed and
    /// bounded set that cannot carry page content. Every document got a fresh <see cref="JSContext"/>
    /// with its own code cache, so all of it was recompiled per document: <b>64.49 ms per eval and
    /// 28.5% of a WPT run</b>, second only to <c>RegisterDocument</c> before that was fixed the same
    /// way.
    /// </para>
    /// <para>
    /// <b>Deliberately not applied to the document's own scripts</b>, which are evaluated through
    /// the plain <c>context.Eval</c> a few lines up. Those are page content: sharing their compiled
    /// form across documents is exactly the cross-document path a conformance runner must not
    /// create, and it would buy little anyway since WPT documents rarely repeat a script verbatim.
    /// </para>
    /// </remarks>
    private static void EvalInjectedStub(JSContext context, string source)
    {
        var previousCache = context.CodeCache;
        context.CodeCache = Broiler.JavaScript.Runtime.DictionaryCodeCache.Current;
        try
        {
            context.Eval(source);
        }
        finally
        {
            context.CodeCache = previousCache;
        }
    }

    private static void PromoteWindowGlobalsToContext(DomBridge bridge)
    {
        try
        {
            bridge.SyncWindowMembersOntoGlobal();
        }
        catch (Exception ex)
        {
            // Best-effort only — a page whose window resists enumeration must not fail the test.
            RenderLogger.LogError(LogCategory.JavaScript, "WptTestRunner.PromoteWindowGlobalsToContext",
                $"Error promoting window members to the global scope: {ex.Message}", ex);
        }
    }

    private static readonly Regex LinkStylesheetTagPattern = new(
        @"<link\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LinkRelStylesheetPattern = new(
        @"\brel\s*=\s*[""']?\s*stylesheet\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LinkHrefPattern = new(
        @"\bhref\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Inlines local <c>&lt;link rel="stylesheet" href="…"&gt;</c> rules as an equivalent
    /// <c>&lt;style&gt;</c> element at the link's position. The cascade already loads linked
    /// stylesheets through the renderer's stylesheet-load handler, so inlining leaves the rendered
    /// cascade unchanged (same rules, same order); its purpose is to make the rules visible to the
    /// DomBridge passes that re-parse <c>&lt;style&gt;</c> source — view transitions (the
    /// <c>::view-transition*</c> pseudo tree and <c>view-transition-group</c> nesting) and animation
    /// snapshots — which otherwise only see inline <c>&lt;style&gt;</c> content. Links that do not
    /// resolve to a readable local file are left untouched.
    /// <para>
    /// DIAGNOSTIC NOTE (WPT issue #1491, problem 25): this inliner runs before the DomBridge
    /// serialization transforms, so it — not <c>ApplyBaseHrefToStyleUrls</c> — is what decides
    /// which file a linked sheet comes from. Resolving the href against the test's own directory
    /// made <c>the-link-element/stylesheet-with-base.html</c> inline the red sibling sheet that
    /// the test plants as a trap, and the test rendered 100% red. Both sites now resolve through
    /// the shared <see cref="HtmlBaseHref"/> seam.
    /// </para>
    /// </summary>
    internal static string InlineLinkedStylesheets(string html, string? testDir, string? wptRoot)
    {
        // HTML §4.2.3: a relative href resolves against the document base URL, which
        // <base href> overrides. Found once for the document, not per link.
        var hasBaseHref = HtmlBaseHref.TryFindBaseHref(html, out var baseHref);

        return LinkStylesheetTagPattern.Replace(html, match =>
        {
            var tag = match.Value;
            if (!LinkRelStylesheetPattern.IsMatch(tag))
                return tag;

            var href = LinkHrefPattern.Match(tag);
            if (!href.Success)
                return tag;

            var target = href.Groups[1].Value;
            if (hasBaseHref && HtmlBaseHref.Resolve(target, baseHref, pageUrl: null) is { } rebased)
                target = rebased;

            var local = ResolveExternalScriptPath(target, testDir, wptRoot);
            if (local == null || !File.Exists(local))
                return tag;

            try { return "<style>" + File.ReadAllText(local) + "</style>"; }
            catch { return tag; }
        });
    }

    /// <summary>
    /// Whether an external script <c>src</c> names one of WPT's testdriver sources:
    /// <c>/resources/testdriver.js</c>, the vendor hook <c>testdriver-vendor.js</c> that is meant
    /// to implement it, or <c>testdriver-actions.js</c>, which supplies
    /// <c>test_driver.Actions</c> on its own. Each is re-topped with
    /// <see cref="TestDriverStubs"/> — matching the whole family rather than testdriver.js alone
    /// is what makes the runner's methods survive whichever of them a test includes last.
    /// </summary>
    private static bool IsTestDriverScript(string src) =>
        src.Contains("testdriver", StringComparison.OrdinalIgnoreCase);

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
