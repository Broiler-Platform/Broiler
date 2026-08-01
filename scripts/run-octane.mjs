// run-octane.mjs — run the Octane 2.0 benchmark suite under Chromium (V8 via
// Playwright) and/or Broiler (the BroilerJS --script-host shell), then emit a
// per-engine result file, a comparison report, and a failure diagnostics report.
//
// Each Octane suite is executed in isolation — a fresh Chromium page or a fresh
// Broiler process — so a crash, hang, or error in one suite never discards the
// others. This matters because Broiler is an experimental engine: some suites
// score, some throw a catchable error, and some abort the whole process.
//
// ── Diagnostics ────────────────────────────────────────────────────────────
// A score of "error" or "crash" is a bug report with the evidence removed, so
// every failing suite is captured in full:
//
//   * The child's entire stdout/stderr is written to <log-dir>/<engine>/<suite>.log
//     along with the exit code, signal, duration, and a copy-pasteable repro
//     command. Broiler prints a JS stack trace on an unhandled throw, and
//     surfaces a CLR fault as a catchable JS error whose *message* carries the
//     .NET exception type and its whole managed stack — both are kept verbatim.
//   * Stack traces are mapped back to Octane sources. Broiler runs one
//     concatenated script per suite, so its traces cite lines in a temporary
//     file; every such line is rewritten to the file and line it came from
//     (base.js:371, crypto.js:104, …) using the offsets recorded when the
//     script was built.
//   * scripts/octane-runner.js streams breadcrumbs as it runs, so a suite that
//     aborts the process — and therefore never reports a result — is still
//     localized to a benchmark, phase, and iteration.
//   * The combined script for a failing suite is kept next to its log, so the
//     failure can be re-run by hand without re-deriving how it was built.
//
// Usage:
//   node run-octane.mjs --octane-dir <dir> [options]
//
// Options:
//   --octane-dir <dir>     Octane checkout (contains base.js, richards.js, …). Required.
//   --engines <list>       Comma-separated: chromium,broiler (default: chromium,broiler)
//   --broiler-dll <path>   BroilerJS.dll to run with `dotnet ... --script-host`.
//                          Required when broiler is in --engines.
//   --out-dir <dir>        Where result JSON/MD are written (default: tests/octane/results)
//   --log-dir <dir>        Where per-suite logs are written (default: tests/octane/logs)
//   --suites <path>        Suite manifest (default: scripts/octane-suites.json)
//   --runner <path>        Shared runner JS (default: scripts/octane-runner.js)
//   --timeout <sec>        Per-suite timeout in seconds (default: 180). A suite
//                          may raise its own budget with "timeoutSec" in the
//                          manifest; this option is the floor for every suite.
//   --repetitions <n>      Run each suite n times and report the median score
//                          per benchmark plus the observed spread (default: 1).
//                          A single run cannot tell a real change from noise.
//   --noise-band <pct>     Spread above which a benchmark is flagged as noisy
//                          in the comparison (default: 7.5, matching the
//                          baseline profile in eng/performance/phase0.json).
//   --only <list>          Comma-separated suite names to run (default: all).
//                          Results go to <log-dir>/partial/ so a debugging run
//                          never overwrites the committed full-run results.
//   --verbose              Stream each child's stdout/stderr live, line-prefixed.
//   --keep-scripts         Keep the combined script for every suite, not just
//                          the failing ones.
//   --no-trace             Disable the runner's breadcrumbs (for a timing run
//                          that must not be perturbed at all).
//   --broiler-env K=V      Extra environment variable for the Broiler child
//                          (repeatable), e.g. BROILER_GENERATE_IL_LOGS=1.

import { spawn } from 'node:child_process';
import { readFileSync, writeFileSync, mkdirSync, existsSync, rmSync } from 'node:fs';
import { join, dirname, resolve, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..');

// Caps. The log files get everything; these bound only what is copied into the
// committed JSON/Markdown reports.
const MAX_CAPTURED_CHARS = 24 * 1024 * 1024; // per stream, per suite
const REPORT_MESSAGE_CHARS = 2000;
const REPORT_JS_FRAMES = 15;
const REPORT_CLR_FRAMES = 30;

function parseArgs(argv) {
  const opts = {
    octaneDir: null,
    engines: 'chromium,broiler',
    broilerDll: null,
    outDir: join(REPO_ROOT, 'tests', 'octane', 'results'),
    logDir: join(REPO_ROOT, 'tests', 'octane', 'logs'),
    suites: join(__dirname, 'octane-suites.json'),
    runner: join(__dirname, 'octane-runner.js'),
    timeout: 180,
    repetitions: 1,
    noiseBand: 7.5,
    only: null,
    verbose: false,
    keepScripts: false,
    trace: true,
    broilerEnv: {},
  };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    const next = () => argv[++i];
    switch (a) {
      case '--octane-dir': opts.octaneDir = next(); break;
      case '--engines': opts.engines = next(); break;
      case '--broiler-dll': opts.broilerDll = next(); break;
      case '--out-dir': opts.outDir = next(); break;
      case '--log-dir': opts.logDir = next(); break;
      case '--suites': opts.suites = next(); break;
      case '--runner': opts.runner = next(); break;
      case '--timeout': opts.timeout = parseInt(next(), 10); break;
      case '--repetitions': opts.repetitions = parseInt(next(), 10); break;
      case '--noise-band': opts.noiseBand = Number(next()); break;
      case '--only': opts.only = next().split(',').map((s) => s.trim()).filter(Boolean); break;
      case '--verbose': opts.verbose = true; break;
      case '--keep-scripts': opts.keepScripts = true; break;
      case '--no-trace': opts.trace = false; break;
      case '--broiler-env': {
        const kv = next();
        const eq = kv.indexOf('=');
        if (eq < 1) throw new Error(`--broiler-env expects KEY=VALUE, got: ${kv}`);
        opts.broilerEnv[kv.slice(0, eq)] = kv.slice(eq + 1);
        break;
      }
      default: throw new Error(`Unknown argument: ${a}`);
    }
  }
  if (!opts.octaneDir) throw new Error('--octane-dir is required');
  if (!Number.isInteger(opts.repetitions) || opts.repetitions < 1) {
    throw new Error(`--repetitions expects a positive integer, got: ${opts.repetitions}`);
  }
  return opts;
}

// ── Per-suite budgets and repeatability ─────────────────────────────────────
// Two suites are far slower than the rest — Mandreel and zlib run for minutes,
// not seconds — so a single global timeout is either too tight for them or too
// slack to catch a hang anywhere else. The manifest may raise a suite's budget;
// it may never lower it below the global value, so --timeout stays a floor and
// a debugging run can still widen everything at once.

export function suiteTimeoutSec(suite, opts) {
  return Math.max(opts.timeout, suite.timeoutSec ?? 0);
}

export function median(values) {
  const s = [...values].sort((a, b) => a - b);
  if (s.length === 0) return null;
  const mid = s.length >> 1;
  return s.length % 2 ? s[mid] : (s[mid - 1] + s[mid]) / 2;
}

// Spread as a percentage of the median. Reported rather than smoothed away: a
// wide band on a latency benchmark is itself a pause-distribution result, and a
// wide band anywhere else means the delta that run is being read for is noise.
export function bandPercent(values) {
  if (values.length < 2) return null;
  const m = median(values);
  if (!m) return null;
  return Number((((Math.max(...values) - Math.min(...values)) / m) * 100).toFixed(1));
}

// Collapse a benchmark's samples across repetitions into the reported score.
// With one repetition this is the identity, so a default run is byte-identical
// to what the harness produced before repetitions existed.
function summarizeSamples(samplesByBenchmark) {
  const benchmarks = {};
  const stability = {};
  for (const [name, values] of Object.entries(samplesByBenchmark)) {
    if (!values.length) continue;
    benchmarks[name] = values.length === 1 ? values[0] : Number(median(values).toFixed(1));
    if (values.length > 1) stability[name] = { samples: values, bandPercent: bandPercent(values) };
  }
  return { benchmarks, stability };
}

// A suite is only `ok` if it was `ok` every time. Anything else reports the
// first bad run — a suite that fails one run in three is a flake, and averaging
// it into a pass is exactly the kind of quiet dishonesty this harness avoids.
function summarizeStatuses(statuses) {
  const bad = statuses.find((s) => s.status !== 'ok');
  const worst = bad ?? statuses[statuses.length - 1];
  if (statuses.length > 1) {
    worst.repetitions = statuses.length;
    worst.statusPerRepetition = statuses.map((s) => s.status);
    if (bad && statuses.some((s) => s.status === 'ok')) worst.flaky = true;
  }
  return worst;
}

function selectSuites(manifest, only) {
  if (!only || only.length === 0) return manifest.suites;
  const wanted = new Set(only.map((s) => s.toLowerCase()));
  const picked = manifest.suites.filter((s) => wanted.has(s.name.toLowerCase()));
  const known = new Set(picked.map((s) => s.name.toLowerCase()));
  const missing = only.filter((s) => !known.has(s.toLowerCase()));
  if (missing.length) {
    throw new Error(
      `--only names no such suite: ${missing.join(', ')}. ` +
      `Known suites: ${manifest.suites.map((s) => s.name).join(', ')}`);
  }
  return picked;
}

// ── Combined-script assembly and line mapping ───────────────────────────────
// Broiler runs one script per suite, so base.js, the benchmark file(s), and the
// runner are concatenated. Recording where each part landed is what later lets
// a trace line in the combined file be reported against its real source.

function countNewlines(s) {
  let n = 0;
  let i = -1;
  while ((i = s.indexOf('\n', i + 1)) !== -1) n++;
  return n;
}

function loadMarker(file) {
  // Announces that `file` finished evaluating. In a multi-file suite this is
  // what identifies the file a load-time failure happened in — the runner
  // itself never gets to execute in that case.
  //
  // The `window` test matches octane-runner.js: in a browser `print` is the
  // print dialog, so the shell's print() is only reachable when there is no
  // window at all.
  const emit = "(typeof window==='undefined'&&typeof print==='function'?print:console.log)";
  return `;try{${emit}('OCTANE_FILE_LOADED ${JSON.stringify(file).slice(1, -1)}');}catch(e){}`;
}

export function buildCombinedScript(parts, config) {
  let text = '';
  let line = 1;
  const segments = [];
  const push = (file, body, kind) => {
    const normalized = body.endsWith('\n') ? body : body + '\n';
    const count = countNewlines(normalized);
    segments.push({ file, kind, outStart: line, outEnd: line + count - 1 });
    text += normalized;
    line += count;
  };

  push('<harness config>', `var __octaneConfig = ${JSON.stringify(config)};`, 'harness');
  for (const part of parts) {
    push(part.file, part.text, part.kind ?? 'source');
    push('<harness marker>', loadMarker(part.file), 'harness');
  }
  return { text, segments };
}

export function mapCombinedLine(segments, line) {
  for (const seg of segments) {
    if (line >= seg.outStart && line <= seg.outEnd) {
      return { file: seg.file, line: line - seg.outStart + 1, kind: seg.kind };
    }
  }
  return null;
}

// Matches a path separator as it may appear in captured output: a forward
// slash, a backslash, or — inside the JSON diagnostic lines the runner prints —
// a backslash that JSON escaping has doubled. The doubled form must come first
// so it wins the alternation.
const SEP = '(?:\\\\\\\\|\\\\|/)';

// Escape a filesystem path for use in a RegExp, accepting any of those
// separator spellings so one pattern matches however the path was written.
function pathPattern(p) {
  let out = '';
  for (const ch of p) {
    if (ch === '\\' || ch === '/') out += SEP;
    else if ('.*+?^${}()|[]'.includes(ch)) out += `\\${ch}`;
    else out += ch;
  }
  return out;
}

// Rewrite every `<combined>:<line>` / `<combined>:line <n>` citation to the
// Octane source it came from. Broiler emits both spellings — the first in the
// managed stack frames it synthesizes for JS functions, the second in the JS
// stack trace it prints for an unhandled throw.
export function annotateCombinedPaths(text, combinedPath, segments) {
  if (!text) return text;
  const re = new RegExp(`${pathPattern(combinedPath)}(?::line[ \\t]+(\\d+)|:(\\d+)(?:,(\\d+))?)`, 'gi');
  return text.replace(re, (match, lineForm, plainLine, column) => {
    const n = Number(lineForm ?? plainLine);
    const mapped = mapCombinedLine(segments, n);
    if (!mapped) return match;
    return column != null ? `${mapped.file}:${mapped.line},${column}` : `${mapped.file}:${mapped.line}`;
  });
}

// Engine frames cite absolute paths into the checkout; trimming the repo root
// keeps them readable without losing which file they name.
export function shortenEnginePaths(text, root = REPO_ROOT) {
  if (!text) return text;
  const re = new RegExp(pathPattern(root) + SEP, 'gi');
  return text.replace(re, '');
}

// ── Failure parsing ─────────────────────────────────────────────────────────

function tryJson(s) {
  try { return JSON.parse(s); } catch { return null; }
}

// Read back the diagnostic lines octane-runner.js streamed. Works on partial
// output, which is the point: a killed process still tells us how far it got.
export function parseDiagnostics(stdout) {
  const out = { start: null, traces: [], errors: [], fatal: null, loaded: [], notes: [], result: null };
  if (!stdout) return out;
  for (const line of String(stdout).replace(/\r/g, '').split('\n')) {
    if (line.startsWith('OCTANE_TRACE ')) {
      const v = tryJson(line.slice('OCTANE_TRACE '.length));
      if (v) out.traces.push(v);
    } else if (line.startsWith('OCTANE_ERROR ')) {
      const v = tryJson(line.slice('OCTANE_ERROR '.length));
      if (v) out.errors.push(v);
    } else if (line.startsWith('OCTANE_FATAL ')) {
      out.fatal = tryJson(line.slice('OCTANE_FATAL '.length)) ?? out.fatal;
    } else if (line.startsWith('OCTANE_START ')) {
      out.start = tryJson(line.slice('OCTANE_START '.length)) ?? out.start;
    } else if (line.startsWith('OCTANE_FILE_LOADED ')) {
      out.loaded.push(line.slice('OCTANE_FILE_LOADED '.length).trim());
    } else if (line.startsWith('OCTANE_NOTE ')) {
      out.notes.push(line.slice('OCTANE_NOTE '.length));
    } else if (line.includes('OCTANE_RESULT_JSON')) {
      out.result = tryJson(line.slice(line.indexOf('{'))) ?? out.result;
    }
  }
  return out;
}

const CLR_HEADER = /^(?:Unhandled exception\.\s*)?((?:[A-Za-z_][\w`+]*\.)+[A-Za-z_][\w`+]*(?:Exception|Error))\s*:\s*(.*)$/;

// A managed frame names a method with an argument list — `Type.Method(args)`,
// optionally followed by ` in <file>.cs:line N`. Broiler also renders *JS*
// frames into a stack trace, and those have no argument list; requiring one is
// what keeps the two kinds of frame from being reported as each other.
const MANAGED_FRAME = /^[\w.`+<>[\]]+\(/;

// Pull the .NET exception out of a blob of text — whether it arrived as an
// unhandled-exception dump on stderr or inside a caught JS error's message.
// Returns null when the text carries no managed exception at all.
export function parseClrException(text) {
  if (!text) return null;
  const lines = String(text).replace(/\r/g, '').split('\n');
  let outer = null;
  const inner = [];
  const frames = [];
  for (const line of lines) {
    const nested = /^\s*--->\s*(.*)$/.exec(line);
    const candidate = nested ? nested[1] : line;
    const header = CLR_HEADER.exec(candidate.trim());
    if (header) {
      const record = { type: header[1], message: header[2] };
      if (!outer) outer = record;
      else inner.push(record);
      continue;
    }
    const frame = /^\s+at\s+(\S.*)$/.exec(line);
    if (frame && outer && MANAGED_FRAME.test(frame[1].trim())) frames.push(frame[1].trim());
  }
  if (!outer) return null;
  return { ...outer, inner, frames };
}

// Broiler spells a JS frame three ways, and each one localizes a different
// class of failure — so all three are recognized:
//
//   at <fn>:<file>:<line>,<col>          the trace attached to a caught error
//   at <fn> in <file>:line <n>           the dump printed for an unhandled throw
//   at <fn>-<file>:<line>,<col>(<args>)  a JS frame rendered into a managed
//                                        stack, which is the only place a fault
//                                        inside generated code is pinpointed
//
// The function/file split is non-greedy, so it lands on the first separator —
// a JS function name cannot contain `-`, but a file name can (`zlib-data.js`).
//
// An unhandled throw prints the same trace twice, in two of those spellings,
// separated by a blank line — so collecting stops at the first blank line after
// a frame rather than reporting every frame a second time.
export function parseJsStack(text) {
  if (!text) return [];
  const frames = [];
  for (const line of String(text).replace(/\r/g, '').split('\n')) {
    const m = /^\s*at\s+(.+?)(?:\s+in\s+|[:-])(\S.*?)(?::line[ \t]+(\d+)|:(\d+))(?:,(\d+))?(?:\([^)]*\))?\s*$/.exec(line);
    if (m) {
      if (MANAGED_FRAME.test(line.trim().replace(/^at\s+/, ''))) continue;
      frames.push({ fn: m[1].trim(), file: m[2].trim(), line: Number(m[3] ?? m[4]) });
      continue;
    }
    if (frames.length && line.trim() === '') break;
  }
  return frames;
}

// Everything from the engine's unhandled-exception banner onward — the fullest
// account of a hard crash, since the process died before anything in-engine
// could describe it.
export function extractUnhandledBlock(stderr) {
  if (!stderr) return null;
  const text = String(stderr).replace(/\r/g, '');
  const at = text.indexOf('Unhandled exception.');
  return at === -1 ? null : text.slice(at).trimEnd();
}

// Exit statuses that mean something specific on a crash. Node reports the
// Windows codes unsigned, which is why they are matched as decimals here.
const EXIT_CODES = new Map([
  [0xE0434352, '.NET unhandled managed exception'],
  [0xC0000005, 'access violation'],
  [0xC00000FD, 'stack overflow'],
  [0xC0000409, 'fast fail / stack buffer overrun'],
  [0xC000012D, 'out of memory (commit limit)'],
  [134, 'SIGABRT — abort()'],
  [137, 'SIGKILL — killed, often by the OOM killer'],
  [139, 'SIGSEGV — segmentation fault'],
]);

export function describeExitCode(code) {
  if (code == null) return '(none)';
  const meaning = EXIT_CODES.get(code);
  if (!meaning) return String(code);
  const hex = code > 0xFFFF ? ` (0x${code.toString(16).toUpperCase()})` : '';
  return `${code}${hex} — ${meaning}`;
}

function formatFrames(frames, limit) {
  const shown = frames.slice(0, limit);
  const lines = shown.map((f) => (typeof f === 'string' ? `    at ${f}` : `    at ${f.fn} (${f.file}:${f.line})`));
  if (frames.length > limit) lines.push(`    … ${frames.length - limit} more frames (see the suite log)`);
  return lines;
}

// ── Child process capture ───────────────────────────────────────────────────

// Bounded accumulator: keeps the head and the tail so both the start of a run
// and whatever preceded the failure survive a pathologically chatty suite.
function createCapture(limit) {
  const half = Math.floor(limit / 2);
  let head = '';
  let tail = '';
  let dropped = 0;
  return {
    push(chunk) {
      if (head.length < half) {
        const room = half - head.length;
        head += chunk.slice(0, room);
        chunk = chunk.slice(room);
        if (!chunk) return;
      }
      tail += chunk;
      if (tail.length > half) {
        const excess = tail.length - half;
        tail = tail.slice(excess);
        dropped += excess;
      }
    },
    text() {
      return dropped ? `${head}\n… [${dropped} characters elided] …\n${tail}` : head + tail;
    },
  };
}

// Streams output line-by-line to stderr under --verbose, so a hang shows its
// last breadcrumb live instead of only after the timeout fires. `transform`
// gets the same path mapping applied that the log file receives, so a trace
// read live cites Octane sources rather than the temporary combined script.
function createLineStreamer(prefix, transform = (l) => l) {
  let pending = '';
  return (chunk) => {
    pending += chunk;
    let nl;
    while ((nl = pending.indexOf('\n')) !== -1) {
      process.stderr.write(`${prefix}${transform(pending.slice(0, nl))}\n`);
      pending = pending.slice(nl + 1);
    }
  };
}

function runProcess(cmd, args, { timeoutMs, env, onStdout, onStderr }) {
  return new Promise((res) => {
    const startedAt = Date.now();
    const stdout = createCapture(MAX_CAPTURED_CHARS);
    const stderr = createCapture(MAX_CAPTURED_CHARS);
    let timedOut = false;
    let child;
    try {
      child = spawn(cmd, args, { env, windowsHide: true });
    } catch (err) {
      res({ spawnError: err, stdout: '', stderr: '', timedOut: false, code: null, signal: null, durationMs: 0 });
      return;
    }
    let settled = false;
    let timer = null;
    let giveUp = null;

    const finish = (extra) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      if (giveUp) clearTimeout(giveUp);
      res({
        stdout: stdout.text(),
        stderr: stderr.text(),
        timedOut,
        code: null,
        signal: null,
        durationMs: Date.now() - startedAt,
        ...extra,
      });
    };

    timer = setTimeout(() => {
      timedOut = true;
      child.kill('SIGKILL');
      // A killed child whose pipes are still held open would never emit
      // 'close', hanging the whole run on one bad suite. Report what was
      // captured instead.
      giveUp = setTimeout(() => finish({ code: null, signal: 'SIGKILL' }), 10_000);
    }, timeoutMs);

    child.stdout.setEncoding('utf8');
    child.stdout.on('data', (d) => { stdout.push(d); onStdout?.(d); });
    child.stderr.setEncoding('utf8');
    child.stderr.on('data', (d) => { stderr.push(d); onStderr?.(d); });
    child.on('error', (err) => finish({ spawnError: err }));
    child.on('close', (code, signal) => finish({ code, signal }));
  });
}

// ── Result normalization ────────────────────────────────────────────────────

// Pull the numeric benchmark scores out of a raw Octane result object,
// classifying the suite as ok/error and computing its geometric mean.
function normalizeRaw(raw) {
  const benchmarks = {};
  const errors = {};
  for (const [k, v] of Object.entries(raw)) {
    if (k.startsWith('__')) continue;
    if (typeof v === 'string' && v.startsWith('ERROR:')) {
      errors[k] = v.slice('ERROR:'.length).trim();
    } else {
      const n = Number(v);
      if (Number.isFinite(n)) benchmarks[k] = n;
      else errors[k] = `non-numeric score: ${v}`;
    }
  }
  return { benchmarks, errors, version: raw.__version__ ?? null, details: raw.__errors__ ?? [] };
}

function geomean(values) {
  const xs = values.filter((v) => Number.isFinite(v) && v > 0);
  if (xs.length === 0) return null;
  const sumLn = xs.reduce((acc, v) => acc + Math.log(v), 0);
  return Math.round(Math.exp(sumLn / xs.length));
}

function clamp(text, limit) {
  const s = String(text ?? '');
  return s.length <= limit ? s : `${s.slice(0, limit)}… [+${s.length - limit} chars]`;
}

// The last breadcrumb before the engine stopped reporting — for a crash or a
// hang this is the only thing that says where it happened.
function lastPhaseOf(diag) {
  const t = diag.traces.length ? diag.traces[diag.traces.length - 1] : null;
  if (!t) return null;
  return { suite: t.suite, benchmark: t.benchmark, phase: t.phase, iteration: t.iteration };
}

function describeWhere(where) {
  if (!where) return null;
  const parts = [];
  if (where.benchmark) parts.push(`benchmark \`${where.benchmark}\``);
  if (where.phase) parts.push(`phase \`${where.phase}\``);
  if (where.phase === 'run' && where.iteration) parts.push(`iteration ${where.iteration}`);
  return parts.length ? parts.join(', ') : null;
}

// ── Per-suite log file ──────────────────────────────────────────────────────

function writeSuiteLog(path, sections) {
  const lines = [];
  for (const [title, body] of sections) {
    lines.push(`===== ${title} ${'='.repeat(Math.max(0, 72 - title.length))}`);
    lines.push(body === '' || body == null ? '(empty)' : String(body).replace(/\s+$/, ''));
    lines.push('');
  }
  writeFileSync(path, `${lines.join('\n')}\n`);
}

// ── Broiler: one isolated `dotnet … --script-host <combined>` process per suite
async function runBroiler(opts, suites, baseJs, runnerJs, dirs) {
  if (!opts.broilerDll) throw new Error('--broiler-dll is required for the broiler engine');
  const logDir = join(dirs.logDir, 'broiler');
  const scriptDir = join(logDir, 'scripts');
  mkdirSync(scriptDir, { recursive: true });

  const benchmarks = {};
  const stability = {};
  const suiteStatus = {};
  let octaneVersion = null;

  for (const suite of suites) {
    const parts = [{ file: 'base.js', text: baseJs }];
    for (const f of suite.files) parts.push({ file: f, text: readFileSync(join(opts.octaneDir, f), 'utf8') });
    parts.push({ file: 'octane-runner.js', text: runnerJs, kind: 'harness' });

    const { text, segments } = buildCombinedScript(parts, { trace: opts.trace });
    const combined = join(scriptDir, `${suite.name}.js`);
    writeFileSync(combined, text);

    const args = [opts.broilerDll, '--script-host', combined];
    const repro = `dotnet ${opts.broilerDll} --script-host ${combined}`;

    // Map every citation of the combined script back to the Octane source, and
    // trim the repo root off engine frames, before anything is read or shown.
    const clean = (t) => shortenEnginePaths(annotateCombinedPaths(t, combined, segments));

    const timeoutSec = suiteTimeoutSec(suite, opts);
    const samples = {};
    const statuses = [];

    for (let rep = 1; rep <= opts.repetitions; rep++) {
      // One log per repetition, so a flake keeps the evidence of the run that
      // failed instead of having it overwritten by the run that passed. The
      // single-repetition name is unchanged.
      const logName = opts.repetitions === 1 ? `${suite.name}.log` : `${suite.name}.rep${rep}.log`;
      const label = opts.repetitions === 1 ? suite.name : `${suite.name} (${rep}/${opts.repetitions})`;

      process.stderr.write(`[broiler] ${label} … `);
      if (opts.verbose) process.stderr.write('\n');

      const res = await runProcess('dotnet', args, {
        timeoutMs: timeoutSec * 1000,
        env: { ...process.env, ...opts.broilerEnv },
        onStdout: opts.verbose ? createLineStreamer(`[broiler:${suite.name}] `, clean) : null,
        onStderr: opts.verbose ? createLineStreamer(`[broiler:${suite.name}!] `, clean) : null,
      });

      const stdout = clean(res.stdout);
      const stderr = clean(res.stderr);
      const mapped = { ...res, stdout, stderr };
      const diag = parseDiagnostics(stdout);

      const status = classifyBroilerSuite(mapped, diag, {
        combined,
        repro,
        log: relative(REPO_ROOT, join(logDir, logName)),
        script: relative(REPO_ROOT, combined),
      });
      status.timeoutSec = timeoutSec;

      if (diag.result) {
        const { benchmarks: b, version } = normalizeRaw(diag.result);
        octaneVersion ??= version;
        for (const [name, score] of Object.entries(b)) (samples[name] ??= []).push(score);
      }
      statuses.push(status);

      writeSuiteLog(join(logDir, logName), [
        ['suite', `${suite.name} (${suite.files.join(', ')})${opts.repetitions === 1 ? '' : ` — repetition ${rep} of ${opts.repetitions}`}`],
        ['status', [
          `status      : ${status.status}`,
          `exit code   : ${describeExitCode(res.code)}${res.signal ? ` signal ${res.signal}` : ''}`,
          `duration    : ${(res.durationMs / 1000).toFixed(1)}s${res.timedOut ? ` (timed out at ${timeoutSec}s)` : ''}`,
          `budget      : ${timeoutSec}s${suite.timeoutSec ? ' (raised by the suite manifest)' : ''}`,
          `files loaded: ${diag.loaded.length ? diag.loaded.join(', ') : '(none — failed before any file finished evaluating)'}`,
          `failed at   : ${describeWhere(status.failedAt) ?? '(unknown)'}`,
          res.spawnError ? `spawn error : ${res.spawnError.message}` : null,
        ].filter(Boolean).join('\n')],
        ['repro', repro],
        ['error', status.message ?? status.error ?? '(none)'],
        ['stderr', stderr],
        ['stdout', stdout],
      ]);

      if (!opts.verbose) process.stderr.write(`${status.status}\n`);
      else process.stderr.write(`[broiler] ${label} … ${status.status}\n`);
      if (status.status !== 'ok') {
        const where = describeWhere(status.failedAt);
        const detail = clamp(status.error ?? '', 160);
        const type = status.errorType && !detail.startsWith(status.errorType) ? `${status.errorType}: ` : '';
        process.stderr.write(`           ${type}${detail}\n`);
        if (where) process.stderr.write(`           at ${where}\n`);
        process.stderr.write(`           log: ${status.log}\n`);
      }
    }

    const summary = summarizeSamples(samples);
    Object.assign(benchmarks, summary.benchmarks);
    Object.assign(stability, summary.stability);
    const status = summarizeStatuses(statuses);
    suiteStatus[suite.name] = status;

    // Keep the combined script only where it is useful — a failing suite is
    // reproducible by hand; a passing one is 25 MB of noise. Drop the repro
    // command with it rather than leaving one that names a deleted file.
    if (status.status === 'ok' && !opts.keepScripts) {
      rmSync(combined, { force: true });
      delete status.script;
      delete status.repro;
    }

    if (status.flaky) {
      process.stderr.write(`           flaky: ${status.statusPerRepetition.join(', ')}\n`);
    }
  }

  return {
    engine: 'broiler',
    engineLabel: 'Broiler.JS (BroilerJS --script-host)',
    octaneVersion,
    perSuiteTimeoutSec: opts.timeout,
    repetitions: opts.repetitions,
    noiseBandPercent: opts.noiseBand,
    benchmarks,
    stability,
    suiteStatus,
    geomean: geomean(Object.values(benchmarks)),
  };
}

// Turn a finished child process into a suite verdict plus everything needed to
// act on it. The status vocabulary (ok/error/timeout/crash) is unchanged; the
// surrounding detail is what is new.
function classifyBroilerSuite(res, diag, paths) {
  const base = {
    exitCode: res.code ?? null,
    signal: res.signal ?? null,
    durationMs: res.durationMs,
    filesLoaded: diag.loaded,
    startedRunning: Boolean(diag.start),
    log: paths.log,
    script: paths.script,
    repro: paths.repro,
  };

  // Reported a result — Octane itself caught anything that went wrong.
  if (diag.result) {
    const { errors, details } = normalizeRaw(diag.result);
    if (Object.keys(errors).length === 0) return { status: 'ok', ...base };
    const detail = details[0] ?? null;
    const summary = detail?.summary ?? Object.values(errors)[0];
    const clr = parseClrException(detail?.message);
    return {
      status: 'error',
      error: summary,
      ...describeFailure(detail, clr),
      failedAt: detail ? { benchmark: detail.benchmark, phase: detail.phase, iteration: detail.iteration } : lastPhaseOf(diag),
      ...base,
    };
  }

  // No result. Either it was killed mid-run or it died.
  const fatal = diag.fatal ?? diag.errors[diag.errors.length - 1] ?? null;
  const clrFromStderr = parseClrException(res.stderr);
  const clr = parseClrException(fatal?.message) ?? clrFromStderr;
  const status = res.timedOut ? 'timeout' : 'crash';

  const error = res.timedOut
    ? `no result within the per-suite timeout${diag.start ? '' : ' (never finished loading)'}`
    : (fatal?.summary ?? clr?.message ?? firstInterestingStderrLine(res.stderr) ?? `exited with code ${res.code}`);

  // A hard crash usually kills the process before the runner can describe
  // anything, so fall back to what the engine dumped on stderr.
  const reported = parseJsStack(fatal?.stack);
  const jsStack = reported.length ? reported : parseJsStack(res.stderr);
  const unhandled = extractUnhandledBlock(res.stderr);

  return {
    status,
    error,
    ...describeFailure(fatal, clr),
    message: fatal?.message
      ? clamp(fatal.message, REPORT_MESSAGE_CHARS)
      : (unhandled ? clamp(unhandled, REPORT_MESSAGE_CHARS) : null),
    jsStack: jsStack.slice(0, REPORT_JS_FRAMES),
    failedAt: fatal
      ? { benchmark: fatal.benchmark, phase: fatal.phase, iteration: fatal.iteration }
      : lastPhaseOf(diag),
    ...base,
  };
}

function describeFailure(detail, clr) {
  // Two places carry JS frames, and which one is useful depends on the fault.
  // An error's own `.stack` stops at the harness when the throw came from
  // inside generated code; the managed trace in its *message* is what names the
  // offending JavaScript (box2d.js:213, zlib-data.js:65). Take whichever
  // actually reached further.
  const fromMessage = parseJsStack(detail?.message);
  const fromStack = parseJsStack(detail?.stack);
  const jsStack = fromMessage.length > fromStack.length ? fromMessage : fromStack;

  return {
    errorType: clr?.type ?? detail?.name ?? detail?.constructor ?? null,
    errorKind: clr ? 'clr' : (detail ? 'js' : null),
    message: detail?.message ? clamp(detail.message, REPORT_MESSAGE_CHARS) : (clr ? clamp(`${clr.type}: ${clr.message}`, REPORT_MESSAGE_CHARS) : null),
    clrStack: clr ? clr.frames.slice(0, REPORT_CLR_FRAMES) : [],
    clrInner: clr?.inner ?? [],
    jsStack: jsStack.slice(0, REPORT_JS_FRAMES),
  };
}

function firstInterestingStderrLine(stderr) {
  if (!stderr) return null;
  const lines = String(stderr).replace(/\r/g, '').split('\n').map((l) => l.trim()).filter(Boolean);
  return lines.find((l) => /exception|error|fatal|abort|stack overflow/i.test(l)) ?? lines[0] ?? null;
}

// ── Chromium: one isolated Playwright page per suite (real V8 in a browser) ──
async function runChromium(opts, suites, baseJs, runnerJs, dirs) {
  // Playwright is installed under tests/octane/node_modules by the orchestrator.
  // ESM bare-specifier resolution ignores NODE_PATH, so resolve it explicitly
  // from that install location.
  const requireFrom = createRequire(join(REPO_ROOT, 'tests', 'octane', 'package.json'));
  const { chromium } = requireFrom('playwright');
  const logDir = join(dirs.logDir, 'chromium');
  mkdirSync(logDir, { recursive: true });

  const browser = await chromium.launch();
  const engineLabel = `Chromium ${browser.version()}`;
  const benchmarks = {};
  const stability = {};
  const suiteStatus = {};
  let octaneVersion = null;

  try {
    for (const suite of suites) {
      const timeoutSec = suiteTimeoutSec(suite, opts);
      const samples = {};
      const statuses = [];
      for (let rep = 1; rep <= opts.repetitions; rep++) {
        const label = opts.repetitions === 1 ? suite.name : `${suite.name} (${rep}/${opts.repetitions})`;
        const logName = opts.repetitions === 1 ? `${suite.name}.log` : `${suite.name}.rep${rep}.log`;
        process.stderr.write(`[chromium] ${label} … `);
        const context = await browser.newContext();
        const page = await context.newPage();
        const startedAt = Date.now();

        // A browser reports out of band: console output (which carries the
        // runner's breadcrumbs), uncaught page errors, and renderer crashes.
        const consoleLines = [];
        const pageErrors = [];
        page.on('console', (m) => consoleLines.push(`[${m.type()}] ${m.text()}`));
        page.on('pageerror', (e) => pageErrors.push(e.stack || String(e)));
        page.on('crash', () => pageErrors.push('the page crashed'));

        let status;
        let loadFailure = null;
        try {
          await page.setContent('<!doctype html><html><head></head><body></body></html>');
          await page.addScriptTag({ content: `var __octaneConfig = ${JSON.stringify({ trace: opts.trace })};` });
          await page.addScriptTag({ content: baseJs });
          for (const f of suite.files) {
            try {
              await page.addScriptTag({ content: readFileSync(join(opts.octaneDir, f), 'utf8') });
            } catch (e) {
              loadFailure = `${f}: ${e.message}`;
              throw e;
            }
          }
          await page.addScriptTag({ content: runnerJs });
          const raw = await Promise.race([
            page.evaluate(() => new Promise((res) => { window.__octaneRun(res); })),
            new Promise((_, rej) => setTimeout(() => rej(new Error('timeout')), timeoutSec * 1000)),
          ]);
          const { benchmarks: b, errors, version, details } = normalizeRaw(raw);
          octaneVersion ??= version;
          for (const [name, score] of Object.entries(b)) (samples[name] ??= []).push(score);
          if (Object.keys(errors).length === 0) {
            status = { status: 'ok' };
          } else {
            const detail = details[0] ?? null;
            // V8 spells stack frames differently from Broiler, so the trace is
            // kept as text rather than parsed into frames.
            const message = [detail?.message, detail?.stack].filter(Boolean).join('\n');
            status = {
              status: 'error',
              error: detail?.summary ?? Object.values(errors)[0],
              errorType: detail?.name ?? null,
              errorKind: 'js',
              message: message ? clamp(message, REPORT_MESSAGE_CHARS) : null,
              jsStack: [],
              clrStack: [],
              failedAt: detail ? { benchmark: detail.benchmark, phase: detail.phase, iteration: detail.iteration } : null,
            };
          }
        } catch (e) {
          const timedOut = String(e.message).includes('timeout');
          const diag = parseDiagnostics(consoleLines.map((l) => l.replace(/^\[\w+\] /, '')).join('\n'));
          status = {
            status: timedOut ? 'timeout' : 'crash',
            error: clamp(loadFailure ?? pageErrors[0] ?? e.message, 300),
            errorType: null,
            errorKind: 'js',
            message: clamp([loadFailure, ...pageErrors, e.stack ?? e.message].filter(Boolean).join('\n'), REPORT_MESSAGE_CHARS),
            jsStack: [],
            clrStack: [],
            failedAt: lastPhaseOf(diag),
          };
        } finally {
          status ??= { status: 'crash', error: 'no verdict' };
          status.durationMs = Date.now() - startedAt;
          status.timeoutSec = timeoutSec;
          status.log = relative(REPO_ROOT, join(logDir, logName));
          writeSuiteLog(join(logDir, logName), [
            ['suite', `${suite.name} (${suite.files.join(', ')})${opts.repetitions === 1 ? '' : ` — repetition ${rep} of ${opts.repetitions}`}`],
            ['status', `status  : ${status.status}\nduration: ${(status.durationMs / 1000).toFixed(1)}s\nbudget  : ${timeoutSec}s`],
            ['error', status.message ?? status.error ?? '(none)'],
            ['page errors', pageErrors.join('\n\n')],
            ['console', consoleLines.join('\n')],
          ]);
          statuses.push(status);
          process.stderr.write(`${status.status}\n`);
          if (status.status !== 'ok') process.stderr.write(`           log: ${status.log}\n`);
          await context.close();
        }
      }

      const summary = summarizeSamples(samples);
      Object.assign(benchmarks, summary.benchmarks);
      Object.assign(stability, summary.stability);
      const suiteVerdict = summarizeStatuses(statuses);
      suiteStatus[suite.name] = suiteVerdict;
      if (suiteVerdict.flaky) {
        process.stderr.write(`           flaky: ${suiteVerdict.statusPerRepetition.join(', ')}\n`);
      }
    }
  } finally {
    await browser.close();
  }

  return {
    engine: 'chromium',
    engineLabel,
    octaneVersion,
    perSuiteTimeoutSec: opts.timeout,
    repetitions: opts.repetitions,
    noiseBandPercent: opts.noiseBand,
    benchmarks,
    stability,
    suiteStatus,
    geomean: geomean(Object.values(benchmarks)),
  };
}

// ── Comparison report ───────────────────────────────────────────────────────
export function buildComparison(results, generatedAt) {
  const chromium = results.chromium;
  const broiler = results.broiler;
  const names = new Set();
  for (const r of [chromium, broiler]) {
    if (!r) continue;
    for (const k of Object.keys(r.benchmarks)) names.add(k);
    for (const k of Object.keys(r.suiteStatus)) names.add(k);
  }
  // Order benchmarks by the manifest; sub-results (…Latency) sort after their suite.
  const rows = [...names].sort();

  function cell(r, name) {
    if (!r) return { score: null, status: 'n/a' };
    if (name in r.benchmarks) return { score: r.benchmarks[name], status: r.suiteStatus[name]?.status ?? 'ok' };
    const st = r.suiteStatus[name];
    return { score: null, status: st ? st.status : 'n/a', error: st?.error, errorType: st?.errorType };
  }

  const comparison = {
    generatedAt,
    octaneVersion: (chromium?.octaneVersion ?? broiler?.octaneVersion) ?? null,
    engines: {
      chromium: chromium ? { label: chromium.engineLabel, geomean: chromium.geomean } : null,
      broiler: broiler ? { label: broiler.engineLabel, geomean: broiler.geomean } : null,
    },
    benchmarks: rows.map((name) => {
      const c = cell(chromium, name);
      const b = cell(broiler, name);
      const ratio = c.score && b.score ? Number((b.score / c.score).toFixed(3)) : null;
      const slower = c.score && b.score ? Number((c.score / b.score).toFixed(1)) : null;
      return {
        name,
        chromium: c,
        broiler: b,
        broilerVsChromium: ratio,
        broilerTimesSlower: slower,
        stability: {
          chromium: chromium?.stability?.[name] ?? null,
          broiler: broiler?.stability?.[name] ?? null,
        },
      };
    }),
  };
  comparison.summary = summarize(comparison, { chromium, broiler });
  return comparison;
}

// The three numbers this comparison is read for. Coverage and spread are here
// because the geomean alone hides both: a run that drops five suites still
// reports a geomean, and a profile with one catastrophic score reports much the
// same total as an evenly-bad one while being a completely different problem.
function summarize(cmp, { chromium, broiler }) {
  const scored = cmp.benchmarks.filter((r) => r.broilerTimesSlower != null);
  const factors = scored.map((r) => r.broilerTimesSlower);
  const best = scored.find((r) => r.broilerTimesSlower === Math.min(...factors)) ?? null;
  const worst = scored.find((r) => r.broilerTimesSlower === Math.max(...factors)) ?? null;
  const count = (r) => (r ? Object.keys(r.benchmarks).length : 0);
  const expected = new Set(cmp.benchmarks.map((r) => r.name)).size;
  return {
    scoresReported: { chromium: count(chromium), broiler: count(broiler), expected },
    geomean: { chromium: chromium?.geomean ?? null, broiler: broiler?.geomean ?? null },
    // "Smoothness": how far apart the best and worst suites are. A uniformly
    // slow engine is a healthier engine than a mostly-fine one with an outlier,
    // because the outlier is a single subsystem failing rather than a broad
    // deficit — see tests/octane/roadmap.md §1.
    spread: scored.length < 2 ? null : {
      factor: Number((Math.max(...factors) / Math.min(...factors)).toFixed(1)),
      best: best && { name: best.name, timesSlower: best.broilerTimesSlower },
      worst: worst && { name: worst.name, timesSlower: worst.broilerTimesSlower },
    },
    repetitions: broiler?.repetitions ?? chromium?.repetitions ?? 1,
    noiseBandPercent: broiler?.noiseBandPercent ?? chromium?.noiseBandPercent ?? null,
  };
}

export function renderMarkdown(cmp, broiler) {
  const c = cmp.engines.chromium;
  const b = cmp.engines.broiler;
  const fmt = (cell) => {
    if (cell.score != null) return String(cell.score);
    if (cell.status === 'n/a') return '—';
    return `_${cell.status}_`;
  };
  const lines = [];
  lines.push(`# Octane 2.0 benchmark — Chromium vs Broiler`);
  lines.push('');
  lines.push(`- Generated: \`${cmp.generatedAt}\``);
  lines.push(`- Octane version: \`${cmp.octaneVersion ?? 'unknown'}\``);
  if (c) lines.push(`- Chromium: \`${c.label}\` — **overall score ${c.geomean ?? 'n/a'}**`);
  if (b) lines.push(`- Broiler: \`${b.label}\` — **overall score ${b.geomean ?? 'n/a'}**`);
  const s = cmp.summary;
  if (s) {
    lines.push(`- Repetitions per suite: ${s.repetitions}${s.repetitions === 1 ? ' — **a single run; deltas against it cannot be distinguished from noise**' : ` (median reported; noise band ${s.noiseBandPercent}%)`}`);
    lines.push('');
    lines.push('| | Chromium | Broiler |');
    lines.push('|---|--:|--:|');
    lines.push(`| Scores reported (of ${s.scoresReported.expected}) | ${s.scoresReported.chromium} | ${s.scoresReported.broiler} |`);
    lines.push(`| Overall (geomean) | ${s.geomean.chromium ?? '—'} | ${s.geomean.broiler ?? '—'} |`);
    if (s.spread) {
      lines.push(`| Spread (worst ÷ best suite) | — | ${s.spread.factor}× |`);
    }
    lines.push('');
    if (s.spread) {
      lines.push(`Broiler's best suite is **${s.spread.best.name}** at ${s.spread.best.timesSlower}× slower ` +
        `and its worst is **${s.spread.worst.name}** at ${s.spread.worst.timesSlower}×. The spread between them ` +
        'is the "smoothness" number — a large one means a single subsystem is pathological rather than the ' +
        'engine being uniformly behind. See [`../roadmap.md`](../roadmap.md).');
      lines.push('');
    }
  }
  lines.push('Higher is better. "Broiler / Chromium" is the ratio of scores on suites both engines completed.');
  lines.push('');
  const noisy = (st) => (st && st.bandPercent != null && cmp.summary && st.bandPercent > cmp.summary.noiseBandPercent ? ' ⚠' : '');
  const showBand = cmp.summary && cmp.summary.repetitions > 1;
  lines.push(`| Benchmark | Chromium | Broiler |${showBand ? ' Broiler spread |' : ''} Broiler / Chromium | × slower |`);
  lines.push(`|---|--:|--:|${showBand ? '--:|' : ''}--:|--:|`);
  for (const row of cmp.benchmarks) {
    const ratio = row.broilerVsChromium != null ? row.broilerVsChromium.toFixed(3) : '—';
    const slower = row.broilerTimesSlower != null ? `${row.broilerTimesSlower}×` : '—';
    const st = row.stability?.broiler;
    const band = showBand ? ` ${st?.bandPercent != null ? `${st.bandPercent}%${noisy(st)}` : '—'} |` : '';
    lines.push(`| ${row.name} | ${fmt(row.chromium)} | ${fmt(row.broiler)} |${band} ${ratio} | ${slower} |`);
  }
  lines.push(`| **Overall (geomean)** | **${c?.geomean ?? '—'}** | **${b?.geomean ?? '—'}** |${showBand ? ' |' : ''} ${c?.geomean && b?.geomean ? (b.geomean / c.geomean).toFixed(3) : '—'} | ${c?.geomean && b?.geomean ? `${(c.geomean / b.geomean).toFixed(0)}×` : '—'} |`);
  lines.push('');
  if (showBand) {
    lines.push(`⚠ marks a benchmark whose spread across ${cmp.summary.repetitions} repetitions exceeded the ${cmp.summary.noiseBandPercent}% band.`);
    lines.push('');
  }

  const failures = Object.entries(broiler?.suiteStatus ?? {}).filter(([, s]) => s.status !== 'ok');
  if (failures.length) {
    lines.push('## Broiler failures');
    lines.push('');
    lines.push('| Suite | Status | Failing type | Where | Detail |');
    lines.push('|---|---|---|---|---|');
    for (const [name, s] of failures) {
      const where = describeWhere(s.failedAt)?.replace(/\|/g, '\\|') ?? '—';
      const type = s.errorType ? `\`${s.errorType}\`` : '—';
      const detail = clamp(s.error ?? '', 140).replace(/\|/g, '\\|');
      lines.push(`| ${name} | ${s.status} | ${type} | ${where} | ${detail} |`);
    }
    lines.push('');
    lines.push('Stack traces, the phase each failure reached, and a repro command are in');
    lines.push('[`diagnostics.md`](diagnostics.md); full per-suite output is under `tests/octane/logs/`.');
    lines.push('');
  }
  lines.push('---');
  lines.push('_Generated by `scripts/run-octane.mjs`. Source suite: https://github.com/chromium/octane_');
  lines.push('');
  return lines.join('\n');
}

// ── Diagnostics report ──────────────────────────────────────────────────────
// The artifact a person actually reads to fix a failing suite: what broke,
// where it broke, the stack on both sides of the JS/CLR boundary, and how to
// re-run just that suite.
function renderDiagnostics(broiler, generatedAt) {
  const lines = [];
  lines.push('# Broiler Octane failure diagnostics');
  lines.push('');
  lines.push(`- Generated: \`${generatedAt}\``);
  lines.push(`- Engine: \`${broiler.engineLabel}\``);
  lines.push(`- Per-suite timeout: ${broiler.perSuiteTimeoutSec}s (floor; a suite may raise its own — the budget each ran under is in its section below)`);
  if ((broiler.repetitions ?? 1) > 1) lines.push(`- Repetitions per suite: ${broiler.repetitions}`);
  lines.push('');

  const entries = Object.entries(broiler.suiteStatus);
  const failures = entries.filter(([, s]) => s.status !== 'ok');
  if (!failures.length) {
    lines.push('All suites completed. Nothing to diagnose.');
    lines.push('');
    return lines.join('\n');
  }

  lines.push(`${failures.length} of ${entries.length} suites did not complete.`);
  lines.push('');
  lines.push('Statuses: **error** — Octane caught the throw and scored the rest;');
  lines.push('**crash** — the process died and took the suite with it;');
  lines.push('**timeout** — no result within the per-suite timeout.');
  lines.push('');
  lines.push('| Suite | Status | Failing type | Where |');
  lines.push('|---|---|---|---|');
  for (const [name, s] of failures) {
    lines.push(`| [${name}](#${name.toLowerCase()}) | ${s.status} | ${s.errorType ? `\`${s.errorType}\`` : '—'} | ${describeWhere(s.failedAt)?.replace(/\|/g, '\\|') ?? '—'} |`);
  }
  lines.push('');

  for (const [name, s] of failures) {
    lines.push(`## ${name}`);
    lines.push('');
    lines.push(`- **Status**: ${s.status}${s.exitCode != null ? ` (exit code ${describeExitCode(s.exitCode)}${s.signal ? `, signal ${s.signal}` : ''})` : ''}`);
    if (s.errorType) lines.push(`- **Failing type**: \`${s.errorType}\`${s.errorKind === 'clr' ? ' (.NET exception surfaced through the engine)' : ''}`);
    lines.push(`- **Where**: ${describeWhere(s.failedAt) ?? 'unknown — no breadcrumb was reached'}`);
    if (s.filesLoaded) {
      lines.push(`- **Files evaluated**: ${s.filesLoaded.length ? s.filesLoaded.join(' → ') : '_none — failed while loading the first file_'}`);
    }
    if (s.durationMs != null) lines.push(`- **Ran for**: ${(s.durationMs / 1000).toFixed(1)}s`);
    if (s.log) lines.push(`- **Full output**: \`${s.log}\``);
    lines.push('');

    if (s.message) {
      lines.push('```text');
      lines.push(clamp(s.message, REPORT_MESSAGE_CHARS).replace(/```/g, "'''"));
      lines.push('```');
      lines.push('');
    } else if (s.error) {
      lines.push(`> ${s.error}`);
      lines.push('');
    }

    if (s.clrStack?.length) {
      lines.push('**Engine (.NET) stack** — where inside Broiler the fault happened:');
      lines.push('');
      lines.push('```text');
      lines.push(...formatFrames(s.clrStack, REPORT_CLR_FRAMES));
      lines.push('```');
      lines.push('');
    }
    if (s.jsStack?.length) {
      lines.push('**JavaScript stack** — mapped back to the Octane sources:');
      lines.push('');
      lines.push('```text');
      lines.push(...formatFrames(s.jsStack, REPORT_JS_FRAMES));
      lines.push('```');
      lines.push('');
    }
    if (s.clrInner?.length) {
      lines.push(`**Inner exceptions**: ${s.clrInner.map((i) => `\`${i.type}\`: ${clamp(i.message, 160)}`).join('; ')}`);
      lines.push('');
    }

    lines.push('Re-run just this suite:');
    lines.push('');
    lines.push('```bash');
    lines.push(`./scripts/run-octane-benchmarks.sh --engines broiler --skip-build --only ${name} --verbose`);
    lines.push('```');
    lines.push('');
  }

  lines.push('---');
  lines.push('_Generated by `scripts/run-octane.mjs`. Re-runs of a single suite write to `tests/octane/logs/partial/`._');
  lines.push('');
  return lines.join('\n');
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  const manifest = JSON.parse(readFileSync(opts.suites, 'utf8'));
  const suites = selectSuites(manifest, opts.only);
  const baseJs = readFileSync(join(opts.octaneDir, 'base.js'), 'utf8');
  const runnerJs = readFileSync(opts.runner, 'utf8');
  const engines = opts.engines.split(',').map((s) => s.trim()).filter(Boolean);
  const generatedAt = new Date().toISOString();

  // A partial run must never overwrite the committed full-run results, so it
  // writes everything under the (gitignored) log directory instead.
  const partial = Boolean(opts.only);
  const outDir = partial ? join(opts.logDir, 'partial') : opts.outDir;
  const dirs = { logDir: partial ? join(opts.logDir, 'partial') : opts.logDir };
  mkdirSync(outDir, { recursive: true });
  mkdirSync(dirs.logDir, { recursive: true });

  if (partial) {
    process.stderr.write(`[octane] --only ${opts.only.join(',')}: writing to ${outDir} (committed results left alone)\n`);
  }

  const results = {};
  for (const engine of engines) {
    let r;
    if (engine === 'broiler') r = await runBroiler(opts, suites, baseJs, runnerJs, dirs);
    else if (engine === 'chromium') r = await runChromium(opts, suites, baseJs, runnerJs, dirs);
    else throw new Error(`Unknown engine: ${engine}`);
    r.generatedAt = generatedAt;
    results[engine] = r;
    const out = join(outDir, `${engine}-results.json`);
    writeFileSync(out, `${JSON.stringify(r, null, 2)}\n`);
    process.stderr.write(`[${engine}] wrote ${out} (overall score: ${r.geomean ?? 'n/a'})\n`);
  }

  if (results.broiler) {
    const diagnostics = join(outDir, 'diagnostics.md');
    writeFileSync(diagnostics, renderDiagnostics(results.broiler, generatedAt));
    const failed = Object.values(results.broiler.suiteStatus).filter((s) => s.status !== 'ok').length;
    process.stderr.write(`[broiler] wrote ${diagnostics} (${failed} failing suite${failed === 1 ? '' : 's'})\n`);
  }

  // A partial run compares nothing: its suite set is a subset, so folding it
  // into the full comparison would silently drop every suite it skipped.
  if (partial) {
    process.stderr.write('[compare] skipped — partial run\n');
    return;
  }

  // Fold in any per-engine result already on disk so a single-engine run still
  // refreshes the comparison against the other engine's last result.
  for (const engine of ['chromium', 'broiler']) {
    if (results[engine]) continue;
    const p = join(outDir, `${engine}-results.json`);
    if (existsSync(p)) results[engine] = JSON.parse(readFileSync(p, 'utf8'));
  }

  const cmp = buildComparison(results, generatedAt);
  writeFileSync(join(outDir, 'comparison.json'), `${JSON.stringify(cmp, null, 2)}\n`);
  writeFileSync(join(outDir, 'comparison.md'), renderMarkdown(cmp, results.broiler));
  process.stderr.write('[compare] wrote comparison.json + comparison.md\n');
}

// Guarded so the parsing helpers above can be imported by the harness self-test
// without launching a benchmark run.
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((e) => { console.error(e); process.exit(1); });
}
