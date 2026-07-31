// harness-selftest.mjs — checks the diagnostic parsing in scripts/run-octane.mjs
// against real Broiler output.
//
//     node tests/octane/harness-selftest.mjs
//
// These are the parts of the Octane harness that can silently degrade: if the
// combined-script line mapping or the stack-trace parsing stops working, the
// benchmark still runs and still reports "crash" — it just stops saying where.
// The fixtures below are verbatim excerpts of what BroilerJS actually printed,
// so a change in either the harness or the engine's output format shows up as
// a failure here rather than as a quietly emptier report.
//
// No engine, no Octane checkout, and no network are needed.

import {
  annotateCombinedPaths,
  buildCombinedScript,
  describeExitCode,
  extractUnhandledBlock,
  mapCombinedLine,
  parseClrException,
  parseDiagnostics,
  parseJsStack,
  shortenEnginePaths,
} from '../../scripts/run-octane.mjs';

let failures = 0;
let checks = 0;

function check(name, actual, expected) {
  checks++;
  const a = JSON.stringify(actual);
  const e = JSON.stringify(expected);
  if (a === e) return;
  failures++;
  process.stderr.write(`FAIL ${name}\n  expected: ${e}\n  actual:   ${a}\n`);
}

function checkTrue(name, condition, detail = '') {
  checks++;
  if (condition) return;
  failures++;
  process.stderr.write(`FAIL ${name}${detail ? `\n  ${detail}` : ''}\n`);
}

// ── Combined-script assembly and line mapping ───────────────────────────────
// Broiler runs one concatenated script per suite, so every line it cites has to
// be translated back to the file it came from.

const built = buildCombinedScript(
  [
    { file: 'base.js', text: 'a\nb\nc\n' },
    { file: 'crypto.js', text: 'd\ne\n' },
    { file: 'octane-runner.js', text: 'f\n', kind: 'harness' },
  ],
  { trace: true },
);

const lineOf = (needle) => built.text.split('\n').findIndex((l) => l === needle) + 1;

check('map: first line of base.js', mapCombinedLine(built.segments, lineOf('a')), { file: 'base.js', line: 1, kind: 'source' });
check('map: last line of base.js', mapCombinedLine(built.segments, lineOf('c')), { file: 'base.js', line: 3, kind: 'source' });
check('map: second file resets to line 1', mapCombinedLine(built.segments, lineOf('d')), { file: 'crypto.js', line: 1, kind: 'source' });
check('map: runner keeps its own numbering', mapCombinedLine(built.segments, lineOf('f')), { file: 'octane-runner.js', line: 1, kind: 'harness' });
check('map: line past the end is unmapped', mapCombinedLine(built.segments, 10_000), null);

checkTrue('config global precedes every source file', built.text.startsWith('var __octaneConfig = {"trace":true};'));
checkTrue('each part is followed by a load marker',
  (built.text.match(/OCTANE_FILE_LOADED/g) ?? []).length === 3,
  built.text);

// A part that does not end in a newline must not swallow the next part's first
// line — that would shift every later mapping by one.
const unterminated = buildCombinedScript([{ file: 'a.js', text: 'x' }, { file: 'b.js', text: 'y\n' }], {});
check('map: unterminated part does not shift the next file',
  mapCombinedLine(unterminated.segments, unterminated.text.split('\n').findIndex((l) => l === 'y') + 1),
  { file: 'b.js', line: 1, kind: 'source' });

// ── Path annotation ─────────────────────────────────────────────────────────

const COMBINED = 'D:\\Broiler-Release\\tests\\octane\\logs\\broiler\\scripts\\Crypto.js';
const segments = [
  { file: 'base.js', kind: 'source', outStart: 1, outEnd: 400 },
  { file: 'crypto.js', kind: 'source', outStart: 401, outEnd: 900 },
];

check('annotate: `<path>:line N` (unhandled-throw spelling)',
  annotateCombinedPaths(`at RunNextBenchmark in ${COMBINED}:line 371`, COMBINED, segments),
  'at RunNextBenchmark in base.js:371');

check('annotate: `<path>:line,col` (attached-trace spelling)',
  annotateCombinedPaths(`    at Encrypt:${COMBINED}:405,12`, COMBINED, segments),
  '    at Encrypt:crypto.js:5,12');

// The runner's diagnostics are JSON, so separators reach us doubled. Missing
// this case silently leaves every stack inside an OCTANE_ERROR line unmapped.
check('annotate: JSON-escaped separators',
  annotateCombinedPaths(`{"stack":"at Encrypt:${COMBINED.replace(/\\/g, '\\\\')}:405,12"}`, COMBINED, segments),
  '{"stack":"at Encrypt:crypto.js:5,12"}');

check('annotate: forward slashes',
  annotateCombinedPaths(`at x in ${COMBINED.replace(/\\/g, '/')}:line 402`, COMBINED, segments),
  'at x in crypto.js:2');

check('annotate: a line outside every segment is left alone',
  annotateCombinedPaths(`at x in ${COMBINED}:line 99999`, COMBINED, segments),
  `at x in ${COMBINED}:line 99999`);

check('shorten: repo root is trimmed off engine frames',
  shortenEnginePaths('at F() in /repo/Broiler.JS/X.cs:line 5', '/repo'),
  'at F() in Broiler.JS/X.cs:line 5');

// ── CLR exception parsing ───────────────────────────────────────────────────
// Verbatim from a zlib run: Broiler surfaces a .NET fault as a catchable JS
// error whose *message* is the entire managed exception. That message is the
// bug report, which is why it is captured rather than summarized to one line.

const ZLIB_MESSAGE = `System.Collections.Generic.KeyNotFoundException: The given key 'Context48' was not present in the dictionary.
   at System.Collections.Generic.Dictionary\`2.get_Item(TKey key)
   at Broiler.JavaScript.ExpressionCompiler.Generator.VariableInfo.get_Item(BParameterExpression exp) in Broiler.JS\\Broiler.JavaScript.ExpressionCompiler\\Generator\\VariableInfo.cs:line 21
   at Broiler.JavaScript.Engine.JSContext.Eval(String code, String codeFilePath, JSValue this) in Broiler.JS\\Broiler.JavaScript.Engine\\JSContext.cs:line 1928
   at InitializeZlibBenchmark-zlib.js:631,0(Closures, Arguments&)`;

const zlib = parseClrException(ZLIB_MESSAGE);
check('clr: exception type', zlib.type, 'System.Collections.Generic.KeyNotFoundException');
check('clr: exception message', zlib.message, "The given key 'Context48' was not present in the dictionary.");
check('clr: managed frames are kept', zlib.frames.length, 3);
checkTrue('clr: the fault site is the first frame', zlib.frames[0].startsWith('System.Collections.Generic.Dictionary'));
// The last frame is a JS function Broiler rendered into the managed stack; it
// has no argument list, so it belongs to the JS stack, not the engine's.
checkTrue('clr: JS frames are not reported as engine frames',
  !zlib.frames.some((f) => f.includes('InitializeZlibBenchmark')),
  zlib.frames.join('\n'));

const nested = parseClrException(`Unhandled exception. System.InvalidProgramException: Common Language Runtime detected an invalid program.
 ---> System.BadImageFormatException: bad IL
   at Broiler.Thing.Emit(Int32 x)`);
check('clr: nested exceptions', nested.inner, [{ type: 'System.BadImageFormatException', message: 'bad IL' }]);
check('clr: text with no managed exception', parseClrException('just some output'), null);

// ── JS stack parsing ────────────────────────────────────────────────────────
// Verbatim from a Crypto run: an unhandled throw prints the same trace twice,
// in both spellings, separated by a blank line.

const CRYPTO_STDERR = `Unhandled exception. Broiler.JavaScript.Runtime.JSException: Object reference not set to an instance of an object.
at InvokeFunction in Broiler.JS\\Broiler.JavaScript.BuiltIns\\Function\\JSFunction.cs:line 811
at RunNextBenchmark in base.js:371
at __octaneRun in octane-runner.js:214

    at InvokeFunction:Broiler.JS\\Broiler.JavaScript.BuiltIns\\Function\\JSFunction.cs:811,1
    at RunNextBenchmark:base.js:371,6
    at __octaneRun:octane-runner.js:214,4`;

const cryptoFrames = parseJsStack(CRYPTO_STDERR);
check('js: the repeated second block is not counted twice', cryptoFrames.length, 3);
check('js: `in <file>:line N` frame', cryptoFrames[0], { fn: 'InvokeFunction', file: 'Broiler.JS\\Broiler.JavaScript.BuiltIns\\Function\\JSFunction.cs', line: 811 });
check('js: frame mapped to an Octane source', cryptoFrames[1], { fn: 'RunNextBenchmark', file: 'base.js', line: 371 });

check('js: `<fn>:<file>:<line>,<col>` frames', parseJsStack('\n    at From:JSException.cs:156,1\n    at RunStep:base.js:150,8'), [
  { fn: 'From', file: 'JSException.cs', line: 156 },
  { fn: 'RunStep', file: 'base.js', line: 150 },
]);

check('js: engine frames are excluded', parseJsStack('   at Broiler.X.Emit(Int32 y) in Broiler.JS\\X.cs:line 5'), []);
check('js: no stack', parseJsStack(null), []);

// Verbatim from Box2D and Mandreel runs. Broiler renders JS frames into the
// managed stack as `<fn>-<file>:<line>,<col>(<args>)`, and for a fault inside
// generated code — an invalid-IL throw, a stack overflow — that is the only
// place the offending JavaScript is named at all.
check('js: JS frame rendered into a managed stack',
  parseJsStack('   at inline-box2d.js:213,91(Closures, Arguments&)'),
  [{ fn: 'inline', file: 'box2d.js', line: 213 }]);

check('js: managed-rendered frame with a dotted function name',
  parseJsStack('   at DynamicClass.global_init-mandreel.js:123784,0(Broiler.JavaScript.ExpressionCompiler.Closures, Broiler.JavaScript.Runtime.Arguments ByRef)'),
  [{ fn: 'DynamicClass.global_init', file: 'mandreel.js', line: 123784 }]);

// The function/file split must land on the first `-`, not inside the file name.
check('js: file name containing a dash',
  parseJsStack('   at InitializeZlibBenchmark-zlib-data.js:65,0(Closures, Arguments&)'),
  [{ fn: 'InitializeZlibBenchmark', file: 'zlib-data.js', line: 65 }]);
check('js: colon-separated frame whose file contains a dash',
  parseJsStack('    at inline:earley-boyer.js:203,4'),
  [{ fn: 'inline', file: 'earley-boyer.js', line: 203 }]);

checkTrue('unhandled block starts at the engine banner',
  extractUnhandledBlock(`noise\n${CRYPTO_STDERR}`).startsWith('Unhandled exception.'));
check('unhandled block absent', extractUnhandledBlock('clean exit'), null);

// ── Runner diagnostics ──────────────────────────────────────────────────────
// The breadcrumbs must survive truncation: a killed process leaves a stdout
// that ends mid-line, and that partial tail is exactly when they matter.

const STDOUT = `OCTANE_FILE_LOADED base.js
OCTANE_FILE_LOADED crypto.js
OCTANE_START {"version":"9","suites":[{"suite":"Crypto","benchmarks":["Encrypt","Decrypt"]}]}
OCTANE_TRACE {"suite":"Crypto","benchmark":"Encrypt","phase":"Setup","iteration":0}
OCTANE_TRACE {"suite":"Crypto","benchmark":"Encrypt","phase":"run","iteration":1}
OCTANE_TRACE {"suite":"Crypto","benchmark":"Encrypt","phase":"run","iteratio`;

const diag = parseDiagnostics(STDOUT);
check('diag: files that finished evaluating', diag.loaded, ['base.js', 'crypto.js']);
check('diag: benchmarks that registered', diag.start.suites[0].benchmarks, ['Encrypt', 'Decrypt']);
check('diag: complete breadcrumbs are kept', diag.traces.length, 2);
check('diag: the last complete breadcrumb localizes the crash', diag.traces[diag.traces.length - 1], { suite: 'Crypto', benchmark: 'Encrypt', phase: 'run', iteration: 1 });
check('diag: no result was reported', diag.result, null);

const scored = parseDiagnostics('OCTANE_RESULT_JSON {"Richards":52.8,"__score__":"52.8"}');
check('diag: result line', scored.result.Richards, 52.8);

const errored = parseDiagnostics('OCTANE_ERROR {"suite":"zlib","summary":"boom","phase":"run","iteration":1}');
check('diag: error records stream out before any result', errored.errors[0].summary, 'boom');
check('diag: empty output', parseDiagnostics('').traces, []);

// ── Exit codes ──────────────────────────────────────────────────────────────

check('exit: .NET unhandled exception', describeExitCode(0xE0434352), '3762504530 (0xE0434352) — .NET unhandled managed exception');
check('exit: stack overflow', describeExitCode(0xC00000FD), '3221225725 (0xC00000FD) — stack overflow');
check('exit: SIGSEGV', describeExitCode(139), '139 — SIGSEGV — segmentation fault');
check('exit: ordinary status', describeExitCode(1), '1');
check('exit: killed before exiting', describeExitCode(null), '(none)');

// ── Report ──────────────────────────────────────────────────────────────────

if (failures) {
  process.stderr.write(`\n${failures} of ${checks} checks failed.\n`);
  process.exit(1);
}
process.stdout.write(`octane harness self-test: ${checks} checks passed\n`);
