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

function __octaneRun(done) {
  var results = {};
  // Match run.js: use each benchmark's own warmup/determinism defaults.
  BenchmarkSuite.config.doWarmup = undefined;
  BenchmarkSuite.config.doDeterministic = undefined;
  BenchmarkSuite.RunSuites({
    NotifyResult: function (name, result) { results[name] = result; },
    NotifyError: function (name, error) {
      var msg = (error && error.message) ? error.message : String(error);
      // Broiler surfaces CLR stack traces in the message; keep only the first
      // line, capped, so the captured JSON stays small.
      msg = String(msg).replace(/\r/g, '').split('\n')[0];
      if (msg.length > 300) msg = msg.slice(0, 300) + '…';
      results[name] = 'ERROR: ' + msg;
    },
    NotifyScore: function (score) {
      results.__score__ = score;
      results.__version__ = BenchmarkSuite.version;
      done(results);
    }
  });
}

if (typeof window === 'undefined' && typeof print === 'function') {
  __octaneRun(function (r) { print('OCTANE_RESULT_JSON ' + JSON.stringify(r)); });
}
