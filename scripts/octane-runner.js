// octane-runner.js — shared Octane harness driver for Broiler and Chromium.
//
// Concatenated after base.js + the benchmark file(s) for a single suite. It
// exposes __octaneRun(done), which runs whatever BenchmarkSuites were
// registered by the loaded files and calls done(results) when scoring
// finishes. `results` maps each benchmark name to its formatted score string
// (or "ERROR: <first line>" when a benchmark throws), plus "__score__" for the
// geometric-mean score of the loaded suite(s).
//
// In a JavaScript shell (Broiler --script-host, d8) there is no `window`, so
// BenchmarkSuite.RunSuites runs synchronously and we print the result line for
// the parent process to capture. In a real browser page (Chromium via
// Playwright) `window` exists and RunSuites yields via setTimeout, so the host
// driver awaits the Promise instead and this file does not auto-run.
//
// ── Diagnostics ────────────────────────────────────────────────────────────
// Broiler is experimental: a suite may score, throw a catchable error, hang, or
// abort the whole process. The result object alone cannot explain the last two,
// because a process that dies never gets to emit one. So this runner also
// streams line-oriented diagnostics to stdout as it goes, which the parent
// (scripts/run-octane.mjs) reads back even from a killed process:
//
//   OCTANE_START  {…}  suites/benchmarks that registered, before anything runs
//   OCTANE_TRACE  {…}  breadcrumb on entering a setup/run/tearDown phase
//   OCTANE_ERROR  {…}  full detail for an error Octane caught and reported
//   OCTANE_FATAL  {…}  full detail for a throw that escaped Octane's handlers
//   OCTANE_RESULT_JSON {…}  the final result object (shell only)
//
// The breadcrumbs are what localize a hard crash: the last OCTANE_TRACE names
// the suite, benchmark, phase, and iteration that were live when the process
// died. `run` breadcrumbs are emitted on iterations 1, 2, 4, 8, … so the cost
// stays O(log n) in a loop that Octane runs for a full second.
//
// Tracing honours an optional `__octaneConfig` global injected ahead of base.js
// by the parent: { trace: false } disables breadcrumbs entirely (for a timing
// run that must not be perturbed), and { maxMessageChars, maxStackChars } cap
// how much of an error is captured.

var __octaneOpts = (typeof __octaneConfig !== 'undefined' && __octaneConfig) ? __octaneConfig : {};

// Where a diagnostic line goes: `print` in a JS shell, `console.log` in a
// browser page (Playwright forwards console messages to the suite log).
//
// The `window` test is not redundant: in a browser `print` *is* defined — it is
// window.print, the print dialog — so testing for `print` alone would throw
// every breadcrumb at the print dialog and emit nothing.
//
// Diagnostics must never be able to break a benchmark run, hence the guard.
function __octaneEmit(line) {
  try {
    if (typeof window === 'undefined' && typeof print === 'function') print(line);
    else if (typeof console !== 'undefined' && console.log) console.log(line);
  } catch (e) { /* ignore — a failed breadcrumb must not fail the suite */ }
}

function __octaneClamp(text, limit) {
  var s = String(text);
  if (s.length <= limit) return s;
  return s.slice(0, limit) + '\n… [truncated, ' + (s.length - limit) + ' more characters]';
}

// The phase the engine is currently inside. Read back when an error is
// reported, and — via the emitted breadcrumbs — reconstructed by the parent
// when the process dies without reporting anything at all.
var __octaneWhere = { suite: null, benchmark: null, phase: null, iteration: 0 };

function __octaneMark(suite, benchmark, phase, iteration) {
  __octaneWhere.suite = suite;
  __octaneWhere.benchmark = benchmark;
  __octaneWhere.phase = phase;
  __octaneWhere.iteration = iteration;
  if (__octaneOpts.trace === false) return;
  try {
    __octaneEmit('OCTANE_TRACE ' + JSON.stringify(__octaneWhere));
  } catch (e) { /* ignore */ }
}

// Wrap a once-per-phase hook (Setup / TearDown / rmsResult). Deliberately does
// not clear the breadcrumb when `original` throws: leaving it pointing at the
// failing phase is the whole point.
function __octaneWrapPhase(suiteName, benchmark, key) {
  var original = benchmark[key];
  if (typeof original !== 'function') return;
  benchmark[key] = function () {
    __octaneMark(suiteName, benchmark.name, key, 0);
    return original.call(this);
  };
}

// Wrap the measured `run` function. Octane calls this in a tight loop for a
// full second, so the per-iteration cost is held to a counter increment, a
// store, and a compare; a breadcrumb is emitted only on iterations 1, 2, 4, 8, …
function __octaneWrapRun(suiteName, benchmark) {
  var original = benchmark.run;
  if (typeof original !== 'function') return;
  var count = 0;
  var nextReport = 1;
  benchmark.run = function () {
    count++;
    __octaneWhere.iteration = count;
    if (count === nextReport) {
      nextReport *= 2;
      __octaneMark(suiteName, benchmark.name, 'run', count);
    }
    return original.call(this);
  };
}

function __octaneInstrument() {
  var suites = BenchmarkSuite.suites;
  var registered = [];
  for (var i = 0; i < suites.length; i++) {
    var suite = suites[i];
    var names = [];
    for (var j = 0; j < suite.benchmarks.length; j++) {
      var benchmark = suite.benchmarks[j];
      names.push(benchmark.name);
      if (__octaneOpts.trace !== false) {
        __octaneWrapPhase(suite.name, benchmark, 'Setup');
        __octaneWrapPhase(suite.name, benchmark, 'TearDown');
        __octaneWrapPhase(suite.name, benchmark, 'rmsResult');
        __octaneWrapRun(suite.name, benchmark);
      }
    }
    registered.push({ suite: suite.name, benchmarks: names });
  }
  return registered;
}

// Everything the parent can learn about a thrown value without re-entering the
// engine. `message` is kept in full (clamped): when Broiler surfaces a CLR
// fault as a catchable JS error, the .NET exception type and its entire managed
// stack trace live in the message — that is the actual diagnostic payload, and
// summarizing it to one line throws the bug report away.
function __octaneDescribeError(suiteName, error) {
  var record = {
    suite: suiteName,
    benchmark: __octaneWhere.benchmark,
    phase: __octaneWhere.phase,
    iteration: __octaneWhere.iteration,
    thrownType: typeof error,
    name: null,
    constructor: null,
    summary: '',
    message: '',
    stack: null
  };

  try { if (error && error.name != null) record.name = String(error.name); } catch (e) { /* getter threw */ }
  try {
    if (error && error.constructor && error.constructor.name) record.constructor = String(error.constructor.name);
  } catch (e) { /* ignore */ }

  var message;
  try {
    message = (error && error.message != null) ? String(error.message) : String(error);
  } catch (e) {
    try { message = '<reading the error message threw: ' + e + '>'; } catch (e2) { message = '<unprintable error>'; }
  }

  record.message = __octaneClamp(message, __octaneOpts.maxMessageChars || 32768);
  record.summary = __octaneClamp(String(message).replace(/\r/g, '').split('\n')[0], 300);

  try {
    if (error && error.stack) record.stack = __octaneClamp(String(error.stack), __octaneOpts.maxStackChars || 16384);
  } catch (e) { /* ignore */ }

  return record;
}

function __octaneRun(done) {
  var results = {};
  var errors = [];

  var registered = [];
  try {
    registered = __octaneInstrument();
  } catch (e) {
    __octaneEmit('OCTANE_NOTE instrumentation failed: ' + e);
  }

  // Emitted before any benchmark code runs, so the parent can tell "the suite
  // never started" (a load/parse failure) apart from "the suite started and
  // died".
  __octaneEmit('OCTANE_START ' + JSON.stringify({
    version: BenchmarkSuite.version,
    suites: registered
  }));

  // Match run.js: use each benchmark's own warmup/determinism defaults.
  BenchmarkSuite.config.doWarmup = undefined;
  BenchmarkSuite.config.doDeterministic = undefined;

  var runner = {
    NotifyStart: function (name) { __octaneMark(name, null, 'start', 0); },
    NotifyResult: function (name, result) { results[name] = result; },
    NotifyError: function (name, error) {
      var record = __octaneDescribeError(name, error);
      errors.push(record);
      // Emitted immediately: if a *later* suite aborts the process, the result
      // object is never produced and this line is all that survives.
      __octaneEmit('OCTANE_ERROR ' + JSON.stringify(record));
      results[name] = 'ERROR: ' + record.summary;
    },
    NotifyScore: function (score) {
      results.__score__ = score;
      results.__version__ = BenchmarkSuite.version;
      results.__errors__ = errors;
      results.__where__ = __octaneWhere;
      done(results);
    }
  };

  // Octane catches throws out of Setup/run/TearDown itself. Anything that gets
  // past it is about to take the process down, so describe it first.
  try {
    BenchmarkSuite.RunSuites(runner);
  } catch (e) {
    __octaneEmit('OCTANE_FATAL ' + JSON.stringify(__octaneDescribeError('<harness>', e)));
    throw e;
  }
}

if (typeof window === 'undefined' && typeof print === 'function') {
  __octaneRun(function (r) { print('OCTANE_RESULT_JSON ' + JSON.stringify(r)); });
}
